using MSCLoader;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// MonoBehaviour component for ALL continuous translation monitoring
    /// Must run in LateUpdate() to translate AFTER game's Update() regenerates text
    /// DIRECT COPY from Plugin.cs LateUpdate - all monitoring logic moved here
    /// Same pattern as legacy LanguageFramework's MainObject for My Summer Car
    /// </summary>
    public class LateUpdateHandler : MonoBehaviour
    {
        // Dependencies
        private MWC_Localization_Core mod;
        private TextMeshTranslator translator;
        private UnifiedTextMeshMonitor textMeshMonitor;
        private TeletextHandler teletextHandler;
        private ArrayListProxyHandler arrayListHandler;
        private SceneTranslationManager sceneManager;
        
        private bool isInitialized = false;
        
        // Throttling timers (MOVED from MWC_Localization_Core.cs)
        private float lastArrayCheckTime = 0f;
        // Coroutine for incremental scene translation
        private Coroutine translationCoroutine = null;
        private int translationBatchSize = LocalizationConstants.TRANSLATION_BATCH_SIZE;

        public void Initialize(
            MWC_Localization_Core modInstance, 
            TextMeshTranslator translatorInstance,
            UnifiedTextMeshMonitor textMeshMonitorInstance,
            TeletextHandler teletextHandlerInstance,
            ArrayListProxyHandler arrayListHandlerInstance,
            SceneTranslationManager sceneManagerInstance)
        {
            mod = modInstance;
            translator = translatorInstance;
            textMeshMonitor = textMeshMonitorInstance;
            teletextHandler = teletextHandlerInstance;
            arrayListHandler = arrayListHandlerInstance;
            sceneManager = sceneManagerInstance;
            isInitialized = true;
            CoreConsole.Print($"[{mod.Name}] LateUpdateHandler initialized");
        }

        // ...no delayed teletext coroutine in original implementation...

        /// <summary>
        /// Coroutine that incrementally translates TextMesh objects in the scene.
        /// Processes up to translationBatchSize items per frame.
        /// </summary>
        IEnumerator TranslateSceneIncremental()
        {
            if (translator == null)
            {
                CoreConsole.Warning($"[{mod.Name}] TranslateSceneIncremental: translator is null");
                yield break;
            }

            // Register path/rule elements before scanning
            if (textMeshMonitor != null)
                textMeshMonitor.RegisterAllPathRuleElements();

            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();
            int translatedCount = 0;
            int processedInBatch = 0;

            for (int i = 0; i < allTextMeshes.Length; i++)
            {
                TextMesh tm = allTextMeshes[i];
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;

                string path = MLCUtils.GetGameObjectPath(tm.gameObject);
                bool translated = translator.TranslateAndApplyFont(tm, path, null);
                if (translated)
                    translatedCount++;

                // Batch yield to avoid doing all work in one frame
                processedInBatch++;
                if (processedInBatch >= translationBatchSize)
                {
                    processedInBatch = 0;
                    yield return null;
                }
            }

            CoreConsole.Print($"[{mod.Name}] Incremental scene translation complete: {translatedCount}/{allTextMeshes.Length} TextMesh objects translated");
            translationCoroutine = null;
        }

        /// <summary>
        /// Start an incremental scene translation using a coroutine to process TextMesh objects
        /// in batches over multiple frames to avoid stalls.
        /// </summary>
        public void StartSceneTranslationIncremental(int batchSize = LocalizationConstants.TRANSLATION_BATCH_SIZE)
        {
            if (!isInitialized)
                return;

            translationBatchSize = batchSize > 0 ? batchSize : LocalizationConstants.TRANSLATION_BATCH_SIZE;

            if (translationCoroutine != null)
            {
                StopCoroutine(translationCoroutine);
                translationCoroutine = null;
            }

            translationCoroutine = StartCoroutine(TranslateSceneIncremental());
        }

        /// <summary>
        /// Stop any running incremental translation coroutine.
        /// </summary>
        public void StopSceneTranslationIncremental()
        {
            if (translationCoroutine != null)
            {
                StopCoroutine(translationCoroutine);
                translationCoroutine = null;
            }
        }

        /// <summary>
        /// LateUpdate runs AFTER all Update() calls (including MSCLoader and game's Update)
        /// This ensures we translate AFTER the game regenerates the text
        /// </summary>
        private void LateUpdate()
        {
            if (!isInitialized)
                return;

            string currentScene = Application.loadedLevelName;

            // GAME scene monitoring - EXACT COPY from Mod_Update
            if (currentScene == "GAME" && sceneManager.HasSceneBeenTranslated("GAME"))
            {
                // Throttled monitoring for regular TextMesh elements
                textMeshMonitor.Update(Time.deltaTime);
                
                // Throttled array monitoring (teletext, PlayMaker ArrayLists)
                if (Time.time - lastArrayCheckTime >= LocalizationConstants.ARRAY_MONITOR_INTERVAL)
                {
                    // Monitor teletext arrays for lazy-loaded content
                    int translated = teletextHandler.MonitorAndTranslateArrays();
                    if (translated > 0)
                    {
                        CoreConsole.Print($"[{mod.Name}] [Runtime] Translated {translated} newly-loaded teletext items");
                        // Apply fonts/translations to teletext display immediately after translation
                        ApplyTeletextFonts();
                    }

        
                    
                    // Monitor and disable FSM-driven Bottomlines (handles late initialization)
                    int fsmDisabled = teletextHandler.DisableTeletextFSMs(translator);
                    if (fsmDisabled > 0)
                    {
                        CoreConsole.Print($"[{mod.Name}] [Runtime] Disabled {fsmDisabled} Bottomline FSMs");
                    }
                    
                    // Monitor generic arrays for lazy-loaded content
                    int arrayTranslated = arrayListHandler.MonitorAndTranslateArrays();
                    if (arrayTranslated > 0)
                    {
                        CoreConsole.Print($"[{mod.Name}] [Runtime] Translated {arrayTranslated} newly-loaded array items");
                    }
                    
                    // Monitor and apply fonts to late-initialized TextMesh components
                    arrayListHandler.ApplyFontsToArrayElements();
                    
                    lastArrayCheckTime = Time.time;
                }
            }

            // Main menu monitoring - EXACT COPY from Mod_Update
            else if (currentScene == "MainMenu" && sceneManager.HasSceneBeenTranslated("MainMenu"))
            {
                // Monitor for dynamic changes in main menu
                textMeshMonitor.Update(Time.deltaTime);
            }
        }
        
        /// <summary>
        /// Helper method - Apply fonts to teletext display
        /// </summary>
        private void ApplyTeletextFonts()
        {
            List<string> teletextRootPaths = new List<string>
            {
                "Systems/TV/Teletext/VKTekstiTV/PAGES",
                "Systems/TV/TVGraphics/CHAT/Generated/Lines"
            };
            int fontChangedCount = 0;

            foreach (string rootPath in teletextRootPaths)
            {
                GameObject root = GameObject.Find(rootPath);
                if (root == null)
                    continue;

                // Get all TextMesh components in teletext display
                TextMesh[] textMeshes = root.GetComponentsInChildren<TextMesh>(true);

                foreach (TextMesh textMesh in textMeshes)
                {
                    if (textMesh == null)
                        continue;

                    string path = MLCUtils.GetGameObjectPath(textMesh.gameObject);

                    // Apply font using the translator's font mapping logic
                    if (translator.ApplyFontOnly(textMesh, path))
                    {
                        fontChangedCount++;
                    }
                }
            }
        }
        
        /// <summary>
        /// Clear cache when scene changes
        /// </summary>
        public void ClearCache()
        {
            lastArrayCheckTime = 0f;
            isInitialized = false;
        }
    }
}
