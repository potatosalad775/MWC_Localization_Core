// Pattern matching system for Translation Strings

using MSCLoader;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Unified pattern matching system for all translation types
    /// Replaces separate FSM, Magazine, and Price pattern logic
    /// </summary>
    public class PatternMatcher
    {
        private List<TranslationPattern> patterns = new List<TranslationPattern>();
        private Dictionary<string, string> translations;

        public PatternMatcher(Dictionary<string, string> translations)
        {
            this.translations = translations;
            
            InitializeBuiltInPatterns();
        }

        /// <summary>
        /// Initialize built-in patterns (can be overridden by config file)
        /// </summary>
        private void InitializeBuiltInPatterns()
        {
            // Magazine price/phone pattern (custom handler)
            var magazinePricePattern = new TranslationPattern(
                "MagazinePrice",
                TranslationMode.CustomHandler,
                "h.{price},- puh.{phone}",
                "{price} MK, {PHONE} - {phone}"
            );
            magazinePricePattern.PathMatcher = path => path.Contains("YellowPagesMagazine");
            magazinePricePattern.TextMatcher = text => text.StartsWith("h.") && text.Contains(",- puh.");
            magazinePricePattern.CustomHandler = TranslateMagazinePriceLine;
            patterns.Add(magazinePricePattern);

            // Magazine comma-separated words
            var magazineWordsPattern = new TranslationPattern(
                "MagazineWords",
                TranslationMode.CommaSeparated,
                "",
                ""
            );
            magazineWordsPattern.PathMatcher = path => path.Contains("YellowPagesMagazine");
            magazineWordsPattern.TextMatcher = text => text.Split(',').Length == 3;
            patterns.Add(magazineWordsPattern);

            // Multi-line Computer command text handler
            var pcScreenPattern = new TranslationPattern(
                "PCMultiLine",
                TranslationMode.CustomHandler,
                "",
                ""
            );
            pcScreenPattern.PathMatcher = path => path.Contains("COMPUTER/SYSTEM/POS/Text");
            pcScreenPattern.TextMatcher = text => text.Contains("\n");
            pcScreenPattern.CustomHandler = TranslateMultilineScreen;
            patterns.Add(pcScreenPattern);
        }

        /// <summary>
        /// Load patterns from file (FSM patterns, custom patterns, etc.)
        /// </summary>
        public void LoadPatternsFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                CoreConsole.Print("No pattern file found, using built-in patterns only");
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                string currentSection = null;
                int loadedCount = 0;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();

                    // Skip comments and empty lines
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    // Check for section header
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2);
                        continue;
                    }

                // Parse pattern - auto-detects FsmPattern vs FsmPatternWithTranslation
                    // Pass original line (not trimmed) to preserve leading spaces in translation values
                    if (TryParseFsmPattern(line, out TranslationPattern pattern))
                    {
                        patterns.Insert(0, pattern);  // Insert at beginning to override built-in patterns
                        loadedCount++;
                    }
                }

                CoreConsole.Print($"Loaded {loadedCount} patterns from file");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"Failed to load patterns: {ex.Message}");
            }
        }

        private bool TryParseFsmPattern(string line, out TranslationPattern pattern)
        {
            pattern = null;
            
            int equalsIndex = FindUnescapedEquals(line);
            if (equalsIndex <= 0)
                return false;

            string original = line.Substring(0, equalsIndex).Trim().ToUpperInvariant();
            // Preserve leading spaces in translation values (they may be significant for layout)
            string translation = line.Substring(equalsIndex + 1).TrimEnd();

            // Unescape special characters
            original = UnescapeString(original);
            translation = UnescapeString(translation);

            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translation))
                return false;

            // Check if this is a pattern (contains {0}, {1}, etc.)
            // Auto-detect: if translation has placeholders, use FsmPatternWithTranslation (translate the params)
            // If translation has NO placeholders, use FsmPattern (just substitute - rare case)
            if (original.Contains("{0}") || original.Contains("{1}") || original.Contains("{2}"))
            {
                bool translationHasPlaceholders = translation.Contains("{0}") || translation.Contains("{1}") || translation.Contains("{2}");
                
                pattern = new TranslationPattern(
                    "FSM_" + original.Substring(0, System.Math.Min(20, original.Length)),
                    translationHasPlaceholders ? TranslationMode.FsmPatternWithTranslation : TranslationMode.FsmPattern,
                    original,
                    translation
                );
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to translate text using pattern matching
        /// Returns null if no pattern matched
        /// </summary>
        public string TryTranslateWithPattern(string text, string path)
        {
            foreach (var pattern in patterns)
            {
                string result = pattern.TryTranslate(text.ToUpperInvariant(), path, translations);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Add a pattern programmatically
        /// </summary>
        public void AddPattern(TranslationPattern pattern)
        {
            patterns.Add(pattern);
        }

        /// <summary>
        /// Clear all patterns
        /// </summary>
        public void ClearPatterns()
        {
            patterns.Clear();
        }

        /// <summary>
        /// Custom handler for magazine price/phone lines
        /// Format: "h.149,- puh.123456" -> "149 MK, PHONE - 123456"
        /// </summary>
        private TranslationPattern.CustomHandlerResult TranslateMagazinePriceLine(string text, string path, Dictionary<string, string> translations)
        {
            try
            {
                // Remove "h." prefix and split by ",- puh."
                if (!text.StartsWith("h."))
                    return new TranslationPattern.CustomHandlerResult(false, null);

                string withoutPrefix = text.Substring(2);
                string[] parts = withoutPrefix.Split(new string[] { ",- puh." }, System.StringSplitOptions.None);

                if (parts.Length == 2)
                {
                    string pricePart = parts[0].Trim();
                    string phonePart = parts[1].Trim();

                    // Get phone label from translations
                    string phoneLabel = translations.TryGetValue("PHONE", out string translation)
                        ? translation
                        : "PHONE";

                    return new TranslationPattern.CustomHandlerResult(true, $"{pricePart} MK, {phoneLabel} - {phonePart}");
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Warning($"Failed to parse magazine price/phone line: {text} - {ex.Message}");
            }

            return new TranslationPattern.CustomHandlerResult(false, null);
        }

        /// <summary>
        /// Custom handler for multi-line computer screen text
        /// Splits lines and translates each line individually
        private TranslationPattern.CustomHandlerResult TranslateMultilineScreen(string text, string path, Dictionary<string, string> translations)
        {
            try
            {
                string[] lines = text.Split(new string[] { "\n" }, System.StringSplitOptions.None);
                List<string> translatedLines = new List<string>();
                bool anyTranslated = false;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        translatedLines.Add("");  // Preserve empty lines
                        continue;
                    }

                    // Try to translate this line
                    string key = trimmed.ToUpperInvariant();
                    if (translations.TryGetValue(key, out string translation))
                    {
                        translatedLines.Add(translation);
                        anyTranslated = true;
                    }
                    else
                    {
                        translatedLines.Add(trimmed);  // Keep original if not found
                    }
                }

                if (anyTranslated)
                {
                    return new TranslationPattern.CustomHandlerResult(true, string.Join("\n", translatedLines.ToArray()));
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Warning($"Failed to translate multi-line screen: {ex.Message}");
            }

            return new TranslationPattern.CustomHandlerResult(false, null);
        }

        // Utility methods from TeletextHandler
        private int FindUnescapedEquals(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '=')
                {
                    // Check if escaped
                    if (i > 0 && line[i - 1] == '\\')
                        continue;
                    return i;
                }
            }
            return -1;
        }

        private string UnescapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            return str.Replace("\\=", "=").Replace("\\n", "\n");
        }
    }
}
