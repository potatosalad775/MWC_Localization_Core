# MWC Localization Core

A BepInEx 5.x plugin system for My Winter Car (Unity 5) that enables automatic UI translation and localization without code modifications.

## Quick Start

### For Language Pack Creators

1. **Copy template files** from `dist/`
2. **Edit `l10n_assets/config.txt`** with your language settings
3. **Update translation files** (`translate.txt`, `translate_magazine.txt`)
4. **(Optional)** Create custom fonts in `fonts.unity3d`
5. **Test in-game with F8 reload!**

### For Developers

```
dotnet build -c Release
```

## Features

**Automatic Translation** - Scans TextMesh components and replaces text  
**Configurable Fonts** - Map game fonts to localized custom fonts  
**Position Adjustments** - Fine-tune text placement per language  
**Live Reload** - Press F8 to test changes without restarting  
**Non-Latin Support** - Korean, Japanese, Chinese, Cyrillic, etc.  
**Magazine Translations** - Special handling for Classified Magazine Pages  
**My Summer Car Compatibility** - Use previous MSC translation as basis

## File Structure

```
BepInEx/plugins/dist/
├── l10n_assets
│   ├── config.txt                  # Language configuration
│   ├── translate.txt               # Main translations
│   ├── translate_magazine.txt      # Magazine-specific translations
│   ├── translate_msc.txt           # Optional: My Summer Car compatibility
│   └── fonts.unity3d               # Optional: Custom font asset bundle
└── MWC_Localization_Core.dll       # Core plugin module
```

## Configuration (config.txt)

### Basic Settings

```ini
LANGUAGE_NAME = Your Language Name
LANGUAGE_CODE = xx-XX
```

| Setting | Purpose | Example |
|---------|---------|---------|
| `LANGUAGE_NAME` | Display name | `Korean`, `Español`, `日本語` |
| `LANGUAGE_CODE` | ISO code | `ko-KR`, `es-ES`, `ja-JP` |

### Font Mappings

```ini
[FONTS]
OriginalGameFont = YourCustomFont
FugazOne-Regular = MyFont-Bold
Heebo-Black = MyFont-Regular
```

Font assets must exist in `fonts.unity3d` with matching names.

## Translation Files

### translate.txt

Main translation file with automatic key normalization.

```
# Comments use #
# Keys are auto-normalized: UPPERCASE, no spaces

BEER = 맥주
BUCKET = 양동이

# Multiline support (Use \n)
Welcome to My Winter Car = 마이 윈터 카에\n오신 것을 환영합니다
```

### translate_msc.txt

You can feed translation file from previous My Summer Car as basis. (as optional)

Contents from `translate.txt` (targeted for MWC) will overwrite `translate_msc.txt`.

### translate_magazine.txt

Special handling for Yellow Pages magazine with comma-separated words and price lines.

```
# Magazine words
headlgh.l = 좌.전조등
headgskt. = 헤.가스켓

# Phone label for price lines
# Used in lines like "h.149,- puh.123456" -> "149 MK, ${PHONE} - (08)123456"
# Example) PHONE = 전화 : "h.149,- puh.123456" -> "149 MK, 전화 - (08)123456"
PHONE = 전화
```

## Position Adjustments (Optional)

Fine-tune text placement for better alignment without code changes.

### Configuration

```ini
[POSITION_ADJUSTMENTS]
Conditions = X,Y,Z
```

### Condition Syntax

| Condition | Matches When |
|-----------|--------------|
| `Contains(path)` | Path contains text |
| `EndsWith(path)` | Path ends with text |
| `StartsWith(path)` | Path starts with text |
| `Equals(path)` | Path exactly matches |
| `!Contains(path)` | Path does NOT contain (negation) |

### Examples

```ini
# Shift HUD labels down (Y = -0.05)
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0

# Adjust ATM screen (exclude rows, shift up)
Contains(PERAPORTTI/ATMs/) & !Contains(/Row) & EndsWith(/Text) = 0,0.25,0

# Teletext TV - shift up
Contains(Systems/TV/Teletext/VKTekstiTV/) = 0,0.25,0
```

### Offset Format

```
X,Y,Z
```
- **X**: Horizontal (+ right, - left)
- **Y**: Vertical (+ up, - down)
- **Z**: Depth (rarely needed)

## Creating Custom Fonts (Optional)

For languages requiring special font support (better readability, special characters, etc.):

1. **Prepare fonts** - TrueType (.ttf) or OpenType (.otf)
2. **Create Unity assets** - Use Unity 5.0.0f4 (same as My Summer Car / My Winter Car)
3. **Build AssetBundle** - Name it "fonts.unity3d"
4. **Match names** - Asset names must match config.txt [FONTS] section

The license logic in Unity 5.0.0f4 is currently broken.  
First, you need to install the 5.6.7f1 version of Unity, activate it, and then you'll be able to run 5.0.0f4.

## Testing & Development

### Live Reload (F8 Key)

Press **F8** in-game to reload all configuration and translation files instantly:
- Edit `config.txt`, `translate.txt`, etc.
- No restart needed
- Perfect for iterative testing

### Debug Workflow

1. Enable BepInEx console: Edit `BepInEx/config/BepInEx.cfg`
   - Set `Enabled = true` under `[Logging.Console]`
2. Launch game and press F12 to open console
3. Check for configuration errors and translation status
4. Edit files and press F8 to test changes
5. Repeat until perfect

### Building the Plugin

```bash
dotnet build -c Release
```

Output: `bin/Release/net35/MWC_Localization_Core.dll`