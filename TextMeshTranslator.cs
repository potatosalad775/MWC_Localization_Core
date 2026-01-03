using BepInEx.Logging;
using System.Collections.Generic;
using System.Linq;
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
        private LocalizationConfig config;
        private ManualLogSource logger;

        public TextMeshTranslator(
            Dictionary<string, string> translations,
            Dictionary<string, Font> customFonts,
            MagazineTextHandler magazineHandler,
            LocalizationConfig config,
            ManualLogSource logger)
        {
            this.translations = translations;
            this.customFonts = customFonts;
            this.magazineHandler = magazineHandler;
            this.config = config;
            this.logger = logger;
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

            // Try complex text handling first (e.g., magazine text, cashier price)
            if (HandleComplexTextMesh(textMesh, path))
            {
                // Complex text was handled, apply font and position
                ApplyCustomFont(textMesh, path);
                return true;
            }

            // Use standard translation
            if (ApplyTranslation(textMesh, path))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Apply custom font and position adjustment to TextMesh
        /// </summary>
        public void ApplyCustomFont(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return;

            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";
            Font customFont = GetCustomFont(originalFontName);

            if (customFont != null)
            {
                textMesh.font = customFont;
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && customFont.material != null && customFont.material.mainTexture != null)
                {
                    renderer.material.mainTexture = customFont.material.mainTexture;
                }
                AdjustTextPosition(textMesh, path);
            }
        }

        /// <summary>
        /// Handle complex text patterns (magazine text, cashier price line)
        /// </summary>
        bool HandleComplexTextMesh(TextMesh textMesh, string path)
        {
            // Check if this is magazine text and handle it
            if (magazineHandler.IsMagazineText(path))
            {
                return magazineHandler.HandleMagazineText(textMesh);
            }

            // Check if this is cashier price line
            if (path.Contains("GUI/Indicators/Interaction") && textMesh.text.Contains("PRICE TOTAL"))
            {
                // Example format: "PRICE TOTAL: 0.00 MK"
                string[] parts = textMesh.text.Split(' ');
                if (parts.Length == 4)
                {
                    string pricePart = parts[2]; // e.g., "0.00"
                    // Get price label from translations (default to "PRICE TOTAL" if not found)
                    string priceLabel = translations.TryGetValue("PRICETOTAL", out string translation)
                        ? translation // Found with normalized key
                        : "PRICE TOTAL";

                    textMesh.text = $"{priceLabel}: {pricePart} MK";
                    return true; // Handled
                }
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

            string currentText = textMesh.text;
            string normalizedKey = StringHelper.FormatUpperKey(currentText);

            // Check if translation exists
            if (!translations.TryGetValue(normalizedKey, out string translation))
                return false;

            // Skip if already translated (unless forced)
            if (!forceUpdate && currentText == translation)
                return false;

            // Get original font name before changing
            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";

            // Apply translation
            textMesh.text = translation;

            // Apply custom font
            Font customFont = GetCustomFont(originalFontName);
            if (customFont != null)
            {
                textMesh.font = customFont;

                // Update material texture
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null && customFont.material != null && customFont.material.mainTexture != null)
                {
                    renderer.material.mainTexture = customFont.material.mainTexture;
                }

                // Adjust position for localized text
                AdjustTextPosition(textMesh, path);
            }

            return true;
        }

        /// <summary>
        /// Get custom font for the given original font name
        /// </summary>
        Font GetCustomFont(string originalFontName)
        {
            // First try direct match
            if (customFonts.ContainsKey(originalFontName))
            {
                return customFonts[originalFontName];
            }

            // Use original if it exists in the dictionary as value
            else if (customFonts.Values.Any(f => f.name == originalFontName))
            {
                return customFonts.Values.FirstOrDefault(f => f.name == originalFontName);
            }

            // Return first loaded font as fallback
            else if (customFonts.Count > 0)
            {
                return customFonts.Values.First();
            }

            return null;
        }

        /// <summary>
        /// Adjust text position for localized characters (per-element basis)
        /// Uses configurable position adjustments from config.txt
        /// Falls back to hardcoded adjustments if no config matches
        /// </summary>
        void AdjustTextPosition(TextMesh textMesh, string path)
        {
            // Try configured position adjustments first
            Vector3 configOffset = config.GetPositionOffset(path);

            if (configOffset != Vector3.zero)
            {
                // Found a matching configuration
                Vector3 pos = textMesh.transform.localPosition;
                textMesh.transform.localPosition = pos + configOffset;
                return;
            }
        }
    }
}
