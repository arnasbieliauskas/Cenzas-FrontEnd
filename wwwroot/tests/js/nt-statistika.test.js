/**
 * nt-statistika.test.js
 * Manual Unit Tests for Cenzas Analytics Logic & Utils
 */

const CenzasTests = {
    runAll: async function() {
        console.group('%c [Cenzas] Starting Unit Tests ', 'background: #176be0; color: white; padding: 4px;');
        
        try {
            await this.testCleanerModule();
            await this.testFilterEngine();
            await this.testSelectionLogic();
            
            console.log('%c ALL TESTS PASSED ', 'color: #4CAF50; font-weight: bold;');
        } catch (err) {
            console.error('%c TEST FAILED ', 'color: #F44336; font-weight: bold;', err);
            console.error(err);
        }
        
        console.groupEnd();
    },

    assert: function(condition, message) {
        if (!condition) {
            throw new Error(message || "Assertion failed");
        }
        console.log(`%c PASS: ${message}`, 'color: #4CAF50');
    },

    /**
     * 1. Test Utils.Cleaner Module
     * Verifies mapping and sanitization logic
     */
    testCleanerModule: async function() {
        console.group('Utils.Cleaner');
        const { Cleaner } = window.CenzasAnalytics.Utils;
        
        this.assert(!!Cleaner, "Cleaner module should be initialized");

        // Test getLabel fallback
        const fallback = Cleaner.getLabel('test_key', 'none');
        this.assert(fallback === 'Test_key', "Fallback should convert to Title Case");

        // Test getLabel with mapping (mocking state if necessary, but assuming loaded)
        const cityLabel = Cleaner.getLabel('vilniuje', 'cities');
        this.assert(cityLabel === 'Vilniuje', "Should map 'vilniuje' to 'Vilniuje'");

        // Test sanitizeList
        const dirty = ['  valid  ', null, '', '  ', 'another'];
        const clean = Cleaner.sanitizeList(dirty);
        this.assert(clean.length === 2, "Should remove null and empty strings");
        this.assert(clean[0] === 'valid', "Should trim whitespace");
        this.assert(clean[1] === 'another', "Should keep valid strings");

        console.groupEnd();
    },

    /**
     * 2. Test Filter Engine
     * Verifies the 9-point dependency map logic
     */
    testFilterEngine: async function() {
        console.group('Logic.FilterEngine');
        const { Logic } = window.CenzasAnalytics;
        
        const mockMetadata = {
            combinations: [
                { City: 'vilniuje', District: 'Antakalnis', Street: 'Antakalnio g.', Rooms: '2', Object: 'Butas' },
                { City: 'vilniuje', District: 'Centras', Street: 'Gedimino pr.', Rooms: '3', Object: 'Butas' },
                { City: 'kaune', District: 'Dainava', Street: 'Pramonės pr.', Rooms: '1', Object: 'Namas' }
            ]
        };

        // Test City Filtering
        let filters = { City: 'vilniuje' };
        let available = Logic.FilterEngine.getAvailableOptions(mockMetadata, filters);
        this.assert(available.Districts.includes('Antakalnis'), "Should include Antakalnis for Vilnius");
        this.assert(!available.Districts.includes('Dainava'), "Should NOT include Kaunas districts for Vilnius");

        // Test Multi-select Districts
        filters = { City: 'vilniuje', Districts: ['Antakalnis', 'Centras'] };
        available = Logic.FilterEngine.getAvailableOptions(mockMetadata, filters);
        this.assert(available.Streets.includes('Antakalnio g.'), "Should include Antakalnio g.");
        this.assert(available.Streets.includes('Gedimino pr.'), "Should include Gedimino pr.");

        console.groupEnd();
    },

    /**
     * 3. Test SelectionBox Component Presence
     */
    testSelectionLogic: async function() {
        console.group('UI.SelectionBox');
        const { UI } = window.CenzasAnalytics;

        this.assert(!!UI.SelectionBox, "SelectionBox should exist");
        this.assert(typeof UI.SelectionBox.init === 'function', "Should have init method");
        this.assert(typeof UI.SelectionBox.renderOptions === 'function', "Should have renderOptions method");

        console.groupEnd();
    }
};

window.CenzasTests = CenzasTests;
console.log("[Cenzas] Modernized Unit Tests loaded. Run 'CenzasTests.runAll()' to execute.");
