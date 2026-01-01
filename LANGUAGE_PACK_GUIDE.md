# Creating Language Packs for MWC Localization Core

This guide explains how to create your own language pack for My Winter Car without modifying the plugin code.

## Quick Start

1. **Copy the template files** from `l10n_assets/`
2. **Edit config.txt** with your language settings
3. **Create translation files** (translate.txt, translate_magazine.txt)
4. **(Optional)** Create custom fonts in fonts.unity3d
5. **Test in-game!**

## File Structure

```
BepInEx/plugins/l10n_assets/
├── config.txt                  # Language configuration
├── translate.txt               # Main translations
├── translate_msc.txt           # Optional: My Summer Car compatibility
├── translate_magazine.txt      # Magazine-specific translations
└── fonts.unity3d              # Optional: Custom font asset bundle
```

## Configuration File (config.txt)

### Basic Configuration

```ini
# Language metadata
LANGUAGE_NAME = Your Language Name
LANGUAGE_CODE = xx-XX

# Unicode detection (see below)
UNICODE_RANGES = 

# Font mappings (see below)
[FONTS]
```

### Unicode Ranges (UNICODE_RANGES)

This setting controls when the plugin detects text as "already translated".

**For Latin Languages (English, Spanish, French, German, etc.):**
```ini
# Leave empty or comment out - no character detection needed
# UNICODE_RANGES = 
```

**For Non-Latin Languages:**
```ini
# Provide Unicode ranges in hexadecimal (START-END,START-END)
# Korean example:
UNICODE_RANGES = AC00-D7AF,1100-11FF,3130-318F

# Japanese example:
UNICODE_RANGES = 3040-309F,30A0-30FF,4E00-9FFF

# Chinese example:
UNICODE_RANGES = 4E00-9FFF,3400-4DBF

# Russian/Cyrillic example:
UNICODE_RANGES = 0400-04FF
```

**Why this matters:**
- **With ranges**: Plugin skips re-translating text that already contains your language's characters
- **Without ranges**: Plugin always attempts translation (correct for Latin languages)

### Font Mappings

Map original game fonts to your custom fonts (must exist in fonts.unity3d).

```ini
[FONTS]
OriginalGameFont = YourCustomFont
FugazOne-Regular = MyFont-Bold
Heebo-Black = MyFont-Regular
```

**For Latin languages:** You can often leave this section empty and use default fonts!

## Translation Files

### translate.txt

Main translation file. Format: `KEY = Translation`

```
# Comments start with #
# Keys are auto-normalized: uppercase, no spaces

# Simple translations
BEER = Cerveza
BUCKET = Cubo

# Keys with spaces get normalized automatically
# "BEER 149 MK" becomes "BEER149MK"
BEER149MK = Cerveza 149 MK

# Multiline translations (use \n)
WELCOME = Bienvenido a\nMy Winter Car
```

**Key normalization rules:**
- Converted to UPPERCASE
- Spaces removed
- Newlines removed
- Example: "Price Total" → "PRICETOTAL"

### translate_magazine.txt

Magazine-specific translations (Yellow Pages).

```
# Magazine words (comma-separated lists)
BUCKET = cubo
HYDRAULIC = hidráulico
OIL = aceite

# Phone label for price lines
PHONE = Teléfono
```

## Creating Custom Fonts (Optional)

For non-Latin languages, you may need custom fonts to display characters correctly.

### 1. Prepare Fonts

- Use TrueType (.ttf) or OpenType (.otf) fonts
- Ensure they include all characters for your language
- Test that they render correctly in Unity

### 2. Create Unity Font Assets

You need Unity 5.x (same version as My Winter Car):

1. Import fonts into Unity project
2. Create Font materials
3. Build AssetBundle named "fonts.unity3d"
4. Extract font assets with correct names matching config.txt

### 3. Font Asset Names

The asset names in fonts.unity3d must match the values in config.txt [FONTS] section.

Example:
```ini
[FONTS]
FugazOne-Regular = MySpanishFont
```
→ Asset in bundle must be named "MySpanishFont"

## Testing Your Language Pack

1. **Install BepInEx** in My Winter Car
2. **Copy plugin DLL** to `BepInEx/plugins/`
3. **Copy l10n_assets folder** with your files
4. **Launch game**
5. **Check BepInEx console** (F12) for errors
6. **Press F9 in-game** to reload translations

### Debug Tips

**Enable BepInEx console:**
- Edit `BepInEx/config/BepInEx.cfg`
- Set `Enabled = true` under `[Logging.Console]`

**Common issues:**
- "Config not found" → Check file path: `BepInEx/plugins/l10n_assets/config.txt`
- "Font not loaded" → Check asset names match config.txt
- "Translation not working" → Check key normalization (uppercase, no spaces)

## Example Language Packs

### Spanish (Latin Language Example)

**config.txt:**
```ini
LANGUAGE_NAME = Spanish
LANGUAGE_CODE = es-ES

# No Unicode ranges needed for Latin languages
# UNICODE_RANGES = 

# No custom fonts needed - Spanish uses Latin alphabet
[FONTS]
```

**translate.txt:**
```
BEER = Cerveza
BUCKET = Cubo
WRENCH = Llave inglesa
PRICE = Precio
PHONE = Teléfono
```

### Japanese (Non-Latin Language Example)

**config.txt:**
```ini
LANGUAGE_NAME = Japanese
LANGUAGE_CODE = ja-JP

# Japanese Unicode ranges
UNICODE_RANGES = 3040-309F,30A0-30FF,4E00-9FFF

# Custom fonts required for Japanese characters
[FONTS]
FugazOne-Regular = NotoSansJP-Bold
Heebo-Black = NotoSansJP-Regular
# ... more mappings
```

**translate.txt:**
```
BEER = ビール
BUCKET = バケツ
WRENCH = レンチ
PRICE = 価格
PHONE = 電話
```

## Distribution

When sharing your language pack:

1. **Include these files:**
   - config.txt
   - translate.txt
   - translate_magazine.txt (if applicable)
   - fonts.unity3d (if using custom fonts)

2. **Write installation instructions:**
   - Copy files to `BepInEx/plugins/l10n_assets/`
   - Overwrite existing files

3. **Credit original authors** if adapting My Summer Car translations

## Advanced: Multiple Language Support

You can create multiple config files for different languages:

```
l10n_assets/
├── config_korean.txt
├── config_spanish.txt
└── config_japanese.txt
```

Users rename their preferred `config_XX.txt` → `config.txt` to activate.

## Need Help?

- Check BepInEx console (F12) for error messages
- Verify file encoding is UTF-8
- Test with simple translations first
- Join the modding community for support

## License

Your language pack is your work! Share it under your preferred license.

---

**Happy translating! 🌍**
