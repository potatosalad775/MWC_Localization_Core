using System.Collections.Generic;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Handles TextMesh translation, mapped font application, and position adjustments.
    /// </summary>
    public class TextMeshTranslator
    {
        private TranslationDictionary translations;
        private Dictionary<string, Font> customFonts;
        private LocalizationConfig config;
        private Dictionary<int, string> appliedFontCache = new Dictionary<int, string>();
        private Dictionary<int, Texture> appliedRuntimeTextureCache = new Dictionary<int, Texture>();
        private Dictionary<int, Material> runtimeMaterialCache = new Dictionary<int, Material>();
        private Dictionary<string, Font> fontNameToFont;  // Reverse lookup: font.name -> Font

        private static readonly string[] ExcludedPath = new string[]
        {
            "Sheets/ServiceBrochure/PagePaintRims/Buttons/CustomColors",
            "Sheets/ServiceBrochure/PagePaintCar/Buttons/CustomColors",
            "GUI/HUD/FPS"
        };

        public TextMeshTranslator(
            TranslationDictionary translations,
            Dictionary<string, Font> customFonts,
            LocalizationConfig config
        )
        {
            this.translations = translations;
            this.customFonts = customFonts;
            this.config = config;

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
        /// Translate a TextMesh, or otherwise handle it, and apply mapped font adjustments.
        /// </summary>
        /// <returns>True if the text was translated, already localized, or handled by a specialized path.</returns>
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

            // Use standard translation before pattern matching; dictionary lookup is cheaper.
            if (ApplyTranslation(textMesh, path))
                return true;

            if (ApplyPatternTranslation(textMesh, path))
                return true;

            if (LocalizationConfig.IsForcedFontPath(path))
            {
                ApplyCustomFont(textMesh, path);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Apply a mapped custom font and its matching position adjustment to a TextMesh.
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
            }

            config.ApplyTextAdjustment(textMesh, path);
        }

        /// <summary>
        /// Handle pattern-based text translations.
        /// </summary>
        bool ApplyPatternTranslation(TextMesh textMesh, string path)
        {
            string patternResult = translations.TryMatchPattern(textMesh.text, path);
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
        /// <param name="forceUpdate">Apply the translation even when the current text already equals the localized value.</param>
        public bool ApplyTranslation(TextMesh textMesh, string path, bool forceUpdate = false)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            string currentText = textMesh.text;

            // Direct lookup (with LRU + already-normalized fast path inside the dict)
            if (!translations.TryGetExact(currentText, out string translation))
            {
                if (currentText.IndexOf('\n') >= 0)
                {
                    string lineTranslation = TranslateTextByLines(currentText);
                    if (lineTranslation != currentText)
                    {
                        ApplyCustomFont(textMesh, path);
                        textMesh.text = lineTranslation;
                        return true;
                    }
                }

                return false;
            }

            // Already-localized text still needs its font + adjustments applied so
            // one-shot monitors can stop cleanly.
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

        private string TranslateTextByLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string[] lines = text.Split('\n');
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string translatedLine = TranslateLinePreservingPrefix(lines[i]);
                if (translatedLine != lines[i])
                {
                    lines[i] = translatedLine;
                    changed = true;
                }
            }

            if (!changed)
                return text;

            return string.Join("\n", lines);
        }

        private string TranslateLinePreservingPrefix(string line)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            string lineNoCr = line.Replace("\r", string.Empty);
            string translated;
            if (translations.TryGetExact(lineNoCr, out translated))
                return translated;

            int textStart = FindTextStartAfterNumericPrefix(lineNoCr);
            if (textStart <= 0 || textStart >= lineNoCr.Length)
                return line;

            string suffix = lineNoCr.Substring(textStart);
            if (!translations.TryGetExact(suffix, out translated))
                return line;

            return lineNoCr.Substring(0, textStart) + translated;
        }

        private int FindTextStartAfterNumericPrefix(string line)
        {
            int i = 0;
            while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == ':' || line[i] == ',' || char.IsWhiteSpace(line[i])))
                i++;

            return i;
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
        /// Load FSM patterns from translation file (delegates to TranslationDictionary).
        /// </summary>
        public void LoadFsmPatterns(string filePath)
        {
            translations.LoadPatternsFromFile(filePath);
        }

        /// <summary>
        /// Reset the loaded pattern registry for a clean reload.
        /// </summary>
        public void ResetPatterns()
        {
            translations.ResetPatterns();
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
