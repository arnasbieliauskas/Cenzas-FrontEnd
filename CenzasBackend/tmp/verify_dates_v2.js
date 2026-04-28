const fs = require('fs');
const path = 'c:/VisualStudioProjects/Cenzas FrontEnd/CenzasBackend/wwwroot/data/filters-metadata.json';

try {
    const data = JSON.parse(fs.readFileSync(path, 'utf8'));
    const city = data.Cities.find(c => c.City === 'Klaipėdoje');
    const combos = city.Combinations.filter(c => c.Street === 'Kauno g.');
    
    const districtStats = {};
    combos.forEach(c => {
        if (!districtStats[c.District]) {
            districtStats[c.District] = { min: c.CollectedOn, max: c.CollectedOn, count: 0 };
        }
        if (c.CollectedOn < districtStats[c.District].min) districtStats[c.District].min = c.CollectedOn;
        if (c.CollectedOn > districtStats[c.District].max) districtStats[c.District].max = c.CollectedOn;
        districtStats[c.District].count++;
    });

    console.log(JSON.stringify(districtStats, null, 2));
} catch (e) {
    console.error(e);
}
