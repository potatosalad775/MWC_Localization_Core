using UnityEngine;

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
        private FsmTextHook fsmTextHook;
        private SceneTranslationManager sceneManager;

        private bool isInitialized = false;
        
        // Throttling timer for array/proxy monitoring. Split the work across
        // frames so lazy-load checks do not land as one frametime spike.
        private float lastArrayMonitorStepTime = 0f;
        private int arrayMonitorStep = 0;

        public void Initialize(
            UnifiedTextMeshMonitor textMeshMonitorInstance,
            TeletextHandler teletextHandlerInstance,
            ArrayListProxyHandler arrayListHandlerInstance,
            HashTableProxyHandler hashTableHandlerInstance,
            FsmTextHook fsmTextHookInstance,
            SceneTranslationManager sceneManagerInstance)
        {
            textMeshMonitor = textMeshMonitorInstance;
            teletextHandler = teletextHandlerInstance;
            arrayListHandler = arrayListHandlerInstance;
            hashTableHandler = hashTableHandlerInstance;
            fsmTextHook = fsmTextHookInstance;
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
                if (fsmTextHook != null)
                {
                    fsmTextHook.UpdateForScene(currentScene, false);
                }
                
                // Throttled array monitoring (teletext, PlayMaker ArrayLists).
                // Each category still runs once per ARRAY_MONITOR_INTERVAL, but
                // the work is staggered across four smaller passes.
                if (Time.time - lastArrayMonitorStepTime >= LocalizationConstants.ARRAY_MONITOR_STEP_INTERVAL)
                {
                    if (arrayMonitorStep == 0)
                    {
                        int translated = teletextHandler.MonitorAndTranslateArrays();
                        if (translated > 0)
                        {
                            CoreConsole.Print($"[LateUpdateHandler] Translated {translated} newly-loaded teletext items");
                        }
                    }
                    else if (arrayMonitorStep == 1)
                    {
                        int arrayTranslated = arrayListHandler.MonitorAndTranslateArrays();
                        if (arrayTranslated > 0)
                        {
                            CoreConsole.Print($"[LateUpdateHandler] Translated {arrayTranslated} newly-loaded array items");
                        }
                    }
                    else if (arrayMonitorStep == 2)
                    {
                        int hashTableTranslated = hashTableHandler.MonitorAndTranslateHashTables();
                        if (hashTableTranslated > 0)
                        {
                            CoreConsole.Print($"[LateUpdateHandler] Translated {hashTableTranslated} newly-loaded hash table items");
                        }
                    }
                    else
                    {
                        // Monitor and apply fonts to late-initialized TextMesh components
                        arrayListHandler.ApplyFontsToArrayElements();
                    }

                    arrayMonitorStep = (arrayMonitorStep + 1) % 4;
                    lastArrayMonitorStepTime = Time.time;
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
            lastArrayMonitorStepTime = 0f;
            arrayMonitorStep = 0;
            isInitialized = false;
        }
    }
}
