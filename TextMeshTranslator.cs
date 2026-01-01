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
        private Dictionary<string, Font> koreanFonts;
        private MagazineTextHandler magazineHandler;
        private ManualLogSource logger;

        public TextMeshTranslator(
            Dictionary<string, string> translations,
            Dictionary<string, Font> koreanFonts,
            MagazineTextHandler magazineHandler,
            ManualLogSource logger)
        {
            this.translations = translations;
            this.koreanFonts = koreanFonts;
            this.magazineHandler = magazineHandler;
            this.logger = logger;
        }

        /// <summary>
        /// Translate TextMesh and apply Korean font + position adjustments
        /// </summary>
        /// <returns>True if text was translated or already in Korean</returns>
        public bool TranslateAndApplyFont(TextMesh textMesh, string path)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            // Skip if already in Korean
            if (StringHelper.ContainsKorean(textMesh.text))
                return true;

            // Try complex text handling first (e.g., magazine text, cashier price)
            if (HandleComplexTextMesh(textMesh, path))
            {
                // Complex text was handled, apply font and position
                ApplyKoreanFont(textMesh, path);
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
        /// Apply Korean font and position adjustment to TextMesh
        /// </summary>
        public void ApplyKoreanFont(TextMesh textMesh, string path)
        {
            if (textMesh == null)
                return;

            string originalFontName = textMesh.font != null ? textMesh.font.name : "unknown";
            Font koreanFont = GetKoreanFont(originalFontName);

            if (koreanFont != null)
            {
                textMesh.font = koreanFont;
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && koreanFont.material != null && koreanFont.material.mainTexture != null)
                {
                    renderer.material.mainTexture = koreanFont.material.mainTexture;
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

            // Apply Korean font
            Font koreanFont = GetKoreanFont(originalFontName);
            if (koreanFont != null)
            {
                textMesh.font = koreanFont;

                // Update material texture
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null && koreanFont.material != null && koreanFont.material.mainTexture != null)
                {
                    renderer.material.mainTexture = koreanFont.material.mainTexture;
                }

                // Adjust position for Korean text
                AdjustTextPosition(textMesh, path);
            }

            return true;
        }

        /// <summary>
        /// Get Korean font for the given original font name
        /// </summary>
        Font GetKoreanFont(string originalFontName)
        {
            // First try direct match
            if (koreanFonts.ContainsKey(originalFontName))
            {
                return koreanFonts[originalFontName];
            }

            // Use original if it exists in the dictionary as value
            else if (koreanFonts.Values.Any(f => f.name == originalFontName))
            {
                return koreanFonts.Values.FirstOrDefault(f => f.name == originalFontName);
            }

            // Return first loaded font as fallback
            else if (koreanFonts.Count > 0)
            {
                return koreanFonts.Values.First();
            }

            return null;
        }

        /// <summary>
        /// Adjust text position for Korean characters (per-element basis)
        /// </summary>
        void AdjustTextPosition(TextMesh textMesh, string path)
        {
            // Adjust position for HUD elements
            if (path.Contains("GUI/HUD/") && path.EndsWith("/HUDLabel"))
            {
                Vector3 pos = textMesh.transform.localPosition;
                textMesh.transform.localPosition = new Vector3(pos.x, pos.y - 0.05f, pos.z);
            }
            else if (path.Contains("PERAPORTTI/ATMs/MoneyATM/Screen") && !path.Contains("/Row") && path.EndsWith("/Text"))
            {
                Vector3 pos = textMesh.transform.localPosition;
                textMesh.transform.localPosition = new Vector3(pos.x, pos.y + 0.25f, pos.z);
            }
            else if (path.Contains("PERAPORTTI/ATMs/FuelATM/Screen") && !path.Contains("/Row") && path.EndsWith("/Text"))
            {
                Vector3 pos = textMesh.transform.localPosition;
                textMesh.transform.localPosition = new Vector3(pos.x, pos.y + 0.45f, pos.z);
            }
            else if (path.Contains("PERAPORTTI/ATMs/FuelATM/Screen") && path.Contains("/Row") && path.EndsWith("/Text"))
            {
                Vector3 pos = textMesh.transform.localPosition;
                textMesh.transform.localPosition = new Vector3(pos.x, pos.y - 0.25f, pos.z);
            }
            else if (path.Contains("Systems/TV/Teletext/VKTekstiTV/"))
            {
                Vector3 pos = textMesh.transform.localPosition;
                textMesh.transform.localPosition = new Vector3(pos.x, pos.y + 0.25f, pos.z);
            }
        }
    }
}
