// 'Classified Magazine' Text Handler

using MSCLoader;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Handles Yellow Pages magazine text translation
    /// Manages comma-separated word lists and price/phone line formatting
    /// </summary>
    public class MagazineTextHandler
    {
        private Dictionary<string, string> magazineTranslations = new Dictionary<string, string>();

        /// <summary>
        /// Load magazine-specific translations from separate file
        /// </summary>
        public void LoadMagazineTranslations(string translationPath)
        {
            if (!File.Exists(translationPath))
            {
                CoreConsole.Warning($"[MagazineTextHandler] Magazine translation file not found: {translationPath}");
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(translationPath, Encoding.UTF8);
                magazineTranslations.Clear();

                foreach (string line in lines)
                {
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(line.Trim()) || line.TrimStart().StartsWith("#"))
                        continue;

                    // Parse KEY=VALUE format
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1).Trim();

                        // Normalize key (remove spaces, convert to uppercase)
                        key = MLCUtils.FormatUpperKey(key);

                        // Handle escaped newlines in value
                        value = value.Replace("\\n", "\n");

                        if (!magazineTranslations.ContainsKey(key))
                        {
                            magazineTranslations[key] = value;
                        }
                    }
                }

                CoreConsole.Print($"[MagazineTextHandler] Loaded {magazineTranslations.Count} magazine translations");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[MagazineTextHandler] Failed to load magazine translations: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if path is a magazine text element
        /// </summary>
        public bool IsMagazineText(string path)
        {
            return path.Contains("Sheets/YellowPagesMagazine/Page") && path.EndsWith("/Lines/YellowLine");
        }

        /// <summary>
        /// Handle magazine text translation
        /// </summary>
        public bool HandleMagazineText(TextMesh textMesh)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            // Skip placeholder text immediately - return false to prevent monitoring registration
            // Placeholder text isn't ready yet, will be filled with real content later
            if (textMesh.text == "88888888888888888888888888888")
                return false;

            // Handling price and phone number line (format: "h.149,- puh.123456")
            if (textMesh.text.StartsWith("h.") && textMesh.text.Contains(",- puh."))
            {
                TranslatePricePhoneLine(textMesh);
                return true; // Handled
            }

            // Try updating rest of the magazine lines in standard way
            string key = MLCUtils.FormatUpperKey(textMesh.text);
            if (magazineTranslations.TryGetValue(key, out string translation))
            {
                textMesh.text = translation;
                return true; // Handled
            }

            // No translation found - mark as handled to prevent infinite retries
            //CoreConsole.Warning($"[MagazineTextHandler] No translation found for: '{textMesh.text}' (key: '{key}')");
            return true; // Handled (even though not translated)
        }

        /// <summary>
        /// Translate price and phone number line (e.g., "h.149,- puh.123456" -> "149 MK, PHONE - 123456")
        /// Uses PHONE key from translate_magazine.txt for the phone label
        /// </summary>
        private void TranslatePricePhoneLine(TextMesh textMesh)
        {
            try
            {
                // Remove "h." prefix and split by ",- puh."
                string withoutPrefix = textMesh.text.Substring(2);
                string[] parts = withoutPrefix.Split(new string[] { ",- puh." }, System.StringSplitOptions.None);

                if (parts.Length == 2)
                {
                    string pricePart = parts[0].Trim();
                    string phonePart = parts[1].Trim();

                    // Get phone label from translations (default to "PHONE" if not found)
                    string phoneLabel = magazineTranslations.TryGetValue("PHONE", out string translation)
                        ? translation
                        : "PHONE";

                    textMesh.text = $"{pricePart} MK, {phoneLabel} - {phonePart}";
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Warning($"[MagazineTextHandler] Failed to parse magazine price/phone line: {textMesh.text} - {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all magazine translations
        /// </summary>
        public void ClearTranslations()
        {
            magazineTranslations.Clear();
        }

        /// <summary>
        /// Get translation for a magazine text (case-insensitive lookup)
        /// Returns null if no translation found
        /// </summary>
        public string GetTranslation(string original)
        {
            if (string.IsNullOrEmpty(original))
                return null;

            string normalizedKey = MLCUtils.FormatUpperKey(original);
            if (magazineTranslations.TryGetValue(normalizedKey, out string translation))
            {
                return translation;
            }

            return null;
        }
    }
}
