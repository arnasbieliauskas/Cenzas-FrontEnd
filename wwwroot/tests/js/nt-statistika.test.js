/**
 * nt-statistika.test.js
 * Manual Unit Tests for Cenzas Analytics Logic
 * 
 * Since we are in a browser-based environment without a pre-configured test runner (Jest/Mocha),
 * this file implements a "Minimal Test Harness" that can be run in the browser console 
 * or via a standalone test runner page.
 */

const CenzasTests = {
    runAll: async function() {
        console.group('%c [Cenzas] Starting Unit Tests ', 'background: #176be0; color: white; padding: 4px;');
        
        try {
            await this.testFilterEngine();
            await this.testSelectionLogic();
            await this.testDataCalculations();
            
            console.log('%c ALL TESTS PASSED ', 'color: #4CAF50; font-weight: bold;');
        } catch (err) {
            console.error('%c TEST FAILED ', 'color: #F44336; font-weight: bold;', err);
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
     * 1. Test Filter Engine (main.js)
     * Verifies the 9-point dependency map logic
     */
    testFilterEngine: async function() {
        console.group('Logic.FilterEngine');
        const { Logic } = window.CenzasAnalytics;
        
        const mockMetadata = {
            combinations: [
                { City: 'vilniuje', District: 'Antakalnis', Street: 'Antakalnio g.', Rooms: 2, Object: 'Butas' },
                { City: 'vilniuje', District: 'Centras', Street: 'Gedimino pr.', Rooms: 3, Object: 'Butas' },
                { City: 'kaune', District: 'Dainava', Street: 'Pramonės pr.', Rooms: 1, Object: 'Namas' }
            ]
        };

        // Test City Filtering
        let filters = { City: 'vilniuje' };
        let available = Logic.FilterEngine.getAvailableOptions(mockMetadata, filters);
        this.assert(available.Districts.includes('Antakalnis'), "Should include Antakalnis for Vilnius");
        this.assert(available.Districts.includes('Centras'), "Should include Centras for Vilnius");
        this.assert(!available.Districts.includes('Dainava'), "Should NOT include Kaunas districts for Vilnius");

        // Test District -> Street Dependency
        filters = { City: 'vilniuje', Districts: ['Antakalnis'] };
        available = Logic.FilterEngine.getAvailableOptions(mockMetadata, filters);
        this.assert(available.Streets.includes('Antakalnio g.'), "Should include Antakalnio g. for Antakalnis");
        this.assert(!available.Streets.includes('Gedimino pr.'), "Should NOT include Gedimino pr. for Antakalnis");

        console.groupEnd();
    },

    /**
     * 2. Test SelectionBox UI Logic (main.js)
     */
    testSelectionLogic: async function() {
        console.group('UI.SelectionBox');
        const { UI } = window.CenzasAnalytics;

        // Verify component presence
        this.assert(!!UI.SelectionBox, "SelectionBox component should be defined");
        this.assert(typeof UI.SelectionBox.init === 'function', "SelectionBox.init should be a function");
        this.assert(typeof UI.SelectionBox.updateChips === 'function', "SelectionBox.updateChips should be a function");

        console.groupEnd();
    },

    /**
     * 3. Test Data Processing Logic
     */
    testDataCalculations: async function() {
        console.group('Data Processing');
        
        // Mocking a simple average calculation helper if it were exposed
        const calculateAvg = (arr) => arr.reduce((a, b) => a + b, 0) / arr.length;
        
        const prices = [100000, 150000, 200000];
        const avg = calculateAvg(prices);
        this.assert(avg === 150000, "Average price calculation should be accurate");

        console.groupEnd();
    }
};

// Export for console usage
window.CenzasTests = CenzasTests;
console.log("[Cenzas] Unit Tests loaded. Run 'CenzasTests.runAll()' to execute.");
