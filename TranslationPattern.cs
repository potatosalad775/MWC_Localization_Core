using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Represents a translation pattern with extraction and formatting logic
    /// Supports placeholders, regex, comma-separated values, and custom handlers
    /// </summary>
    public class TranslationPattern
    {
        public string Name { get; private set; }
        public TranslationMode Mode { get; private set; }
        public string OriginalPattern { get; private set; }
        public string TranslatedTemplate { get; private set; }
        
        // For path-based matching
        public Func<string, bool> PathMatcher { get; set; }
        public Func<string, bool> TextMatcher { get; set; }
        
        // For FsmPattern mode
        private string[] originalParts;
        private string[] translationParts;
        
        // For RegexExtract mode
        private Regex regexPattern;
        
        // For CustomHandler mode
        public Func<string, string, Dictionary<string, string>, CustomHandlerResult> CustomHandler { get; set; }
        
        // Result struct for custom handlers (NET35 compatible)
        public struct CustomHandlerResult
        {
            public bool Success;
            public string Result;
            
            public CustomHandlerResult(bool success, string result)
            {
                Success = success;
                Result = result;
            }
        }

        public TranslationPattern(string name, TranslationMode mode, string originalPattern, string translatedTemplate)
        {
            Name = name;
            Mode = mode;
            OriginalPattern = originalPattern;
            TranslatedTemplate = translatedTemplate;
            
            Initialize();
        }

        private void Initialize()
        {
            switch (Mode)
            {
                case TranslationMode.FsmPattern:
                case TranslationMode.FsmPatternWithTranslation:
                    InitializeFsmPattern();
                    break;
                    
                case TranslationMode.RegexExtract:
                    InitializeRegexPattern();
                    break;
            }
        }

        private void InitializeFsmPattern()
        {
            // Split patterns by placeholders to get static parts
            // Example: "pakkasta {0} astetta" -> ["pakkasta ", " astetta"]
            originalParts = SplitPattern(OriginalPattern);
            translationParts = SplitPattern(TranslatedTemplate);
        }

        private string[] SplitPattern(string pattern)
        {
            List<string> parts = new List<string>();
            int lastIndex = 0;
            
            // Find all placeholders {0}, {1}, {2}, ... in the pattern
            for (int i = 0; i < 10; i++)
            {
                string placeholder = "{" + i + "}";
                int index = pattern.IndexOf(placeholder, lastIndex);
                
                if (index >= 0)
                {
                    // Add text before this placeholder
                    parts.Add(pattern.Substring(lastIndex, index - lastIndex));
                    lastIndex = index + placeholder.Length;
                }
                else
                {
                    // No more placeholders found - break early
                    break;
                }
            }
            
            // Add remaining text after last placeholder
            if (lastIndex < pattern.Length)
            {
                parts.Add(pattern.Substring(lastIndex));
            }
            else if (lastIndex == pattern.Length && parts.Count > 0)
            {
                // Pattern ends with a placeholder - add empty string
                parts.Add("");
            }
            
            return parts.ToArray();
        }

        private void InitializeRegexPattern()
        {
            try
            {
                regexPattern = new Regex(OriginalPattern, RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to compile regex pattern '{OriginalPattern}': {ex.Message}");
            }
        }

        /// <summary>
        /// Check if this pattern matches the given text and path
        /// </summary>
        public bool Matches(string text, string path)
        {
            // Check path matcher if defined
            if (PathMatcher != null && !PathMatcher(path))
                return false;
            
            // Check text matcher if defined
            if (TextMatcher != null && !TextMatcher(text))
                return false;
            
            // Mode-specific matching
            switch (Mode)
            {
                case TranslationMode.FsmPattern:
                case TranslationMode.FsmPatternWithTranslation:
                    return TryExtractFsmValues(text) != null;
                    
                case TranslationMode.RegexExtract:
                    return regexPattern != null && regexPattern.IsMatch(text);
                    
                case TranslationMode.CommaSeparated:
                    return text.Contains(",");
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// Extract values from text and apply translation template
        /// Returns null if extraction failed
        /// </summary>
        public string TryTranslate(string text, string path, Dictionary<string, string> translations)
        {
            if (!Matches(text, path))
                return null;
            
            switch (Mode)
            {
                case TranslationMode.FsmPattern:
                    return TranslateWithFsmPattern(text);
                    
                case TranslationMode.FsmPatternWithTranslation:
                    return TranslateWithFsmPatternAndTranslateParams(text, translations);
                    
                case TranslationMode.RegexExtract:
                    return TranslateWithRegex(text, translations);
                    
                case TranslationMode.CommaSeparated:
                    return TranslateCommaSeparated(text, translations);
                    
                case TranslationMode.CustomHandler:
                    if (CustomHandler != null)
                    {
                        var result = CustomHandler(text, path, translations);
                        return result.Success ? result.Result : null;
                    }
                    break;
            }
            
            return null;
        }

        private string TranslateWithFsmPattern(string text)
        {
            string[] values = TryExtractFsmValues(text);
            if (values == null)
                return null;
            
            string result = TranslatedTemplate;
            for (int i = 0; i < values.Length; i++)
            {
                result = result.Replace("{" + i + "}", values[i]);
            }
            
            return result;
        }

        private string TranslateWithFsmPatternAndTranslateParams(string text, Dictionary<string, string> translations)
        {
            string[] values = TryExtractFsmValues(text);
            if (values == null)
                return null;
            
            string result = TranslatedTemplate;
            for (int i = 0; i < values.Length; i++)
            {
                string originalValue = values[i];
                
                // Try to translate the parameter value
                string translatedValue = originalValue;
                string key = MLCUtils.FormatUpperKey(originalValue);
                
                if (translations.TryGetValue(key, out string translation))
                {
                    translatedValue = translation;
                }
                
                result = result.Replace("{" + i + "}", translatedValue);
            }
            
            return result;
        }

        private string[] TryExtractFsmValues(string input)
        {
            if (originalParts == null || originalParts.Length == 0)
                return null;
            
            List<string> values = new List<string>();
            string remaining = input;
            
            for (int i = 0; i < originalParts.Length; i++)
            {
                string part = originalParts[i];
                
                // Skip empty parts in the middle (consecutive placeholders like {0}{1})
                if (string.IsNullOrEmpty(part) && i < originalParts.Length - 1)
                {
                    // Empty part between placeholders - no static text to match
                    continue;
                }
                
                if (i == originalParts.Length - 1)
                {
                    // Last part - must end with this
                    if (!remaining.EndsWith(part))
                        return null;
                    
                    // Extract everything before this last part (only if part is non-empty)
                    if (part.Length > 0)
                    {
                        if (remaining.Length > part.Length)
                        {
                            values.Add(remaining.Substring(0, remaining.Length - part.Length));
                        }
                    }
                    else
                    {
                        // Pattern ends with placeholder - remaining text IS the last value
                        if (!string.IsNullOrEmpty(remaining))
                        {
                            values.Add(remaining);
                        }
                    }
                }
                else
                {
                    // Middle part - find this part in remaining string
                    int idx = remaining.IndexOf(part);
                    if (idx < 0)
                        return null;
                    
                    // Extract value before this part (if any)
                    if (idx > 0)
                    {
                        values.Add(remaining.Substring(0, idx));
                    }
                    
                    // Move past this part
                    remaining = remaining.Substring(idx + part.Length);
                }
            }
            
            return values.ToArray();
        }

        private string TranslateWithRegex(string text, Dictionary<string, string> translations)
        {
            Match match = regexPattern.Match(text);
            if (!match.Success)
                return null;
            
            string result = TranslatedTemplate;
            
            // Replace {0}, {1}, {2} with captured groups
            for (int i = 1; i < match.Groups.Count; i++)
            {
                result = result.Replace("{" + (i - 1) + "}", match.Groups[i].Value);
            }
            
            // Replace {KEYNAME} with translations (e.g., {PRICETOTAL})
            foreach (Match keyMatch in Regex.Matches(result, @"\{([A-Z]+)\}"))
            {
                string key = keyMatch.Groups[1].Value;
                if (translations.TryGetValue(key, out string translation))
                {
                    result = result.Replace("{" + key + "}", translation);
                }
            }
            
            return result;
        }

        private string TranslateCommaSeparated(string text, Dictionary<string, string> translations)
        {
            string[] words = text.Split(',');
            
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].Trim();
                string key = MLCUtils.FormatUpperKey(word);
                
                if (translations.TryGetValue(key, out string translation))
                {
                    words[i] = translation;
                }
                else
                {
                    words[i] = word;
                }
            }
            
            return string.Join(", ", words);
        }
    }
}
