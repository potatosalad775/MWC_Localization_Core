using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// NOTE: FSM pattern matching lives in TranslationDictionary (placeholder patterns).
    /// </summary>
    public class TeletextHandler : ITranslationSurface
    {
        public string Name { get { return "TeletextHandler"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return IsStaticArrayMonitoringComplete; } }
        public bool IsStaticArrayMonitoringComplete { get; private set; }

        // Dynamic TV chat is split into TeletextChatHandler (Medium cadence). This handler
        // owns the loaded translation data; the chat handler reads it via TryGetChatTranslations.
        public const string ChatMessagesPath = "Systems/TV/ChatMessages";
        public const string ChatMessagesCategoryName = "ChatMessages.Messages";

        private TranslationDictionary sharedTranslations;
        private string teletextFilePath;

        // Category-based translations: [referenceName][originalText] = translatedText (for key-based lookup)
        private Dictionary<string, Dictionary<string, string>> categoryTranslations = 
            new Dictionary<string, Dictionary<string, string>>();
        
        // Index-based translations: [categoryName][index] = translatedText (for runtime replacement)
        private Dictionary<string, List<string>> indexBasedTranslations = 
            new Dictionary<string, List<string>>();
        
        // Track which arrays have been translated already
        private HashSet<string> translatedArrays = new HashSet<string>();

        // Total translations loaded on the most recent LoadTeletextTranslations call.
        private int lastLoadedTranslationCount = 0;
        
        // GameObject path to category mapping.
        // ChatMessagesPath is registered so the alias setup still applies, but
        // MonitorAndTranslateArrays skips it — TeletextChatHandler owns the chat tick.
        private Dictionary<string, string> pathPrefixes = new Dictionary<string, string>
        {
            { "Systems/TV/Teletext/VKTekstiTV/Database", "" },  // Use referenceName directly
            { ChatMessagesPath, "ChatMessages" },               // Prefix with "ChatMessages."
        };
        
        // Path Prefix Proxy cache
        private Dictionary<string, PlayMakerArrayListProxy[]> proxyCache = 
            new Dictionary<string, PlayMakerArrayListProxy[]>();

        public TeletextHandler()
        {
        }

        public void Initialize(TranslationContext ctx)
        {
            sharedTranslations = ctx.Translations;
            teletextFilePath = Path.Combine(ctx.AssetsFolder, "translate_teletext.txt");
            LoadTeletextTranslations(teletextFilePath);
            // Teletext file also carries placeholder FSM patterns shared across the dictionary.
            sharedTranslations.LoadPatternsFromFile(teletextFilePath);
        }

        public int InitialPass()
        {
            return MonitorAndTranslateArrays();
        }

        public int MonitorTick(float deltaTime)
        {
            return MonitorAndTranslateArrays();
        }

        public void ClearTranslations()
        {
            categoryTranslations.Clear();
            indexBasedTranslations.Clear();
            lastLoadedTranslationCount = 0;
            Reset();
        }

        /// <summary>
        /// Load teletext translations from INI-style file with category sections
        /// Supports both key-value pairs and index-based translations (in order)
        /// </summary>
        public void LoadTeletextTranslations(string filePath)
        {
            TranslationFileParser.ParseCategoryBasedFile(
                filePath,
                out Dictionary<string, Dictionary<string, string>> loadedCategoryTranslations,
                out Dictionary<string, List<string>> loadedIndexBasedTranslations);

            categoryTranslations = loadedCategoryTranslations;
            indexBasedTranslations = loadedIndexBasedTranslations;

            // Create alias: ChatMessages.Messages uses ChatMessages.All translations.
            // Mirror the alias on the index-based map too so the ordered fallback in
            // TranslateArrayListProxy works for ChatMessages.Messages - otherwise entries
            // that don't exact-match by key fall through with no translation at all.
            if (categoryTranslations.TryGetValue("ChatMessages.All", out Dictionary<string, string> chatAllDict))
            {
                categoryTranslations["ChatMessages.Messages"] = chatAllDict;
            }
            if (indexBasedTranslations.TryGetValue("ChatMessages.All", out List<string> chatAllList))
            {
                indexBasedTranslations["ChatMessages.Messages"] = chatAllList;
            }

            lastLoadedTranslationCount = 0;
            foreach (var category in categoryTranslations.Values)
            {
                lastLoadedTranslationCount += category.Count;
            }
        }

        /// <summary>
        /// Get total count of loaded teletext translations (sum across categories).
        /// </summary>
        public int GetTranslationCount()
        {
            return lastLoadedTranslationCount;
        }

        /// <summary>
        /// Exposes the loaded chat-messages translation tables for TeletextChatHandler.
        /// Returns false if no chat data was loaded from translate_teletext.txt.
        /// </summary>
        public bool TryGetChatTranslations(out Dictionary<string, string> exact, out List<string> indexed)
        {
            categoryTranslations.TryGetValue(ChatMessagesCategoryName, out exact);
            indexBasedTranslations.TryGetValue(ChatMessagesCategoryName, out indexed);
            return exact != null && exact.Count > 0;
        }

        /// <summary>
        /// Monitor and translate teletext arrays
        /// Returns number of new items translated
        /// </summary>
        public int MonitorAndTranslateArrays()
        {
            if (IsStaticArrayMonitoringComplete)
                return 0;

            try
            {
                int totalTranslated = 0;
                bool allStaticArraysComplete = true;

                foreach (var pathPrefix in pathPrefixes.Keys)
                {
                    // Dynamic TV chat is handled by TeletextChatHandler at Medium cadence.
                    if (pathPrefix == ChatMessagesPath)
                        continue;

                    PlayMakerArrayListProxy[] proxies;
                    if (!proxyCache.ContainsKey(pathPrefix))
                    {
                        // First time accessing this path - cache proxies
                        GameObject dataObject = LocalizationUtils.FindGameObjectCached(pathPrefix);
                        if (dataObject == null)
                        {
                            allStaticArraysComplete = false;
                            continue;
                        }

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

                        // Try translating only if not already done. Chat is excluded above,
                        // so everything reaching here is static and can be marked done.
                        if (!translatedArrays.Contains(arrayKey))
                        {
                            int translated = TranslateArrayListProxy(proxies[i], categoryName);

                            // Mark as processed if it's already populated, or there are no translations
                            // available (to avoid repeated checks).
                            bool isPopulated = proxies[i] != null && proxies[i]._arrayList != null && proxies[i]._arrayList.Count > 0;
                            bool isTranslationAvailable = categoryTranslations.ContainsKey(categoryName) &&
                                                          categoryTranslations[categoryName].Count > 0;

                            if (translated > 0 || isPopulated || !isTranslationAvailable)
                            {
                                if (translated > 0)
                                {
                                    CoreConsole.Print($"[TeletextHandler] Translated '{categoryName}' with {translated} items");
                                    totalTranslated += translated;
                                }
                                translatedArrays.Add(arrayKey);
                            }
                        }

                        if (!translatedArrays.Contains(arrayKey))
                            allStaticArraysComplete = false;
                    }
                }

                if (allStaticArraysComplete)
                    IsStaticArrayMonitoringComplete = true;

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
                    string translationKey = TranslationFileParser.UnescapeString(normalizedOriginal);

                    // Keep fallback alignment based on non-empty source entries only.
                    nonEmptySourceIndex++;
                    
                    // Try exact key match first
                    if (translations.TryGetValue(translationKey, out string translation))
                    {
                        if (original != translation)
                        {
                            arrayList[i] = translation;
                            translatedCount++;
                        }
                    }
                    // Fallback: Use index-based translation if available
                    else if (hasIndexFallback && nonEmptySourceIndex < indexBasedTranslations[categoryName].Count)
                    {
                        string indexTranslation = indexBasedTranslations[categoryName][nonEmptySourceIndex];
                        if (!string.IsNullOrEmpty(indexTranslation) && original != indexTranslation)
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
            IsStaticArrayMonitoringComplete = false;
        }
    }
}



