using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Applies FSM/action source translations that are owned by
    /// PlayMaker variables/actions.
    /// </summary>
    public class FsmTextHook
    {
        private Dictionary<string, string> translations;
        private Dictionary<string, string> reverseTranslationKeysByValue = new Dictionary<string, string>();
        private PatternMatcher patternMatcher;
        private string appliedTarget;
        private HashSet<string> loggedReadyTargets = new HashSet<string>();
        private List<PlayMakerFSM> cachedEnnusteDataFsms = new List<PlayMakerFSM>();
        private Dictionary<string, List<PlayMakerFSM>> fsmsByPathCache = new Dictionary<string, List<PlayMakerFSM>>();
        private Dictionary<string, PlayMakerFSM> fsmPathStateCache = new Dictionary<string, PlayMakerFSM>();
        private Dictionary<string, PlayMakerFSM> fsmPathNameCache = new Dictionary<string, PlayMakerFSM>();
        private Dictionary<string, PlayMakerFSM> fsmPathComponentCache = new Dictionary<string, PlayMakerFSM>();
        private Dictionary<string, TextMesh> textMeshPathCache = new Dictionary<string, TextMesh>();
        private Dictionary<string, PlayMakerArrayListProxy> arrayListProxyPathCache = new Dictionary<string, PlayMakerArrayListProxy>();
        private float lastEnnusteDataFsmScanTime = -10f;
        private float lastFsmPathCacheBuildTime = -1000f;
        private float lastTextMeshPathCacheBuildTime = -1000f;
        private float lastArrayListProxyPathCacheBuildTime = -1000f;
        private float lastMainMenuPollTime = -10f;
        private float lastGamePollTime = -10f;
        private bool fsmPathCacheBuilt;
        private bool textMeshPathCacheBuilt;
        private bool arrayListProxyPathCacheBuilt;
        private bool mainMenuApplied;
        private bool mainMenuRadioApplied;
        private bool mainMenuCreditsApplied;

        private const float BootstrapPollInterval = 0.5f;
        private const float MaintenancePollInterval = 2.0f;
        private const float EnnusteDataRescanInterval = 2f;
        private const float ObjectPathCacheRefreshInterval = 10f;

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
            TeletextBuildStringSimple,
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

        private sealed class BuildStringTemplateTarget
        {
            public string ObjectPath;
            public string FsmName;
            public string StateName;
            public int ActionIndex;
            public string OriginalPattern;
            public string AppliedLabel;

            public BuildStringTemplateTarget(
                string objectPath,
                string fsmName,
                string stateName,
                int actionIndex,
                string originalPattern,
                string appliedLabel)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                StateName = stateName;
                ActionIndex = actionIndex;
                OriginalPattern = originalPattern;
                AppliedLabel = appliedLabel;
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
                3),
            // Chat-TV clock label (e.g. "KLO" prefix) uses a BuildString action; only part[0]
            // should be translated - the other parts hold dynamic time/date values.
            new FsmStrategyTarget(
                "Systems/TV/TVGraphics/CHAT/Day/Time",
                "Clock",
                FsmStrategyType.TeletextBuildStringSimple,
                "GAME Teletext Clock",
                "TTX_CLOCK_READY",
                "[FsmTextHook] Teletext Clock FSM target is ready.",
                "State 3",
                2)
        };

        private static readonly BuildStringTemplateTarget[] GameTeletextSportsTemplateTargets = new BuildStringTemplateTarget[]
        {
            new BuildStringTemplateTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1",
                "Data",
                "State 1",
                2,
                "Sarjatilanne kun pelattu {0} ottelua.",
                "GAME Teletext Sports"),
            new BuildStringTemplateTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1",
                "Data",
                "State 1",
                2,
                "Pääsarjan kierroksen {0} tulokset.",
                "GAME Teletext Sports"),
            new BuildStringTemplateTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1",
                "Data",
                "State 1",
                2,
                "Kierros {0} tulokset",
                "GAME Teletext Sports"),
            new BuildStringTemplateTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1",
                "Data",
                "State 1",
                3,
                "Kierros {0} pelikohteet",
                "GAME Teletext Sports")
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

        // CONLINE chat-status target: FSM nested setProperty on Download/Fail/Upload states that
        // pushes status strings (e.g. "ONLINE", "CALL FAILED") into an FsmString on the
        // chat-TV UI. The nested reflection doesn't always resolve on the first ready check,
        // so this strategy is intentionally retried until it actually makes a change.
        private static readonly FsmStrategyTarget[] GameConlineTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT",
                "Type",
                FsmStrategyType.ConlineChatStatus,
                "GAME Conline Chat",
                "CONLINE_CHAT_READY",
                "[FsmTextHook] Conline Chat FSM target is ready.",
                null,
                -1)
        };

        public void Initialize(Dictionary<string, string> translations, string[] patternFiles)
        {
            this.translations = translations;
            this.patternMatcher = new PatternMatcher(translations);
            BuildReverseTranslationLookup();
            ResetRuntimeState();

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
        }

        public void ResetRuntimeState()
        {
            appliedTarget = null;
            mainMenuApplied = false;
            mainMenuRadioApplied = false;
            mainMenuCreditsApplied = false;
            lastMainMenuPollTime = -10f;
            lastGamePollTime = -10f;
            lastEnnusteDataFsmScanTime = -10f;
            cachedEnnusteDataFsms.Clear();
            ClearObjectPathCaches();
            loggedReadyTargets.Clear();
        }

        private void BuildReverseTranslationLookup()
        {
            reverseTranslationKeysByValue.Clear();
            if (translations == null)
                return;

            foreach (KeyValuePair<string, string> pair in translations)
            {
                if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                    continue;

                string normalizedValue = MLCUtils.FormatUpperKey(pair.Value);
                if (string.IsNullOrEmpty(normalizedValue))
                    continue;

                if (!reverseTranslationKeysByValue.ContainsKey(normalizedValue))
                {
                    reverseTranslationKeysByValue[normalizedValue] = pair.Key;
                }
            }
        }

        private void ClearObjectPathCaches()
        {
            fsmsByPathCache.Clear();
            fsmPathStateCache.Clear();
            fsmPathNameCache.Clear();
            fsmPathComponentCache.Clear();
            textMeshPathCache.Clear();
            arrayListProxyPathCache.Clear();

            fsmPathCacheBuilt = false;
            textMeshPathCacheBuilt = false;
            arrayListProxyPathCacheBuilt = false;
            lastFsmPathCacheBuildTime = -1000f;
            lastTextMeshPathCacheBuildTime = -1000f;
            lastArrayListProxyPathCacheBuildTime = -1000f;
        }

        public bool UpdateForCurrentScene(bool force)
        {
            return UpdateForScene(Application.loadedLevelName, force);
        }

        public bool UpdateForScene(string currentScene, bool force)
        {
            if (translations == null || translations.Count == 0)
                return false;

            if (currentScene == "MainMenu")
            {
                if (mainMenuApplied)
                    return false;

                if (!force && !ShouldPoll(ref lastMainMenuPollTime, BootstrapPollInterval))
                    return false;

                if (!TryApplyMainMenuTranslations())
                    return false;

                appliedTarget = "MainMenu";
                mainMenuApplied = true;
                string targetLabel = string.IsNullOrEmpty(appliedTarget) ? "Unknown" : appliedTarget;
                CoreConsole.Print("[FsmTextHook] FSM text translations applied (" + targetLabel + ")");
                return true;
            }

            if (currentScene == "GAME")
            {
                if (!force && !ShouldPoll(ref lastGamePollTime, MaintenancePollInterval))
                    return false;

                return TryApplyGameTranslations();
            }

            return false;
        }

        private bool ShouldPoll(ref float lastPollTime, float interval)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastPollTime < interval)
                return false;

            lastPollTime = now;
            return true;
        }

        private bool TryApplyGameTranslations()
        {
            if (translations == null)
                return false;

            bool anyChanged = false;
            anyChanged |= TryApplyGamePosFsmTranslations();
            anyChanged |= TryApplyGamePosFsmMappings();
            anyChanged |= TryApplyGameTeletextSportsTemplateTranslations();
            anyChanged |= TryApplyGameTeletextBottomlineFsmTranslations();
            anyChanged |= TryApplyGameTeletextBuildStringPatternTranslations();
            anyChanged |= TryApplyGameTeletextControlTranslations();
            anyChanged |= TryApplyGameTeletextWeatherUpdaterFsmTranslations();
            anyChanged |= TryApplyGameUnemployPaperFsmTranslations();
            anyChanged |= TryApplyGameConlineInitializeTranslations();
            anyChanged |= TryApplyGameConlineChatFsmTranslations();
            anyChanged |= TryApplyGameConlineTextTranslations();
            anyChanged |= TryApplyGameComputerGamesTranslations();

            if (anyChanged)
            {
                string targetLabel = string.IsNullOrEmpty(appliedTarget) ? "GAME" : appliedTarget;
                CoreConsole.Print("[FsmTextHook] FSM text translations applied (" + targetLabel + ")");
            }

            return anyChanged;
        }

        private bool TryApplyMainMenuTranslations()
        {
            if (!mainMenuRadioApplied)
            {
                mainMenuRadioApplied = TryApplyMainMenuRadioTranslations();
            }

            if (!mainMenuCreditsApplied)
            {
                mainMenuCreditsApplied = TryApplyMainMenuCreditsTranslations();
            }

            return mainMenuRadioApplied && mainMenuCreditsApplied;
        }

        private bool TryApplyMainMenuRadioTranslations()
        {
            GameObject folkObj = MLCUtils.FindGameObjectCached("Radio/Folk");
            GameObject cdObj = MLCUtils.FindGameObjectCached("Radio/CD");

            if (folkObj == null || cdObj == null)
                return false;

            PlayMakerFSM folkFsm = FindFsmWithState(folkObj, "Off");
            PlayMakerFSM cdFsm = FindFsmWithState(cdObj, "State 1");

            if (folkFsm == null || cdFsm == null)
                return false;

            if (!IsFsmReady(folkFsm) || !IsFsmReady(cdFsm))
                return false;

            string notImported = GetTranslation("NOT IMPORTED", "NOT IMPORTED");
            string radioImported = GetTranslation("RADIO IMPORTED", "RADIO IMPORTED");
            string cdsImported = GetTranslation("CD'S IMPORTED", "CD'S IMPORTED");

            SetFsmStringVariable(folkFsm, "Path", notImported);
            SetFsmStringVariable(cdFsm, "Path", notImported);

            ApplyStateSetStringValue(folkFsm, "Off", 1, radioImported);
            ApplyStateSetStringValue(cdFsm, "State 1", 0, cdsImported);

            return true;
        }

        private bool TryApplyMainMenuCreditsTranslations()
        {
            PlayMakerFSM creditsFsm = FindFsmIncludingInactiveByPathAndState("Interface/Credits/CreditsTV/creditsCAM/name", "State 2");
            if (!IsFsmReady(creditsFsm))
                return false;

            ApplyStateSetPropertyStringParameterTranslation(creditsFsm, "State 2", 2);
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

            return anyChanged;
        }

        private bool TryApplyGamePosFsmMappings()
        {
            bool anyChanged = false;

            PlayMakerFSM noOsFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/POS/NoOS", "State 1");
            if (IsFsmReady(noOsFsm))
            {
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(noOsFsm, "State 1", 0);
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(noOsFsm, "State 3", 0);
            }

            PlayMakerFSM statusBarFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", "State 1");
            if (IsFsmReady(statusBarFsm))
            {
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(statusBarFsm, "State 1", 0);
            }

            anyChanged |= ApplyTextMeshTranslationByPath("COMPUTER/SYSTEM/TELEBBS/Software/text");

            if (anyChanged)
            {
                appliedTarget = "GAME POS";
            }

            return anyChanged;
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

            return anyChanged;
        }

        private bool TryApplyGameTeletextSportsTemplateTranslations()
        {
            bool anyChanged = false;

            for (int i = 0; i < GameTeletextSportsTemplateTargets.Length; i++)
            {
                BuildStringTemplateTarget target = GameTeletextSportsTemplateTargets[i];
                if (target == null)
                    continue;

                PlayMakerFSM fsm = FindFsmIncludingInactiveByPathAndName(target.ObjectPath, target.FsmName);
                if (!IsFsmReady(fsm))
                    continue;

                if (ApplyBuildStringTemplateTranslation(fsm, target.StateName, target.ActionIndex, target.OriginalPattern))
                {
                    anyChanged = true;
                    appliedTarget = target.AppliedLabel;
                }
            }

            return anyChanged;
        }

        private bool TryApplyGameTeletextBuildStringPatternTranslations()
        {
            bool anyChanged = false;
            List<PlayMakerFSM> teletextFsms = GetFsmsByPathPrefix("Systems/TV/Teletext");

            for (int i = 0; i < teletextFsms.Count; i++)
            {
                PlayMakerFSM fsm = teletextFsms[i];
                if (!IsFsmReady(fsm) || fsm.FsmStates == null)
                    continue;

                for (int stateIndex = 0; stateIndex < fsm.FsmStates.Length; stateIndex++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[stateIndex];
                    if (state == null || state.Actions == null)
                        continue;

                    for (int actionIndex = 0; actionIndex < state.Actions.Length; actionIndex++)
                    {
                        object action = state.Actions[actionIndex];
                        if (action == null)
                            continue;

                        string actionTypeName = action.GetType().Name;
                        if (actionTypeName != "BuildStringFast" && actionTypeName != "BuildString")
                            continue;

                        anyChanged |= ApplyBuildStringActionPatternTranslationOnly(fsm, state.Name, actionIndex);
                    }
                }
            }

            if (anyChanged)
            {
                appliedTarget = "GAME Teletext Patterns";
            }

            return anyChanged;
        }

        private bool TryApplyGameTeletextControlTranslations()
        {
            bool anyChanged = false;

            PlayMakerFSM teletextFsm = FindFsmIncludingInactiveByPathAndState("Systems/TV/Teletext", "Load");
            if (IsFsmReady(teletextFsm))
            {
                anyChanged |= ApplyFirstStateSetPropertyStringParameterTranslation(teletextFsm, 4);
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(teletextFsm, "Open page", 0);
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(teletextFsm, "Load", 0);
            }

            if (anyChanged)
            {
                appliedTarget = "GAME Teletext Controls";
            }

            return anyChanged;
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

                LogReadyOnce("TTX_WX_READY", "[FsmTextHook] Teletext weather updater FSM targets are ready.");
                ApplyWeatherUpdaterTokenTranslations(fsm, ref anyChanged, ref hasAnyTarget);
            }

            if (anyChanged)
            {
                appliedTarget = "GAME Teletext Weather";
            }

            return anyChanged;
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

            return anyChanged;
        }

        private bool TryApplyGameConlineInitializeTranslations()
        {
            bool anyChanged = false;

            PlayMakerFSM initializeFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "Wait");
            if (IsFsmReady(initializeFsm))
            {
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(initializeFsm, "Wait", 0);
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(initializeFsm, "Too short", 0);
            }

            if (anyChanged)
            {
                appliedTarget = "GAME Conline";
            }

            return anyChanged;
        }

        private bool TryApplyGameConlineChatFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;

            ApplyStrategyTargets(GameConlineTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME Conline Chat";
            }

            return anyChanged;
        }

        private bool TryApplyGameConlineTextTranslations()
        {
            bool anyChanged = false;
            anyChanged |= ApplyTextMeshTranslationByPath("COMPUTER/SYSTEM/TELEBBS/CONLINE/GFX/text");

            if (anyChanged)
            {
                appliedTarget = "GAME Conline";
            }

            return anyChanged;
        }

        private bool TryApplyGameComputerGamesTranslations()
        {
            bool anyChanged = false;

            anyChanged |= TryApplyKaappisFishgameTranslations();
            anyChanged |= TryApplyKaappisGrilliTranslations();
            anyChanged |= TryApplyProPilkkiTranslations();
            anyChanged |= TryApplyKaappisWildvestTranslations();
            anyChanged |= TryApplyRamiGameTranslations();

            if (anyChanged)
            {
                appliedTarget = "GAME Computer";
            }

            return anyChanged;
        }

        private bool TryApplyKaappisFishgameTranslations()
        {
            bool anyChanged = false;

            PlayMakerFSM beerCounterFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/Kaappis-Fishgame", "Reset");
            if (IsFsmReady(beerCounterFsm))
            {
                string[] beerStates = new string[]
                {
                    "Reset",
                    "kännikala 6",
                    "Kalja 1",
                    "Kalja 2",
                    "Kalja 3",
                    "Kalja 4",
                    "Kalja 5"
                };

                for (int i = 0; i < beerStates.Length; i++)
                {
                    anyChanged |= ApplyStateSetPropertyStringParameterTranslation(beerCounterFsm, beerStates[i], 1);
                }
            }

            PlayMakerFSM fishFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/Kaappis-Fishgame", "Peruskala");
            if (IsFsmReady(fishFsm))
            {
                string[] fishStates = new string[]
                {
                    "Peruskala",
                    "Karkasi",
                    "Ahven",
                    "Hauki",
                    "Särki",
                    "Lahna",
                    "Kalakukko",
                    "Sakko",
                    "Kännikala",
                    "Erikoiskala",
                    "UKK",
                    "Kultakala",
                    "Rahasäkki",
                    "Tonnikala",
                    "Rosvo"
                };

                for (int i = 0; i < fishStates.Length; i++)
                {
                    anyChanged |= ApplyStateSetPropertyStringParameterTranslation(fishFsm, fishStates[i], 0);
                }
            }

            PlayMakerFSM menuFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/Kaappis-Fishgame", "Play");
            if (IsFsmReady(menuFsm))
            {
                anyChanged |= ApplyFirstStateSetPropertyStringParameterTranslation(menuFsm, 5);
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(menuFsm, "Play", 2);
            }

            return anyChanged;
        }

        private bool TryApplyKaappisGrilliTranslations()
        {
            bool anyChanged = false;

            PlayMakerFSM grilliFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/Kaappis-Grilli", "Game over");
            if (IsFsmReady(grilliFsm))
            {
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(grilliFsm, "Game over", 0);
            }

            return anyChanged;
        }

        private bool TryApplyProPilkkiTranslations()
        {
            bool anyChanged = false;

            PlayMakerFSM catchFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", "State 7");
            if (IsFsmReady(catchFsm))
            {
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(catchFsm, "State 7", 1);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(catchFsm, "State 8", 1);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(catchFsm, "State 11", 1);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(catchFsm, "State 16", 1);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(catchFsm, "State 21", 1);
            }

            PlayMakerFSM biggestFishFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetPelaaja/SuurinKala", "State 1");
            if (IsFsmReady(biggestFishFsm))
            {
                anyChanged |= ApplyBuildStringActionStringPartTranslation(biggestFishFsm, "State 1", 5, 0);
                anyChanged |= ApplyBuildStringActionStringPartTranslation(biggestFishFsm, "State 1", 5, 4);
            }

            PlayMakerFSM settingsFsm = FindFsmIncludingInactiveByPathAndComponentIndex("COMPUTER/SYSTEM/PROCYON-ProPilkki/Alkuvalikko/Asetukset", 1);
            if (IsFsmReady(settingsFsm))
            {
                anyChanged |= TranslateFsmStringVariableExact(settingsFsm, "Name");
                anyChanged |= ApplyStateActionFsmStringFieldTranslation(settingsFsm, "State 2", 0, "storeValue");
            }

            PlayMakerFSM cpuPlayersFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin", "State 7");
            if (!IsFsmReady(cpuPlayersFsm))
                cpuPlayersFsm = FindFsmIncludingInactiveByPathAndName("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin", "Kilpailijat");
            if (IsFsmReady(cpuPlayersFsm))
            {
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(cpuPlayersFsm, "State 7", 0);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(cpuPlayersFsm, "State 8", 0);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(cpuPlayersFsm, "State 11", 0);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(cpuPlayersFsm, "State 16", 0);
                anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(cpuPlayersFsm, "State 21", 0);
            }

            PlayMakerFSM resultsFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", "Grammat");
            if (IsFsmReady(resultsFsm))
            {
                anyChanged |= ApplyBuildStringActionStringPartTranslation(resultsFsm, "Grammat", 3, 1);
                anyChanged |= ApplyBuildStringActionStringPartTranslation(resultsFsm, "Kalan paino", 2, 1);
            }

            anyChanged |= TranslateArrayListProxyByPathAndIndex("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", 2);

            anyChanged |= ApplyTextMeshTranslationByPaths(
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Alkuvalikko/Asetukset/Nimi",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin/Nimi",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit/Ahven",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit/FishName",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit/Kiiski",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit/Lahna",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit/Sarki",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit/Siika",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/Fail/Otsikko",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetKisa/Numerot",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetKisa/Otsikko",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetPelaaja/Otsikko",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetPelaaja/SuurinKala",
                "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetPelaaja/Yhteispaino");

            return anyChanged;
        }

        private bool TryApplyKaappisWildvestTranslations()
        {
            bool anyChanged = false;

            PlayMakerFSM wildvestFsm = FindFsmIncludingInactiveByPathAndState("COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "State 2");
            if (IsFsmReady(wildvestFsm))
            {
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(wildvestFsm, "State 2", 0);
                anyChanged |= ApplyStateSetPropertyStringParameterTranslation(wildvestFsm, "Lose", 0);
            }

            return anyChanged;
        }

        private bool TryApplyRamiGameTranslations()
        {
            bool anyChanged = false;

            anyChanged |= ApplyTextMeshTranslationByPath("COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Antenna");
            anyChanged |= ApplyTextMeshTranslationByPath("COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle");
            anyChanged |= ApplyTextMeshTranslationByPath("COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Oven");

            return anyChanged;
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

                PlayMakerFSM fsm = FindFsmIncludingInactiveByPathAndName(target.ObjectPath, target.FsmName);
                if (!IsFsmReady(fsm))
                    continue;

                LogReadyOnce(target.ReadyLogKey, target.ReadyLogMessage);

                ApplyStrategyForTarget(target, fsm, ref anyChanged, ref hasAnyTarget);
            }
        }

        private void ApplyStrategyForTarget(FsmStrategyTarget target, PlayMakerFSM fsm, ref bool anyChanged, ref bool hasAnyTarget)
        {
            if (target == null || fsm == null)
                return;

            switch (target.Strategy)
            {
                case FsmStrategyType.PosUse:
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "State 1", 0, 0);
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "State 3", 0, 2);
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "State 4", 0, 1);
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "State 5", 0, 1);
                    hasAnyTarget |= HasAnyState(fsm, PosUseStateNames);
                    break;

                case FsmStrategyType.PosTyper:
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Error", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Format disk", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Format drive", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Copy disk", 1);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Data error", 1);
                    anyChanged |= ApplyStateSetFsmStringActionTranslation(fsm, "Write new line 2", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Reset POS 2", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Error 2", 0);
                    anyChanged |= ApplyStateSetFsmStringActionTranslation(fsm, "Calling...", 0);
                    anyChanged |= ApplyStateSetFsmStringActionTranslation(fsm, "Waiting...", 0);
                    anyChanged |= ApplyStateSetFsmStringActionTranslation(fsm, "Calling....", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Wrong number", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Incorrect", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "New baud", 0);
                    anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "Mem error", 1);
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "Copyying", 4, 0);
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "Remove mem", 3, 0);
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "Remove mem 2", 3, 0);
                    anyChanged |= ApplyStringAddNewLineActionStringPartTranslation(fsm, "Dir list A", 3, 1);
                    anyChanged |= ApplyStringAddNewLineActionStringPartTranslation(fsm, "Dir list C", 3, 1);
                    anyChanged |= ApplyExactFsmTranslation(fsm, "Volume in drive A is A");
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "Spezzer", 1, 2);
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, "State 3", 1, 2);
                    hasAnyTarget |= HasAnyState(fsm, PosTyperCommandStateNames);
                    break;

                case FsmStrategyType.TeletextBuildStringPattern:
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, target.StateName, target.ActionIndex, true);
                    hasAnyTarget |= HasState(fsm, target.StateName);
                    break;

                case FsmStrategyType.TeletextBuildStringSimple:
                    // Translate only part[0] (e.g. "KLO " -> localized prefix) and leave the
                    // dynamic time/date parts 1 and 2 untouched.
                    anyChanged |= ApplyBuildStringActionStringPartTranslation(fsm, target.StateName, target.ActionIndex, 0);
                    hasAnyTarget |= HasState(fsm, target.StateName);
                    break;

                case FsmStrategyType.TeletextWeatherUpdaterTokens:
                    ApplyWeatherUpdaterTokenTranslations(fsm, ref anyChanged, ref hasAnyTarget);
                    break;

                case FsmStrategyType.UnemployPaperButtonVariables:
                    anyChanged |= ApplyUnemployPaperButtonVariableTranslations(fsm);
                    hasAnyTarget = true;
                    break;

                case FsmStrategyType.ConlineChatStatus:
                    anyChanged |= ApplyConlineChatStatusTranslations(fsm);
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
            if (fsm == null || fsm.Fsm == null)
                return false;

            if (!fsm.Fsm.Initialized)
            {
                try
                {
                    fsm.Fsm.InitData();
                }
                catch
                {
                    return false;
                }
            }

            return fsm.Fsm.Initialized && fsm.FsmStates != null;
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

        // Translate status strings written into a nested FsmString via setProperty actions
        // inside the CHAT/Type FSM (Download + Fail states).
        private bool ApplyConlineChatStatusTranslations(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            bool changed = false;
            changed |= ApplyConlineNestedTranslation(fsm, "Download", 1);
            changed |= ApplyConlineNestedTranslation(fsm, "Fail", 0);
            changed |= ApplyConlineNestedTranslation(fsm, "Upload", 1);
            return changed;
        }

        private bool ApplyConlineNestedTranslation(PlayMakerFSM fsm, string stateName, int actionIndex)
        {
            return ApplyStateSetPropertyStringParameterTranslation(fsm, stateName, actionIndex);
        }

        private bool TranslateFsmStringVariableExact(PlayMakerFSM fsm, string variableName)
        {
            if (fsm == null || fsm.FsmVariables == null || string.IsNullOrEmpty(variableName))
                return false;

            HutongGames.PlayMaker.FsmString target = fsm.FsmVariables.GetFsmString(variableName);
            if (target == null || string.IsNullOrEmpty(target.Value))
                return false;

            return TranslateFsmStringValue(target);
        }

        private bool TranslateFsmStringValue(HutongGames.PlayMaker.FsmString target)
        {
            if (target == null || string.IsNullOrEmpty(target.Value))
                return false;

            string original = target.Value;
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                target.Value = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    target.Value = lineTranslated;
                    return true;
                }
            }

            return false;
        }

        private bool ApplyFirstStateSetPropertyStringParameterTranslation(PlayMakerFSM fsm, int actionIndex)
        {
            if (fsm == null || fsm.FsmStates == null || fsm.FsmStates.Length == 0)
                return false;

            HutongGames.PlayMaker.FsmState state = fsm.FsmStates[0];
            if (state == null || state.Actions == null || actionIndex < 0 || actionIndex >= state.Actions.Length)
                return false;

            return TranslateSetPropertyStringParameter(state.Actions[actionIndex]);
        }

        private bool ApplyStateSetPropertyStringParameterTranslation(PlayMakerFSM fsm, string stateName, int actionIndex)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName))
                return false;

            HutongGames.PlayMaker.FsmState targetState = FindState(fsm, stateName);
            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            return TranslateSetPropertyStringParameter(targetState.Actions[actionIndex]);
        }

        private bool TranslateSetPropertyStringParameter(object action)
        {
            if (action == null || action.GetType().Name != "SetProperty")
                return false;

            FieldInfo targetPropertyField = GetCachedField(action.GetType(), "targetProperty");
            if (targetPropertyField == null)
                return false;

            object targetProperty = targetPropertyField.GetValue(action);
            if (targetProperty == null)
                return false;

            FieldInfo stringParamField = GetCachedField(targetProperty.GetType(), "StringParameter");
            if (stringParamField == null)
                return false;

            HutongGames.PlayMaker.FsmString fsmString = stringParamField.GetValue(targetProperty) as HutongGames.PlayMaker.FsmString;
            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value))
                return false;

            return TranslateFsmStringValue(fsmString);
        }

        private List<PlayMakerFSM> GetEnnusteDataFsms()
        {
            bool shouldRescan = cachedEnnusteDataFsms.Count == 0 || (Time.realtimeSinceStartup - lastEnnusteDataFsmScanTime) >= EnnusteDataRescanInterval;
            if (!shouldRescan)
                return cachedEnnusteDataFsms;

            lastEnnusteDataFsmScanTime = Time.realtimeSinceStartup;
            cachedEnnusteDataFsms.Clear();

            FindFsmsIncludingInactiveByPathPrefixAndName(TeletextEnnusteUpdaterPrefix, "Data", cachedEnnusteDataFsms);

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

        private HutongGames.PlayMaker.FsmState FindState(PlayMakerFSM fsm, string stateName)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName))
                return null;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state != null && state.Name == stateName)
                    return state;
            }

            return null;
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
                if (!IsFsmReady(fsm))
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

        private PlayMakerFSM FindFsmWithName(GameObject obj, string fsmName)
        {
            if (obj == null || string.IsNullOrEmpty(fsmName))
                return null;

            PlayMakerFSM[] fsms = obj.GetComponents<PlayMakerFSM>();
            if (fsms == null)
                return null;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm != null && fsm.FsmName == fsmName)
                    return fsm;
            }

            return null;
        }

        private PlayMakerFSM FindFsmIncludingInactiveByPathAndState(string objectPath, string stateName)
        {
            if (string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(stateName))
                return null;

            string cacheKey = objectPath + "|" + stateName;
            PlayMakerFSM cachedFsm;
            if (fsmPathStateCache.TryGetValue(cacheKey, out cachedFsm)
                && cachedFsm != null
                && cachedFsm.gameObject != null
                && IsFsmReady(cachedFsm)
                && HasState(cachedFsm, stateName))
            {
                return cachedFsm;
            }

            GameObject activeObject = MLCUtils.FindGameObjectCached(objectPath);
            PlayMakerFSM activeFsm = FindFsmWithState(activeObject, stateName);
            if (activeFsm != null)
            {
                fsmPathStateCache[cacheKey] = activeFsm;
                return activeFsm;
            }

            EnsureFsmPathCache();

            List<PlayMakerFSM> fsms;
            if (!fsmsByPathCache.TryGetValue(objectPath, out fsms) || fsms == null)
                return null;

            for (int i = 0; i < fsms.Count; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || fsm.gameObject == null)
                    continue;

                if (IsFsmReady(fsm) && HasState(fsm, stateName))
                {
                    fsmPathStateCache[cacheKey] = fsm;
                    return fsm;
                }
            }

            return null;
        }

        private PlayMakerFSM FindFsmIncludingInactiveByPathAndName(string objectPath, string fsmName)
        {
            if (string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(fsmName))
                return null;

            string cacheKey = objectPath + "|" + fsmName;
            PlayMakerFSM cachedFsm;
            if (fsmPathNameCache.TryGetValue(cacheKey, out cachedFsm)
                && cachedFsm != null
                && cachedFsm.gameObject != null
                && cachedFsm.FsmName == fsmName)
            {
                return cachedFsm;
            }

            GameObject activeObject = MLCUtils.FindGameObjectCached(objectPath);
            PlayMakerFSM activeFsm = FindFsmWithName(activeObject, fsmName);
            if (activeFsm != null)
            {
                fsmPathNameCache[cacheKey] = activeFsm;
                return activeFsm;
            }

            EnsureFsmPathCache();

            List<PlayMakerFSM> fsms;
            if (!fsmsByPathCache.TryGetValue(objectPath, out fsms) || fsms == null)
                return null;

            for (int i = 0; i < fsms.Count; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || fsm.gameObject == null || fsm.FsmName != fsmName)
                    continue;

                fsmPathNameCache[cacheKey] = fsm;
                return fsm;
            }

            return null;
        }

        private void FindFsmsIncludingInactiveByPathPrefixAndName(string pathPrefix, string fsmName, List<PlayMakerFSM> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(fsmName))
                return;

            EnsureFsmPathCache();

            foreach (KeyValuePair<string, List<PlayMakerFSM>> pair in fsmsByPathCache)
            {
                if (string.IsNullOrEmpty(pair.Key) || !pair.Key.StartsWith(pathPrefix) || pair.Value == null)
                    continue;

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    PlayMakerFSM fsm = pair.Value[i];
                    if (fsm != null && fsm.gameObject != null && fsm.FsmName == fsmName)
                    {
                        results.Add(fsm);
                    }
                }
            }
        }

        private List<PlayMakerFSM> GetFsmsByPathPrefix(string pathPrefix)
        {
            List<PlayMakerFSM> results = new List<PlayMakerFSM>();
            if (string.IsNullOrEmpty(pathPrefix))
                return results;

            EnsureFsmPathCache();

            foreach (KeyValuePair<string, List<PlayMakerFSM>> pair in fsmsByPathCache)
            {
                if (string.IsNullOrEmpty(pair.Key) || !pair.Key.StartsWith(pathPrefix) || pair.Value == null)
                    continue;

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    PlayMakerFSM fsm = pair.Value[i];
                    if (fsm != null && fsm.gameObject != null)
                    {
                        results.Add(fsm);
                    }
                }
            }

            return results;
        }

        private PlayMakerFSM FindFsmIncludingInactiveByPathAndComponentIndex(string objectPath, int componentIndex)
        {
            if (string.IsNullOrEmpty(objectPath) || componentIndex < 0)
                return null;

            string cacheKey = objectPath + "|" + componentIndex.ToString();
            PlayMakerFSM cachedFsm;
            if (fsmPathComponentCache.TryGetValue(cacheKey, out cachedFsm)
                && cachedFsm != null
                && cachedFsm.gameObject != null)
            {
                return cachedFsm;
            }

            GameObject activeObject = MLCUtils.FindGameObjectCached(objectPath);
            PlayMakerFSM indexedFsm = GetComponentAtIndex<PlayMakerFSM>(activeObject, componentIndex);
            if (indexedFsm != null)
            {
                fsmPathComponentCache[cacheKey] = indexedFsm;
                return indexedFsm;
            }

            EnsureFsmPathCache();

            List<PlayMakerFSM> fsms;
            if (!fsmsByPathCache.TryGetValue(objectPath, out fsms) || fsms == null || fsms.Count == 0)
                return null;

            PlayMakerFSM indexedInactiveFsm = GetComponentAtIndex<PlayMakerFSM>(fsms[0].gameObject, componentIndex);
            if (indexedInactiveFsm != null)
                fsmPathComponentCache[cacheKey] = indexedInactiveFsm;

            return indexedInactiveFsm;
        }

        private T GetComponentAtIndex<T>(GameObject obj, int componentIndex) where T : Component
        {
            if (obj == null || componentIndex < 0)
                return null;

            T[] components = obj.GetComponents<T>();
            if (components == null || componentIndex >= components.Length)
                return null;

            return components[componentIndex];
        }

        private void EnsureFsmPathCache()
        {
            float now = Time.realtimeSinceStartup;
            if (fsmPathCacheBuilt && now - lastFsmPathCacheBuildTime < ObjectPathCacheRefreshInterval)
                return;

            fsmsByPathCache.Clear();
            fsmPathStateCache.Clear();
            fsmPathNameCache.Clear();
            fsmPathComponentCache.Clear();

            PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if (allFsms != null)
            {
                for (int i = 0; i < allFsms.Length; i++)
                {
                    PlayMakerFSM fsm = allFsms[i];
                    if (fsm == null || fsm.gameObject == null)
                        continue;

                    string path = MLCUtils.GetGameObjectPath(fsm.gameObject);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    List<PlayMakerFSM> fsms;
                    if (!fsmsByPathCache.TryGetValue(path, out fsms))
                    {
                        fsms = new List<PlayMakerFSM>();
                        fsmsByPathCache[path] = fsms;
                    }

                    fsms.Add(fsm);
                }
            }

            fsmPathCacheBuilt = true;
            lastFsmPathCacheBuildTime = now;
        }

        private bool ApplyTextMeshTranslationByPath(string objectPath)
        {
            if (string.IsNullOrEmpty(objectPath))
                return false;

            TextMesh cachedTextMesh;
            if (textMeshPathCache.TryGetValue(objectPath, out cachedTextMesh)
                && cachedTextMesh != null
                && cachedTextMesh.gameObject != null)
            {
                return TranslateTextMesh(cachedTextMesh);
            }

            GameObject activeObject = MLCUtils.FindGameObjectCached(objectPath);
            if (activeObject != null)
            {
                TextMesh activeTextMesh = activeObject.GetComponent<TextMesh>();
                if (activeTextMesh != null)
                    textMeshPathCache[objectPath] = activeTextMesh;

                return TranslateTextMesh(activeTextMesh);
            }

            EnsureTextMeshPathCache();

            if (textMeshPathCache.TryGetValue(objectPath, out cachedTextMesh)
                && cachedTextMesh != null
                && cachedTextMesh.gameObject != null)
            {
                return TranslateTextMesh(cachedTextMesh);
            }

            return false;
        }

        private bool ApplyTextMeshTranslationByPaths(params string[] objectPaths)
        {
            if (objectPaths == null || objectPaths.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < objectPaths.Length; i++)
            {
                changed |= ApplyTextMeshTranslationByPath(objectPaths[i]);
            }

            return changed;
        }

        private void EnsureTextMeshPathCache()
        {
            float now = Time.realtimeSinceStartup;
            if (textMeshPathCacheBuilt && now - lastTextMeshPathCacheBuildTime < ObjectPathCacheRefreshInterval)
                return;

            textMeshPathCache.Clear();

            TextMesh[] allTextMeshes = MLCUtils.GetAllTextMeshesIncludingInactive();
            if (allTextMeshes != null)
            {
                for (int i = 0; i < allTextMeshes.Length; i++)
                {
                    TextMesh textMesh = allTextMeshes[i];
                    if (textMesh == null || textMesh.gameObject == null)
                        continue;

                    string path = MLCUtils.GetGameObjectPath(textMesh.gameObject);
                    if (string.IsNullOrEmpty(path) || textMeshPathCache.ContainsKey(path))
                        continue;

                    textMeshPathCache[path] = textMesh;
                }
            }

            textMeshPathCacheBuilt = true;
            lastTextMeshPathCacheBuildTime = now;
        }

        private bool TranslateTextMesh(TextMesh textMesh)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            string original = textMesh.text;
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                textMesh.text = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    textMesh.text = lineTranslated;
                    return true;
                }
            }

            return false;
        }

        private bool TranslateArrayListProxyByPathAndIndex(string objectPath, int componentIndex)
        {
            PlayMakerArrayListProxy proxy = FindArrayListProxyIncludingInactiveByPathAndIndex(objectPath, componentIndex);
            if (proxy == null)
                return false;

            bool changed = false;
            changed |= TranslateArrayListValues(proxy._arrayList);
            changed |= TranslateStringListValues(proxy.preFillStringList);
            return changed;
        }

        private PlayMakerArrayListProxy FindArrayListProxyIncludingInactiveByPathAndIndex(string objectPath, int componentIndex)
        {
            if (string.IsNullOrEmpty(objectPath) || componentIndex < 0)
                return null;

            string cacheKey = objectPath + "|" + componentIndex.ToString();
            PlayMakerArrayListProxy cachedProxy;
            if (arrayListProxyPathCache.TryGetValue(cacheKey, out cachedProxy)
                && cachedProxy != null
                && cachedProxy.gameObject != null)
            {
                return cachedProxy;
            }

            GameObject activeObject = MLCUtils.FindGameObjectCached(objectPath);
            PlayMakerArrayListProxy indexedProxy = GetComponentAtIndex<PlayMakerArrayListProxy>(activeObject, componentIndex);
            if (indexedProxy != null)
            {
                arrayListProxyPathCache[cacheKey] = indexedProxy;
                return indexedProxy;
            }

            EnsureArrayListProxyPathCache();

            if (arrayListProxyPathCache.TryGetValue(cacheKey, out cachedProxy)
                && cachedProxy != null
                && cachedProxy.gameObject != null)
            {
                return cachedProxy;
            }

            return null;
        }

        private void EnsureArrayListProxyPathCache()
        {
            float now = Time.realtimeSinceStartup;
            if (arrayListProxyPathCacheBuilt && now - lastArrayListProxyPathCacheBuildTime < ObjectPathCacheRefreshInterval)
                return;

            arrayListProxyPathCache.Clear();

            PlayMakerArrayListProxy[] allProxies = Resources.FindObjectsOfTypeAll<PlayMakerArrayListProxy>();
            if (allProxies != null)
            {
                for (int i = 0; i < allProxies.Length; i++)
                {
                    PlayMakerArrayListProxy proxy = allProxies[i];
                    if (proxy == null || proxy.gameObject == null)
                        continue;

                    string path = MLCUtils.GetGameObjectPath(proxy.gameObject);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    PlayMakerArrayListProxy[] proxies = proxy.gameObject.GetComponents<PlayMakerArrayListProxy>();
                    if (proxies == null)
                        continue;

                    for (int componentIndex = 0; componentIndex < proxies.Length; componentIndex++)
                    {
                        if (proxies[componentIndex] == null)
                            continue;

                        string cacheKey = path + "|" + componentIndex.ToString();
                        if (!arrayListProxyPathCache.ContainsKey(cacheKey))
                        {
                            arrayListProxyPathCache[cacheKey] = proxies[componentIndex];
                        }
                    }
                }
            }

            arrayListProxyPathCacheBuilt = true;
            lastArrayListProxyPathCacheBuildTime = now;
        }

        private bool TranslateArrayListValues(ArrayList arrayList)
        {
            if (arrayList == null || arrayList.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < arrayList.Count; i++)
            {
                if (arrayList[i] == null)
                    continue;

                string original = arrayList[i].ToString();
                string translated;
                if (TryTranslateString(original, out translated))
                {
                    arrayList[i] = translated;
                    changed = true;
                }
            }

            return changed;
        }

        private bool TranslateStringListValues(List<string> values)
        {
            if (values == null || values.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                string original = values[i];
                string translated;
                if (TryTranslateString(original, out translated))
                {
                    values[i] = translated;
                    changed = true;
                }
            }

            return changed;
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

        private void ApplyStateSetStringValue(PlayMakerFSM fsm, string stateName, int actionIndex, string value)
        {
            if (fsm == null || fsm.FsmStates == null)
                return;

            HutongGames.PlayMaker.FsmState targetState = FindState(fsm, stateName);
            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return;

            HutongGames.PlayMaker.Actions.SetStringValue action = targetState.Actions[actionIndex] as HutongGames.PlayMaker.Actions.SetStringValue;
            if (action != null)
                action.stringValue = value;
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

        private bool ApplyStateSetFsmStringActionTranslation(PlayMakerFSM fsm, string stateName, int actionIndex)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName))
                return false;

            HutongGames.PlayMaker.FsmState targetState = FindState(fsm, stateName);
            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            object action = targetState.Actions[actionIndex];
            if (action == null || action.GetType().Name != "SetFsmString")
                return false;

            return TranslateActionFsmStringField(action, "setValue");
        }

        private bool ApplyStateActionFsmStringFieldTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, string fieldName)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName) || string.IsNullOrEmpty(fieldName))
                return false;

            HutongGames.PlayMaker.FsmState targetState = FindState(fsm, stateName);
            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            object action = targetState.Actions[actionIndex];
            return TranslateActionFsmStringField(action, fieldName);
        }

        private bool ApplyBuildStringActionStringPartTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, int stringPartIndex)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            HutongGames.PlayMaker.FsmState targetState = FindState(fsm, stateName);
            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            object action = targetState.Actions[actionIndex];
            if (action == null)
                return false;

            string actionTypeName = action.GetType().Name;
            if (actionTypeName != "BuildStringFast" && actionTypeName != "BuildString")
                return false;

            FieldInfo stringPartsField = GetCachedField(action.GetType(), "stringParts");
            if (stringPartsField == null)
                return false;

            HutongGames.PlayMaker.FsmString[] parts = stringPartsField.GetValue(action) as HutongGames.PlayMaker.FsmString[];
            if (parts == null || stringPartIndex < 0 || stringPartIndex >= parts.Length)
                return false;

            return TranslateStringPart(parts[stringPartIndex]);
        }

        private bool ApplyBuildStringTemplateTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, string originalPattern)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName) || string.IsNullOrEmpty(originalPattern))
                return false;

            string template = GetTranslation(originalPattern, originalPattern);
            if (template == originalPattern || !template.Contains("{0}"))
                return false;

            HutongGames.PlayMaker.FsmState targetState = FindState(fsm, stateName);
            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(targetState.Actions[actionIndex]);
            if (parts == null || parts.Length < 3 || parts[0] == null || parts[1] == null || parts[2] == null)
                return false;

            int placeholderIndex = template.IndexOf("{0}", System.StringComparison.Ordinal);
            if (placeholderIndex < 0)
                return false;

            string prefix = template.Substring(0, placeholderIndex);
            string suffix = template.Substring(placeholderIndex + 3);

            bool changed = false;
            if (parts[0].Value != prefix)
            {
                parts[0].Value = prefix;
                changed = true;
            }

            if (parts[2].Value != suffix)
            {
                parts[2].Value = suffix;
                changed = true;
            }

            return changed;
        }

        private bool ApplyBuildStringActionPatternTranslationOnly(PlayMakerFSM fsm, string stateName, int actionIndex)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName))
                return false;

            HutongGames.PlayMaker.FsmState targetState = FindState(fsm, stateName);
            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            object action = targetState.Actions[actionIndex];
            if (action == null)
                return false;

            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(action);
            if (parts == null || parts.Length < 3)
                return false;

            bool resolved;
            return ApplyBuildStringPatternTranslationCore(fsm, stateName, actionIndex, parts, false, out resolved);
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

            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(action);
            if (parts == null || parts.Length == 0)
                return false;

            bool changed = false;
            bool patternResolved = false;
            if (allowPatternSplit)
            {
                patternResolved = ApplyBuildStringFastPatternTranslation(fsm, stateName, actionIndex, parts);
                changed = patternResolved;
            }

            // If the pattern path rewrote parts[0]/parts[2] around parts[1], DON'T then
            // run per-part translation - it would double-translate the already rewritten
            // prefix/suffix and corrupt the result. Only fall back to per-part when no
            // pattern matched.
            if (!patternResolved)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (ShouldSkipIndex(i, skipPartIndexes))
                        continue;

                    changed |= TranslateStringPart(parts[i]);
                }
            }

            return changed;
        }

        private bool ApplyBuildStringFastPatternTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, HutongGames.PlayMaker.FsmString[] parts)
        {
            bool resolved;
            return ApplyBuildStringPatternTranslationCore(fsm, stateName, actionIndex, parts, true, out resolved);
        }

        private bool ApplyBuildStringPatternTranslationCore(
            PlayMakerFSM fsm,
            string stateName,
            int actionIndex,
            HutongGames.PlayMaker.FsmString[] parts,
            bool returnResolvedWhenUnchanged,
            out bool resolved)
        {
            resolved = false;
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
            {
                string spacedCandidate = InsertMissingSpacesAroundDynamicValues(combinedText);
                if (!string.IsNullOrEmpty(spacedCandidate) && spacedCandidate != combinedText)
                {
                    translatedCombined = patternMatcher.TryTranslateWithPattern(spacedCandidate, path);
                }
            }

            if (string.IsNullOrEmpty(translatedCombined) || translatedCombined == combinedText)
            {
                string restoredCandidate = BuildRestoredPatternCandidate(parts);
                if (!string.IsNullOrEmpty(restoredCandidate) && restoredCandidate != combinedText)
                {
                    translatedCombined = patternMatcher.TryTranslateWithPattern(restoredCandidate, path);
                }
            }

            if (string.IsNullOrEmpty(translatedCombined) || translatedCombined == combinedText)
                return false;

            int middleIndex = translatedCombined.IndexOf(middleValue, System.StringComparison.Ordinal);
            if (middleIndex < 0)
                return false;

            resolved = true;
            string newPrefix = translatedCombined.Substring(0, middleIndex);
            string newSuffix = translatedCombined.Substring(middleIndex + middleValue.Length);

            // If parts[0]/parts[2] already match the translated prefix/suffix, the pattern
            // has already been applied on a previous tick. Return true so the caller still
            // treats this as "pattern-resolved" (skipping per-part translation) without
            // needlessly writing back identical values.
            string currentPart0 = part0.Value ?? string.Empty;
            string currentPart2 = part2.Value ?? string.Empty;
            if (currentPart0 == newPrefix && currentPart2 == newSuffix)
                return returnResolvedWhenUnchanged;

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

        private HutongGames.PlayMaker.FsmString[] GetBuildStringParts(object action)
        {
            if (action == null)
                return null;

            string actionTypeName = action.GetType().Name;
            if (actionTypeName != "BuildStringFast" && actionTypeName != "BuildString")
                return null;

            FieldInfo stringPartsField = GetCachedField(action.GetType(), "stringParts");
            if (stringPartsField == null)
                return null;

            return stringPartsField.GetValue(action) as HutongGames.PlayMaker.FsmString[];
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

        private bool ApplyExactFsmTranslation(PlayMakerFSM fsm, string original)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(original))
                return false;

            string translated = GetTranslation(original, original);
            if (translated == original)
                return false;

            bool changed = false;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state == null || state.Actions == null)
                    continue;

                for (int j = 0; j < state.Actions.Length; j++)
                {
                    changed |= ApplyExactActionStringTranslation(state.Actions[j], original, translated);
                }
            }

            return changed;
        }

        private bool ApplyExactActionStringTranslation(object action, string original, string translated)
        {
            if (action == null)
                return false;

            bool changed = false;
            FieldInfo[] fields = action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fields == null)
                return false;

            for (int i = 0; i < fields.Length; i++)
            {
                object value = fields[i].GetValue(action);

                HutongGames.PlayMaker.FsmString fsmString = value as HutongGames.PlayMaker.FsmString;
                if (fsmString != null)
                {
                    changed |= TranslateExactFsmString(fsmString, original, translated);
                    continue;
                }

                HutongGames.PlayMaker.FsmString[] parts = value as HutongGames.PlayMaker.FsmString[];
                if (parts == null)
                    continue;

                string combinedText = BuildCombinedText(parts);
                if (TextMatchesExact(combinedText, original))
                {
                    changed |= SetStringPartsToStaticTranslation(parts, translated);
                    continue;
                }

                for (int j = 0; j < parts.Length; j++)
                {
                    changed |= TranslateExactFsmString(parts[j], original, translated);
                }
            }

            return changed;
        }

        private bool TranslateExactFsmString(HutongGames.PlayMaker.FsmString fsmString, string original, string translated)
        {
            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value) || !TextMatchesExact(fsmString.Value, original))
                return false;

            if (fsmString.Value == translated)
                return false;

            fsmString.Value = translated;
            return true;
        }

        private bool SetStringPartsToStaticTranslation(HutongGames.PlayMaker.FsmString[] parts, string translated)
        {
            if (parts == null || parts.Length == 0)
                return false;

            int targetIndex = -1;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
                return false;

            bool changed = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;

                string nextValue = i == targetIndex ? translated : string.Empty;
                if (parts[i].Value != nextValue)
                {
                    parts[i].Value = nextValue;
                    changed = true;
                }
            }

            return changed;
        }

        private bool TextMatchesExact(string value, string expected)
        {
            if (value == expected)
                return true;

            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(expected))
                return false;

            return MLCUtils.FormatUpperKey(value) == MLCUtils.FormatUpperKey(expected);
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

        private string BuildRestoredPatternCandidate(HutongGames.PlayMaker.FsmString[] parts)
        {
            if (parts == null || parts.Length == 0 || reverseTranslationKeysByValue == null || reverseTranslationKeysByValue.Count == 0)
                return null;

            bool restoredAnyPart = false;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || string.IsNullOrEmpty(parts[i].Value))
                    continue;

                string value = parts[i].Value;
                string restored = RestoreOriginalKeyFromTranslatedValue(value);
                if (restored != value)
                {
                    restoredAnyPart = true;
                }

                sb.Append(restored);
            }

            if (!restoredAnyPart)
                return null;

            return InsertMissingSpacesAroundDynamicValues(sb.ToString());
        }

        private string RestoreOriginalKeyFromTranslatedValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string leadingWhitespace = GetLeadingWhitespace(value);
            string trailingWhitespace = GetTrailingWhitespace(value);
            string core = value.Trim();
            if (string.IsNullOrEmpty(core))
                return value;

            string normalizedValue = MLCUtils.FormatUpperKey(core);
            string originalKey;
            if (reverseTranslationKeysByValue.TryGetValue(normalizedValue, out originalKey))
            {
                return leadingWhitespace + originalKey + trailingWhitespace;
            }

            return value;
        }

        private string GetLeadingWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int count = 0;
            while (count < value.Length && char.IsWhiteSpace(value[count]))
            {
                count++;
            }

            return count > 0 ? value.Substring(0, count) : string.Empty;
        }

        private string GetTrailingWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int index = value.Length - 1;
            while (index >= 0 && char.IsWhiteSpace(value[index]))
            {
                index--;
            }

            int start = index + 1;
            return start < value.Length ? value.Substring(start) : string.Empty;
        }

        private string InsertMissingSpacesAroundDynamicValues(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2)
                return text;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (i > 0)
                {
                    char previous = text[i - 1];
                    if (NeedsSpaceBetween(previous, current))
                    {
                        sb.Append(' ');
                    }
                }

                sb.Append(current);
            }

            return sb.ToString();
        }

        private bool NeedsSpaceBetween(char previous, char current)
        {
            if (char.IsWhiteSpace(previous) || char.IsWhiteSpace(current))
                return false;

            return (char.IsLetter(previous) && char.IsDigit(current))
                || (char.IsDigit(previous) && char.IsLetter(current));
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

        private bool TryTranslateString(string original, out string translated)
        {
            translated = original;
            if (string.IsNullOrEmpty(original))
                return false;

            string directTranslation = GetTranslation(original, original);
            if (directTranslation != original)
            {
                translated = directTranslation;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    translated = lineTranslated;
                    return true;
                }
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

    }
}
