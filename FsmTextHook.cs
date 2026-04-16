using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Applies FSM translations that are owned by PlayMaker variables/actions.
    /// This is needed for labels where TextMesh text is regenerated directly from FSM values.
    /// </summary>
    public class FsmTextHook : MonoBehaviour
    {
        private Dictionary<string, string> translations;
        private GameObject hostObject;
        private PatternMatcher patternMatcher;
        private string appliedTarget;
        private HashSet<string> loggedReadyTargets = new HashSet<string>();
        private HashSet<string> completedStrategyTargets = new HashSet<string>();
        private HashSet<int> completedEnnusteFsmInstances = new HashSet<int>();
        private List<PlayMakerFSM> cachedEnnusteDataFsms = new List<PlayMakerFSM>();
        private float lastEnnusteDataFsmScanTime = -10f;

        private static readonly WaitForSeconds BootstrapPollDelay = new WaitForSeconds(0.5f);
        private static readonly WaitForSeconds MaintenancePollDelay = new WaitForSeconds(3.0f);
        private const float EnnusteDataRescanInterval = 10f;

        // Reflection cache: (Type, fieldName) -> FieldInfo to avoid repeated GetField calls
        private static readonly Dictionary<System.Type, Dictionary<string, FieldInfo>> reflectionCache
            = new Dictionary<System.Type, Dictionary<string, FieldInfo>>();

        private static FieldInfo GetCachedField(System.Type type, string fieldName)
        {
            if (!reflectionCache.TryGetValue(type, out Dictionary<string, FieldInfo> fields))
            {
                fields = new Dictionary<string, FieldInfo>();
                reflectionCache[type] = fields;
            }
            if (!fields.TryGetValue(fieldName, out FieldInfo fi))
            {
                fi = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                fields[fieldName] = fi;
            }
            return fi;
        }

        private enum FsmStrategyType
        {
            PosUse,
            PosTyper,
            TeletextBuildStringPattern,
            TeletextWeatherUpdaterTokens,
            UnemployPaperButtonVariables,
            ConlineChatStatus
        }

        private sealed class FsmStrategyTarget
        {
            public string ObjectPath;
            public string FsmName;
            public FsmStrategyType Strategy;
            public string AppliedLabel;
            public string ReadyLogKey;
            public string ReadyLogMessage;
            public string StateName;
            public int ActionIndex;

            public FsmStrategyTarget(
                string objectPath,
                string fsmName,
                FsmStrategyType strategy,
                string appliedLabel,
                string readyLogKey,
                string readyLogMessage,
                string stateName,
                int actionIndex)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                Strategy = strategy;
                AppliedLabel = appliedLabel;
                ReadyLogKey = readyLogKey;
                ReadyLogMessage = readyLogMessage;
                StateName = stateName;
                ActionIndex = actionIndex;
            }
        }

        private static readonly string[] PosUseStateNames = new string[] { "State 1", "State 3", "State 4", "State 5" };
        private static readonly string[] PosTyperCommandStateNames = new string[] { "Player input", "Player input 2", "Type", "Type 2", "Drive mem", "Disk mem", "Change baud" };

        private static readonly FsmStrategyTarget[] GamePosTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "COMPUTER/SYSTEM/POS/BootSequence",
                "Use",
                FsmStrategyType.PosUse,
                "GAME POS",
                "POS_USE_READY",
                "[FsmTextHook] Use FSM is initialized and ready.",
                null,
                -1),
            new FsmStrategyTarget(
                "COMPUTER/SYSTEM/POS/Command",
                "Typer",
                FsmStrategyType.PosTyper,
                "GAME POS",
                "POS_TYPER_READY",
                "[FsmTextHook] Typer FSM is initialized and ready.",
                null,
                -1)
        };

        private static readonly FsmStrategyTarget[] GameTeletextTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                2),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                2),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                2),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                3)
        };

        private static readonly string TeletextEnnusteUpdaterPrefix = "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste/";

        private static readonly FsmStrategyTarget[] GameTeletextWeatherTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Nyt",
                "Logic",
                FsmStrategyType.TeletextWeatherUpdaterTokens,
                "GAME Teletext Weather",
                "TTX_WX_READY",
                "[FsmTextHook] Teletext weather updater FSM targets are ready.",
                null,
                -1),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste",
                "Logic",
                FsmStrategyType.TeletextWeatherUpdaterTokens,
                "GAME Teletext Weather",
                "TTX_WX_READY",
                "[FsmTextHook] Teletext weather updater FSM targets are ready.",
                null,
                -1)
        };

        private static readonly FsmStrategyTarget[] GameUnemployPaperTargets = BuildUnemployPaperTargets();
        private static FsmStrategyTarget[] BuildUnemployPaperTargets()
        {
            List<FsmStrategyTarget> targets = new List<FsmStrategyTarget>();
            string[] groups = new string[] { "2A", "2B", "2C", "2D" };

            for (int g = 0; g < groups.Length; g++)
            {
                string group = groups[g];
                for (int i = 1; i <= 7; i++)
                {
                    string path = "Sheets/UnemployPaper/" + group + "/" + i.ToString();
                    targets.Add(new FsmStrategyTarget(
                        path,
                        "Button",
                        FsmStrategyType.UnemployPaperButtonVariables,
                        "GAME UnemployPaper",
                        "UNEMPLOY_READY",
                        "[FsmTextHook] UnemployPaper Button FSM targets are ready.",
                        null,
                        -1));
                }
            }

            return targets.ToArray();
        }

        private static readonly FsmStrategyTarget[] GameConlineTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT",
                "Type",
                FsmStrategyType.ConlineChatStatus,
                "GAME CONLINE",
                "CONLINE_CHAT_READY",
                "[FsmTextHook] CONLINE chat FSM targets are ready.",
                null,
                -1)
        };


        public void Initialize(Dictionary<string, string> translations, GameObject hostObject, string[] patternFiles)
        {
            this.translations = translations;
            this.hostObject = hostObject;
            this.patternMatcher = new PatternMatcher(translations);

            if (patternFiles != null)
            {
                for (int i = 0; i < patternFiles.Length; i++)
                {
                    string filePath = patternFiles[i];
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        patternMatcher.LoadPatternsFromFile(filePath);
                    }
                }
            }

            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            while (hostObject != null)
            {
                string currentScene = Application.loadedLevelName;

                if (currentScene == "MainMenu")
                {
                    if (TryApplyMainMenuTranslations())
                    {
                        appliedTarget = "MainMenu";
                        string targetLabel = string.IsNullOrEmpty(appliedTarget) ? "Unknown" : appliedTarget;
                        CoreConsole.Print("[FsmTextHook] FSM text translations applied (" + targetLabel + ")");
                        Cleanup();
                        yield break;
                    }

                    yield return BootstrapPollDelay;
                    continue;
                }

                if (currentScene == "GAME")
                {
                    TryApplyGameTranslations();
                    yield return MaintenancePollDelay;
                    continue;
                }

                yield return BootstrapPollDelay;
            }
        }

        private void TryApplyGameTranslations()
        {
            if (translations == null)
                return;

            bool anyChanged = false;
            anyChanged |= TryApplyGamePosFsmTranslations();
            anyChanged |= TryApplyGameTeletextBottomlineFsmTranslations();
            anyChanged |= TryApplyGameTeletextWeatherUpdaterFsmTranslations();
            anyChanged |= TryApplyGameUnemployPaperFsmTranslations();
            anyChanged |= TryApplyGameConlineChatFsmTranslations();

            if (anyChanged)
            {
                string targetLabel = string.IsNullOrEmpty(appliedTarget) ? "GAME" : appliedTarget;
                CoreConsole.Print("[FsmTextHook] FSM text translations applied (" + targetLabel + ")");
            }
        }

        private bool TryApplyMainMenuTranslations()
        {
            GameObject folkObj = GameObject.Find("Radio/Folk");
            GameObject cdObj = GameObject.Find("Radio/CD");

            if (folkObj == null || cdObj == null)
                return false;

            PlayMakerFSM folkFsm = FindFsmWithState(folkObj, "Off");
            PlayMakerFSM cdFsm = FindFsmWithState(cdObj, "State 1");

            if (folkFsm == null || cdFsm == null)
                return false;

            if (folkFsm.Fsm == null || cdFsm.Fsm == null)
                return false;

            if (!folkFsm.Fsm.Initialized || !cdFsm.Fsm.Initialized)
                return false;

            string notImported = GetTranslation("NOT IMPORTED", "NOT IMPORTED");
            string radioImported = GetTranslation("RADIO IMPORTED", "RADIO IMPORTED");
            string cdsImported = GetTranslation("CD'S IMPORTED", "CD'S IMPORTED");

            SetFsmStringVariable(folkFsm, "Path", notImported);
            SetFsmStringVariable(cdFsm, "Path", notImported);

            ApplyStateSetStringValue(folkFsm, "Off", radioImported);
            ApplyStateSetStringValue(cdFsm, "State 1", cdsImported);

            return true;
        }

        private bool TryApplyGamePosFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;
            ApplyStrategyTargets(GamePosTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME POS";
            }

            return anyChanged || hasAnyTarget;
        }

        private bool TryApplyGameTeletextBottomlineFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;
            ApplyStrategyTargets(GameTeletextTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME Teletext Bottomline";
            }

            return anyChanged || hasAnyTarget;
        }

        private bool TryApplyGameTeletextWeatherUpdaterFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;

            ApplyStrategyTargets(GameTeletextWeatherTargets, ref anyChanged, ref hasAnyTarget);

            List<PlayMakerFSM> ennusteDataFsms = GetEnnusteDataFsms();
            for (int i = 0; i < ennusteDataFsms.Count; i++)
            {
                PlayMakerFSM fsm = ennusteDataFsms[i];
                if (!IsFsmReady(fsm))
                    continue;

                int instanceID = fsm.GetInstanceID();
                if (completedEnnusteFsmInstances.Contains(instanceID))
                    continue;

                LogReadyOnce("TTX_WX_READY", "[FsmTextHook] Teletext weather updater FSM targets are ready.");
                ApplyWeatherUpdaterTokenTranslations(fsm, ref anyChanged, ref hasAnyTarget);
                completedEnnusteFsmInstances.Add(instanceID);
            }

            if (anyChanged)
            {
                appliedTarget = "GAME Teletext Weather";
            }

            return anyChanged || hasAnyTarget;
        }

        private bool TryApplyGameUnemployPaperFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;

            ApplyStrategyTargets(GameUnemployPaperTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME UnemployPaper";
            }

            return anyChanged || hasAnyTarget;
        }


        private bool TryApplyGameConlineChatFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;

            ApplyStrategyTargets(GameConlineTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME CONLINE";
            }

            return anyChanged || hasAnyTarget;
        }

        private void ApplyStrategyTargets(FsmStrategyTarget[] targets, ref bool anyChanged, ref bool hasAnyTarget)
        {
            if (targets == null || targets.Length == 0)
                return;

            for (int i = 0; i < targets.Length; i++)
            {
                FsmStrategyTarget target = targets[i];
                if (target == null)
                    continue;

                string targetKey = BuildTargetKey(target);
                if (completedStrategyTargets.Contains(targetKey))
                    continue;

                PlayMakerFSM fsm = MLCUtils.FindFsmIncludingInactiveByPathAndName(target.ObjectPath, target.FsmName);
                if (!IsFsmReady(fsm))
                    continue;

                LogReadyOnce(target.ReadyLogKey, target.ReadyLogMessage);
                ApplyStrategyForTarget(target, fsm, ref anyChanged, ref hasAnyTarget);
                completedStrategyTargets.Add(targetKey);
            }
        }

        private static string BuildTargetKey(FsmStrategyTarget target)
        {
            return target.ObjectPath + "|" + target.FsmName + "|" + target.Strategy.ToString() + "|" + target.StateName + "|" + target.ActionIndex.ToString();
        }

        private void ApplyStrategyForTarget(FsmStrategyTarget target, PlayMakerFSM fsm, ref bool anyChanged, ref bool hasAnyTarget)
        {
            if (target == null || fsm == null)
                return;

            switch (target.Strategy)
            {
                case FsmStrategyType.PosUse:
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 1", 0, false);
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 3", 0, false);
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 4", 0, false);
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 5", 0, false);
                    anyChanged |= ApplyAllStateSetStringValueTranslation(fsm);
                    hasAnyTarget |= HasAnyState(fsm, PosUseStateNames);
                    break;

                case FsmStrategyType.PosTyper:
                    // Player input / BuildStringFast action[1] = [old, path, command]
                    // Skip command slot (index 2) to avoid fighting live user input.
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "Remove mem", 3, false, 1); // Formatting message
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "Remove mem 2", 3, false, 1); // Formatting message
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "Copyying", 4, false, 1); // Copying message
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "Spezzer", 1, false, 1); // Virus Message
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 3", 1, false, 1); // Virus Message
                    anyChanged |= ApplyStringAddNewLineActionStringPartTranslation(fsm, "Dir list A", 3, 1);
                    anyChanged |= ApplyStringAddNewLineActionStringPartTranslation(fsm, "Dir list C", 3, 1);
                    // Do not translate setter actions in states that can carry live command text.
                    anyChanged |= ApplyAllStateSetStringValueTranslation(fsm, PosTyperCommandStateNames);
                    anyChanged |= ApplyAllStateSetFsmStringTranslation(fsm, PosTyperCommandStateNames);
                    hasAnyTarget |= HasAnyState(fsm, PosTyperCommandStateNames);
                    break;

                case FsmStrategyType.TeletextBuildStringPattern:
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, target.StateName, target.ActionIndex, true);
                    hasAnyTarget |= HasState(fsm, target.StateName);
                    break;

                case FsmStrategyType.TeletextWeatherUpdaterTokens:
                    ApplyWeatherUpdaterTokenTranslations(fsm, ref anyChanged, ref hasAnyTarget);
                    break;

                case FsmStrategyType.ConlineChatStatus:
                    anyChanged |= ApplyConlineChatStatusTranslations(fsm);
                    hasAnyTarget = true;
                    break;

                case FsmStrategyType.UnemployPaperButtonVariables:
                    anyChanged |= ApplyUnemployPaperButtonVariableTranslations(fsm);
                    hasAnyTarget = true;
                    break;
            }

            if (anyChanged && !string.IsNullOrEmpty(target.AppliedLabel))
            {
                appliedTarget = target.AppliedLabel;
            }
        }

        private void LogReadyOnce(string key, string message)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
                return;

            if (loggedReadyTargets.Contains(key))
                return;

            loggedReadyTargets.Add(key);
            CoreConsole.Print(message);
        }

        private bool IsFsmReady(PlayMakerFSM fsm)
        {
            return fsm != null && fsm.Fsm != null && fsm.Fsm.Initialized && fsm.FsmStates != null;
        }

        private void ApplyWeatherUpdaterTokenTranslations(PlayMakerFSM fsm, ref bool anyChanged, ref bool hasAnyTarget)
        {
            if (fsm == null)
                return;

            anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "State 4", 0, 1);
            anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "State 6", 0, 1);

            hasAnyTarget |= HasState(fsm, "State 4") || HasState(fsm, "State 6");
        }

        private bool ApplyUnemployPaperButtonVariableTranslations(PlayMakerFSM fsm)
        {
            if (fsm == null)
                return false;

            bool changed = false;

            changed |= TranslateFsmStringVariableExact(fsm, "jobNo");
            changed |= TranslateFsmStringVariableExact(fsm, "JobNo");
            changed |= TranslateFsmStringVariableExact(fsm, "jobYes");
            changed |= TranslateFsmStringVariableExact(fsm, "JobYes");

            return changed;
        }

        private bool TranslateFsmStringVariableExact(PlayMakerFSM fsm, string variableName)
        {
            if (fsm == null || fsm.FsmVariables == null || string.IsNullOrEmpty(variableName))
                return false;

            HutongGames.PlayMaker.FsmString target = fsm.FsmVariables.GetFsmString(variableName);
            if (target == null || string.IsNullOrEmpty(target.Value))
                return false;

            string original = target.Value;
            string translated = GetTranslation(original, original);
            if (translated == original)
                return false;

            target.Value = translated;
            return true;
        }

        private List<PlayMakerFSM> GetEnnusteDataFsms()
        {
            bool shouldRescan = cachedEnnusteDataFsms.Count == 0 || (Time.realtimeSinceStartup - lastEnnusteDataFsmScanTime) >= EnnusteDataRescanInterval;
            if (!shouldRescan)
                return cachedEnnusteDataFsms;

            lastEnnusteDataFsmScanTime = Time.realtimeSinceStartup;
            cachedEnnusteDataFsms.Clear();

            PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if (allFsms == null)
                return cachedEnnusteDataFsms;

            for (int i = 0; i < allFsms.Length; i++)
            {
                PlayMakerFSM fsm = allFsms[i];
                if (fsm == null || fsm.gameObject == null)
                    continue;

                if (fsm.FsmName != "Data")
                    continue;

                string path = MLCUtils.GetGameObjectPath(fsm.gameObject);
                if (path.StartsWith(TeletextEnnusteUpdaterPrefix))
                {
                    cachedEnnusteDataFsms.Add(fsm);
                }
            }

            return cachedEnnusteDataFsms;
        }

        private bool HasState(PlayMakerFSM fsm, string stateName)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                    return true;
            }

            return false;
        }

        private bool HasAnyState(PlayMakerFSM fsm, string[] stateNames)
        {
            if (fsm == null || stateNames == null || stateNames.Length == 0)
                return false;

            for (int i = 0; i < stateNames.Length; i++)
            {
                if (HasState(fsm, stateNames[i]))
                    return true;
            }

            return false;
        }

        private PlayMakerFSM FindFsmWithState(GameObject obj, string stateName)
        {
            if (obj == null)
                return null;

            PlayMakerFSM[] fsms = obj.GetComponents<PlayMakerFSM>();
            if (fsms == null)
                return null;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || fsm.FsmStates == null)
                    continue;

                for (int j = 0; j < fsm.FsmStates.Length; j++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[j];
                    if (state != null && state.Name == stateName)
                        return fsm;
                }
            }

            return null;
        }

        private void SetFsmStringVariable(PlayMakerFSM fsm, string variableName, string value)
        {
            if (fsm == null || fsm.FsmVariables == null)
                return;

            HutongGames.PlayMaker.FsmString target = fsm.FsmVariables.GetFsmString(variableName);
            if (target != null)
                target.Value = value;
        }

        private void ApplyStateSetStringValue(PlayMakerFSM fsm, string stateName, string value)
        {
            if (fsm == null || fsm.FsmStates == null)
                return;

            HutongGames.PlayMaker.FsmState targetState = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    targetState = fsm.FsmStates[i];
                    break;
                }
            }

            if (targetState == null || targetState.Actions == null)
                return;

            for (int i = 0; i < targetState.Actions.Length; i++)
            {
                HutongGames.PlayMaker.Actions.SetStringValue action = targetState.Actions[i] as HutongGames.PlayMaker.Actions.SetStringValue;
                if (action != null)
                    action.stringValue = value;
            }
        }

        private bool ApplyStateSetStringValueActionIndexesTranslation(PlayMakerFSM fsm, string stateName, params int[] actionIndexes)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName))
                return false;

            HutongGames.PlayMaker.FsmState targetState = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    targetState = fsm.FsmStates[i];
                    break;
                }
            }

            if (targetState == null || targetState.Actions == null || actionIndexes == null || actionIndexes.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < actionIndexes.Length; i++)
            {
                int actionIndex = actionIndexes[i];
                if (actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                    continue;

                HutongGames.PlayMaker.Actions.SetStringValue action = targetState.Actions[actionIndex] as HutongGames.PlayMaker.Actions.SetStringValue;
                if (action == null || action.stringValue == null || string.IsNullOrEmpty(action.stringValue.Value))
                    continue;

                changed |= TranslateSetStringValue(action);
            }

            return changed;
        }

        private bool ApplyBuildStringActionStringPartsTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, bool allowPatternSplit, params int[] skipPartIndexes)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            HutongGames.PlayMaker.FsmState targetState = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    targetState = fsm.FsmStates[i];
                    break;
                }
            }

            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            object action = targetState.Actions[actionIndex];
            if (action == null)
                return false;

            string actionTypeName = action.GetType().Name;
            bool isBuildStringFast = actionTypeName == "BuildStringFast";
            bool isBuildString = actionTypeName == "BuildString";
            if (!isBuildStringFast && !isBuildString)
                return false;

            FieldInfo stringPartsField = GetCachedField(action.GetType(), "stringParts");
            if (stringPartsField == null)
                return false;

            HutongGames.PlayMaker.FsmString[] parts = stringPartsField.GetValue(action) as HutongGames.PlayMaker.FsmString[];
            if (parts == null || parts.Length == 0)
                return false;

            bool changed = false;
            if (allowPatternSplit && isBuildStringFast)
            {
                changed = ApplyBuildStringFastPatternTranslation(fsm, stateName, actionIndex, parts);
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (ShouldSkipIndex(i, skipPartIndexes))
                    continue;

                changed |= TranslateStringPart(parts[i]);
            }

            return changed;
        }

        private bool ApplyBuildStringFastPatternTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, HutongGames.PlayMaker.FsmString[] parts)
        {
            if (patternMatcher == null || fsm == null || parts == null || parts.Length < 3)
                return false;

            HutongGames.PlayMaker.FsmString part0 = parts[0];
            HutongGames.PlayMaker.FsmString part1 = parts[1];
            HutongGames.PlayMaker.FsmString part2 = parts[2];

            if (part0 == null || part1 == null || part2 == null)
                return false;

            string middleValue = part1.Value ?? string.Empty;
            if (string.IsNullOrEmpty(middleValue))
                return false;

            string combinedText = BuildCombinedText(parts);
            if (string.IsNullOrEmpty(combinedText))
                return false;

            string path = MLCUtils.GetGameObjectPath(fsm.gameObject) + "|" + fsm.FsmName + "|" + stateName + "|" + actionIndex.ToString();
            string translatedCombined = patternMatcher.TryTranslateWithPattern(combinedText, path);
            if (string.IsNullOrEmpty(translatedCombined) || translatedCombined == combinedText)
                return false;

            int middleIndex = translatedCombined.IndexOf(middleValue, System.StringComparison.Ordinal);
            if (middleIndex < 0)
                return false;

            string newPrefix = translatedCombined.Substring(0, middleIndex);
            string newSuffix = translatedCombined.Substring(middleIndex + middleValue.Length);
            bool changed = false;

            if (part0.Value != newPrefix)
            {
                part0.Value = newPrefix;
                changed = true;
            }

            if (part2.Value != newSuffix)
            {
                part2.Value = newSuffix;
                changed = true;
            }

            return changed;
        }

        private bool ApplyStringAddNewLineActionStringPartTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, int stringPartIndex)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            HutongGames.PlayMaker.FsmState targetState = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    targetState = fsm.FsmStates[i];
                    break;
                }
            }

            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            object action = targetState.Actions[actionIndex];
            if (action == null || action.GetType().Name != "StringAddNewLine")
                return false;

            FieldInfo stringPartsField = GetCachedField(action.GetType(), "stringParts");
            if (stringPartsField == null)
                return false;

            HutongGames.PlayMaker.FsmString[] parts = stringPartsField.GetValue(action) as HutongGames.PlayMaker.FsmString[];
            if (parts == null || stringPartIndex < 0 || stringPartIndex >= parts.Length)
                return false;

            return TranslateStringPart(parts[stringPartIndex]);
        }

        private string BuildCombinedText(HutongGames.PlayMaker.FsmString[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && !string.IsNullOrEmpty(parts[i].Value))
                {
                    sb.Append(parts[i].Value);
                }
            }

            return sb.ToString();
        }

        private bool ApplyAllStateSetStringValueTranslation(PlayMakerFSM fsm, params string[] skipStateNames)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            bool changed = false;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state == null || state.Actions == null)
                    continue;

                if (ShouldSkipState(state.Name, skipStateNames))
                    continue;

                for (int j = 0; j < state.Actions.Length; j++)
                {
                    HutongGames.PlayMaker.Actions.SetStringValue action = state.Actions[j] as HutongGames.PlayMaker.Actions.SetStringValue;
                    if (action == null || action.stringValue == null || string.IsNullOrEmpty(action.stringValue.Value))
                        continue;

                    changed |= TranslateSetStringValue(action);
                }
            }

            return changed;
        }

        private bool ApplyAllStateSetFsmStringTranslation(PlayMakerFSM fsm, params string[] skipStateNames)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            bool changed = false;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state == null || state.Actions == null)
                    continue;

                if (ShouldSkipState(state.Name, skipStateNames))
                    continue;

                for (int j = 0; j < state.Actions.Length; j++)
                {
                    object action = state.Actions[j];
                    if (action == null || action.GetType().Name != "SetFsmString")
                        continue;

                    changed |= TranslateActionFsmStringField(action, "setValue");
                }
            }

            return changed;
        }

        private bool TranslateSetStringValue(HutongGames.PlayMaker.Actions.SetStringValue action)
        {
            if (action == null || action.stringValue == null || string.IsNullOrEmpty(action.stringValue.Value))
                return false;

            string original = action.stringValue.Value;
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                action.stringValue.Value = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    action.stringValue.Value = lineTranslated;
                    return true;
                }
            }

            return false;
        }

        private bool TranslateActionFsmStringField(object action, string fieldName)
        {
            if (action == null || string.IsNullOrEmpty(fieldName))
                return false;

            FieldInfo field = GetCachedField(action.GetType(), fieldName);
            if (field == null)
                return false;

            HutongGames.PlayMaker.FsmString fsmString = field.GetValue(action) as HutongGames.PlayMaker.FsmString;
            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value))
                return false;

            string original = fsmString.Value;
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                fsmString.Value = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    fsmString.Value = lineTranslated;
                    return true;
                }
            }

            return false;
        }

        private bool TranslateStringPart(HutongGames.PlayMaker.FsmString part)
        {
            if (part == null || string.IsNullOrEmpty(part.Value))
                return false;

            string original = part.Value;
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                part.Value = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    part.Value = lineTranslated;
                    return true;
                }
            }

            return false;
        }

        private bool ShouldSkipIndex(int index, int[] skipPartIndexes)
        {
            if (skipPartIndexes == null || skipPartIndexes.Length == 0)
                return false;

            for (int i = 0; i < skipPartIndexes.Length; i++)
            {
                if (skipPartIndexes[i] == index)
                    return true;
            }

            return false;
        }

        private bool ShouldSkipState(string stateName, string[] skipStateNames)
        {
            if (string.IsNullOrEmpty(stateName) || skipStateNames == null || skipStateNames.Length == 0)
                return false;

            for (int i = 0; i < skipStateNames.Length; i++)
            {
                if (skipStateNames[i] == stateName)
                    return true;
            }

            return false;
        }

        private string TranslateTextByLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string[] lines = text.Split('\n');
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string originalLine = lines[i];
                if (string.IsNullOrEmpty(originalLine))
                    continue;

                string lineNoCr = originalLine.Replace("\r", string.Empty);
                string translatedLine = GetTranslation(lineNoCr, lineNoCr);
                if (translatedLine != lineNoCr)
                {
                    lines[i] = translatedLine;
                    changed = true;
                }
            }

            if (!changed)
                return text;

            return string.Join("\n", lines);
        }

        private string GetTranslation(string key, string fallback)
        {
            if (translations == null)
                return fallback;

            string normalizedKey = MLCUtils.FormatUpperKey(key);
            string value;
            if (translations.TryGetValue(normalizedKey, out value))
                return value;

            return fallback;
        }


        private bool ApplyConlineChatStatusTranslations(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            bool changed = false;

            changed |= ApplyConlineNestedTranslation(fsm, "Download", 1);
            changed |= ApplyConlineNestedTranslation(fsm, "Fail", 0);

            return changed;
        }

        private bool ApplyConlineNestedTranslation(PlayMakerFSM fsm, string stateName, int actionIndex)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            HutongGames.PlayMaker.FsmState state = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    state = fsm.FsmStates[i];
                    break;
                }
            }

            if (state == null || state.Actions == null || actionIndex < 0 || actionIndex >= state.Actions.Length)
                return false;

            object action = state.Actions[actionIndex];
            if (action == null)
                return false;

            var targetProperty = GetCachedField(action.GetType(), "targetProperty")?.GetValue(action);
            if (targetProperty == null)
                return false;

            var fsmString = GetCachedField(targetProperty.GetType(), "StringParameter")?
                .GetValue(targetProperty) as HutongGames.PlayMaker.FsmString;

            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value))
                return false;

            string original = fsmString.Value;
            string translated = GetTranslation(original, original);

            if (translated != original)
            {
                fsmString.Value = translated;
                return true;
            }

            return false;
        }

        private void Cleanup()
        {
            if (hostObject != null)
                Object.Destroy(hostObject);
        }
    }
}
