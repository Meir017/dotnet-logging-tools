const sharp = require('sharp');
const path = require('path');

const svgPath = path.join(__dirname, '..', 'icon.svg');
const pngPath = path.join(__dirname, '..', 'icon.png');

console.log('Converting SVG icon to PNG...');

sharp(svgPath)
  .resize(128, 128)
  .png()
  .toFile(pngPath)
  .then(() => {
    console.log('✓ Icon converted successfully to icon.png (128x128)');
  })
  .catch(error => {
    console.error('Error converting icon:', error);
    process.exit(1);
  });
