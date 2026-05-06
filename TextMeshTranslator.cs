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
        private Dictionary<int, Material> runtimeMaterialCache = new Dictionary<int, Material>();
        private Dictionary<string, Font> fontNameToFont;  // Reverse lookup: font.name -> Font

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

            // Build reverse font lookup for O(1) access by font.name
            RebuildFontNameLookup();
        }

        private void RebuildFontNameLookup()
        {
            fontNameToFont = new Dictionary<string, Font>();
            if (customFonts == null) return;
            foreach (var kvp in customFonts)
            {
                if (kvp.Value != null && !fontNameToFont.ContainsKey(kvp.Value.name))
                    fontNameToFont[kvp.Value.name] = kvp.Value;
            }
        }

        /// <summary>
        /// Translate TextMesh and apply custom font + position adjustments
        /// </summary>
        /// <returns>True if text was translated or already localized</returns>
        public bool TranslateAndApplyFont(TextMesh textMesh, string path)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            // Skip excluded paths
            foreach(string excluded in ExcludedPath)
            {
                if (path.StartsWith(excluded))
                    return false;
            }

            if (magazineHandler.IsMagazineText(path))
            {
                ApplyCustomFont(textMesh, path);
                magazineHandler.HandleMagazineText(textMesh);
                return true;
            }

            // Use standard translation before pattern matching; dictionary lookup is cheaper.
            if (ApplyTranslation(textMesh, path))
                return true;

            if (ApplyPatternTranslation(textMesh, path))
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
                    Texture targetMainTexture = customFont.material.mainTexture;
                    int rendererInstanceID = renderer.GetInstanceID();
                    bool needsTextureApply = !appliedRuntimeTextureCache.TryGetValue(rendererInstanceID, out Texture cachedTexture)
                        || cachedTexture != targetMainTexture;

                    if (needsTextureApply && targetMainTexture != null)
                    {
                        Material runtimeMaterial;
                        if (!runtimeMaterialCache.TryGetValue(rendererInstanceID, out runtimeMaterial) || runtimeMaterial == null)
                        {
                            // Preserve per-renderer material properties while avoiding repeated renderer.material instantiation.
                            runtimeMaterial = renderer.material;
                            if (runtimeMaterial != null)
                            {
                                runtimeMaterialCache[rendererInstanceID] = runtimeMaterial;
                            }
                        }

                        if (runtimeMaterial != null)
                        {
                            if (runtimeMaterial.mainTexture != targetMainTexture)
                            {
                                runtimeMaterial.mainTexture = targetMainTexture;
                            }

                            appliedRuntimeTextureCache[rendererInstanceID] = targetMainTexture;
                        }
                    }
                }

                config.ApplyTextAdjustment(textMesh, path);
            }
        }

        /// <summary>
        /// Handle pattern-based text translations.
        /// </summary>
        bool ApplyPatternTranslation(TextMesh textMesh, string path)
        {
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

            // If the text already equals its mapped value, still apply font/adjustments
            // and report it as handled so one-shot monitors can stop cleanly.
            if (!forceUpdate && currentText == translation)
            {
                ApplyCustomFont(textMesh, path);
                return true;
            }

            // Apply custom font first
            ApplyCustomFont(textMesh, path);
            // Apply translation
            textMesh.text = translation;

            return true;
        }

        /// <summary>
        /// Get custom font for the given original font name
        /// </summary>
        Font GetCustomFont(string originalFontName)
        {
            // First try direct match by mapping key
            if (customFonts.TryGetValue(originalFontName, out Font font))
                return font;

            // Reverse lookup: font already has a custom font name (e.g. after reload)
            if (fontNameToFont != null && fontNameToFont.TryGetValue(originalFontName, out Font reverseFont))
                return reverseFont;

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
            runtimeMaterialCache.Clear();
        }
    }
}
