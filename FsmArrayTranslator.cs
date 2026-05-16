using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Translates PlayMakerArrayListProxy content that is populated by the SplitTextToArrayList
    /// FSM action (teletext news + TV chat in vanilla MSC).
    ///
    /// Strategy:
    ///   1. At InitialPass, walk every PlayMakerFSM on each tracked GameObject. For every
    ///      SplitTextToArrayList action, splice a custom FsmStateAction right after it that
    ///      translates the just-populated ArrayList in place. When the FSM runs the state,
    ///      SplitText fills the array with Finnish, then our injected action immediately
    ///      replaces entries with translated copies - all before any reader sees the array.
    ///   2. Also translate any ArrayLists that have already been populated (e.g. chat's
    ///      'All' pool, whose FSM ran at game launch and won't re-fire).
    ///
    /// Lookup chain: per-entry translations from translate_teletext.txt first (multi-line keys
    /// normalized via NormalizeMultiLineKey), then the global TranslationDictionary loaded from
    /// translate.txt etc. as a fallback. Authors can put context-specific overrides in
    /// translate_teletext.txt and rely on shared phrases living in translate.txt.
    ///
    /// Once a path has been processed (FSM walked + live ArrayLists translated), it is never
    /// revisited. No continuous polling.
    /// </summary>
    public class FsmArrayTranslator : ITranslationSurface
    {
        public string Name { get { return "FsmArrayTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return processedPaths.Count >= targetPaths.Count; } }

        private TranslationDictionary sharedTranslations;
        private string teletextFilePath;

        // Flat per-entry translations from translate_teletext.txt. Stable reference so the
        // injected actions don't need re-wiring when F8 reload refills it in place.
        private readonly Dictionary<string, string> teletextTranslations = new Dictionary<string, string>();

        private readonly HashSet<string> targetPaths = new HashSet<string>
        {
            "Systems/Teletext/VKTekstiTV/Database",
        };

        private readonly HashSet<string> processedPaths = new HashSet<string>();

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
            return ProcessAllPaths();
        }

        public int MonitorTick(float deltaTime)
        {
            return ProcessAllPaths();
        }

        public void ClearTranslations()
        {
            teletextTranslations.Clear();
            Reset();
        }

        public void Reset()
        {
            processedPaths.Clear();
        }

        public void LoadTeletextTranslations(string filePath)
        {
            TranslationFileParser.ParseCategoryBasedFile(
                filePath,
                out Dictionary<string, Dictionary<string, string>> loadedCategoryTranslations);

            // Flatten category sections into a single per-entry dict. Categories survive in the
            // file format for backward compatibility but no longer affect runtime lookup -
            // each entry's key is the original text, regardless of which section it lives in.
            teletextTranslations.Clear();
            foreach (var category in loadedCategoryTranslations.Values)
            {
                foreach (KeyValuePair<string, string> pair in category)
                    teletextTranslations[pair.Key] = pair.Value;
            }
        }

        public int GetTranslationCount()
        {
            return teletextTranslations.Count;
        }

        private int ProcessAllPaths()
        {
            try
            {
                int totalTranslated = 0;
                foreach (string path in targetPaths)
                {
                    if (processedPaths.Contains(path))
                        continue;

                    GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(path);
                    if (go == null)
                        continue;

                    InjectTranslationActions(go);
                    totalTranslated += TranslatePopulatedProxies(go);
                    processedPaths.Add(path);
                }
                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[FsmArrayTranslator] Error processing paths: {ex.Message}");
                return 0;
            }
        }

        // Walk every PlayMakerFSM on the GameObject. For each state that contains one or more
        // SplitTextToArrayList actions, build a new Actions array where each SplitText action
        // is followed by an injected TranslateArrayListAction that translates the proxy this
        // SplitText writes to.
        private void InjectTranslationActions(GameObject go)
        {
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();

            for (int fi = 0; fi < fsms.Length; fi++)
            {
                PlayMakerFSM fsm = fsms[fi];
                if (fsm == null || fsm.FsmStates == null) continue;

                for (int si = 0; si < fsm.FsmStates.Length; si++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[si];
                    if (state == null || state.Actions == null) continue;

                    int injected = SpliceTranslationActionsInto(state, go);
                    if (injected > 0)
                        CoreConsole.Print($"[FsmArrayTranslator] Injected {injected} translate action(s) into {fsm.FsmName}/{state.Name}");
                }
            }
        }

        private int SpliceTranslationActionsInto(HutongGames.PlayMaker.FsmState state, GameObject go)
        {
            HutongGames.PlayMaker.FsmStateAction[] oldActions = state.Actions;

            // F8-reload guard: if this state already has one of our injected actions, the
            // previous injection is still in place. Skip - the teletextTranslations dict
            // reference is stable, so updated translations are picked up automatically.
            for (int i = 0; i < oldActions.Length; i++)
            {
                if (oldActions[i] is TranslateArrayListAction)
                    return 0;
            }

            List<HutongGames.PlayMaker.FsmStateAction> newActions = null;
            int injected = 0;

            for (int ai = 0; ai < oldActions.Length; ai++)
            {
                HutongGames.PlayMaker.FsmStateAction action = oldActions[ai];
                if (newActions != null)
                    newActions.Add(action);

                if (action == null) continue;
                if (action.GetType().Name != "SplitTextToArrayList") continue;

                TranslateArrayListAction inj = BuildInjectionAction(action, go);
                if (inj == null) continue;

                // First match in this state - lazy-init the new action list with everything seen so far.
                if (newActions == null)
                {
                    newActions = new List<HutongGames.PlayMaker.FsmStateAction>(oldActions.Length + 2);
                    for (int j = 0; j <= ai; j++)
                        newActions.Add(oldActions[j]);
                }
                newActions.Add(inj);
                injected++;
            }

            if (newActions != null)
                state.Actions = newActions.ToArray();

            return injected;
        }

        private TranslateArrayListAction BuildInjectionAction(
            HutongGames.PlayMaker.FsmStateAction splitAction,
            GameObject go)
        {
            FieldInfo referenceField = FsmUtils.GetField(splitAction.GetType(), "reference");
            if (referenceField == null) return null;

            HutongGames.PlayMaker.FsmString referenceFsm = referenceField.GetValue(splitAction) as HutongGames.PlayMaker.FsmString;
            string refName = referenceFsm != null ? referenceFsm.Value : null;
            if (string.IsNullOrEmpty(refName)) return null;

            // Resolve the target proxy at injection time so the runtime action doesn't need
            // to walk components on every state entry.
            PlayMakerArrayListProxy target = null;
            PlayMakerArrayListProxy[] proxies = go.GetComponents<PlayMakerArrayListProxy>();
            for (int p = 0; p < proxies.Length; p++)
            {
                if (proxies[p] != null && proxies[p].referenceName == refName)
                {
                    target = proxies[p];
                    break;
                }
            }
            if (target == null) return null;

            return new TranslateArrayListAction
            {
                target = target,
                teletextTranslations = teletextTranslations,
                sharedTranslations = sharedTranslations,
                label = refName,
            };
        }

        // For ArrayLists already populated before our injection lands (chat's 'All' pool, whose
        // FSM ran at game launch and won't re-fire), translate the live entries in place.
        private int TranslatePopulatedProxies(GameObject go)
        {
            PlayMakerArrayListProxy[] proxies = go.GetComponents<PlayMakerArrayListProxy>();
            int totalTranslated = 0;

            for (int i = 0; i < proxies.Length; i++)
            {
                if (proxies[i] == null) continue;
                string refName = proxies[i].referenceName;
                if (string.IsNullOrEmpty(refName)) continue;

                int translated = TranslateArrayListInPlace(proxies[i]._arrayList, teletextTranslations, sharedTranslations);
                if (translated > 0)
                {
                    CoreConsole.Print($"[FsmArrayTranslator] Translated live '{refName}' with {translated} items");
                    totalTranslated += translated;
                }
            }
            return totalTranslated;
        }

        // Lookup chain: per-entry teletext dict first, global TranslationDictionary second.
        // The teletext dict uses NormalizeMultiLineKey-shaped keys (per-line trim + drop empty
        // lines); the global dict uses FormatUpperKey-shaped keys via TryGetExact.
        internal static int TranslateArrayListInPlace(
            ArrayList list,
            Dictionary<string, string> teletextDict,
            TranslationDictionary fallback)
        {
            if (list == null) return 0;
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                string original = list[i].ToString();
                if (string.IsNullOrEmpty(original)) continue;

                string translation = null;
                string teletextKey = TranslationFileParser.NormalizeMultiLineKey(
                    TranslationFileParser.UnescapeString(original));
                if (!string.IsNullOrEmpty(teletextKey)
                    && teletextDict != null
                    && teletextDict.TryGetValue(teletextKey, out translation)
                    && translation == original)
                {
                    translation = null;
                }
                if (translation == null && fallback != null)
                {
                    fallback.TryGetExact(original, out translation);
                    if (translation == original)
                        translation = null;
                }

                if (!string.IsNullOrEmpty(translation))
                {
                    list[i] = translation;
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Custom FsmStateAction spliced into PlayMaker FSMs right after each SplitTextToArrayList
        /// action. When the state runs, SplitText fills the proxy's ArrayList with the original
        /// Finnish entries; this action then immediately translates them in place before any
        /// subsequent action or reader sees the array.
        /// </summary>
        public class TranslateArrayListAction : HutongGames.PlayMaker.FsmStateAction
        {
            public PlayMakerArrayListProxy target;
            public Dictionary<string, string> teletextTranslations;
            public TranslationDictionary sharedTranslations;
            public string label;

            public override void OnEnter()
            {
                try
                {
                    if (target != null)
                    {
                        int n = TranslateArrayListInPlace(target._arrayList, teletextTranslations, sharedTranslations);
                        if (n > 0)
                            CoreConsole.Print($"[FsmArrayTranslator] '{label}' post-split: translated {n} entries");
                    }
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Error($"[FsmArrayTranslator] OnEnter error ({label}): {ex.Message}");
                }
                Finish();
            }
        }
    }
}
