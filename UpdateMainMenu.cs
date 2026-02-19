using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    public class UpdateMainMenu : MonoBehaviour
    {
        private bool Loaded;
        private Dictionary<string, string> translations;

        /// <summary>
        /// Initialize with the translation dictionary and start the waiting coroutine
        /// </summary>

        public void Initialize(Dictionary<string, string> translationDict)
        {
            translations = translationDict;
            // Start a coroutine to wait until FSMs are ready and then apply translations once
            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            if (Loaded) yield break;

            // Wait until translations are provided
            while (translations == null)
                yield return null;

            // Try to locate objects and FSMs, yielding each frame until available and initialized
            PlayMakerFSM folkFsm = null;
            PlayMakerFSM cdFsm = null;

            while (!Loaded)
            {
                var folkObj = GameObject.Find("Radio/Folk");
                var cdObj = GameObject.Find("Radio/CD");

                if (folkObj == null || cdObj == null)
                {
                    yield return null;
                    continue;
                }

                var folkFsms = folkObj.GetComponents<PlayMakerFSM>();
                if (folkFsms == null || folkFsms.Length < 2)
                {
                    yield return null;
                    continue;
                }

                folkFsm = folkFsms[1];
                cdFsm = cdObj.GetComponent<PlayMakerFSM>();

                if (folkFsm == null || cdFsm == null)
                {
                    yield return null;
                    continue;
                }

                if (!folkFsm.Fsm.Initialized || !cdFsm.Fsm.Initialized)
                {
                    yield return null;
                    continue;
                }

                // Both FSMs ready - apply translations
                ApplyTranslationsToFsms(folkFsm, cdFsm);

                Loaded = true;
                // Destroy host GameObject to avoid leaving empty object behind
                Destroy(this.gameObject);
                yield break;
            }
        }

        private void ApplyTranslationsToFsms(PlayMakerFSM folkFsm, PlayMakerFSM cdFsm)
        {
            // Get translations from dictionary (use TryGetValue for performance)
            string radioNotImportedText = GetTranslation("NOT IMPORTED", "");
            string radioImportedText = GetTranslation("RADIO IMPORTED", "");
            string cdsImportedText = GetTranslation("CD'S IMPORTED", "");

            // NOT IMPORTED (Path variables)
            var folkPath = folkFsm.FsmVariables.GetFsmString("Path");
            if (folkPath != null) folkPath.Value = radioNotImportedText;

            var cdPath = cdFsm.FsmVariables.GetFsmString("Path");
            if (cdPath != null) cdPath.Value = radioNotImportedText;

            // RADIO IMPORTED (state Off, action[1])
            HutongGames.PlayMaker.FsmState offState = FindStateByName(folkFsm, "Off");
            if (offState != null && offState.Actions != null && offState.Actions.Length > 1)
            {
                var radioimp = offState.Actions[1] as HutongGames.PlayMaker.Actions.SetStringValue;
                if (radioimp != null) radioimp.stringValue = radioImportedText;
            }

            // CD'S IMPORTED (state State 1, action[0])
            HutongGames.PlayMaker.FsmState st1 = FindStateByName(cdFsm, "State 1");
            if (st1 != null && st1.Actions != null && st1.Actions.Length > 0)
            {
                var cdimp = st1.Actions[0] as HutongGames.PlayMaker.Actions.SetStringValue;
                if (cdimp != null) cdimp.stringValue = cdsImportedText;
            }
        }

        private HutongGames.PlayMaker.FsmState FindStateByName(PlayMakerFSM fsm, string name)
        {
            if (fsm == null || fsm.FsmStates == null) return null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                var s = fsm.FsmStates[i];
                if (s != null && s.Name == name) return s;
            }
            return null;
        }

        /// <summary>
        /// Get translation from dictionary, with fallback to default value
        /// </summary>
        private string GetTranslation(string key, string defaultValue)
        {
            if (translations == null)
                return defaultValue;

            string normalizedKey = MLCUtils.FormatUpperKey(key);
            return translations.ContainsKey(normalizedKey) ? translations[normalizedKey] : defaultValue;
        }
    }
}
