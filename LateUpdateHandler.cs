using MSCLoader;
using UnityEngine;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// MonoBehaviour component for ALL continuous translation monitoring
    /// Must run in LateUpdate() to translate AFTER game's Update() regenerates text
    /// Centralizes continuous translation work outside the main mod entry point
    /// </summary>
    public class LateUpdateHandler : MonoBehaviour
    {
        // Dependencies
        private UnifiedTextMeshMonitor textMeshMonitor;
        private TeletextHandler teletextHandler;
        private ArrayListProxyHandler arrayListHandler;
        private HashTableProxyHandler hashTableHandler;
        private SceneTranslationManager sceneManager;

        private bool isInitialized = false;
        
        // Throttling timer for array and proxy monitoring
        private float lastArrayCheckTime = 0f;

        public void Initialize(
            UnifiedTextMeshMonitor textMeshMonitorInstance,
            TeletextHandler teletextHandlerInstance,
            ArrayListProxyHandler arrayListHandlerInstance,
            HashTableProxyHandler hashTableHandlerInstance,
            SceneTranslationManager sceneManagerInstance)
        {
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

            // GAME scene monitoring
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

            // Main menu monitoring
            else if (currentScene == "MainMenu" && sceneManager.HasSceneBeenTranslated("MainMenu"))
            {
                // Monitor for dynamic changes in main menu
                textMeshMonitor.Update(Time.deltaTime);
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
