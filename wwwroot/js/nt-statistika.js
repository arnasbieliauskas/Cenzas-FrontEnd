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

    const updateBtn = document.getElementById('btn-update-analytics');

    // UI Elements - Displays
    const avgPriceEl = document.getElementById('avg-price');
    const avgPriceSqmEl = document.getElementById('avg-price-sqm');
    const stabilityScoreEl = document.getElementById('stability-score');
    const totalCountEl = document.getElementById('total-count');
    const listingsContainer = document.getElementById('listings-container');

    let chart = null;

    // Wait for metadata
    await Core.init();

    // Helper to get formatted label from mapping
    function getLabel(key, category = 'cities') {
        const mapping = Core.state.mapping?.[category];
        if (mapping && mapping[key]) {
            return mapping[key];
        }
        // Fallback: Title Case
        return key.charAt(0).toUpperCase() + key.slice(1);
    }

    // Dynamically populate city selection from metadata
    if (Core.state.metadata?.combinations) {
        const PRIORITY_CITIES = ['vilniuje', 'kaune', 'klaipedoje', 'siauliuose', 'panevezyje', 'alytuje', 'palangoje'];
        
        const rawCities = [...new Set(Core.state.metadata.combinations.map(c => c.City))];
        
        // Group cities
        const priority = rawCities.filter(c => PRIORITY_CITIES.includes(c));
        const others = rawCities.filter(c => !PRIORITY_CITIES.includes(c));

        // Sort both groups by their mapped label
        const sortByLabel = (a, b) => getLabel(a, 'cities').localeCompare(getLabel(b, 'cities'), 'lt');
        priority.sort(sortByLabel);
        others.sort(sortByLabel);

        console.log('[Recovery] Dropdown Populated with Priority and Others groups');

        // Clear except placeholder
        while (citySelect.options.length > 1) {
            citySelect.remove(1);
        }

        // 1. Add Priority Cities
        priority.forEach(city => {
            const option = document.createElement('option');
            option.value = city;
            option.textContent = getLabel(city, 'cities'); 
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
            option.textContent = getLabel(city, 'cities'); 
            citySelect.appendChild(option);
        });
    }

    // 1. Dependency Map Synchronization (Rule #20: 9-point dependency)
    function syncFilters() {
        const current = getActiveFilters();
        const available = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, current);

        if (!available) return;

        // Update all filter selections
        syncFilterButton('districts', current.Districts);
        syncFilterButton('streets', current.Streets);
        syncFilterButton('objects', current.Objects);
        syncFilterButton('rooms', current.Rooms);
        syncFilterButton('heating', current.Heating);
        syncFilterButton('equipped', current.Equipped);
        syncFilterButton('energy', current.EnergyClass);

        // Hide/Show sections based on availability
        toggleSectionVisibility('district-group', available.Districts);
        toggleSectionVisibility('street-group', available.Streets);
    }

    function syncFilterButton(id, selectedItems) {
        const container = document.getElementById(`${id}-chips`);
        if (!container) return;

        if (!selectedItems || selectedItems.length === 0) {
            container.textContent = 'Pasirinkti';
            return;
        }

        // Render chips
        container.innerHTML = selectedItems.map(item => `
            <span class="chip">
                ${item}
            </span>
        `).join('');
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
            PriceMin: document.getElementById('price-min').value || null,
            PriceMax: document.getElementById('price-max').value || null,
            AreaMin: document.getElementById('area-min').value || null,
            AreaMax: document.getElementById('area-max').value || null,
            YearMin: document.getElementById('year-min').value || null,
            YearMax: document.getElementById('year-max').value || null,
            DateFrom: document.getElementById('date-from').value || null,
            DateTo: document.getElementById('date-to').value || null
        };
    }

    function getSelectedValues(selector) {
        return Array.from(document.querySelectorAll(`${selector}:checked`)).map(cb => cb.value);
    }

    // 2. Parallel API Requests
    async function updateAnalytics() {
        const filters = getActiveFilters();
        if (!filters.City) {
            alert('Prašome pasirinkti miestą.');
            return;
        }

        setLoading(true);

        try {
            // FIRE PARALLEL REQUESTS (Rule #2 from request)
            const [stats, trend, listings] = await Promise.all([
                Data.Service.post('/api/statistics/analyze', filters, 'stats'),
                Data.Service.post('/api/statistics/market-trend', filters, 'trend'),
                Data.Service.post('/api/statistics/listings', { ...filters, Page: 1 }, 'listings')
            ]);

            renderStats(stats);
            renderChart(trend);
            renderListings(listings.Listings);
            totalCountEl.textContent = listings.TotalCount;

        } catch (err) {
            if (err.name !== 'AbortError') console.error('Update failed:', err);
        } finally {
            setLoading(false);
        }
    }

    function renderStats(data) {
        avgPriceEl.textContent = data.AvgPrice ? `${Math.round(data.AvgPrice).toLocaleString()} €` : '—';
        avgPriceSqmEl.textContent = data.AvgPriceSqm ? `${Math.round(data.AvgPriceSqm).toLocaleString()} €` : '—';
        stabilityScoreEl.textContent = data.StabilityScore !== undefined ? `${data.StabilityScore}/100` : '—';
    }

    function renderChart(trendData) {
        const ctx = document.getElementById('marketTrendChart').getContext('2d');
        if (chart) chart.destroy();

        chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: trendData.map(d => d.Label),
                datasets: [{
                    label: 'Kaina',
                    data: trendData.map(d => d.Value),
                    borderColor: '#176be0',
                    tension: 0.4,
                    fill: true,
                    backgroundColor: 'rgba(23, 107, 224, 0.05)'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } }
            }
        });
    }

    function renderListings(listings) {
        listingsContainer.innerHTML = listings.map(l => `
            <div class="listing-card animate-fade-in">
                <div class="listing-card__content">
                    <h3 class="listing-card__title">${l.Title}</h3>
                    <div style="font-size: 24px; font-weight: 700; color: var(--color-primary); margin: 12px 0;">
                        ${Math.round(l.Price).toLocaleString()} €
                    </div>
                    <div style="font-size: 14px; color: var(--color-text-light);">
                        ${l.District} | ${l.Area} m²
                    </div>
                </div>
            </div>
        `).join('');
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
        const available = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, current);
        const options = available[config.key] || [];

        UI.SelectionBox.renderOptions(config, options, current[config.key] || []);
    }

    function setLoading(isLoading) {
        const els = [avgPriceEl, avgPriceSqmEl, stabilityScoreEl];
        els.forEach(el => el.classList.toggle('skeleton-text', isLoading));
    }

    // Event Listeners
    citySelect.addEventListener('change', () => {
        // Clear all filters when city changes (Rule #20: top-down reset)
        document.querySelectorAll('input[type="checkbox"]').forEach(cb => cb.checked = false);
        syncFilters();
    });

    updateBtn.addEventListener('click', updateAnalytics);

    // Initial Sync
    syncFilters();
});
