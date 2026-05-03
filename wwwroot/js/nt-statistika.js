/**
 * nt-statistika.js
 * UI Controller for Analytics Dashboard
 */

document.addEventListener('DOMContentLoaded', async () => {
    const { Data, Logic, Core, UI } = window.CenzasAnalytics;

    // UI Elements - Filters
    const citySelect = document.getElementById('filter-city');
    const districtBtn = document.getElementById('btn-select-districts');
    const streetsBtn = document.getElementById('btn-select-streets');
    const objectsBtn = document.getElementById('btn-select-objects');
    const roomsBtn = document.getElementById('btn-select-rooms');
    const heatingBtn = document.getElementById('btn-select-heating');
    const equippedBtn = document.getElementById('btn-select-equipped');
    const energyBtn = document.getElementById('btn-select-energy');

    // const updateBtn = document.getElementById('btn-update-analytics'); // Rule #23: Removed manual trigger

    // UI Elements - Displays
    const avgPriceEl = document.getElementById('avg-price');
    const avgPriceSqmEl = document.getElementById('avg-price-sqm');
    const stabilityScoreEl = document.getElementById('stability-score');
    const totalCountEl = document.getElementById('total-count');
    const listingsContainer = document.getElementById('listings-container');
    const listingsWrapper = document.getElementById('listings-wrapper');

    let chart = null;

    // Wait for metadata
    await Core.init();

    const { Cleaner } = window.CenzasAnalytics.Utils;
    let currentFilteredData = []; 
    let currentPriceFilter = 'all'; 
    let currentStatusFilter = 'all'; 

    // Dynamically populate city selection from metadata
    if (Core.state.metadata?.combinations) {
        const PRIORITY_CITIES = ['vilniuje', 'kaune', 'klaipedoje', 'siauliuose', 'panevezyje', 'alytuje', 'palangoje'];
        
        const rawCities = [...new Set(Core.state.metadata.combinations.map(c => c.City))];
        const cleanedCities = Cleaner.sanitizeList(rawCities);
        
        // Group cities
        const priority = cleanedCities.filter(c => PRIORITY_CITIES.includes(c));
        const others = cleanedCities.filter(c => !PRIORITY_CITIES.includes(c));

        // Sort both groups by their mapped label
        const sortByLabel = (a, b) => Cleaner.getLabel(a, 'cities').localeCompare(Cleaner.getLabel(b, 'cities'), 'lt');
        priority.sort(sortByLabel);
        others.sort(sortByLabel);

        console.log('[Cleaner] Dropdown Populated with Sanitized Groups');

        // Clear except placeholder
        while (citySelect.options.length > 1) {
            citySelect.remove(1);
        }

        // 1. Add Priority Cities
        priority.forEach(city => {
            const option = document.createElement('option');
            option.value = city;
            option.textContent = Cleaner.getLabel(city, 'cities'); 
            citySelect.appendChild(option);
        });

        // 2. Add Divider
        if (priority.length > 0 && others.length > 0) {
            const divider = document.createElement('option');
            divider.disabled = true;
            divider.textContent = '──────────';
            citySelect.appendChild(divider);
        }

        // 3. Add Others
        others.forEach(city => {
            const option = document.createElement('option');
            option.value = city;
            option.textContent = Cleaner.getLabel(city, 'cities'); 
            citySelect.appendChild(option);
        });
    }

    // 1. Dependency Map Synchronization (Rule #20: 9-point dependency)
    function syncFilters() {
        try {
            const current = getActiveFilters();

            // Rule: Empty Initial State Guard
            if (!current.City) {
                avgPriceEl.textContent = '—';
                avgPriceSqmEl.textContent = '—';
                stabilityScoreEl.textContent = '—';
                totalCountEl.textContent = '0';
                if (listingsContainer) listingsContainer.innerHTML = '';
                if (chart) {
                    chart.destroy();
                    chart = null;
                }
                const pc = document.getElementById('pagination-container');
                if (pc) pc.style.display = 'none';
                return;
            }

            // Rule #23: Calculate visibility based on City context ONLY to prevent vanishing sections
            const cityOnlyContext = { City: current.City };
            const visibility = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, cityOnlyContext);
            
            // Calculate available options and stats based on FULL filter set
            const available = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, current);

            if (!available || !visibility) return;

            // Update all filter selections (Chips)
            syncFilterButton('districts', current.Districts);
            syncFilterButton('streets', current.Streets);
            syncFilterButton('objects', current.Objects);
            syncFilterButton('rooms', current.Rooms);
            syncFilterButton('heating', current.Heating);
            syncFilterButton('equipped', current.Equipped);
            syncFilterButton('energy', current.EnergyClass);

            // Hide/Show sections based on CITY context only (Prevents UI jumpiness)
            toggleSectionVisibility('district-group', visibility.Districts || []);
            toggleSectionVisibility('street-group', visibility.Streets || []);

            // Rule #24: Metadata-Driven Authority
            let combinations = available.combinations || [];
            
            // 1. Apply Price Trend Filter
            if (currentPriceFilter === 'up') {
                combinations = combinations.filter(c => {
                    const initial = parseFloat(c.InitialPrice || c.initialPrice || 0);
                    const latest = parseFloat(c.LatestPrice || c.latestPrice || c.Price || c.price || 0);
                    return latest > initial && latest !== 0;
                });
            } else if (currentPriceFilter === 'down') {
                combinations = combinations.filter(c => {
                    const initial = parseFloat(c.InitialPrice || c.initialPrice || 0);
                    const latest = parseFloat(c.LatestPrice || c.latestPrice || c.Price || c.price || 0);
                    return latest < initial && latest !== 0;
                });
            }

            // 2. Apply Expiration/Status Filter
            if (currentStatusFilter === 'valid' || currentStatusFilter === 'expired') {
                const thresholdDays = parseInt(Core.state.metadata?.ListingExpirationDays) || 1;
                const today = new Date();
                today.setHours(0, 0, 0, 0); 
                
                combinations = combinations.filter(c => {
                    if (!c) return false;
                    const latestDateStr = c.LatestDate || c.latestDate;
                    if (!latestDateStr) return currentStatusFilter === 'expired'; 
                    
                    const latest = new Date(latestDateStr);
                    latest.setHours(0, 0, 0, 0);
                    
                    const diffTime = Math.abs(today - latest);
                    const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));
                    const isExpired = diffDays > thresholdDays;
                    
                    return currentStatusFilter === 'expired' ? isExpired : !isExpired;
                });
            }
            
            currentFilteredData = combinations;
            Core.state.currentPage = 1;

            // Rule #23.2: Local Calculation Engine (Instant KPIs)
            const stats = Logic.calculateStats(currentFilteredData);
            renderStats(stats);
            
            // Rule #23.4: Background Fetches (Debounced Trend Only)
            debouncedUpdateAnalytics();
            
            // Rule #24: Instant List Rendering
            refreshListings();
        } catch (err) {
            console.error('[Cenzas] syncFilters failed:', err);
        }
    }

    // Rule #21.6: Throttling Standards
    let updateTimeout = null;
    function debouncedUpdateAnalytics() {
        if (updateTimeout) clearTimeout(updateTimeout);
        updateTimeout = setTimeout(() => updateAnalytics(), 300);
    }

    function syncFilterButton(id, selectedItems) {
        const container = document.getElementById(`${id}-chips`);
        if (!container) return;

        if (!selectedItems || selectedItems.length === 0) {
            container.textContent = 'Pasirinkti';
            return;
        }

        // Render chips
        container.innerHTML = selectedItems.map(item => {
            const label = id === 'objects' ? Cleaner.getLabel(item, 'objects') : item;
            return `<span class="chip">${label}</span>`;
        }).join('');
    }

    function toggleSectionVisibility(id, options) {
        const section = document.getElementById(id);
        if (section) {
            section.style.display = options.length > 0 ? 'block' : 'none';
        }
    }

    function getActiveFilters() {
        return {
            City: citySelect.value,
            Districts: getSelectedValues('.checkbox-districts'),
            Streets: getSelectedValues('.checkbox-streets'),
            Objects: getSelectedValues('.checkbox-objects'),
            Rooms: getSelectedValues('.checkbox-rooms'),
            Heating: getSelectedValues('.checkbox-heating'),
            Equipped: getSelectedValues('.checkbox-equipped'),
            EnergyClass: getSelectedValues('.checkbox-energyclass'),
            PriceFrom: document.getElementById('price-min').value || null,
            PriceTo: document.getElementById('price-max').value || null,
            AreaFrom: document.getElementById('area-min').value || null,
            AreaTo: document.getElementById('area-max').value || null,
            BuildYearFrom: document.getElementById('year-min').value || null,
            BuildYearTo: document.getElementById('year-max').value || null,
            DateFrom: document.getElementById('date-from').value || null,
            DateTo: document.getElementById('date-to').value || null,
            
            // New Filter Axes (Rule #5)
            PriceStatus: currentPriceFilter,
            ValidityStatus: currentStatusFilter,
            ExpiredThresholdDays: parseInt(Core.state.metadata?.ListingExpirationDays) || 1
        };
    }

    function getSelectedValues(selector) {
        return Array.from(document.querySelectorAll(`${selector}:checked`)).map(cb => cb.value);
    }

    // 2. Parallel API Requests (Background Data)
    async function updateAnalytics() {
        const filters = getActiveFilters();
        if (!filters.City) {
            return;
        }

        try {
            // Rule #10: Check LocalStore Cache for Trend
            const cachedTrend = Data.LocalStore.get('trend', filters);
            let trend;

            if (cachedTrend) {
                trend = cachedTrend;
            } else {
                // Rule #23.4: Trend Fetch Only (Listings are now local)
                trend = await Data.Service.post('/api/statistics/market-trend', filters, 'trend-load');
                Data.LocalStore.set('trend', filters, trend);
            }

            renderChart(trend);
        } catch (err) {
            if (err.name !== 'AbortError') console.error('Background update failed:', err);
        }
    }

    // Rule #24: Local Pagination & Rendering
    function refreshListings() {
        if (!listingsContainer) return;

        const total = currentFilteredData.length;
        if (totalCountEl) totalCountEl.textContent = total.toLocaleString('lt-LT');

        if (total === 0) {
            listingsContainer.innerHTML = `
                <div class="empty-state" style="grid-column: 1/-1; text-align: center; padding: 40px; color: var(--color-text-light);">
                    <p>Skelbimų pagal jūsų kriterijus nerasta.</p>
                </div>
            `;
            const pc = document.getElementById('pagination-container');
            if (pc) pc.style.display = 'none';
            return;
        }

        const pageSize = 25;
        const start = (Core.state.currentPage - 1) * pageSize;
        const pageData = currentFilteredData.slice(start, start + pageSize);

        renderListings(pageData);
        renderPagination(total);

        // Ensure pagination is visible if multiple pages
        const pc = document.getElementById('pagination-container');
        if (pc) pc.style.display = total > pageSize ? 'block' : 'none';
    }

    function renderPagination(totalItems) {
        if (!listingsContainer) return;
        
        const pageSize = 25;
        const totalPages = Math.ceil(totalItems / pageSize);
        
        let paginationContainer = document.getElementById('pagination-container');
        if (!paginationContainer) {
            paginationContainer = document.createElement('div');
            paginationContainer.id = 'pagination-container';
            paginationContainer.className = 'pagination-controls';
            listingsContainer.after(paginationContainer);
        }

        if (totalPages <= 1) {
            paginationContainer.innerHTML = '';
            paginationContainer.style.display = 'none';
            return;
        }
        
        paginationContainer.style.display = 'block';

        const page = Core.state.currentPage;
        
        // Rule #24.2: Dynamic Sliding Window (±5 Pages)
        let start = Math.max(1, page - 5);
        let end = Math.min(totalPages, page + 5);

        // Build page numbers
        let pagesHtml = '';
        
        // Always show page 1 + dots if start > 1
        if (start > 1) {
            pagesHtml += `<button class="page-num" data-page="1">1</button>`;
            if (start > 2) pagesHtml += `<span class="page-dots">...</span>`;
        }

        for (let i = start; i <= end; i++) {
            pagesHtml += `<button class="page-num ${i === page ? 'is-active' : ''}" data-page="${i}">${i}</button>`;
        }

        // Always show last page + dots if end < totalPages
        if (end < totalPages) {
            if (end < totalPages - 1) pagesHtml += `<span class="page-dots">...</span>`;
            pagesHtml += `<button class="page-num" data-page="${totalPages}">${totalPages}</button>`;
        }

        paginationContainer.innerHTML = `
            <div class="pagination-inner">
                <button class="btn btn--secondary btn--sm" id="btn-prev" ${page === 1 ? 'disabled' : ''}>Atgal</button>
                <div class="page-numbers">${pagesHtml}</div>
                <button class="btn btn--secondary btn--sm" id="btn-next" ${page === totalPages ? 'disabled' : ''}>Pirmyn</button>
            </div>
        `;

        // Event Listeners
        paginationContainer.querySelectorAll('.page-num').forEach(btn => {
            btn.addEventListener('click', () => {
                const newPage = parseInt(btn.dataset.page);
                if (newPage !== Core.state.currentPage) {
                    Core.state.currentPage = newPage;
                    refreshListings();
                    window.scrollTo({ top: listingsContainer.offsetTop - 100, behavior: 'smooth' });
                }
            });
        });

        document.getElementById('btn-prev')?.addEventListener('click', () => {
            if (Core.state.currentPage > 1) {
                Core.state.currentPage--;
                refreshListings();
                window.scrollTo({ top: listingsContainer.offsetTop - 100, behavior: 'smooth' });
            }
        });

        document.getElementById('btn-next')?.addEventListener('click', () => {
            if (Core.state.currentPage < totalPages) {
                Core.state.currentPage++;
                refreshListings();
                window.scrollTo({ top: listingsContainer.offsetTop - 100, behavior: 'smooth' });
            }
        });
    }

    function renderStats(data) {
        avgPriceEl.textContent = Cleaner.formatPrice(data.avgPrice);
        avgPriceSqmEl.textContent = Cleaner.formatPrice(data.avgPricePerM2);
        stabilityScoreEl.textContent = Cleaner.formatMarketStability(data.initialAvg, data.latestAvg);
        // Note: totalCountEl is updated via Phase 2 server response in updateAnalytics
    }

    function renderChart(trendData) {
        // Rule #23: Robust Data Normalization (direct array or wrapped object)
        const dataArray = Array.isArray(trendData) ? trendData : (trendData?.trends || trendData?.Trends || []);
        
        const canvas = document.getElementById('marketTrendChart');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        if (chart) chart.destroy();

        if (dataArray.length === 0) {
            console.warn('[Cleaner] No trend data available for chart');
            return;
        }

        // Create Gradients
        const priceGradient = ctx.createLinearGradient(0, 0, 0, 400);
        priceGradient.addColorStop(0, 'rgba(23, 107, 224, 0.1)');
        priceGradient.addColorStop(1, 'rgba(23, 107, 224, 0)');

        const countGradient = ctx.createLinearGradient(0, 0, 0, 400);
        countGradient.addColorStop(0, 'rgba(100, 116, 139, 0.1)');
        countGradient.addColorStop(1, 'rgba(100, 116, 139, 0)');

        chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: dataArray.map(d => d.t),
                datasets: [
                    {
                        label: 'Vidutinė kaina (€)',
                        data: dataArray.map(d => d.v),
                        borderColor: '#176be0',
                        backgroundColor: priceGradient,
                        borderWidth: 3,
                        tension: 0.4,
                        fill: true,
                        yAxisID: 'y',
                        pointRadius: 0,
                        pointHoverRadius: 6,
                        pointHoverBackgroundColor: '#176be0',
                        pointHoverBorderColor: '#fff',
                        pointHoverBorderWidth: 2
                    },
                    {
                        label: 'Skelbimų kiekis',
                        data: dataArray.map(d => d.c),
                        borderColor: '#64748b',
                        backgroundColor: countGradient,
                        borderWidth: 2,
                        borderDash: [5, 5],
                        tension: 0.4,
                        fill: true,
                        yAxisID: 'y1',
                        pointRadius: 0,
                        pointHoverRadius: 4,
                        pointHoverBackgroundColor: '#64748b',
                        pointHoverBorderColor: '#fff',
                        pointHoverBorderWidth: 2
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false,
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        align: 'end',
                        labels: {
                            usePointStyle: true,
                            boxWidth: 6,
                            font: { size: 12, weight: '600' },
                            padding: 20
                        }
                    },
                    tooltip: {
                        backgroundColor: '#fff',
                        titleColor: '#1e293b',
                        bodyColor: '#475569',
                        borderColor: '#e2e8f0',
                        borderWidth: 1,
                        padding: 12,
                        boxPadding: 6,
                        usePointStyle: true,
                        callbacks: {
                            label: function(context) {
                                let label = context.dataset.label || '';
                                if (label) label += ': ';
                                if (context.datasetIndex === 0) {
                                    label += new Intl.NumberFormat('lt-LT').format(context.parsed.y) + ' €';
                                } else {
                                    label += context.parsed.y;
                                }
                                return label;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: {
                            font: { size: 11 },
                            color: '#94a3b8',
                            maxRotation: 0,
                            autoSkip: true,
                            maxTicksLimit: 10
                        }
                    },
                    y: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        grid: { color: '#f1f5f9' },
                        ticks: {
                            font: { size: 11 },
                            color: '#94a3b8',
                            callback: value => value.toLocaleString('lt-LT') + ' €'
                        }
                    },
                    y1: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        grid: { drawOnChartArea: false },
                        ticks: {
                            font: { size: 11 },
                            color: '#94a3b8',
                            callback: value => value
                        }
                    }
                }
            }
        });
    }

    function renderListings(listings) {
        if (!listingsContainer) return;

        if (!listings || listings.length === 0) {
            listingsContainer.innerHTML = `
                <div class="empty-state" style="grid-column: 1/-1; text-align: center; padding: 40px; color: var(--color-text-light);">
                    <p>Skelbimų pagal jūsų kriterijus nerasta.</p>
                </div>
            `;
            return;
        }

        listingsContainer.innerHTML = listings.map(l => {
            if (!l) return '';

            // Mapping with Fallbacks (PascalCase from Metadata)
            const rawType = l.Object || l.object || l.Title || l.title || 'Skelbimas';
            const title = Cleaner.getLabel(rawType, 'objects');
            
            const initialPrice = Math.round(parseFloat(l.InitialPrice || l.initialPrice || 0));
            let latestPrice = Math.round(parseFloat(l.LatestPrice || l.latestPrice || l.Price || l.price || 0));
            
            // Safeguard: If latest price is 0 (100% drop), treat as no change and show initial price
            if (latestPrice === 0) {
                latestPrice = initialPrice;
            }

            const priceDiff = latestPrice - initialPrice;
            const priceChangePct = initialPrice > 0 ? ((priceDiff / initialPrice) * 100).toFixed(1) : 0;
            
            const district = l.District || l.district || '';
            const street = l.Street || l.street || '';
            const rooms = l.Rooms || l.rooms || 0;
            const area = l.Area || l.area || 0;
            const year = l.BuildYear || l.buildYear || '';
            
            const heating = l.Heating || l.heating || '';
            const equipped = l.Equipped || l.equipped || '';
            const energy = l.EnergyClass || l.energyClass || '';
            const initialDate = l.InitialDate || l.initialDate || '';
            const latestDate = l.LatestDate || l.latestDate || '';
            const url = l.Url || l.url || '#';

            // Rule #26: Expiration Guard (Probability of invalidity)
            const thresholdDays = parseInt(Core.state.metadata?.ListingExpirationDays) || 1;
            let isExpired = false;
            if (latestDate) {
                const latest = new Date(latestDate);
                latest.setHours(0, 0, 0, 0);
                const today = new Date();
                today.setHours(0, 0, 0, 0);
                const diffTime = Math.abs(today - latest);
                const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));
                if (diffDays > thresholdDays) {
                    isExpired = true;
                }
            }

            return `
                <div class="listing-card animate-fade-in ${isExpired ? 'listing-card--expired' : ''}">
                    ${isExpired ? `
                        <div class="listing-card__expired-tag">
                            Tikėtina, kad negalioja
                        </div>
                    ` : ''}
                    <div class="listing-card__content">
                        <h3 class="listing-card__title">${title}</h3>
                        <div class="listing-card__price-wrapper">
                            <div class="listing-card__prices">
                                ${priceDiff !== 0 ? `
                                    <div class="price-item price-item--initial">
                                        <span class="price-value">${initialPrice.toLocaleString('lt-LT')} €</span>
                                    </div>
                                ` : ''}
                                <div class="price-item price-item--latest">
                                    <span class="price-value">${latestPrice.toLocaleString('lt-LT')} €</span>
                                </div>
                            </div>
                            ${priceDiff !== 0 ? `
                                <div class="price-change ${priceDiff < 0 ? 'price-change--down' : 'price-change--up'}">
                                    ${priceDiff < 0 ? '↓' : '↑'} ${Math.abs(priceChangePct)}%
                                </div>
                            ` : ''}
                        </div>
                        <div class="listing-card__location">${district}${street ? `, ${street}` : ''}</div>
                        <div class="listing-card__details">
                            <span><strong>${rooms}</strong> kamb.</span> | <span><strong>${area}</strong> m²</span> | <span><strong>${year}</strong> m.</span>
                        </div>
                        <div class="listing-card__dates">
                            <div class="date-item">Pirma reg.: <span>${Cleaner.formatDate(initialDate)}</span></div>
                            <div class="date-item">Paskutinė reg.: <span>${Cleaner.formatDate(latestDate)}</span></div>
                        </div>
                        <div class="listing-card__footer">
                            <div class="listing-card__badges">
                                ${heating ? `<span class="badge">${heating}</span>` : ''}
                                ${equipped ? `<span class="badge">${equipped}</span>` : ''}
                                ${energy ? `<span class="badge badge--energy">${energy}</span>` : ''}
                            </div>
                            <a href="${url}" target="_blank" class="btn-view-listing" title="Peržiūrėti originalų skelbimą">
                                Peržiūrėti
                                <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            </a>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    // 3. Modal & SelectionBox Management
    const filterConfigs = {
        districts: { modalId: 'districts-modal', containerId: 'district-options', chipAreaId: 'districts-modal-chips', clearBtnId: 'clear-districts', checkboxClass: 'checkbox-districts', key: 'Districts', btn: districtBtn },
        streets: { modalId: 'streets-modal', containerId: 'street-options', chipAreaId: 'streets-modal-chips', clearBtnId: 'clear-streets', checkboxClass: 'checkbox-streets', key: 'Streets', btn: streetsBtn },
        objects: { modalId: 'objects-modal', containerId: 'objects-options', chipAreaId: 'objects-modal-chips', clearBtnId: 'clear-objects', checkboxClass: 'checkbox-objects', key: 'Objects', btn: objectsBtn },
        rooms: { modalId: 'rooms-modal', containerId: 'rooms-options', chipAreaId: 'rooms-modal-chips', clearBtnId: 'clear-rooms', checkboxClass: 'checkbox-rooms', key: 'Rooms', btn: roomsBtn },
        heating: { modalId: 'heating-modal', containerId: 'heating-options', chipAreaId: 'heating-modal-chips', clearBtnId: 'clear-heating', checkboxClass: 'checkbox-heating', key: 'Heating', btn: heatingBtn },
        equipped: { modalId: 'equipped-modal', containerId: 'equipped-options', chipAreaId: 'equipped-modal-chips', clearBtnId: 'clear-equipped', checkboxClass: 'checkbox-equipped', key: 'Equipped', btn: equippedBtn },
        energy: { modalId: 'energy-modal', containerId: 'energy-options', chipAreaId: 'energy-modal-chips', clearBtnId: 'clear-energy', checkboxClass: 'checkbox-energyclass', key: 'EnergyClass', btn: energyBtn }
    };

    // Initialize all SelectionBoxes
    Object.keys(filterConfigs).forEach(id => {
        const config = filterConfigs[id];
        UI.SelectionBox.init(config);

        // Open Modal Handler
        config.btn?.addEventListener('click', () => {
            renderFilterOptions(config);
            document.getElementById(config.modalId).classList.add('active');
        });

        // Close/Confirm Handler
        const modal = document.getElementById(config.modalId);
        const closeBtn = modal.querySelector('.close-modal');
        const confirmBtn = modal.querySelector('.btn--primary');

        [closeBtn, confirmBtn].forEach(b => b?.addEventListener('click', () => {
            modal.classList.remove('active');
            syncFilters();
        }));
    });

    function renderFilterOptions(config) {
        const current = getActiveFilters();
        
        // Rule: Clear current category filter to show all globally available options for the selected context
        const forEngine = { ...current, [config.key]: [] };
        const available = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, forEngine);
        
        let optionsSet = new Set((available[config.key] || []).map(o => o.toString()));
        
        // Rule #23: Selection Preservation Logic
        // Ensure currently selected items stay in the list even if hidden by other cross-filters
        const selected = (current[config.key] || []).map(s => s.toString());
        selected.forEach(s => optionsSet.add(s));

        // Convert back to array and sort (numerically if possible, otherwise alphabetically)
        let options = Array.from(optionsSet).sort((a, b) => {
            const numA = parseFloat(a);
            const numB = parseFloat(b);
            if (!isNaN(numA) && !isNaN(numB)) return numA - numB;
            return a.localeCompare(b, 'lt');
        });

        // Rule #25: Map labels for Objects category before rendering
        if (config.key === 'Objects') {
            options = options.map(opt => ({
                value: opt,
                label: Cleaner.getLabel(opt, 'objects')
            }));
        }

        UI.SelectionBox.renderOptions(config, options, selected);
    }

    function setLoading(isLoading) {
        const els = [avgPriceEl, avgPriceSqmEl, stabilityScoreEl];
        els.forEach(el => el.classList.toggle('skeleton-text', isLoading));
    }

    // Event Listeners
    citySelect.addEventListener('change', () => {
        // Rule #20: Top-down reset of all secondary filters
        document.querySelectorAll('input[type="checkbox"]').forEach(cb => cb.checked = false);
        
        // Strict State & UI Reset (Rule #23)
        Object.values(filterConfigs).forEach(config => {
            const container = document.getElementById(config.containerId);
            if (container) container.innerHTML = ''; // Clear modal options
            UI.SelectionBox.updateChips(config); // Physical removal of ghost chips/tags
        });

        // Trigger fresh calculation for the new city (with zero other filters)
        syncFilters();
    });

    // Filter Tab Handlers
    document.querySelectorAll('.filter-tabs').forEach(group => {
        const groupType = group.dataset.group;
        const tabs = group.querySelectorAll('.filter-tab');
        
        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                tabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
                
                if (groupType === 'price') {
                    currentPriceFilter = tab.dataset.type;
                } else if (groupType === 'status') {
                    currentStatusFilter = tab.dataset.type;
                }
                
                syncFilters();
            });
        });
    });

    // Initial Sync
    syncFilters();
});
