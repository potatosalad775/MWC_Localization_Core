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
            // Use shared parser from TranslationFileParser
            TranslationFileParser.ParseCategoryBasedFile(
                filePath,
                out Dictionary<string, Dictionary<string, string>> loadedCategoryTranslations,
                out Dictionary<string, List<string>> loadedIndexBasedTranslations);

            categoryTranslations = loadedCategoryTranslations;
            indexBasedTranslations = loadedIndexBasedTranslations;
            
            // Create alias: ChatMessages.Messages uses ChatMessages.All translations (both category and index-based)
            if (categoryTranslations.TryGetValue("ChatMessages.All", out Dictionary<string, string> chatAllDict))
            {
                categoryTranslations["ChatMessages.Messages"] = chatAllDict;
            }
            
            // Also create alias for index-based translations (preserve exact array order)
            if (indexBasedTranslations.TryGetValue("ChatMessages.All", out List<string> chatAllIndexList))
            {
                // Copy the list (not reference) to preserve independence
                List<string> copiedList = new List<string>(chatAllIndexList);
                indexBasedTranslations["ChatMessages.Messages"] = copiedList;
            }

            // Count total loaded translations
            int loadedCount = 0;
            foreach (var category in categoryTranslations.Values)
            {
                loadedCount += category.Count;
            }

            CoreConsole.Print($"[TeletextHandler] Loaded {loadedCount} teletext translations across {categoryTranslations.Count} categories");
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
                        if (!string.IsNullOrEmpty(translation))
                        {
                            arrayList[i] = translation;
                            translatedCount++;
                        }
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



