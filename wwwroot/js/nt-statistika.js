/**
 * nt-statistika.js
 * UI Controller for Analytics Dashboard
 */

document.addEventListener('DOMContentLoaded', async () => {
    const { Data, Logic, Core } = window.CenzasAnalytics;

    // UI Elements - Filters
    const citySelect = document.getElementById('filter-city');
    const districtBtn = document.getElementById('btn-select-districts');
    const roomsBtn = document.getElementById('btn-select-rooms');
    const objectsBtn = document.getElementById('btn-select-objects');
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

    // 1. Dependency Map Synchronization
    function syncFilters() {
        const current = getActiveFilters();
        const available = Logic.FilterEngine.getAvailableOptions(Core.state.metadata, current);
        
        if (!available) return;

        // Toggle visibility and update counts
        updateFilterButton(districtBtn, 'Districts', available.Districts);
        updateFilterButton(roomsBtn, 'Rooms', available.Rooms);
        updateFilterButton(objectsBtn, 'Objects', available.Objects);
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
        btn.closest('.filter-group').style.display = options.length > 0 ? 'block' : 'none';
    }

    function getActiveFilters() {
        return {
            City: citySelect.value,
            Districts: getSelectedValues('.checkbox-districts'),
            Rooms: getSelectedValues('.checkbox-rooms'),
            Objects: getSelectedValues('.checkbox-objects'),
            Heating: getSelectedValues('.checkbox-heating'),
            Equipped: getSelectedValues('.checkbox-equipped'),
            EnergyClass: getSelectedValues('.checkbox-energyclass'),
            PriceMin: document.getElementById('price-min').value || null,
            PriceMax: document.getElementById('price-max').value || null,
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

    function setLoading(isLoading) {
        const els = [avgPriceEl, avgPriceSqmEl, stabilityScoreEl];
        els.forEach(el => el.classList.toggle('skeleton-text', isLoading));
    }

    // Event Listeners
    citySelect.addEventListener('change', syncFilters);
    updateBtn.addEventListener('click', updateAnalytics);

    // Initial Sync
    syncFilters();
});
