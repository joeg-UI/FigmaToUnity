# FigmaSync

A Unity Editor tool for importing Figma designs with proper auto-layout translation, semantic type detection, and atomic design prefab support.

## Features

- **Pixel-perfect layout translation** - FIXED/HUG/FILL sizing modes work correctly
- **SPACE_BETWEEN support** - Proper spacing distribution
- **Smart type detection** - Automatically detects Buttons, Labels, Toggles, etc.
- **Optional AI-assisted detection** - Use OpenAI or Anthropic for ambiguous elements
- **Atomic design hierarchy** - Atoms → Molecules → Organisms → Screens
- **Asset export** - All images exported as properly-named PNGs
- **Page selection** - Choose which pages to import

## Installation

### Via Package Manager (Git URL)

1. Open Unity Package Manager (Window → Package Manager)
2. Click the **+** button → **Add package from git URL**
3. Enter: `https://github.com/joeg-UI/FigmaToUnity.git`
4. Click **Add**

### Manual Installation

1. Download or clone this repository
2. Copy the folder to your project's `Packages` folder

## Usage

1. Open **Tools → FigmaSync** (or press `Ctrl+Shift+F`)
2. Enter your Figma file URL
3. Enter your [Personal Access Token](https://www.figma.com/developers/api#access-tokens)
4. Click **Fetch Pages** to load available pages
5. Select pages and configure atomic design levels
6. Click **Sync Document**

## Requirements

- Unity 2021.3 or later
- TextMeshPro package

## Configuration

### Figma Connection
- **Figma File URL**: The URL of your Figma file
- **Personal Access Token**: Your Figma API token (stored securely in EditorPrefs)

### Import Options
- **Only Selected Pages**: Import only checked pages
- **Image Scale**: Export scale for images (1x-4x)
- **Image Format**: PNG, JPG, or SVG

### Type Detection
- **Enable AI Detection**: Use LLM for ambiguous element classification
- **AI Provider**: OpenAI or Anthropic

### Output Folders
- **Prefabs**: Where generated prefabs are saved
- **Assets**: Where downloaded images are saved
- **Fonts**: Where fonts are saved

## License

MIT License - see LICENSE file
