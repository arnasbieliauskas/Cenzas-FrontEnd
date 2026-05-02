# Cenzas Analytics - Program Map & Architecture

This document serves as the technical "Source of Truth" for the Cenzas Analytics platform architecture. All future modifications must adhere to the data dependencies and logic flows defined here.

## 1. 9-Point Filter Hierarchy (Rule #20)
Filtering follows a strict top-down dependency map to ensure combinatorial integrity and prevent zero-result states:

1.  **City** (Master Context)
2.  **District** (Scoped to City)
3.  **Street** (Scoped to District/City)
4.  **Rooms** (Scoped to Location)
5.  **Object Type** (Apartment, House, etc.)
6.  **Heating** (Central, Gas, etc.)
7.  **Equipped** (Full, Partial, etc.)
8.  **Energy Class** (A++, A, etc.)
9.  **Title** (Final record identification)

---

## 2. Technical Workflow (Rule #23)

### Phase 1: Local Calculation (Instant)
**Trigger**: Any filter change in the UI.
1.  `nt-statistika.js` -> `syncFilters()` is called.
2.  `main.js` -> `Logic.FilterEngine.getAvailableOptions()` processes `Core.state.metadata`.
3.  `main.js` -> `Logic.calculateStats()` computes KPIs (Avg Price, Sqm Price, Stability) locally from the filtered metadata.
4.  `nt-statistika.js` -> `renderStats()` updates the UI cards instantly.

### Phase 2: Background Fetch (Throttled)
**Trigger**: 300ms after the last filter change.
1.  `nt-statistika.js` -> `debouncedUpdateAnalytics()` fires.
2.  `nt-statistika.js` -> `updateAnalytics()` sends parallel POST requests to:
    - `/api/statistics/market-trend` (Historical chart data)
    - `/api/statistics/listings` (Actual property list)
3.  UI components (`renderChart`, `renderListings`) update asynchronously.

---

## 3. Component Responsibilities

### main.js
- **Data Namespace**: API Orchestration (Storm Suppression) and Signature-based LocalStore.
- **Logic Namespace**: The "Brain" of the app. Handles faceted search (`FilterEngine`) and client-side KPI math (`calculateStats`).
- **Core Namespace**: Global state management and metadata initialization.
- **UI Namespace**: Shared UI components like `SelectionBox` (Modals/Chips).

### nt-statistika.js
- **UI Controller**: Orchestrates page-specific events (Filter changes, Modal interactions).
- **State Sync**: Bridges the DOM state with the Logic Engine.
- **Background Manager**: Manages the lifecycle of server-side data fetching and loading states.

### nt-duomenu-valymas.js
- **Utils.Cleaner**: Pure utility module for:
    - Localization/Labels (Keys to Lithuanian text).
    - Numeric Formatting (Price and Stability formatting).
    - Data Sanitization (Removing duplicates, cleaning strings).

---

## 4. Key Rules & Standards

- **Rule #23 (Real-Time)**: The `/api/statistics/analyze` endpoint is DEPRECATED. All price cards must be calculated locally.
- **Rule #20 (Isolation)**: Switching City MUST trigger a "Surgical Flush" of all secondary filters and chips.
- **Rule #8 (Storm Suppression)**: Rapidly repeating API requests for the same ID must abort previous pending requests.
- **Rule #10 (Signature Cache)**: Background fetch results are cached in LocalStore with a TTL of 1 hour, indexed by the MD5/JSON signature of the active filters.
