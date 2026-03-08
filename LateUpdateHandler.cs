using MSCLoader;
using UnityEngine;
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
        private TextMeshTranslator translator;
        private UnifiedTextMeshMonitor textMeshMonitor;
        private TeletextHandler teletextHandler;
        private ArrayListProxyHandler arrayListHandler;
        private HashTableProxyHandler hashTableHandler;
        private SceneTranslationManager sceneManager;
        
        // Cached references for critical UI (EveryFrame monitoring)
        private class CriticalUIReference
        {
            public string Path;
            public TextMesh TextMesh;
            public int RetryCount;
            public float NextRetryTime;
            public bool IsRegistered;
        }

        private bool isInitialized = false;
        
        // Throttling timers (MOVED from MWC_Localization_Core.cs)
        private float lastArrayCheckTime = 0f;
        private float lastPosBootCheckTime = 0f;
        private TextMesh posBootTextMesh;
        private TextMesh posCommandTextMesh;

        public void Initialize(
            TextMeshTranslator translatorInstance,
            UnifiedTextMeshMonitor textMeshMonitorInstance,
            TeletextHandler teletextHandlerInstance,
            ArrayListProxyHandler arrayListHandlerInstance,
            HashTableProxyHandler hashTableHandlerInstance,
            SceneTranslationManager sceneManagerInstance)
        {
            translator = translatorInstance;
            textMeshMonitor = textMeshMonitorInstance;
            teletextHandler = teletextHandlerInstance;
            arrayListHandler = arrayListHandlerInstance;
            hashTableHandler = hashTableHandlerInstance;
            sceneManager = sceneManagerInstance;
            isInitialized = true;
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

                // Direct fallback for POS boot sequence TextMeshes (FSM-driven dynamic output).
                if (Time.time - lastPosBootCheckTime >= LocalizationConstants.FAST_POLLING_INTERVAL)
                {
                    ApplyPosBootSequenceDirectFallback();
                    lastPosBootCheckTime = Time.time;
                }
                
                // Throttled array monitoring (teletext, PlayMaker ArrayLists)
                if (Time.time - lastArrayCheckTime >= LocalizationConstants.ARRAY_MONITOR_INTERVAL)
                {
                    // Monitor teletext arrays for lazy-loaded content
                    int translated = teletextHandler.MonitorAndTranslateArrays();
                    if (translated > 0)
                    {
                        CoreConsole.Print($"[LateUpdateHandler] Translated {translated} newly-loaded teletext items");
                    }
                    
                    // Monitor generic arrays for lazy-loaded content
                    int arrayTranslated = arrayListHandler.MonitorAndTranslateArrays();
                    if (arrayTranslated > 0)
                    {
                        CoreConsole.Print($"[LateUpdateHandler] Translated {arrayTranslated} newly-loaded array items");
                    }

                    int hashTableTranslated = hashTableHandler.MonitorAndTranslateHashTables();
                    if (hashTableTranslated > 0)
                    {
                        CoreConsole.Print($"[LateUpdateHandler] Translated {hashTableTranslated} newly-loaded hash table items");
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
        /// Helper method - Scan for new main menu elements
        /// </summary>
        private void ScanForNewMainMenuElements()
        {
            GameObject mainMenuRoot = MLCUtils.FindGameObjectCached("MainMenu");
            if (mainMenuRoot == null)
                return;

            TextMesh[] allTextMeshes = mainMenuRoot.GetComponentsInChildren<TextMesh>(true);
            
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;

                string path = MLCUtils.GetGameObjectPath(tm.gameObject);
                translator.TranslateAndApplyFont(tm, path, null);
            }
        }

        /// <summary>
        /// Fallback path for POS computer boot text that may bypass standard FSM/TextMesh monitors.
        /// Applies font and line-by-line translation on live TextMesh content.
        /// </summary>
        private void ApplyPosBootSequenceDirectFallback()
        {
            ApplyPosMultilineTranslation("COMPUTER/SYSTEM/POS/Text", ref posBootTextMesh);
            ApplyPosCommandFontOnly("COMPUTER/SYSTEM/POS/Command", ref posCommandTextMesh);
        }

        private void ApplyPosMultilineTranslation(string path, ref TextMesh cachedTextMesh)
        {
            TextMesh tm = ResolveTextMeshIncludingInactive(path, ref cachedTextMesh);
            if (tm == null || string.IsNullOrEmpty(tm.text))
                return;

            translator.ApplyFontOnly(tm, path);
            bool translated = translator.TranslateAndApplyFont(tm, path, null);
            if (!translated)
            {
                translator.TranslateMultilineByLines(tm, path);
            }
        }

        private TextMesh ResolveTextMeshIncludingInactive(string fullPath, ref TextMesh cachedTextMesh)
        {
            if (cachedTextMesh != null && cachedTextMesh.gameObject != null)
                return cachedTextMesh;

            cachedTextMesh = MLCUtils.FindTextMeshIncludingInactiveByPath(fullPath);
            return cachedTextMesh;
        }

        private void ApplyPosCommandFontOnly(string path, ref TextMesh cachedTextMesh)
        {
            TextMesh tm = ResolveTextMeshIncludingInactive(path, ref cachedTextMesh);
            if (tm == null)
                return;

            translator.ApplyFontOnly(tm, path);
        }

        /// <summary>
        /// Clear cache when scene changes
        /// </summary>
        public void ClearCache()
        {
            lastArrayCheckTime = 0f;
            lastPosBootCheckTime = 0f;
            posBootTextMesh = null;
            posCommandTextMesh = null;
            isInitialized = false;
        }
    }
}
