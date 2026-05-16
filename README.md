# MSC Localization Core

A MSCLoader plugin system for My Summer Car that enables automatic localization without code modifications.

See at [NexusMods](https://www.nexusmods.com/mysummercar/mods/197)

> [!NOTE]
> If you are looking for a My Winter Car version of this plugin, please refer to [here](https://github.com/potatosalad775/MWC_Localization_Core)

## Quick Start

### For Language Pack Creators

1. **Copy template files** from `dist/`
2. **Edit `l10n_assets/config.txt`** with your language settings
3. **Update translation files**:
   - `translate.txt` - Main UI text
   - `translate_teletext.txt` - TV/Teletext content
   - `translate_mod.txt (optional)` - Mod content
4. **(Optional)** Create custom fonts in `fonts.unity3d`
5. **Test in-game with F8 reload!**

### For Developers

```
dotnet build -c Release
```

## Features

**Automatic Translation** - Scans TextMesh components and replaces text  
**Teletext/TV Translation** - TV news, weather, recipes get translated  
**Magazine Translations** - Special handling for Yellow Pages magazine  
**Configurable Fonts** - Map game fonts to localized custom fonts  
**Position Adjustments** - Fine-tune text placement per language  
**Live Reload** - Press F8 to test changes without restarting  
**Non-Latin Support** - Korean, Japanese, Chinese, Cyrillic, etc.  
**My Summer Car Compatibility** - Use previous MSC translation as basis

## File Structure

```
BepInEx/plugins/dist/
├── l10n_assets/
│   ├── config.txt                  # Language configuration
│   ├── translate.txt               # Main UI translations
│   ├── translate_teletext.txt      # TV/Teletext content translations
│   ├── translate_mod.txt           # Optional: Mod content translations
│   └── fonts.unity3d               # Optional: Custom font asset bundle
└── MSC_Localization_Core.dll       # Core plugin module
```

## Configuration (config.txt)

### Basic Settings

```ini
# Your language information
LANGUAGE_NAME = Korean
LANGUAGE_CODE = ko-KR
```

| Setting | Purpose | Example |
|---------|---------|---------|
| `LANGUAGE_NAME` | Display name | `Korean`, `Español`, `日本語` |
| `LANGUAGE_CODE` | ISO language code | `ko-KR`, `es-ES`, `ja-JP` |

### Font Mappings

If you are using custom fonts, you must map original game fonts to them via `config.txt`:

```ini
[FONTS]
OriginalGameFont = YourCustomFont
FugazOne-Regular = MyFont-Bold
Heebo-Black = MyFont-Regular
```

Font assets must exist in `fonts.unity3d` with matching names (right side values).

## Translation Files

### translate.txt - Main UI Translations

Main translation file that covers My Summer Car lines.

```
# Comments use #
# Keys are auto-normalized: UPPERCASE, no spaces

BEER = 맥주
BUCKET = 양동이
MONDAY = 월요일
WITHDRAWAL = 출금

# Multiline support (Use \n)
Welcome to My Summer Car = 마이 윈터 카에\n오신 것을 환영합니다
```

### translate_teletext.txt - TV/Teletext Content

Category-based translations for TV teletext (news, weather, recipes, etc.) & TV chat pages.
This separate file was introduced to workaround the game constantly trying to overwrite translations from plugin.

**ORDER & [CATEGORY] MATTERS!**
At least in this file. It's recommended NOT to modify these.

```
# Category sections match teletext pages
[day]
MONDAY = 월요일
TUESDAY = 화요일

[kotimaa]
# Domestic news headlines (in order they appear)
MAKELIN CEO FIRED = 마켈린 CEO 해고
TAXI REFORM PLANNED = 택시 개혁안

[urheilu]
# Sports news
FOOTBALL RESULTS = 축구 결과

# Multi-line format:
Long news
Headline here
=
긴 뉴스
헤드라인
```

**Categories:**
- `day` - Day names
- `kotimaa` - Domestic news
- `ulkomaat` - Foreign news  
- `talous` - Economy news
- `urheilu` - Sports news
- `ruoka` - Recipes
- `ajatus` - Quotes
- `kulttuuri` - Culture

Please note that few TV lines might not 'look translated' due to the reason mentioned above.

## Text Adjustments (Optional)

Fine-tune text placement, size, spacing, and width for better appearance without code changes.

### Configuration

```ini
[POSITION_ADJUSTMENTS]
Conditions = X,Y,Z[,FontSize,LineSpacing,WidthScale]
```

### Condition Syntax

| Condition | Matches When |
|-----------|--------------|
| `Contains(path)` | Path contains text |
| `EndsWith(path)` | Path ends with text |
| `StartsWith(path)` | Path starts with text |
| `Equals(path)` | Path exactly matches |
| `!Contains(path)` | Path does NOT contain (negation) |

**Tip:** Use the BepInEx console (F12) to see GameObject paths when text appears. This helps you write conditions.

### Examples

```ini
# Position adjustment only: Shift HUD labels down (Y = -0.05)
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0

# Make text wider: Scale width to 1.2x (last parameter)
Contains(Systems/Narrow/Text) = 0,0,0,,,1.2

# Full adjustment: position + size + line spacing + width
Contains(GUI/Menu/Title) = 0,0.1,0,0.12,1.0,1.3

# Skip parameters with commas: position + width scale (skip font size and line spacing)
Contains(PERAPORTTI/ATMs/) & EndsWith(/Text) = 0,0.25,0,,,0.9

# Combine multiple conditions with negation
Contains(PERAPORTTI/ATMs/) & !Contains(/Row) & EndsWith(/Text) = 0,0.25,0
```

### Parameter Format

```
X,Y,Z[,FontSize,LineSpacing,WidthScale]
```

| Parameter | Type | Purpose | Example Values |
|-----------|------|---------|----------------|
| **X** | Required | Horizontal offset (+ right, - left) | `0`, `0.5`, `-0.3` |
| **Y** | Required | Vertical offset (+ up, - down) | `0`, `0.25`, `-0.05` |
| **Z** | Required | Depth offset (rarely needed) | `0` |
| **FontSize** | Optional | Character size (TextMesh.characterSize) | `0.1`, `0.15`, `0.2` |
| **LineSpacing** | Optional | Line spacing multiplier | `1.0`, `1.2`, `0.8` |
| **WidthScale** | Optional | Character width scale (transform.localScale.x) | `1.0`, `1.2` (wider), `0.8` (narrower) |

**Tips:**
- Leave optional parameters empty to skip: `0,0,0,,1.2` (skip FontSize, set LineSpacing)
- Use `WidthScale > 1.0` to make text wider (good for narrow fonts)
- Use `WidthScale < 1.0` to make text narrower (good for condensed layouts)
- Combine with FontSize to control both height and width independently

### Drift-tracked paths

A few sheets (e.g. `Sheets/Magazine/Products`) can have their transform rewritten by the game while the screen is open, which would normally let the position offset drift. These paths are listed in a small in-code whitelist (`TextAdjustment.DriftTrackedPathPatterns`). Once a matching TextMesh is adjusted by the normal translation pass, only that touched TextMesh is re-pinned during LateUpdate. Adding a path to the whitelist is a code change, not a config change — open an issue if you find another sheet that flickers.

## Creating Custom Fonts (Optional)

For languages requiring special font support (better readability, special characters, etc.):

1. **Prepare fonts** - TrueType (.ttf) or OpenType (.otf)
2. **Create Unity assets** - Use Unity 5.0.0f4 (same version as My Summer Car)
3. **Build AssetBundle** - Name it `fonts.unity3d`
4. **Match names** - Font asset names must match `config.txt` [FONTS] section values
5. **Place in l10n_assets** - Put `fonts.unity3d` alongside other translation files

**Unity Setup Notes:**
- Unity 5.0.0f4 has broken licensing - install 5.6.7f1 first to activate, then run 5.0.0f4
- AssetBundle build target must match game (typically Windows Standalone)

## Testing & Development

### Live Reload (F8 Key)

Press **F8** in-game to reload all configuration and translation files instantly:
- Edit `config.txt`, translation files, etc.
- No game restart needed
- Perfect for iterative translation work

### Debug Workflow

1. Enable BepInEx console: Edit `BepInEx/config/BepInEx.cfg`
   - Set `Enabled = true` under `[Logging.Console]`
2. Launch game and press **F12** to open console
3. Check for configuration errors and translation status
4. Watch for GameObject paths when text appears (helps with position adjustments)
5. Edit files and press **F8** to test changes
6. Repeat until perfect

### Common Issues

**Text not translating?**
- Enable console messages via MSCLoader Mod settings
- Check console (F12) for errors
- Make sure key matches
- For teletext, check if you're using the right category section

**Wrong font?**
- Verify font names in `config.txt` [FONTS] section
- Check if `fonts.unity3d` exists and loads successfully
- Console will show "Loaded [font] for [original]" messages

**Text position off?**
- Use `Developer Toolkit` or something else to find GameObject path
- Add position adjustment in `config.txt`
- Test with F8 reload

### Building the Plugin

```bash
dotnet build -c Release
```
