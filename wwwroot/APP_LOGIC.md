# Cenzas Analytics: Application Logic & Orchestration (APP_LOGIC.md)

This document serves as the absolute **Source of Truth** for the technical implementation of the Cenzas Analytics platform. It bridges the gap between the frontend visualization layer and the high-performance backend infrastructure.

---

## 1. Backend API & Data Infrastructure

The backend is built as a high-performance C# API designed to query a dataset of **844k+ records** with sub-second response times.

### 1.1 Primary Endpoints
*   `POST /api/statistics/analyze`: Calculates real-time summary statistics for a specific filter set (Avg Price, Min/Max ranges, Market Health Index).
*   `POST /api/statistics/market-trend`: Aggregates historical price data into time-series points for trend visualization.
*   `GET /data/filters-metadata.json`: A static, pre-calculated dependency map of all valid city/district/street/object combinations.

### 1.2 AnalysisRequest Payload structure
The system uses a unified request object to ensure consistency between stats and trends:
```json
{
  "City": "Vilnius",
  "Districts": ["Senamiestis", "Antakalnis"],
  "Streets": null,
  "Rooms": [1, 2],
  "Objects": ["Butai pardavimui"],
  "DateFrom": "2024-01-01",
  "DateTo": "2024-12-31",
  "ExpiredThresholdDays": 3,
  "Heating": [],
  "Equipped": [],
  "EnergyClass": []
}
```

### 1.3 Database Optimization (The Chain Reaction)
Performance is maintained via an automated "Chain Reaction" triggered by RPA imports:
1.  **Detection**: `RpaJobWatcher` monitors new data imports every 60 seconds.
2.  **Validation**: Ensures `LastCollectedDate` columns are synced with the price history.
3.  **Optimization**: Automatically defragments tables and recreates 13 strategic performance indexes (e.g., `idx_last_collected`, `idx_addlist_lookup_v2`).
4.  **Sync**: Immediately Refreshes `filters-metadata.json` to reflect new data availability.

---

## 2. Frontend Logic & State Orchestration

The frontend implements a decoupled, state-driven architecture to handle complex comparison scenarios.

### 2.1 The Cross-Filtering Engine (`Logic.FilterEngine`)
Built on a **9-point dependency map**, this engine calculates the intersection of available options:
*   **Mechanism**: Every time a filter changes, the engine scans the `Combinations` metadata.
*   **Logic**: It determines which options in Category B are valid given selections in Categories A, C, and D.
*   **UX Guard**: Options that don't exist in the current selection's context are hidden (`display: none`) to prevent "0 Results" queries.

### 2.2 Data Persistence Layer (`LocalStore`)
To eliminate redundant network traffic, the system uses a signature-based cache:
*   **Signature**: A JSON string of the current `AnalysisRequest`.
*   **Logic**: Before calling the API, the system checks `LocalStore` for a matching `blockId + signature`.
*   **Benefit**: Instant UI switching between previously selected cities without new API hits.

### 2.3 Multi-Block State Synchronization
Multiple analysis blocks (Vilnius vs. Kaunas) are managed in a global `Core.state`:
*   **Decoupling**: Each block maintains its own filter set and result cache.
*   **Storm Suppression**: Fetching is debounced (300ms) and can be aborted via `AbortController` if inputs change mid-request.

---

## 3. Visualization & Interaction Engine

The platform utilizes Chart.js with custom plugins to provide a premium, interactive experience.

### 3.1 Dynamic Naming Hierarchy
To ensure clarity in comparisons, object headers and legends follow an adaptive hierarchy:
1.  **City Name** (e.g., "Vilnius")
2.  **Object Type** (e.g., "Butai pardavimui")
3.  **Generic Label** (e.g., "Objektas A")
*Translation mapping ensures slugs like `butu-nuoma` are displayed as "Butai nuomai".*

### 3.2 Focus Mode Implementation
The chart implement's a dual-mode interaction logic in `nt-palyginimas-grafikas.js`:
*   **Global Mode (`index`)**: Tooltips display prices for **ALL** active objects at the hovered date, facilitating easy comparison.
*   **Internal Mode (`nearest`)**: Used within the `onHover` handler to visually isolate (bold/thicken) the single line closest to the cursor.

### 3.3 The "Index 0 Reset Fix"
Stability of the primary dataset (Vilnius, Index 0) is guaranteed by strict type checking:
*   **Problem**: In JavaScript, `if (0)` is falsy, causing Index 0 datasets to get "stuck" or ignored during focus resets.
*   **Solution**: All hover/leave logic uses strict `!== -1` and `!== null` comparisons to treat Index 0 as a valid, interactive element.
*   **Global Reset**: A `mouseleave` listener on the canvas ensures visibility is restored regardless of mouse velocity.

---

## 4. Performance Thresholds & Constraints

*   **UI Debounce**: Trends update after **200ms**, standard statistics after **300ms**.
*   **Adaptive Grouping**: Trends span discovery dynamically switches between Daily, Weekly (if >180 days), and Monthly (if >730 days) aggregation.
*   **Payload Limit**: Transmission is capped at **500 data points** per dataset to maintain 60FPS browser performance.
*   **Zero Suppression**: Any data point where `AveragePrice <= 0` is strictly suppressed from trends to prevent "vertical drop" artifacts.
