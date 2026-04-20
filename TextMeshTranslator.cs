using MSCLoader;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Handles TextMesh translation, font application, and position adjustments
    /// Centralizes all translation logic for better maintainability
    /// </summary>
    public class TextMeshTranslator
    {
        private Dictionary<string, string> translations;
        private Dictionary<string, Font> customFonts;
        private MagazineTextHandler magazineHandler;
        private PatternMatcher patternMatcher;
        private LocalizationConfig config;
        private Dictionary<int, string> appliedFontCache = new Dictionary<int, string>();
        private Dictionary<int, Texture> appliedRuntimeTextureCache = new Dictionary<int, Texture>();

        private static readonly string[] ExcludedPath = new string[]
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

            // Initialize unified pattern matcher
            this.patternMatcher = new PatternMatcher(translations);
        }

        /// <summary>
        /// Translate TextMesh and apply custom font + position adjustments
        /// </summary>
        /// <param name="translatedTextMeshes">HashSet tracking which TextMesh objects have been translated</param>
        /// <returns>True if text was translated or already localized</returns>
        public bool TranslateAndApplyFont(TextMesh textMesh, string path, HashSet<TextMesh> translatedTextMeshes)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            // Skip if already translated (language-agnostic check)
            if (translatedTextMeshes != null && translatedTextMeshes.Contains(textMesh))
                return true;

            // Skip excluded paths
            foreach(string excluded in ExcludedPath)
            {
                if (path.StartsWith(excluded))
                    return false;
            }

            // Try complex text handling first (e.g., magazine text, cashier price)
            if (HandleComplexTextMesh(textMesh, path))
                return true;

            // Use standard translation
            if (ApplyTranslation(textMesh, path))
                return true;

            return false;
        }

        /// <summary>
        /// Apply custom font and position adjustment to TextMesh
        /// </summary>
        public void ApplyCustomFont(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return;

            // Get custom font based on original font name
            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";
            Font customFont = GetCustomFont(originalFontName);

            if (customFont != null)
            {
                int instanceID = textMesh.GetInstanceID();
                string targetFontKey = customFont.name;

                bool needsFontApply = true;
                if (appliedFontCache.TryGetValue(instanceID, out string cachedFontKey) && cachedFontKey == targetFontKey)
                {
                    needsFontApply = false;
                }

                if (needsFontApply || textMesh.font != customFont)
                {
                    textMesh.font = customFont;
                    appliedFontCache[instanceID] = targetFontKey;
                }

                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && customFont.material != null)
                {
                    if (IsRuntimeSensitiveTvPath(path))
                    {
                        Texture targetMainTexture = customFont.material.mainTexture;
                        int rendererInstanceID = renderer.GetInstanceID();
                        bool needsTextureApply = !appliedRuntimeTextureCache.TryGetValue(rendererInstanceID, out Texture cachedTexture)
                            || cachedTexture != targetMainTexture;

                        if (needsTextureApply && targetMainTexture != null)
                        {
                            // Keep the runtime material/shader setup intact and swap only the atlas texture.
                            Material runtimeMaterial = renderer.material;
                            if (runtimeMaterial != null && runtimeMaterial.mainTexture != targetMainTexture)
                            {
                                runtimeMaterial.mainTexture = targetMainTexture;
                            }

                            appliedRuntimeTextureCache[rendererInstanceID] = targetMainTexture;
                        }
                    }
                    else
                    {
                        // Do not mutate shared material textures in-place.
                        // Rebinding the renderer material avoids corrupting other font atlases.
                        if (renderer.sharedMaterial != customFont.material)
                        {
                            renderer.sharedMaterial = customFont.material;
                        }
                    }
                }

                config.ApplyTextAdjustment(textMesh, path);
            }
        }

        /// <summary>
        /// Some TV paths rely on runtime material/shader properties for tint/visibility effects.
        /// </summary>
        private bool IsRuntimeSensitiveTvPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.Contains("Systems/TV/TVGraphics/") ||
                   path.Contains("Systems/TV/Teletext/VKTekstiTV/");
        }

        /// <summary>
        /// Handle complex text patterns (magazine text, cashier price line)
        /// Now uses unified pattern matcher for all pattern-based translations
        /// </summary>
        bool HandleComplexTextMesh(TextMesh textMesh, string path)
        {
            // Check magazine text FIRST - it requires special handling (comma-separated words, price/phone format)
            if (magazineHandler.IsMagazineText(path))
            {
                // Apply custom font first
                ApplyCustomFont(textMesh, path);
                // Apply Translation
                return magazineHandler.HandleMagazineText(textMesh);
            }
            
            // Try pattern matching for other complex texts (FSM, Price patterns)
            string patternResult = patternMatcher.TryTranslateWithPattern(textMesh.text, path);
            if (patternResult != null)
            {
                // Apply custom font first
                ApplyCustomFont(textMesh, path);
                // Apply Translation
                textMesh.text = patternResult;
                return true;
            }

            return false; // Not handled, use standard translation
        }

        /// <summary>
        /// Apply standard translation to TextMesh
        /// </summary>
        /// <param name="forceUpdate">Force update even if text hasn't changed</param>
        public bool ApplyTranslation(TextMesh textMesh, string path, bool forceUpdate = false)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            // Normalize current text for lookup
            string currentText = textMesh.text;
            string normalizedKey = MLCUtils.FormatUpperKey(currentText);

            // Check if translation exists
            if (!translations.TryGetValue(normalizedKey, out string translation))
                return false;

            // Skip translation if already translated (unless forced)
            if (!forceUpdate && currentText == translation)
                return false;

            // Apply custom font first
            ApplyCustomFont(textMesh, path);
            // Apply translation
            textMesh.text = translation;

            return true;
        }

        /// <summary>
        /// Translate multiline text line-by-line.
        /// Useful for dynamic displays that append lines over time.
        /// </summary>
        public bool TranslateMultilineByLines(TextMesh textMesh, string path)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            string original = textMesh.text;
            string[] lines = original.Split('\n');
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                    continue;

                string trimmedCr = line.Replace("\r", string.Empty);
                string normalizedKey = MLCUtils.FormatUpperKey(trimmedCr);
                if (string.IsNullOrEmpty(normalizedKey))
                    continue;

                if (translations.TryGetValue(normalizedKey, out string translatedLine) && trimmedCr != translatedLine)
                {
                    lines[i] = translatedLine;
                    changed = true;
                }
            }

            if (!changed)
                return false;

            ApplyCustomFont(textMesh, path);
            textMesh.text = string.Join("\n", lines);
            return true;
        }

        /// <summary>
        /// Get custom font for the given original font name (idempotent)
        /// </summary>
        Font GetCustomFont(string originalFontName)
        {
            // Try direct match by mapping key
            if (customFonts.TryGetValue(originalFontName, out Font font))
                return font;

            // Fallback: scan all entries to find one whose mapped font has this name
            // This handles the case where textMesh.font.name is already the localized name
            foreach (var kvp in customFonts)
            {
                if (kvp.Value != null && kvp.Value.name == originalFontName)
                    return kvp.Value;
            }

            return null;
        }

        /// <summary>
        /// Apply custom font without translating text (for teletext display)
        /// </summary>
        public bool ApplyFontOnly(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return false;
            
            string beforeFontName = textMesh.font != null ? textMesh.font.name : string.Empty;
            ApplyCustomFont(textMesh, path);
            string afterFontName = textMesh.font != null ? textMesh.font.name : string.Empty;
            return beforeFontName != afterFontName;
        }
        
        /// <summary>
        /// Load FSM patterns from teletext translation file
        /// </summary>
        public void LoadFsmPatterns(string filePath)
        {
            patternMatcher.LoadPatternsFromFile(filePath);
        }

        /// <summary>
        /// Reset pattern registry to built-ins for clean reload.
        /// </summary>
        public void ResetPatterns()
        {
            patternMatcher.ResetPatterns();
        }

        /// <summary>
        /// Clear per-instance font cache when scene changes or on reload.
        /// </summary>
        public void ClearRuntimeCaches()
        {
            appliedFontCache.Clear();
            appliedRuntimeTextureCache.Clear();
            MLCUtils.ClearFormatKeyCache();
        }
    }
}
