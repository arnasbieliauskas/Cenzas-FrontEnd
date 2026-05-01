/**
 * nt-statistika.js
 * UI Controller for Analytics Dashboard
 */

document.addEventListener('DOMContentLoaded', async () => {
    const { Data, Logic, Core } = window.CenzasAnalytics;

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

    // 1. Dependency Map Synchronization (Rule #20: 9-point dependency)
    function syncFilters() {
        const current = getActiveFilters();
        const available = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, current);
        
        if (!available) return;

        // Toggle visibility and update counts in hierarchical order
        updateFilterButton(districtBtn, 'Districts', available.Districts);
        updateFilterButton(streetsBtn, 'Streets', available.Streets);
        updateFilterButton(objectsBtn, 'Objects', available.Objects);
        updateFilterButton(roomsBtn, 'Rooms', available.Rooms);
        updateFilterButton(heatingBtn, 'Heating', available.Heating);
        updateFilterButton(equippedBtn, 'Equipped', available.Equipped);
        updateFilterButton(energyBtn, 'EnergyClass', available.EnergyClass);
    }

    function updateFilterButton(btn, key, options) {
        if (!btn) return;
        const countSpan = btn.querySelector('span');
        const selected = Array.from(document.querySelectorAll(`.checkbox-${key.toLowerCase()}:checked`));
        if (countSpan) countSpan.textContent = selected.length;
        
        // Show/Hide based on availability
        const section = btn.closest('.filter-group');
        if (section) section.style.display = options.length > 0 ? 'block' : 'none';
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

    // 3. Modal Management
    const modals = {
        districts: { btn: districtBtn, modal: document.getElementById('districts-modal'), container: document.getElementById('district-options'), key: 'Districts' },
        streets: { btn: streetsBtn, modal: document.getElementById('streets-modal'), container: document.getElementById('street-options'), key: 'Streets' },
        objects: { btn: objectsBtn, modal: document.getElementById('objects-modal'), container: document.getElementById('objects-options'), key: 'Objects' },
        rooms: { btn: roomsBtn, modal: document.getElementById('rooms-modal'), container: document.getElementById('rooms-options'), key: 'Rooms' },
        heating: { btn: heatingBtn, modal: document.getElementById('heating-modal'), container: document.getElementById('heating-options'), key: 'Heating' },
        equipped: { btn: equippedBtn, modal: document.getElementById('equipped-modal'), container: document.getElementById('equipped-options'), key: 'Equipped' },
        energy: { btn: energyBtn, modal: document.getElementById('energy-modal'), container: document.getElementById('energy-options'), key: 'EnergyClass' }
    };

    Object.keys(modals).forEach(id => {
        const m = modals[id];
        if (!m.btn) return;

        m.btn.addEventListener('click', () => {
            renderModalOptions(m);
            m.modal.classList.add('active');
        });

        const closeBtn = m.modal.querySelector('.close-modal');
        const confirmBtn = m.modal.querySelector('.btn--primary');

        [closeBtn, confirmBtn].forEach(b => b?.addEventListener('click', () => {
            m.modal.classList.remove('active');
            syncFilters();
        }));
    });

    function renderModalOptions(m) {
        const current = getActiveFilters();
        const available = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, current);
        const options = available[m.key] || [];
        
        m.container.innerHTML = options.map(opt => {
            const isChecked = current[m.key]?.includes(opt.toString());
            return `
                <label class="checkbox-group">
                    <input type="checkbox" class="checkbox-${m.key.toLowerCase()}" value="${opt}" ${isChecked ? 'checked' : ''}>
                    <span>${opt}</span>
                </label>
            `;
        }).join('');
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
