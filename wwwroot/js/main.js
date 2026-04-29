/**
 * Cenzas Analytics - main.js
 * Primary Orchestrator & Logic Engine
 */

window.CenzasAnalytics = window.CenzasAnalytics || {};

// ====================================================================
// 1. DATA NAMESPACE: API Orchestration & Persistence
// ====================================================================
window.CenzasAnalytics.Data = {
    Service: {
        activeRequests: new Map(),

        fetchMetadata: async function() {
            const response = await fetch('/data/filters-metadata.json');
            if (!response.ok) throw new Error("Nepavyko užkrauti filtrų duomenų.");
            return await response.json();
        },

        /**
         * Enhanced POST with Storm Suppression
         * Aborts previous identical requestId to prevent race conditions
         */
        post: async function(endpoint, payload, requestId = 'global') {
            // Rule #8: Storm Suppression logic
            if (this.activeRequests.has(requestId)) {
                this.activeRequests.get(requestId).abort();
            }

            const controller = new AbortController();
            this.activeRequests.set(requestId, controller);

            try {
                const response = await fetch(endpoint, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload),
                    signal: controller.signal
                });

                if (!response.ok) throw new Error(`API Klaida: ${response.statusText}`);
                return await response.json();
            } finally {
                if (this.activeRequests.get(requestId) === controller) {
                    this.activeRequests.delete(requestId);
                }
            }
        }
    },

    /**
     * Signature-based LocalStore (Rule #10)
     * TTL: 1 Hour
     */
    LocalStore: {
        _ttl: 3600000, // 1 Hour in ms

        get: function(blockId, payload) {
            const signature = btoa(JSON.stringify(payload));
            const key = `cenzas_${blockId}_${signature}`;
            const cached = localStorage.getItem(key);
            
            if (cached) {
                const data = JSON.parse(cached);
                if (Date.now() - data.timestamp < this._ttl) {
                    return data.results;
                }
                localStorage.removeItem(key);
            }
            return null;
        },

        set: function(blockId, payload, results) {
            const signature = btoa(JSON.stringify(payload));
            const key = `cenzas_${blockId}_${signature}`;
            localStorage.setItem(key, JSON.stringify({
                results: results,
                timestamp: Date.now()
            }));
        }
    }
};

// ====================================================================
// 2. LOGIC NAMESPACE: 9-Point Filter Engine
// ====================================================================
window.CenzasAnalytics.Logic = {
    FilterEngine: {
        /**
         * 9-point Dependency Map (City -> District -> Street -> Rooms -> Object -> Heating -> Equipped -> EnergyClass -> Title)
         */
        getAvailableOptions: function(metadata, currentFilters) {
            if (!metadata || !metadata.combinations) return null;

            const results = {
                Districts: new Set(),
                Streets: new Set(),
                Rooms: new Set(),
                Objects: new Set(),
                Heating: new Set(),
                Equipped: new Set(),
                EnergyClass: new Set(),
                Titles: new Set()
            };

            for (const c of metadata.combinations) {
                // Hierarchical Guard
                if (currentFilters.City && c.City !== currentFilters.City) continue;
                if (currentFilters.Districts?.length && !currentFilters.Districts.includes(c.District)) continue;
                if (currentFilters.Streets?.length && !currentFilters.Streets.includes(c.Street)) continue;
                if (currentFilters.Rooms?.length && !currentFilters.Rooms.includes(c.Rooms)) continue;
                if (currentFilters.Objects?.length && !currentFilters.Objects.includes(c.Object)) continue;

                results.Districts.add(c.District);
                results.Streets.add(c.Street);
                results.Rooms.add(c.Rooms);
                results.Objects.add(c.Object);
                results.Heating.add(c.Heating);
                results.Equipped.add(c.Equipped);
                results.EnergyClass.add(c.EnergyClass);
                results.Titles.add(c.Title);
            }

            const combinations = Array.from(metadata.combinations).filter(c => {
                if (currentFilters.City && c.City !== currentFilters.City) return false;
                if (currentFilters.Districts?.length && !currentFilters.Districts.includes(c.District)) return false;
                if (currentFilters.Streets?.length && !currentFilters.Streets.includes(c.Street)) return false;
                if (currentFilters.Rooms?.length && !currentFilters.Rooms.includes(c.Rooms)) return false;
                if (currentFilters.Objects?.length && !currentFilters.Objects.includes(c.Object)) return false;
                return true;
            });

            return {
                Districts: Array.from(results.Districts).sort(),
                Streets: Array.from(results.Streets).sort(),
                Rooms: Array.from(results.Rooms).sort((a, b) => a - b),
                Objects: Array.from(results.Objects).sort(),
                Heating: Array.from(results.Heating).sort(),
                Equipped: Array.from(results.Equipped).sort(),
                EnergyClass: Array.from(results.EnergyClass).sort(),
                Titles: Array.from(results.Titles).sort(),
                matchCount: combinations.length,
                availableDateRange: {
                    min: combinations.length ? combinations.reduce((min, c) => c.Date < min ? c.Date : min, combinations[0].Date) : null,
                    max: combinations.length ? combinations.reduce((max, c) => c.Date > max ? c.Date : max, combinations[0].Date) : null
                }
            };
        }
    }
};

// ====================================================================
// 3. CORE NAMESPACE: Global App State
// ====================================================================
window.CenzasAnalytics.Core = {
    state: {
        metadata: null,
        isMetadataLoading: false
    },

    init: async function() {
        if (this.state.isMetadataLoading) return;
        this.state.isMetadataLoading = true;
        
        try {
            this.state.metadata = await window.CenzasAnalytics.Data.Service.fetchMetadata();
            console.log("[Cenzas] Metadata loaded.");
        } catch (err) {
            console.error("[Cenzas] Core init failed:", err);
        } finally {
            this.state.isMetadataLoading = false;
        }
    }
};

// ====================================================================
// 4. UTILS NAMESPACE: UI Helpers
// ====================================================================
window.CenzasAnalytics.UI = {
    /**
     * Unified component loader
     */
    loadComponents: async function() {
        await Promise.all([
            this.loadComponent('header-placeholder', 'components/header.html'),
            this.loadComponent('footer-placeholder', 'components/footer.html'),
            this.loadComponent('loan-application-placeholder', 'components/loan-application-section.html')
        ]);
        
        if (window.initLoanForm) window.initLoanForm();
        this.highlightActiveLink();
        this.initNTAnalysisHandler();
    },

    loadComponent: async function(id, path) {
        const placeholder = document.getElementById(id);
        if (!placeholder) return;

        try {
            const response = await fetch(path);
            if (!response.ok) throw new Error(`Nepavyko užkrauti komponento: ${path}`);
            placeholder.innerHTML = await response.text();
            console.log(`[Cenzas] ${id} injected.`);
        } catch (err) {
            console.error(`[Cenzas] Component load failed (${path}):`, err);
        }
    },

    highlightActiveLink: function() {
        const currentPath = window.location.pathname.split('/').pop() || 'index.html';
        const links = document.querySelectorAll('.nav__link');
        links.forEach(link => {
            if (link.getAttribute('href') === currentPath) {
                link.classList.add('nav__link--active');
            } else {
                link.classList.remove('nav__link--active');
            }
        });
    },

    initNTAnalysisHandler: function() {
        const btn = document.getElementById('btn-nav-analysis');
        if (!btn) return;

        btn.addEventListener('click', function(e) {
            if (window.CenzasAnalytics?.UI?.LegalDisclaimer && !window.CenzasAnalytics.UI.LegalDisclaimer.accepted) {
                e.preventDefault();
                window.CenzasAnalytics.UI.LegalDisclaimer.init();
                
                window.addEventListener('cenzas:consent_given', function() {
                    window.location.href = 'nt-statistika.html';
                }, { once: true });
            }
        });
    }
};

// Auto-boot
document.addEventListener('DOMContentLoaded', () => {
    window.CenzasAnalytics.Core.init();
    window.CenzasAnalytics.UI.loadComponents();
});
