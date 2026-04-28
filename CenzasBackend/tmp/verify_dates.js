const fs = require('fs');
const path = 'c:/VisualStudioProjects/Cenzas FrontEnd/CenzasBackend/wwwroot/data/filters-metadata.json';

try {
    const data = JSON.parse(fs.readFileSync(path, 'utf8'));
    const city = data.Cities.find(c => c.City === 'Klaipėdoje');
    if (!city) {
        console.log('City not found');
        process.exit(1);
    }

    const combos = city.Combinations.filter(c => 
        c.District === 'Kaunas' && c.Street === 'Kauno g.'
    );

    if (combos.length === 0) {
        console.log('No combinations found for Kaunas / Kauno g. in Klaipėdoje');
        // Let's try searching for Kaunas in districts
        const districts = [...new Set(city.Combinations.map(c => c.District))];
        console.log('Available districts:', districts.filter(d => d.includes('Kaunas')));
        process.exit(1);
    }

    const dates = combos.map(c => new Date(c.CollectedOn).getTime()).filter(d => !isNaN(d));
    if (dates.length === 0) {
        console.log('No valid dates found');
        process.exit(1);
    }

    const minDate = new Date(Math.min(...dates)).toISOString().split('T')[0];
    const maxDate = new Date(Math.max(...dates)).toISOString().split('T')[0];

    console.log(`Min: ${minDate}`);
    console.log(`Max: ${maxDate}`);
} catch (e) {
    console.error(e);
}
