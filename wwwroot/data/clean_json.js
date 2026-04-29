const fs = require('fs');
const path = 'filters-metadata.json';
try {
    const data = fs.readFileSync(path, 'utf8');
    fs.writeFileSync(path, data.trim(), 'utf8');
    console.log('Successfully trimmed filters-metadata.json');
} catch (err) {
    console.error('Error:', err);
}
