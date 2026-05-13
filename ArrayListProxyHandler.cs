// Handler for translating configured PlayMakerArrayListProxy data sources.
// Used for HUD, magazine, and other known array-backed content.

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace MWC_Localization_Core
{
    public class ArrayListProxyHandler : ITranslationSurface
    {
        public string Name { get { return "ArrayListProxyHandler"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return IsArrayMonitoringComplete && IsFontMonitoringComplete; } }
        public bool IsArrayMonitoringComplete { get { return translatedArrays.Count >= arrayPaths.Count; } }
        public bool IsFontMonitoringComplete
        {
            get { return parentSearchPaths != null && completedParentPaths.Count >= parentSearchPaths.Count; }
        }

        // Reference to main translation dictionary (from Plugin)
        private TranslationDictionary mainTranslations;
        private MagazineTextHandler magazineHandler;
        private TextMeshTranslator translator;
        
        // Config: List of array paths to translate (path:index)
        // Example: "GUI/HUD/Day/HUDValue:0"
        private HashSet<string> arrayPaths = new HashSet<string>();
        
        // Track which arrays we've already translated
        private HashSet<string> translatedArrays = new HashSet<string>();
        
        // Track which TextMesh instances already have fonts applied (by instance ID)
        private HashSet<int> fontAppliedInstances = new HashSet<int>();
        
        // Track parent paths whose current TextMeshes no longer need a new font application.
        private HashSet<string> completedParentPaths = new HashSet<string>();
        
        // Parent paths to search for TextMesh components (for performance)
        // Apply fonts to all TextMeshes under these paths.
        private List<string> parentSearchPaths;
        
        // Cache for array proxies to avoid repeated lookups
        private Dictionary<string, PlayMakerArrayListProxy> arrayProxyCache 
            = new Dictionary<string, PlayMakerArrayListProxy>();

        public void Initialize(TranslationContext ctx)
        {
            mainTranslations = ctx.Translations;
            magazineHandler = ctx.Magazine;
            translator = ctx.Translator;
            InitializeArrayPaths();
        }

        public int InitialPass()
        {
            int total = TranslateAllArrays();
            ApplyFontsToArrayElements();
            return total;
        }

        public int MonitorTick(float deltaTime)
        {
            int total = MonitorAndTranslateArrays();
            ApplyFontsToArrayElements();
            return total;
        }

        public void InitializeArrayPaths()
        {
            // Hardcoded array paths discovered from game data
            // Format: "GameObject/Path:ComponentIndex"
            arrayPaths.Clear();
            
            // HUD Elements
            arrayPaths.Add("GUI/HUD/Day/HUDValue:0");  // Day names: MONDAY, TUESDAY, etc.
            
            // Magazine System
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsFI:0"); // LinesSelected (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsFI:1"); // LinesRandom1 (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsFI:2"); // LinesRandom2 (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsEN:0"); // LinesSelected (EN)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsEN:1"); // LinesRandom1 (EN)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsEN:2"); // LinesRandom2 (EN)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/VINLIST_TirePics:1"); // Tire picture descriptions (FI)
            arrayPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/VINLIST_TirePics:2"); // Tire picture descriptions (EN)

            // Initialize TextMesh display path mappings
            InitializeTextMeshMappings();
        }

        private void InitializeTextMeshMappings()
        {
            // Parent paths to search under - apply fonts to all TextMeshes under these paths
            // Paths are specific enough that all children should get the localized font mapping
            parentSearchPaths = new List<string>
            {
                "GUI/HUD/Day",                            // HUD Day Display
                "Sheets/YellowPagesMagazine/Page1/Row1",  // Magazine Page 1 Row 1
                "Sheets/YellowPagesMagazine/Page1/Row2",  // Magazine Page 1 Row 2
                "Sheets/YellowPagesMagazine/Page2/Row3",  // Magazine Page 2 Row 3
                "Sheets/YellowPagesMagazine/Page2/Row4",  // Magazine Page 2 Row 4
                // Add more parent paths as needed
            };
        }

        public void ClearTranslations()
        {
            Reset();
        }

        public void Reset()
        {
            translatedArrays.Clear();
            arrayProxyCache.Clear();
            fontAppliedInstances.Clear();
            completedParentPaths.Clear();
        }

        // Translate configured arrays during scene initialization.
        public int TranslateAllArrays()
        {
            int totalTranslated = 0;

            foreach (string arrayKey in arrayPaths)
            {
                // Skip if already translated
                if (translatedArrays.Contains(arrayKey))
                    continue;

                int translated = TranslateArray(arrayKey);
                if (translated > 0)
                {
                    totalTranslated += translated;
                    translatedArrays.Add(arrayKey);
                }
            }

            return totalTranslated;
        }

        // Translate a specific array by path:index key
        private int TranslateArray(string arrayKey)
        {
            // Parse arrayKey: "GameObject/Path:ComponentIndex"
            if (!arrayKey.Contains(":"))
            {
                CoreConsole.Warning($"Invalid array key format (expected 'path:index'): {arrayKey}");
                return 0;
            }

            PlayMakerArrayListProxy proxy;
            if (!arrayProxyCache.TryGetValue(arrayKey, out proxy))
            {
                string[] parts = arrayKey.Split(':');
                string objectPath = parts[0];
                int componentIndex;

                if (!int.TryParse(parts[1], out componentIndex))
                {
                    CoreConsole.Warning($"Invalid component index in array key: {arrayKey}");
                    return 0;
                }

                // Find GameObject
                GameObject obj = LocalizationUtils.FindGameObjectCached(objectPath);
                if (obj == null)
                {
                    // Not available yet - this is normal for lazy-loaded content
                    return 0;
                }

                // Get PlayMakerArrayListProxy component
                PlayMakerArrayListProxy[] proxies = obj.GetComponents<PlayMakerArrayListProxy>();
                if (proxies == null || componentIndex >= proxies.Length)
                {
                    CoreConsole.Warning($"PlayMakerArrayListProxy[{componentIndex}] not found at {objectPath}");
                    return 0;
                }

                proxy = proxies[componentIndex];
                // Cache the proxy for later monitoring
                arrayProxyCache[arrayKey] = proxy;
            }

            // Translate array contents using existing translation dictionaries
            int translatedCount = 0;
            ArrayList arrayList = proxy.arrayList;

            for (int i = 0; i < arrayList.Count; i++)
            {
                if (arrayList[i] == null)
                    continue;

                string original = arrayList[i].ToString();
                if (string.IsNullOrEmpty(original))
                    continue;

                string translation = FindTranslation(original);
                if (translation != null)
                {
                    arrayList[i] = translation;
                    translatedCount++;
                }
            }

            if (translatedCount > 0)
            {
                CoreConsole.Print($"[ArrayListProxyHandler] Translated {translatedCount}/{arrayList.Count} items in {arrayKey}");
            }

            return translatedCount;
        }

        // Monitor configured arrays that may appear or populate after scene initialization.
        // Returns number of newly translated items
        public int MonitorAndTranslateArrays()
        {
            int totalTranslated = 0;

            foreach (string arrayKey in arrayPaths)
            {
                // Try to translate arrays that failed before (not yet loaded)
                if (!translatedArrays.Contains(arrayKey))
                {
                    int translated = TranslateArray(arrayKey);
                    
                    // Check if array exists and is populated (even if no new translations found)
                    // This handles cases where arrays are already translated or didn't need translation
                    bool isPopulated = false;
                    if (arrayProxyCache.TryGetValue(arrayKey, out PlayMakerArrayListProxy proxy))
                    {
                        if (proxy != null && proxy.arrayList != null && proxy.arrayList.Count > 0)
                        {
                            isPopulated = true;
                        }
                    }

                    // Mark as processed if we translated something OR if the array is fully populated
                    // This prevents infinite retry loops for already-translated arrays
                    if (translated > 0 || isPopulated)
                    {
                        if (translated > 0) totalTranslated += translated;
                        translatedArrays.Add(arrayKey);
                    }
                }
            }

            return totalTranslated;
        }

        // Find translation from main translations or magazine translations
        private string FindTranslation(string original)
        {
            // Try main translations first (TryGetExact handles normalization + LRU cache).
            if (mainTranslations.TryGetExact(original, out string translation))
                return translation;

            // Try magazine translations using the magazine handler's normalized lookup.
            return magazineHandler.GetTranslation(original);
        }

        // Apply localized fonts to TextMesh components displaying array data
        // Call this once during scene initialization, then periodically until all paths are complete
        public int ApplyFontsToArrayElements()
        {
            if (translator == null)
                return 0;

            // Early exit if all parent paths have been fully processed
            if (completedParentPaths.Count >= parentSearchPaths.Count)
                return 0;

            int fontsApplied = 0;

            // Search only under known parent paths to avoid a scene-wide lookup.
            foreach (string parentPath in parentSearchPaths)
            {
                // Skip already completed parent paths (huge performance boost)
                if (completedParentPaths.Contains(parentPath))
                    continue;

                GameObject parent = LocalizationUtils.FindGameObjectCached(parentPath);
                if (parent == null)
                    continue; // Not loaded yet - will try again later

                // Get all TextMesh components under this parent and apply fonts to ALL of them
                TextMesh[] textMeshes = parent.GetComponentsInChildren<TextMesh>(true);
                
                bool anyNewFonts = false;

                foreach (TextMesh textMesh in textMeshes)
                {
                    if (textMesh == null)
                        continue;

                    // Skip if already processed this instance
                    int instanceId = textMesh.GetInstanceID();
                    if (fontAppliedInstances.Contains(instanceId))
                        continue;

                    string textMeshPath = LocalizationUtils.GetGameObjectPath(textMesh.gameObject);

                    // Apply font to this TextMesh
                    if (translator.ApplyFontOnly(textMesh, textMeshPath))
                    {
                        fontsApplied++;
                        fontAppliedInstances.Add(instanceId);
                        anyNewFonts = true;
                    }
                }

                // Only mark this parent path as complete if we actually found TextMeshes under it
                // and none of them produced a new font application. If textMeshes.Length == 0 the
                // parent exists but children haven't been lazy-loaded yet - retry on the next tick.
                if (textMeshes.Length > 0 && !anyNewFonts)
                {
                    completedParentPaths.Add(parentPath);
                }
            }

            if (fontsApplied > 0)
            {
                CoreConsole.Print($"[ArrayListProxyHandler] Applied Custom font to {fontsApplied} TextMesh components ({completedParentPaths.Count}/{parentSearchPaths.Count} paths complete)");
            }

            return fontsApplied;
        }
    }
}
