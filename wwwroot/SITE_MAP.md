# Cenzas Platform: SITE_MAP.md

This document serves as the **Functional Roadmap** for the Cenzas platform. It maps the User Interface (UI) to the underlying technical logic defined in `APP_LOGIC.md` and the data structures in `DB_SCHEMA.md`.

---

## 1. System Architecture & Global Logic

The platform follows a modular, namespace-encapsulated design to ensure scalability and maintainability.

### 1.1 Core Namespaces (`window.CenzasAnalytics`)
| Namespace | Responsibility | Primary File |
| :--- | :--- | :--- |
| `Core` | Application lifecycle, block management, and module registration. | `main.js` |
| `Data` | API communication (`Service`) and client-side caching (`LocalStore`). | `main.js` |
| `UI` | Reusable components (Modals, MultiSelect, Toggles, Navigation). | `main.js` |
| `Logic` | Heavy-lifting client-side operations (e.g., `FilterEngine`). | `main.js` |
| `Utils` | Shared utilities (Debounce, Data Sanitize helpers). | `main.js`, `nt-duomenu-valymas.js` |

### 1.2 Global Infrastructure
*   **Navigation & Header**: Handled by `UI.Navigation` and `UI.Header` modules in `main.js`.
*   **State Management**: Centralized in `Core.state`, tracking active comparison blocks and date synchronization.
*   **Caching**: `Data.LocalStore` implements signature-based caching for API responses (Analyze/Trend) to reduce backend load.

---

## 2. Functional Sections Breakdown

### 2.1 Static Informational Sections
These sections provide business context and service descriptions. They are primarily informational with basic navigation logic.

*   **Home Page (`index.html`)**: Overview of services and entry point to Analytics.
*   **About Us (`apie.html`)**: Company information and mission.
*   **Services (`greitos-paskolos.html`, `paskolos-be-pajamu-vertinimo.html`)**: Detailed loan products.
*   **Contacts (`kontaktai.html`)**: Physical address, Google Maps integration, and contact details.

### 2.2 Application Form (Pildyti paraišką)
**File**: `pildyti-paraiska.html` | **Logic**: `UI.Filters.Validator` (`main.js`)

#### Workflow:
1.  **Input**: User fills in loan amount, term, property details, and personal info.
2.  **Validation**: Triggered on `submit` event. The `Validator` module checks for required fields and proper formatting (e.g., email, phone).
3.  **Submission**: Data is gathered into a JSON payload and sent via a POST request to the backend.
4.  **Feedback**: Upon success, `success-modal` is displayed; on failure, validation errors are shown inline.

**Event Flow**:  
`User Input` -> `Submit Click` -> `Validator.init()` -> `Browser/Custom Validation Check` -> `Fetch POST` -> `UI Success/Error Display`.

---

## 3. Analytics Module: Market Overview
**File**: `nt-statistika.html` | **Logic**: `nt-statistika.js`

Provides real-time analysis of the 844k+ record database.

#### Functional Components:
*   **Search Engine**: Powered by `Logic.FilterEngine`, implementing a 9-point dependency map to prevent "Zero Result" selections.
*   **Stats Grid**: Displays Avg Price, Avg Price/m², and Total Offers using skeleton loaders during fetch.
*   **Listings View**: Paginated view of raw listing data with sortable cards and "Likely Expired" badges.

#### Logical Bindings:
*   **Filters**: `UI.MultiSelect` (Modals for Districts, Streets, Rooms, etc.).
*   **Data Service**: Calls `/api/statistics/analyze` via `CenzasAnalytics.Data.Service`.
*   **Data Link**: Interacts with the `addlist` table for current listing status.

---

## 4. Analytics Module: Comparison
**File**: `nt-palyginimas.html` | **Logic**: `nt-palyginimas.js`, `nt-palyginimas-grafikas.js`

Advanced multi-dataset comparison with interactive visualizations.

#### Functional Components:
*   **Multi-Block Workspace**: Users can add/remove comparison blocks (e.g., Vilnius vs. Kaunas).
*   **Synchronized Charting**: Unified Chart.js visualization for multiple time-series datasets.
*   **Focus Mode**: Visual isolation of trending lines on hover (`nt-palyginimas-grafikas.js`).

#### Logical Bindings:
*   **Namespaces**: `UI.Charts.ComparisonChart`, `Core.state.activeFilters.blocks`.
*   **Data Service**: Calls `/api/statistics/market-trend`.
*   **Data Link**: References `secaddcollection` for historical price trends.

**Event Flow (Comparison)**:  
`Filter Change` -> `Core.state Update` -> `Data.Service.triggerFetch()` (Debounced) -> `LocalStore Check` -> `API Call (MarketTrend)` -> `ComparisonChart.update()`.

---

## 5. Data & Backend Interconnect

### 5.1 Primary API Endpoints
| Endpoint | Consumer | Description |
| :--- | :--- | :--- |
| `POST /api/statistics/analyze` | Market Overview | Calculates high-level summary statistics. |
| `POST /api/statistics/market-trend` | Comparison | Returns aggregated time-series data for charting. |
| `GET /data/filters-metadata.json` | Global | Static dependency map for cities/districts/streets. |

### 5.2 Database References (See `DB_SCHEMA.md`)
*   **`addlist`**: Primary source for Market Overview and active filters. Uses `idx_addlist_lookup_v2` for high-speed searches.
*   **`secaddcollection`**: Historical data source for Comparison trends. Polled via `ExternalId`.
*   **Maintenance**: RPA detection triggers `DatabaseMaintenanceService` to sync metadata and optimize indexes.

---

## 6. Implementation Principles

*   **Modular Design**: Each UI section is treated as an independent module. Complex logic (e.g., `FilterEngine`) is decoupled from DOM manipulation.
*   **Single Source of Truth**: UI configurations (like `ExpiredThresholdDays`) must match the backend's source of truth defined in `APP_LOGIC.md` Section 4.
*   **Storm Suppression**: All data-fetching interactions are debounced (200-300ms) to prevent database congestion.

---
*Document Version: 1.0 (Architecture Final)*
