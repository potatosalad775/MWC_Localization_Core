using MSCLoader;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Handles TextMesh translation, font application, and position adjustments
    /// OPTIMIZED: Cache, no LINQ, pre-computed keys, minimal allocations
    /// </summary>
    public class TextMeshTranslator
    {
        private Dictionary<string, string> translations;
        private Dictionary<string, Font> customFonts;
        private MagazineTextHandler magazineHandler;
        private PatternMatcher patternMatcher;
        private LocalizationConfig config;

        // OPTIMIZATION: Cache for recently translated text (InstanceID -> last text)
        private Dictionary<int, string> textCache = new Dictionary<int, string>();
        
        private List<string> ExcludedPath = new List<string>
        {
            "HOMENEW/Functions/FunctionsDisable/Stereos/Player/Screen/Settings/Bass/LCD",
            "CARPARTS/VINPlate",
            "Sheets/ServiceBrochure/PagePaintRims/Buttons/CustomColors",
            "Sheets/ServiceBrochure/PagePaintCar/Buttons/CustomColors",
            "GUI/HUD/FPS"
        };

        public TextMeshTranslator(
            Dictionary<string, string> translations,
            Dictionary<string, Font> customFonts,
            MagazineTextHandler magazineHandler,
            LocalizationConfig config
        )
        {
            this.translations = translations;
            this.customFonts = customFonts;
            this.magazineHandler = magazineHandler;
            this.config = config;
            
            this.patternMatcher = new PatternMatcher(translations);
        }

        /// <summary>
        /// Translate TextMesh and apply custom font + position adjustments
        /// OPTIMIZED: Cache check, early exit on no-op
        /// </summary>
        public bool TranslateAndApplyFont(TextMesh textMesh, string path, HashSet<TextMesh> translatedTextMeshes)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            int instanceId = textMesh.GetInstanceID();
            string currentText = textMesh.text;

            // OPTIMIZATION: Skip if text hasn't changed since last translation
            if (textCache.TryGetValue(instanceId, out string cachedText))
            {
                if (cachedText == currentText)
                    return true; // Already translated this text
            }

            // Skip if already translated (language-agnostic check)
            if (translatedTextMeshes != null && translatedTextMeshes.Contains(textMesh))
                return true;

            // OPTIMIZATION: Early exit for excluded paths using StartsWith
            for (int i = 0; i < ExcludedPath.Count; i++)
            {
                if (path.StartsWith(ExcludedPath[i]))
                    return false;
            }

            // Try complex text handling first
            if (HandleComplexTextMesh(textMesh, path))
            {
                textCache[instanceId] = textMesh.text;
                return true;
            }

            // If a custom font exists for this TextMesh, apply it even if there's no translation yet.
            // This ensures glyphs (acentos) are available immediately for UI elements
            // where the translation may be set via FSM or other means later.
            string originalFontNameForApply = textMesh.font != null ? textMesh.font.name : "unknown";
            if (GetCustomFont(originalFontNameForApply) != null)
            {
                ApplyCustomFont(textMesh, path);
                // cache current text after applying font to avoid redundant work
                textCache[instanceId] = textMesh.text;
            }

            // Use standard translation
            if (ApplyTranslation(textMesh, path))
            {
                textCache[instanceId] = textMesh.text;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Apply custom font and position adjustment to TextMesh
        /// OPTIMIZED: Avoid MeshRenderer lookup if no custom font
        /// </summary>
        public void ApplyCustomFont(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return;

            // Get custom font based on original font name
            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";
            
            // OPTIMIZATION: Direct dictionary lookup, no LINQ
            if (!customFonts.TryGetValue(originalFontName, out Font customFont) || customFont == null)
                return;

            // Assign font
            if (textMesh.font != customFont)
                textMesh.font = customFont;

            // Only update renderer if material has texture
            if (customFont.material != null && customFont.material.mainTexture != null)
            {
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.mainTexture = customFont.material.mainTexture;
                }
            }

            config.ApplyTextAdjustment(textMesh, path);
        }

        /// <summary>
        /// Handle complex text patterns (magazine text, cashier price line)
        /// </summary>
        bool HandleComplexTextMesh(TextMesh textMesh, string path)
        {
            // Check magazine text FIRST
            if (magazineHandler.IsMagazineText(path))
            {
                ApplyCustomFont(textMesh, path);
                return magazineHandler.HandleMagazineText(textMesh);
            }
            
            // Try pattern matching for other complex texts
            string patternResult = patternMatcher.TryTranslateWithPattern(textMesh.text, path);
            if (patternResult != null)
            {
                ApplyCustomFont(textMesh, path);
                textMesh.text = patternResult;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Apply standard translation to TextMesh
        /// OPTIMIZED: Pre-normalize key, skip if already correct
        /// </summary>
        public bool ApplyTranslation(TextMesh textMesh, string path, bool forceUpdate = false)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            string currentText = textMesh.text;
            
            // OPTIMIZATION: Normalize key outside validation loop
            string normalizedKey = MLCUtils.FormatUpperKey(currentText);

            // Check if translation exists
            if (!translations.TryGetValue(normalizedKey, out string translation))
                return false;

            // OPTIMIZATION: Skip if already correct
            if (!forceUpdate && currentText == translation)
                return false;

            // Apply custom font first
            ApplyCustomFont(textMesh, path);
            textMesh.text = translation;

            return true;
        }

        /// <summary>
        /// Get custom font for the given original font name
        /// OPTIMIZATION: Direct dict lookup instead of LINQ
        /// </summary>
        Font GetCustomFont(string originalFontName)
        {
            // Direct match first - fast path
            if (customFonts.TryGetValue(originalFontName, out Font font))
                return font;

            return null;
        }

        /// <summary>
        /// Apply custom font without translating text (for teletext display)
        /// </summary>
        public bool ApplyFontOnly(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return false;

            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";
            Font customFont = GetCustomFont(originalFontName);

            if (customFont == null)
                return false;

            if (textMesh.font != customFont)
                textMesh.font = customFont;

            // Update material texture if present
            if (customFont.material != null && customFont.material.mainTexture != null)
            {
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.mainTexture = customFont.material.mainTexture;
                }
            }

            config.ApplyTextAdjustment(textMesh, path);
            return true;
        }
        
        /// <summary>
        /// Load FSM patterns from teletext translation file
        /// </summary>
        public void LoadFsmPatterns(string filePath)
        {
            patternMatcher.LoadPatternsFromFile(filePath);
        }

        /// <summary>
        /// Clear text cache (useful when language changes)
        /// </summary>
        public void ClearCache()
        {
            textCache.Clear();
        }
    }
}

