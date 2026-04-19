using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Handles direct translation of Teletext/TV content by modifying underlying data sources
    /// This is MUCH more efficient than constantly updating TextMesh components
    /// Based on My Summer Car's ExtraMod.cs approach
    /// 
    /// Supports category-based translations from translate_teletext.txt:
    /// [day]
    /// Monday = Monday (localized)
    /// [kotimaa]
    /// News headline = News headline (localized)
    /// 
    /// NOTE: FSM pattern matching moved to unified PatternMatcher system
    /// </summary>
    public class TeletextHandler
    {
        // Category-based translations: [referenceName][originalText] = translatedText (for key-based lookup)
        private Dictionary<string, Dictionary<string, string>> categoryTranslations = 
            new Dictionary<string, Dictionary<string, string>>();
        
        // Index-based translations: [categoryName][index] = translatedText (for runtime replacement)
        private Dictionary<string, List<string>> indexBasedTranslations = 
            new Dictionary<string, List<string>>();
        
        // Track which arrays have been translated already
        private HashSet<string> translatedArrays = new HashSet<string>();
        
        // GameObject path to category mapping
        private Dictionary<string, string> pathPrefixes = new Dictionary<string, string>
        {
            { "Systems/TV/Teletext/VKTekstiTV/Database", "" },  // Use referenceName directly
            { "Systems/TV/ChatMessages", "ChatMessages" },      // Prefix with "ChatMessages."
            { "Systems/TV/TVGraphics/CHAT/Day", "Chat.Day" }    // Prefix with "Chat.Day."
        };
        
        // Path Prefix Proxy cache
        private Dictionary<string, PlayMakerArrayListProxy[]> proxyCache = 
            new Dictionary<string, PlayMakerArrayListProxy[]>();

        public TeletextHandler()
        {
        }

        /// <summary>
        /// Load teletext translations from INI-style file with category sections
        /// Supports both key-value pairs and index-based translations (in order)
        /// </summary>
        public void LoadTeletextTranslations(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                CoreConsole.Warning($"Teletext translation file not found: {filePath}");
                return;
            }

            try
            {
                categoryTranslations.Clear();
                indexBasedTranslations.Clear();
                
                string currentCategory = null;
                Dictionary<string, string> currentDict = null;
                List<string> currentIndexList = null;
                int loadedCount = 0;

                string[] lines = System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                
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
                    int equalsIndex = FindUnescapedEquals(line);
                    if (equalsIndex > 0 && !readingValue)
                    {
                        // Single-line format: KEY = VALUE
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1).Trim();
                        
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

                // Create alias: ChatMessages.Messages uses ChatMessages.All translations
                Dictionary<string, string> chatAllDict;
                if (categoryTranslations.TryGetValue("ChatMessages.All", out chatAllDict))
                {
                    categoryTranslations["ChatMessages.Messages"] = chatAllDict;
                }

                CoreConsole.Print($"[TeletextHandler] Loaded {loadedCount} teletext translations across {categoryTranslations.Count} categories");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[TeletextHandler] Error loading teletext translations: {ex.Message}");
            }
        }

        /// <summary>
        /// Find the index of the first unescaped '=' character
        /// Returns -1 if no unescaped '=' is found
        /// </summary>
        private int FindUnescapedEquals(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '=')
                {
                    // Check if it's escaped (preceded by backslash)
                    if (i > 0 && line[i - 1] == '\\')
                    {
                        // This equals is escaped, skip it
                        continue;
                    }
                    return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Unescape special characters: \= -> =, \n -> newline
        /// </summary>
        private string UnescapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            // Replace escape sequences
            return input.Replace("\\=", "=").Replace("\\n", "\n");
        }
        
        /// <summary>
        /// Helper method to save a multi-line entry
        /// </summary>
        private void SaveEntry(Dictionary<string, string> dict, List<string> indexList, List<string> keyLines, List<string> valueLines, ref int count)
        {
            if (keyLines.Count == 0) return;

            // Join lines with newlines, preserving original formatting
            string key = string.Join("\n", keyLines.ToArray());
            string value = valueLines.Count > 0 ? string.Join("\n", valueLines.ToArray()) : "";

            // Trim trailing/leading empty lines but preserve internal structure
            key = key.Trim();
            value = value.Trim();
            
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
        /// Monitor and translate teletext arrays
        /// Returns number of new items translated
        /// </summary>
        public int MonitorAndTranslateArrays()
        {
            try
            {
                int totalTranslated = 0;

                foreach (var pathPrefix in pathPrefixes.Keys)
                {
                    PlayMakerArrayListProxy[] proxies;
                    if (!proxyCache.ContainsKey(pathPrefix))
                    {
                        // First time accessing this path - cache proxies
                        GameObject dataObject = MLCUtils.FindGameObjectCached(pathPrefix);
                        if (dataObject == null) continue;

                        proxies = dataObject.GetComponents<PlayMakerArrayListProxy>();
                        proxyCache[pathPrefix] = proxies;
                    }
                    else
                    {
                        // Use cached proxies
                        proxies = proxyCache[pathPrefix];
                    }

                    for (int i = 0; i < proxies.Length; i++)
                    {
                        string refName = proxies[i].referenceName;
                        if (string.IsNullOrEmpty(refName)) continue;

                        string prefix = pathPrefixes[pathPrefix];
                        string categoryName = string.IsNullOrEmpty(prefix) ? refName : $"{prefix}.{refName}";

                        // Create unique key for this array
                        string arrayKey = $"{pathPrefix}[{i}]:{refName}";

                        // Try translating only if not already done
                        if (!translatedArrays.Contains(arrayKey))
                        {
                            int translated = TranslateArrayListProxy(proxies[i], categoryName);

                            // Mark as processed
                            // ... if it doesn't require constant monitoring...
                            bool isDynamic = categoryName == "ChatMessages.Messages";
                            // ... or if the array is already populated ...
                            bool isPopulated = proxies[i] != null && proxies[i]._arrayList != null && proxies[i]._arrayList.Count > 0;
                            // ... or if there are no translations available (to avoid repeated checks)
                            bool isTranslationAvailable = categoryTranslations.ContainsKey(categoryName) &&
                                                          categoryTranslations[categoryName].Count > 0;

                            if (translated > 0 || isPopulated || !isTranslationAvailable)
                            {
                                if (translated > 0)
                                {
                                    CoreConsole.Print($"[TeletextHandler] Translated '{categoryName}' with {translated} items");
                                    totalTranslated += translated;
                                }
                                if (!isDynamic) translatedArrays.Add(arrayKey); // Mark as translated
                            }
                        }
                    }
                }

                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[Teletext] Error monitoring teletext arrays: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Translate a single PlayMakerArrayListProxy component using key-value lookup
        /// Loops through original array and looks up each element's translation
        /// Falls back to index-based translation if exact key match fails
        /// Preserves empty/null elements naturally
        /// </summary>
        private int TranslateArrayListProxy(PlayMakerArrayListProxy proxy, string categoryName)
        {
            if (proxy == null || proxy._arrayList == null) 
                return 0;

            // Get translation dictionary for this category
            if (!categoryTranslations.ContainsKey(categoryName))
                return 0;

            Dictionary<string, string> translations = categoryTranslations[categoryName];
            if (translations.Count == 0)
                return 0;

            int translatedCount = 0;
            int fallbackCount = 0;
            try
            {
                // Translate array in-place by looking up each element
                ArrayList arrayList = proxy._arrayList;
                
                // Check if we have index-based translations as fallback
                bool hasIndexFallback = indexBasedTranslations.ContainsKey(categoryName) && 
                                        indexBasedTranslations[categoryName].Count > 0;
                int nonEmptySourceIndex = -1;
                
                for (int i = 0; i < arrayList.Count; i++)
                {
                    // Skip null or empty elements
                    if (arrayList[i] == null)
                        continue;
                    
                    string original = arrayList[i].ToString();
                    string normalizedOriginal = original.Trim();
                    if (string.IsNullOrEmpty(normalizedOriginal))
                        continue;

                    // Keep fallback alignment based on non-empty source entries only.
                    nonEmptySourceIndex++;
                    
                    // Try exact key match first
                    if (translations.TryGetValue(normalizedOriginal, out string translation))
                    {
                        arrayList[i] = translation;
                        translatedCount++;
                    }
                    // Fallback: Use index-based translation if available
                    else if (hasIndexFallback && nonEmptySourceIndex < indexBasedTranslations[categoryName].Count)
                    {
                        string indexTranslation = indexBasedTranslations[categoryName][nonEmptySourceIndex];
                        if (!string.IsNullOrEmpty(indexTranslation))
                        {
                            arrayList[i] = indexTranslation;
                            translatedCount++;
                            fallbackCount++;
                        }
                    }
                }
                
                if (fallbackCount > 0)
                {
                    CoreConsole.Print($"[TeletextHandler] '{categoryName}': Used index fallback for {fallbackCount} items");
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[TeletextHandler] Error translating category '{categoryName}': {ex.Message}");
            }

            return translatedCount;
        }

        /// <summary>
        /// Reset translation state (useful for testing or scene changes)
        /// </summary>
        public void Reset()
        {
            translatedArrays.Clear();
            proxyCache.Clear();
        }
    }
}



