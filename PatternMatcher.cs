// Pattern matching system for Translation Strings

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
        private HashSet<string> patternSignatures = new HashSet<string>();

        public PatternMatcher(Dictionary<string, string> translations)
        {
            this.translations = translations;
            ResetPatterns();
        }

        /// <summary>
        /// Reset pattern registry to a clean built-in state.
        /// Call before loading file patterns to make reload idempotent.
        /// </summary>
        public void ResetPatterns()
        {
            patterns.Clear();
            patternSignatures.Clear();
            InitializeBuiltInPatterns();
        }

        /// <summary>
        /// Initialize built-in patterns (can be overridden by config file)
        /// </summary>
        private void InitializeBuiltInPatterns()
        {
            // Multi-line Computer command text handler
            var pcScreenPattern = new TranslationPattern(
                "PCMultiLine",
                TranslationMode.CustomHandler,
                "",
                ""
            );
            pcScreenPattern.PathMatcher = path => path.Contains("COMPUTER/SYSTEM/POS/Command");
            pcScreenPattern.TextMatcher = text => text.Contains("\n");
            pcScreenPattern.CustomHandler = TranslateMultilineScreen;
            AddPatternInternal(pcScreenPattern, true);
        }

        /// <summary>
        /// Load patterns from file (FSM patterns, custom patterns, etc.)
        /// </summary>
        public void LoadPatternsFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                CoreConsole.Print("[PatternMatcher] No pattern file found, using built-in patterns only");
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
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
                        continue;
                    }

                    // Parse pattern - auto-detects FsmPattern vs FsmPatternWithTranslation
                    if (TryParseFsmPattern(trimmed, out TranslationPattern pattern))
                    {
                        if (AddPatternInternal(pattern, false))
                        {
                            loadedCount++;
                        }
                    }
                }

                string fileName = Path.GetFileName(filePath);
                CoreConsole.Print($"[PatternMatcher] Loaded {loadedCount} patterns from {fileName}");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[PatternMatcher] Failed to load patterns: {ex.Message}");
            }
        }

        private bool TryParseFsmPattern(string line, out TranslationPattern pattern)
        {
            pattern = null;
            
            int equalsIndex = TranslationFileParser.FindKeyValueSeparator(line);
            if (equalsIndex <= 0)
                return false;

            string original = line.Substring(0, equalsIndex).Trim().ToUpperInvariant();
            string translation = line.Substring(equalsIndex + 1).Trim();

            // Unescape special characters
            original = TranslationFileParser.UnescapeString(original);
            translation = TranslationFileParser.UnescapeString(translation);

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
            if (patterns.Count == 0)
                return null;

            // Defer ToUpperInvariant until we know at least one pattern needs it
            string upperText = null;
            foreach (var pattern in patterns)
            {
                // CustomHandler patterns work on original text; FSM patterns need uppercase
                bool needsUpper = pattern.Mode != TranslationMode.CustomHandler;
                string inputText;
                if (needsUpper)
                {
                    if (upperText == null)
                        upperText = text.ToUpperInvariant();
                    inputText = upperText;
                }
                else
                {
                    inputText = text;
                }

                string result = pattern.TryTranslate(inputText, path, translations);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private bool AddPatternInternal(TranslationPattern pattern, bool appendToEnd)
        {
            if (pattern == null)
                return false;

            string signature = BuildSignature(pattern);
            if (!patternSignatures.Add(signature))
                return false;

            if (appendToEnd)
            {
                patterns.Add(pattern);
            }
            else
            {
                // User file patterns take priority over built-ins.
                patterns.Insert(0, pattern);
            }

            return true;
        }

        private string BuildSignature(TranslationPattern pattern)
        {
            string original = pattern.OriginalPattern ?? string.Empty;
            string translated = pattern.TranslatedTemplate ?? string.Empty;
            return ((int)pattern.Mode).ToString() + "|" + original + "|" + translated;
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
                    string key = MLCUtils.FormatUpperKey(trimmed);
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

    }
}
