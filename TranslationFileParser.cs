using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Unified translation file parser - eliminates code duplication
    /// Supports both simple KEY=VALUE files and category-based INI-style files
    /// </summary>
    public static class TranslationFileParser
    {
        /// <summary>
        /// Parse KEY=VALUE translation file (simple format)
        /// Supports escaped equals (\=) and preserves intentional spacing
        /// </summary>
        public static Dictionary<string, string> ParseKeyValueFile(string filePath, bool normalizeKeys = true)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return result;

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    // Find the first UNESCAPED '=' character
                    int separatorIndex = FindKeyValueSeparatorIndex(line);
                    if (separatorIndex <= 0)
                        continue;

                    // Extract key and value, unescape \= -> =
                    string key = line.Substring(0, separatorIndex).Trim().Replace("\\=", "=");
                    // Preserve intentional leading AND trailing spaces in translation values.
                    // Spaces are needed for proper formatting in concatenated strings
                    string value = line.Substring(separatorIndex + 1).Replace("\\=", "=");

                    // Common authoring style is: "key = value".
                    // In that specific case, drop only the single separator space.
                    if (line.Length > separatorIndex + 1 && line[separatorIndex + 1] == ' ')
                    {
                        bool hasSecondSpace = (line.Length > separatorIndex + 2 && line[separatorIndex + 2] == ' ');
                        if (!hasSecondSpace && value.Length > 0 && value[0] == ' ')
                            value = value.Substring(1);
                    }

                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (normalizeKeys)
                        key = MLCUtils.FormatUpperKey(key);

                    string processedValue = value.Replace("\\n", "\n");

                    if (!result.ContainsKey(key))
                        result[key] = processedValue;
                }
            }
            catch (Exception ex)
            {
                CoreConsole.Error($"[TranslationFileParser] Error parsing KEY=VALUE file: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Find the index of the first unescaped '=' character
        /// Public utility for other components that need to parse KEY=VALUE format
        /// Returns -1 if no unescaped '=' is found or if line is null/empty
        /// </summary>
        public static int FindKeyValueSeparator(string line)
        {
            if (string.IsNullOrEmpty(line))
                return -1;
                
            return FindKeyValueSeparatorIndex(line);
        }

        /// <summary>
        /// Unescape special characters: \= -> =, \n -> newline
        /// Public utility for other components
        /// </summary>
        public static string UnescapeValue(string input)
        {
            return UnescapeString(input);
        }

        /// <summary>
        /// Find the index of the first unescaped '=' character
        /// Returns -1 if no unescaped '=' is found
        /// </summary>
        private static int FindKeyValueSeparatorIndex(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '=')
                    continue;

                int backslashCount = 0;
                for (int j = i - 1; j >= 0 && line[j] == '\\'; j--)
                {
                    backslashCount++;
                }

                bool isEscaped = (backslashCount % 2) == 1;
                if (!isEscaped)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Parse INI-style category-based file with [categoryName] sections
        /// Used for teletext translations with category-based + index-based lookups
        /// Returns both dictionary-based and list-based (index-based) translations
        /// </summary>
        public static void ParseCategoryBasedFile(
            string filePath,
            out Dictionary<string, Dictionary<string, string>> categoryTranslations,
            out Dictionary<string, List<string>> indexBasedTranslations)
        {
            categoryTranslations = new Dictionary<string, Dictionary<string, string>>();
            indexBasedTranslations = new Dictionary<string, List<string>>();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                string currentCategory = null;
                Dictionary<string, string> currentDict = null;
                List<string> currentIndexList = null;
                int loadedCount = 0;

                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                List<string> keyLines = new List<string>();
                List<string> valueLines = new List<string>();
                bool readingValue = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.Trim();

                    // Skip comments
                    if (trimmed.StartsWith("#"))
                        continue;

                    // Check for category header [categoryName]
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        // Save previous entry if exists
                        if (keyLines.Count > 0 && currentDict != null)
                        {
                            SaveEntry(currentDict, currentIndexList, keyLines, valueLines, ref loadedCount);
                        }

                        keyLines.Clear();
                        valueLines.Clear();
                        readingValue = false;

                        currentCategory = trimmed.Substring(1, trimmed.Length - 2);

                        if (!categoryTranslations.ContainsKey(currentCategory))
                            categoryTranslations[currentCategory] = new Dictionary<string, string>();

                        if (!indexBasedTranslations.ContainsKey(currentCategory))
                            indexBasedTranslations[currentCategory] = new List<string>();

                        currentDict = categoryTranslations[currentCategory];
                        currentIndexList = indexBasedTranslations[currentCategory];
                        continue;
                    }

                    // Skip empty lines outside of key/value context
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        // Empty line between entries - save current entry
                        if (keyLines.Count > 0 && currentDict != null)
                        {
                            SaveEntry(currentDict, currentIndexList, keyLines, valueLines, ref loadedCount);
                            keyLines.Clear();
                            valueLines.Clear();
                            readingValue = false;
                        }
                        continue;
                    }

                    // Check if this line is just "=" (separator)
                    if (trimmed == "=")
                    {
                        readingValue = true;
                        continue;
                    }

                    // Check if line contains unescaped "=" (single-line format)
                    int equalsIndex = FindKeyValueSeparatorIndex(line);
                    if (equalsIndex > 0 && !readingValue)
                    {
                        // Single-line format: KEY = VALUE
                        string key = line.Substring(0, equalsIndex).Trim();

                        // Only remove a single optional space immediately after '='
                        int valueStart = equalsIndex + 1;
                        if (valueStart < line.Length && line[valueStart] == ' ')
                        {
                            valueStart++; // Skip single space after '='
                        }
                        string value = valueStart < line.Length ? line.Substring(valueStart) : "";

                        // Unescape special characters
                        key = UnescapeString(key);
                        value = UnescapeString(value);

                        if (!string.IsNullOrEmpty(key) && currentDict != null)
                        {
                            currentDict[key] = value;
                            currentIndexList.Add(value); // Add to index list in order
                            loadedCount++;
                        }
                        continue;
                    }

                    // Accumulate lines for multi-line key or value
                    if (currentDict != null)
                    {
                        if (readingValue)
                        {
                            valueLines.Add(line);
                        }
                        else
                        {
                            keyLines.Add(line);
                        }
                    }
                }

                // Save last entry if exists
                if (keyLines.Count > 0 && currentDict != null)
                {
                    SaveEntry(currentDict, currentIndexList, keyLines, valueLines, ref loadedCount);
                }

                CoreConsole.Print($"[TranslationFileParser] Loaded {loadedCount} category-based translations from {categoryTranslations.Count} categories");
            }
            catch (Exception ex)
            {
                CoreConsole.Error($"[TranslationFileParser] Error parsing category-based file: {ex.Message}");
            }
        }



        /// <summary>
        /// Unescape special characters: \= -> =, \n -> newline
        /// </summary>
        public static string UnescapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Replace escape sequences
            return input.Replace("\\=", "=").Replace("\\n", "\n");
        }

        /// <summary>
        /// Helper method to save a multi-line entry
        /// </summary>
        private static void SaveEntry(Dictionary<string, string> dict, List<string> indexList, List<string> keyLines, List<string> valueLines, ref int count)
        {
            if (keyLines.Count == 0) return;

            // Join lines with newlines, preserving original formatting
            string key = string.Join("\n", keyLines.ToArray());
            string value = valueLines.Count > 0 ? string.Join("\n", valueLines.ToArray()) : "";

            // Only strip wholly empty leading/trailing lines (preserving lines that contain spaces)
            key = TrimEmptyBoundaryLines(key);
            value = TrimEmptyBoundaryLines(value);

            // Unescape special characters in both key and value
            key = UnescapeString(key);
            value = UnescapeString(value);

            if (!string.IsNullOrEmpty(key))
            {
                dict[key] = value;
                indexList.Add(value); // Add to index list in order
                count++;
            }
        }

        /// <summary>
        /// Trim only wholly empty leading and trailing lines, preserving lines that contain spaces.
        /// This preserves padding required for fixed-width layouts.
        /// </summary>
        private static string TrimEmptyBoundaryLines(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] lines = input.Split('\n');
            int start = 0;
            int end = lines.Length - 1;

            // Find first non-empty line
            while (start <= end && lines[start].Length == 0)
            {
                start++;
            }

            // Find last non-empty line
            while (end >= start && lines[end].Length == 0)
            {
                end--;
            }

            // If all lines are empty
            if (start > end)
                return "";

            // Reconstruct string with only the trimmed range
            StringBuilder result = new StringBuilder();
            for (int i = start; i <= end; i++)
            {
                if (i > start)
                    result.Append('\n');
                result.Append(lines[i]);
            }

            string trimmedResult = result.ToString();
            result.Length = 0;  // NET 3.5 compatible: reset StringBuilder for reuse
            return trimmedResult;
        }
    }
}