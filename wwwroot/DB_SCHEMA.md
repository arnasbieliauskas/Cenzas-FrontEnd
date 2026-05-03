# Cenzas Analytics: Database Schema & Performance (DB_SCHEMA.md)

This document defines the MySQL database structure, indexing strategy, and data integrity rules for the Cenzas platform, managing a dataset of **844k+ records**.

---

## 1. `addlist` Table Schema
The primary table containing the latest metadata and current status for all property listings.

| Column | Type | Description |
| :--- | :--- | :--- |
| `id` | INT | Primary Key (Auto-increment). |
| `ExternalId` | VARCHAR(255) | Unique identifier from the source RPA import. |
| `City` | VARCHAR(100) | Major city (Vilnius, Kaunas, etc.). |
| `District` | VARCHAR(100) | Neighborhood or district name. |
| `Street` | VARCHAR(255) | Street name (nullable). |
| `Rooms` | INT | Number of rooms. |
| `Object` | VARCHAR(100) | Type of listing (e.g., "Butai pardavimui"). |
| `Price` | DECIMAL(15,2) | Current listed price in EUR. |
| `Area` | DECIMAL(10,2) | Total area in m². |
| `BuildYear` | INT | Year of construction. |
| `Renovation` | INT | Year of renovation (nullable). |
| `Heating` | VARCHAR(100) | Heating system type. |
| `Equipped` | VARCHAR(100) | Equipment status (e.g., "Įrengtas"). |
| `EnergyClass` | VARCHAR(10) | Energy efficiency class (A++, B, etc.). |
| `LastCollectedDate` | DATETIME | Timestamp of the most recent snapshot in `secaddcollection`. |

## 2. `secaddcollection` Table Schema
A high-volume historical table used for time-series trend analysis.

| Column | Type | Description |
| :--- | :--- | :--- |
| `id` | INT | Primary Key. |
| `ExternalId` | VARCHAR(255) | Foreign Key reference to `addlist`. |
| `Price` | DECIMAL(15,2) | Recorded price at the time of snapshot. |
| `secdata` | DATE | The date the snapshot was captured. |

## 3. `rpa_job` Table Schema
Operational table for monitoring automated data imports.

| Column | Type | Description |
| :--- | :--- | :--- |
| `id` | INT | Primary Key. |
| `status` | VARCHAR(50) | Current job status (`done`, `pending`, `failed`). |
| `finished_at` | DATETIME | Completion timestamp. |

## 4. Critical Performance Indexes (13 Total)
Strategic indexing is mandatory to maintain sub-second response times on the 844k+ dataset.

### `addlist` Indexes:
1.  **`idx_addlist_lookup_v2`**: `(City, District, Street, Rooms, Object)` - Primary composite index for cross-filtering.
2.  **`idx_addlist_external`**: `(ExternalId)` - Used for joins and RPA updates.
3.  **`idx_last_collected`**: `(LastCollectedDate)` - Facilitates "Likely Expired" detection.
4.  **`idx_city_district`**: `(City, District)` - High-speed city-level filtering.
5.  **`idx_rooms`**: `(Rooms)` - Room count filtering.
6.  **`idx_addlist_city_buildyear`**: `(City, BuildYear)` - Range query optimization.
7.  **`idx_addlist_city_renovation`**: `(City, Renovation)` - Renovation range optimization.
8.  **`idx_addlist_city_energy`**: `(City, EnergyClass)` - Energy class filtering.
9.  **`idx_addlist_heating_equipped`**: `(Heating, Equipped)` - Building detail filtering.

### `secaddcollection` Indexes:
10. **`idx_secadd_external_date`**: `(ExternalId, secdata DESC)` - Rapid retrieval of latest historical price.
11. **`idx_secadd_price`**: `(Price)` - Price-based filtering in trends.
12. **`idx_secadd_date_only`**: `(secdata)` - Grouping for Daily/Weekly/Monthly aggregation.
13. **`idx_secadd_date_price`**: `(secdata, Price)` - Covering index for trend calculations.

## 5. Data Relationships
*   **One-to-Many**: `addlist` (1) -> `secaddcollection` (N). Each master listing record is linked to multiple historical price snapshots via `ExternalId`.
*   **Trend Joins**: All trend queries prioritize filtering `addlist` via indexed columns before joining `secaddcollection` to ensure performance.

## 6. Data Integrity & Maintenance
*   **Sync Logic**: The `LastCollectedDate` in `addlist` must be updated after every RPA job using the `MAX(secdata)` from `secaddcollection` for that `ExternalId`.
*   **Optimization**: RPA job completion triggers `DatabaseMaintenanceService` which executes `OPTIMIZE TABLE` to defragment storage and ensures index health.

## 7. Default UI States & Filter Logic
*   **Initial State**: All building-specific filters (Heating, Equipped, EnergyClass, BuildYear, Renovation) must be unchecked/empty by default.
*   **Status Logic**: Selection indicators for listing status (`filter-valid`, `filter-expired`) must be unchecked.
    4. Price status buttons UI must be updated (set "Visi" as active).
* **Rationale:** This prevents users from seeing a filtered dashboard (e.g., only expired listings) for a new location without realizing a refinement is still active.

## 8. Indexing for Mass Trends
* **Covering Indexes:** The `secaddcollection` table uses `idx_secadd_date_only` and `idx_secadd_date_price` to allow lightning-fast `AVG(Price)` calculations grouped by date.
* **Query Strategy:** When fetching trend data, always prioritize filtering the `addlist` table first, then join `secaddcollection` using `ExternalId`.

## 9. Trend Aggregation Logic
* **Backend Responsiblity:** The C# API must return ONLY aggregated data (e.g., one row per day or week with an average price). 
* **Data Volume:** Never transmit more than 500 data points to the frontend to keep the chart interactive and responsive.

## 10. Statistical Data Integrity
* **Null Handling:** Always check for `DBNull` when reading aggregate results (like `AVG(Price)`) in C#. If the result is null, return 0 or skip the data point to prevent `InvalidCastException`.
* **Query Resilience:** All statistical queries involving joins between `addlist` and `secaddcollection` must have a `CommandTimeout` of at least 120 seconds.

## 11. Statistical Anomaly Suppression
* **Zero Price Exclusion:** The system must ignore any aggregate data point where the average price is 0 or less to ensure the integrity of visualizations and trend analysis.

## 12. Extended Building Filters & Anomaly Suppression
* **Supported Columns (addlist):** The system must support filtering by `buildyear` (int), `renovation` (int), `heating` (string), `equipped` (string), and `energyclass` (string).
* **Filter Logic Implementation:**
    * **Year Ranges:** Fields like `buildyear` and `renovation` must support "From" and "To" range filtering.
    * **String Matches:** Fields like `heating`, `equipped`, and `energyclass` must use `LIKE @param` for security and flexible matching (e.g., handling "A++" or "Dujinis").
* **Statistical Anomaly Suppression:** * The `market-trend` SQL query must include a `HAVING AveragePrice > 0` clause to prevent data gaps from creating vertical drops to zero on the chart.
    * The C# data reader loop must perform a safety check: if the aggregated price is 0 or less, use `continue;` to skip the record entirely.
* **Performance:** All new filters must target the `a` alias (the `addlist` table) to leverage existing composite indexes and maintain sub-second response times.

## 13. UI Filter Patterns & Logic
* **Modal-Tag Pattern:** `heating`, `equipped`, and `energyclass` MUST follow the same UI/UX pattern as Districts/Streets. This includes a trigger button, a modal with checkboxes, and a tag container for active selections.
* **Range Pattern:** `buildyear` and `renovation` MUST be implemented as numeric range inputs ("From" and "To") placed directly in the filter row.
* **State Management:** All new filter values must be integrated into the global `request` object in `nt-statistika.js` and trigger the `debouncedRefresh()` function upon change.

## 14. Contextual Watermarks for Range Filters
* **Logic:** The `api/statistics/analyze` endpoint must return the absolute minimum and maximum values for ALL range-based fields: `Price`, `Area`, `BuildYear`, and `Renovation`.
* **UI Implementation:** The frontend must use these values to update the `placeholder` attribute of the corresponding "From" and "To" inputs.
* **Formatting:** Years should be displayed as simple integers (e.g., "Nuo: 1757"), while area and price follow established decimal/currency formatting.

## 15. SQL String Construction & List Filtering
* **SQL Spacing:** Always ensure a white space exists between dynamic clauses (e.g., `{joinClause} {baseWhereClause}`) to prevent syntax errors like `aWHERE`.
* **Multi-Value Logic:** Filters derived from modals (Heating, Equipped, EnergyClass) must be transmitted as Arrays (List<string>) and processed using SQL `IN` clauses, identical to the District/Street logic. `LIKE` is forbidden for multi-select fields.

## 16. UI Layout & Placeholder Visibility
* **Responsive Minimums:** Year range inputs must have a minimum width that prevents text truncation on standard desktop resolutions.

* **Grid Proportions:** In rows containing both ranges and multi-select modals, range inputs MUST be prioritized for width (e.g., 1.5fr to 2fr) to ensure watermarks like "Nuo: 1757" are fully legible.
* **Filter State Sync:** The `resetFilters` function in `nt-statistika.js` must clear all building-specific inputs, tags, and re-initialize modal containers to maintain sub-second response times on the 844k+ dataset.

## 17. Dynamic Trend Period & Discovery
* **Default Range Removal:** The `MarketTrend` endpoint MUST NOT apply a default 12-month limit if `DateFrom` is missing. It must show the full available history based on the primary filters.
* **Adaptive Aggregation:** To respect Rule #9 (max 500 points), the SQL must adaptively group data:
    * If the time span is > 180 days, group by `YEARWEEK(s.secdata)`.
    * If the time span is > 730 days, group by `MONTH(s.secdata)`.
* **User Override:** Specific dates selected in the main UI (`DateFrom`/`DateTo`) must always take precedence over the dynamic discovery logic.

## 18. Object Selection Enforcement (UX Guard)
* **Goal:** Prevent inaccurate statistical averages by ensuring users are aware when no specific "Object" (e.g., House, Apartment) is selected.
* **Interceptor Logic:** If `selectedObjects` array is empty, clicking "Analyze Market" or "Show Listings" must trigger a one-time warning popup per session or until a city/filter reset.
* **Warning Actions:** * "Select Object": Closes warning and opens the existing Object Selection modal.
    * "I Understand": Closes warning and allows the request to proceed.
* **State Reset:** The warning flag (`hasSeenObjectWarning`) must be reset to `false` upon city change or "Clear Filters" action.

## 19. Object Selection Guard & Color Standards
* **UX Guard Logic:** If `selectedObjects` is empty, clicking "Analyze Market" or "Show Listings" must trigger a one-time warning popup (`object-warning-modal`) to ensure statistical accuracy.
* **Visual Standards for Price Changes:**
    * **Price Drop (Atpigę):** MUST be displayed in **GREEN**. This indicates a discount/value for the buyer.
    * **Price Hike (Pabrangę):** MUST be displayed in **RED**. This indicates a price increase.
* **State Sync:** The `hasSeenObjectWarning` flag must reset to `false` whenever the city is changed or "Clear Filters" is pressed.

## 20. Expanded Metadata & Cross-Filtering Logic (Stage 1)
* **Metadata Structure:** The `filters-metadata.json` (or the internal `combinations` array) must be expanded to include: `Heating`, `Equipped`, and `EnergyClass` for every record.
* **Filtering Engine:** The `updateDropdowns` function in `nt-statistika.js` must be refactored to support a 9-point dependency map: `City` -> `District` -> `Street` -> `Rooms` -> `Object` -> `Heating` -> `Equipped` -> `EnergyClass` -> `Title`.
* **Dynamic Visibility:** Instead of just graying out options, the UI must hide (`display: none`) any checkbox/label in the modals if that specific combination does not exist in the metadata for the current selection.
* **Initialization:** Metadata must be re-processed every time the primary `City` selection changes to ensure sub-second UI responsiveness.

## Rule #21: Advanced State Persistence & Performance Orchestration
Objective: To ensure near-instant UI responsiveness and prevent server-side resource exhaustion in the Comparison Module, especially when handling datasets exceeding 844k+ records.

* **21.1 Dual-Layer Data Persistence:**

All successful API responses (Analysis and Trends) MUST be stored in a signature-based LocalStore (for instant retrieval when filters are toggled back) AND mirrored in the global Core.state.activeFilters.blocks[].results object.

The results object in the state acts as the authoritative source for UI re-hydration during re-render cycles.

* **21.2 Surgical UI Updates:**

Avoid full container re-renders (e.g., innerHTML = '') during block removal. Use surgical DOM manipulation to remove specific elements and update indices/titles (A, B, C...) manually to maintain state without triggering new network requests.

* **21.3 Auth Priority Guard (Data Integrity):**

During UI re-hydration or re-rendering, the system MUST prioritize premium API results (block.results) over local engine estimations (FilterEngine.getAvailableOptions).

Local estimations should only be displayed if no server-side data exists for the current filter signature.

* **21.4 Request Storm & Zombie Query Suppression:**

Frontend: Implement a global AbortController for all trend and analysis fetches. Subsequent UI interactions MUST abort pending obsolete requests before firing new ones.

Backend: Mandatory propagation of CancellationToken (ct) from the API Controller down to the MySqlConnector async methods (e.g., ExecuteReaderAsync(ct)). This ensures that aborted frontend requests physically terminate expensive SQL queries on the server.

* **21.5 Counter & Metric Restoration:**

The "Rasta skelbimų" counter and "Market Health" metrics MUST be restored synchronously from the state during UI rendering.

A loading skeleton MUST only be shown if data is missing from both the LocalStore and the global state object.

* **21.6 Throttling Standards:**

Market Trend fetches (heavy aggregation) MUST use a 200ms debounce.

Analysis fetches (standard metrics) MUST use a 300ms debounce.

## Rule #22: 
* **Metadata Synchronization & Startup StabilizationData Source:** 
    Metadata MUST be extracted directly from the addlist table using an optimized SELECT DISTINCT query to populate the 9-point dependency map.  
* **Required Attributes:**
     Every record in the combinations array MUST include: City, District, Address (mapped to frontend as Street), Rooms, Object, Heating, Equipped, EnergyClass, BuildYear, and Renovation.  
* **Startup Execution:**
     The system MUST force a metadata refresh during the application startup sequence in Program.cs. This ensures the filters-metadata.json is valid and present before any frontend requests are served.  
* **Data Integrity & Type Safety:**
     Varchar-to-Int Mapping: Database fields BuildYear and Renovation are stored as VARCHAR(16/64) and MUST be safely parsed to int in C# (defaulting to 0 on NULL or failure).  
* **Atomic Persistence:**
     The JSON output MUST be minified (WriteIndented = false) and saved using atomic byte-level writing (File.WriteAllBytesAsync) to prevent null-byte corruption or whitespace filling.  
* **Capacity:**
     The generator is verified to handle and export at least 28,000+ unique combinations within sub-second execution time.  

## Rule #23:
* **Real-Time Client-Side Analytics OrchestrationObjective:**
     To achieve near-instant UI responsiveness and minimize network overhead by calculating core market KPIs directly from the metadata stored in browser memory. 
* **23.1 Removal of Manual Refresh:**
     The manual "ATNAUJINTI ANALIZĘ" button MUST be removed from the UI. All statistical recalculations MUST be triggered automatically upon any filter state change (Trigger-on-Change). 
* **23.2 Local Calculation Engine:**
     Primary KPIs (Average Price, Price per Sqm, and Market Stability) MUST be calculated on the client side using the Logic.calculateStats function, iterating over the filtered Core.state.metadata.combinations array.  
* **23.3 Price Lifecycle Utilization:**
     Market Stability calculation MUST utilize the InitialPrice (baseline) and LatestPrice (current) fields provided in the metadata. Formula: Stability = 100 - (abs(LatestAvg - InitialAvg) / InitialAvg * 100).  
* **23.4 API Request Suppression:**
     The /api/statistics/analyze endpoint is officially deprecated and MUST NOT be called during filtering. Only /api/statistics/market-trend and /api/statistics/listings remain active as background fetches.  
* **23.5 Zero-Value Data Guard:**
     The calculation loop MUST exclude any record where LatestPrice or InitialPrice is 0 or null to prevent skewed statistical averages and division-by-zero errors.  

## Rule #24:
* **Instant Metadata-Driven Pagination**
      * **Data Source Authority**The property listing list must be generated exclusively from the combinations array returned by Logic.FilterEngine.getAvailableOptions.Phase 2 (Background Fetch) via /api/statistics/listings is deprecated for the primary list rendering to avoid server overhead and latency.  
* **Pagination Logic** 
      (The "25-Rule")Page Size: Exactly 25 items per view.Slicing Mechanism: Use JavaScript .slice() on the local filtered array: const pageData = filteredArray.slice((page-1) * 25, page * 25).  
* **State Management**
     currentPage must be tracked in the CenzasAnalytics.Core.state and reset to 1 every time the City or any secondary filter changes.  
* **UI Synchronization**
     Header Count: The "Skelbimų Sąrašas (X)" count must always equal filteredArray.length.  
* **Navigation Controls:** 
     Render "Previous", "Next", and "Page X of Y" controls below the listing container.  
* **Lazy Rendering:** 
     Only the 25 active property cards should exist in the DOM at any given time to prevent browser memory exhaustion.  
* **Execution Order**
     Filter Change: User interacts with UI.  
     Logic Update: FilterEngine computes the valid subset from metadata.  
     KPI Render: Instant update of Average Price, Sqm Price, and Stability.  
     List Slice: Take the first 25 records from the computed subset.  
     DOM Render: renderListings clears the container and draws the 25 cards.  

#    Rule #25:
* **High-Volume Data Orchestration & Indexing Strategy**
     * **1. Data Loading Pipeline (The "Snapshot" Protocol)Atomic Refreshes:**
           The analytics_snapshot table MUST be fully rebuilt using a DROP/CREATE cycle to eliminate data fragmentation and ensure peak query performance.
     * **Temporary Compute:**
           Prior to populating the snapshot, all current market prices from secaddcollection MUST be pre-calculated using a TEMPORARY TABLE. The use of nested sub-queries within the primary INSERT statement is strictly forbidden to prevent SQL timeouts.  
     * **Sanitization at Rest:** 
           SQL functions such as TRIM() or LOWER() MUST NOT be used on indexed columns (ExternalId, City) during query execution. Data must be sanitized and normalized during the RPA import phase.  
     * **2. Mandatory Indexing StandardsCovering Indexes:** 
           The secaddcollection table MUST maintain a composite index on (ExternalId, secdata, Price) to allow trend extraction directly from the index tree without accessing raw data pages.  
     * **Search Optimization:**
           The addlist table MUST utilize the idx_addlist_lookup_v2 index, covering the 5 primary filter dimensions (City, District, Street, Rooms, Object) to ensure sub-second filtering.  
     * **RPA Synchronicity:**
           Every record in the addlist table MUST be indexed by LastCollectedDate to allow the RpaJobWatcher to instantly identify stale or missing listings.  
     * **3. Execution & Timeout SafetyDifferentiated Timeouts:**
           Standard schema validations carry a 600s limit, while high-volume data operations (Snapshot population) are granted a priority limit of 1200s (20 minutes).  
     * **Post-Maintenance Clean-up:**
           Every mass data load MUST be followed by an OPTIMIZE TABLE command for both core tables to defragment storage and rebuild index statistics.  
     * **4. Watcher DependencyVerification Guard:**
           The RpaJobWatcher MUST NOT initiate the MetadataGeneratorService (Step 2) until the DatabaseMaintenanceService (Step 1) explicitly confirms the successful completion of the Snapshot generation.  
     