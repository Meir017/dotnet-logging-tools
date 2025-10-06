# Extension Icon

## Current Status

An SVG icon has been created at `icon.svg` with the following design:
- Dark circular background with VS Code blue border
- White document representing a log file
- Colored horizontal lines representing log entries (using VS Code theme colors)
- Blue magnifying glass overlay symbolizing analysis/search functionality

## Converting to PNG

To convert the SVG to PNG (128x128), you can use one of these methods:

### Option 1: Using ImageMagick
```bash
magick icon.svg -resize 128x128 icon.png
```

### Option 2: Using Inkscape
```bash
inkscape icon.svg --export-type=png --export-filename=icon.png --export-width=128 --export-height=128
```

### Option 3: Using Node.js (sharp)
```bash
npm install sharp
node -e "require('sharp')('icon.svg').resize(128, 128).png().toFile('icon.png')"
```

### Option 4: Online Converter
- Upload `icon.svg` to https://cloudconvert.com/svg-to-png
- Set dimensions to 128x128
- Download as `icon.png`

## Design Notes

The icon design follows VS Code extension icon guidelines:
- 128x128 pixels (will be scaled down by VS Code)
- Simple, recognizable design
- Uses VS Code theme colors for consistency
- Clear focal point (magnifying glass over log file)
- Works well at small sizes

## Color Palette

- Background: #1E1E1E (VS Code dark theme background)
- Border: #007ACC (VS Code blue accent)
- Document: #FFFFFF (white)
- Log lines: Various VS Code syntax colors
  - Green (#6A9955) - Comments/strings
  - Teal (#4EC9B0) - Types
  - Orange (#CE9178) - Strings
  - Gray (#D4D4D4) - Text
  - Blue (#569CD6) - Keywords
