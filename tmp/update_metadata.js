const fs = require('fs');
const path = 'c:/VisualStudioProjects/Cenzas FrontEnd/CenzasBackend/wwwroot/data/filters-metadata.json';
const data = JSON.parse(fs.readFileSync(path, 'utf8'));

data.GlobalSettings = {
    ExpiredThresholdDays: 1,
    ActiveListingsLabel: "Galiojantys skelbimai",
    ExpirationWarningText: "Tikėtina, kad nebegalioja"
};

// Reorder keys to keep LastUpdated at top if possible, or just stringify
fs.writeFileSync(path, JSON.stringify(data, null, 2), 'utf8');
console.log('Successfully updated filters-metadata.json with GlobalSettings');
