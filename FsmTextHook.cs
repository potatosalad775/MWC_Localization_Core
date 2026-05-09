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
        private const string TvSchedulePathPrefix = "Systems/TV/TVGraphics/GFXTanaan";
        private const string TvSchedulePrefixOriginal = "ohjelmat ";
        private const string RallyPenaltyPath = "Sheets/RallyResults/PlayerPenalties";
        private const string ServicePaymentPathPrefix = "Sheets/ServicePayment";
        private const string ServicePaymentLinePathPrefix = "Sheets/ServicePayment/Line";
        private const string ServicePaymentBreakdownReference = "Breakdown";
        private const int ServicePaymentLineCount = 12;
        private static readonly string[] TvScheduleOriginalDays =
        {
            "sunnuntai",
            "maanantai",
            "tiistai",
            "keskiviikko",
            "torstai",
            "perjantai",
            "lauantai"
        };
        private static readonly string[] MechanicServiceOriginalKeys =
        {
            "Vanteiden kiilotus / Rim polish",
            "Rengastyöt / tire job",
            "Custom automaalaus / Custom paint",
            "Metalliväri / Metallic color",
            "Alkuperäisväri / original color",
            "Tehtaan erikoismaalaus / factory special paint",
            "Vanteet metalliväri / Rim metallic",
            "Vanteet maalattuna / Rim paint",
            "Moottorin säätö / Engine adjust",
            "Aurauskulmien säätö / Toe adjust",
            "Jarruhuolto / brake service",
            "Moottorin viritys / engine tune up",
            "Ripustusten suoristus / susp. repair",
            "Ovien turvaverkot / door safety nets",
            "Turvakehikon asennus / rollcage install",
            "Tuulilasin vaihto / windshield replacement",
            "Perävälityksen vaihto / ratio change",
            "Turvakehikon poisto / rollcage removal",
            "Peltityöt / sheet metal work",
            "Vinyylikaton poisto / vinyl removal",
            "Mittatilausjouset / Coil spring order"
        };
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

        private enum FsmTextTargetKind
        {
            BuildStringPart,
            FsmVariable,
            TextMeshFromVariable,
            TextMeshFromBuildString,
            TranslateAllBuildStringAndDisplayStrings
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

        private sealed class FsmTextTarget
        {
            public string ObjectPath;
            public string FsmName;
            public string StateName;
            public int ActionIndex;
            public FsmTextTargetKind Kind;
            public int StringPartIndex;
            public string VariableName;
            public string OriginalKey;
            public string TextMeshPath;
            public string AppliedLabel;

            public FsmTextTarget(
                string objectPath,
                string fsmName,
                string stateName,
                int actionIndex,
                FsmTextTargetKind kind,
                int stringPartIndex,
                string variableName,
                string originalKey,
                string textMeshPath,
                string appliedLabel)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                StateName = stateName;
                ActionIndex = actionIndex;
                Kind = kind;
                StringPartIndex = stringPartIndex;
                VariableName = variableName;
                OriginalKey = originalKey;
                TextMeshPath = textMeshPath;
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

        private static readonly FsmTextTarget[] GameRallyTargets = new FsmTextTarget[]
        {
            new FsmTextTarget(
                "Sheets/RallyResults/PlayerResults",
                "Data",
                null,
                -1,
                FsmTextTargetKind.FsmVariable,
                -1,
                "CurrentClass",
                null,
                null,
                "GAME Rally class"),
            new FsmTextTarget(
                "Sheets/RallyRegistration/Functions/Class",
                "Data",
                null,
                -1,
                FsmTextTargetKind.FsmVariable,
                -1,
                "CurrentClass",
                null,
                null,
                "GAME Rally class"),
            new FsmTextTarget(
                "Sheets/RallyResults/PlayerResults",
                "Data",
                "State 1",
                5,
                FsmTextTargetKind.TranslateAllBuildStringAndDisplayStrings,
                -1,
                null,
                null,
                null,
                "GAME Rally class"),
            new FsmTextTarget(
                "Sheets/RallyResults/PlayerResults",
                "Data",
                "State 1",
                5,
                FsmTextTargetKind.TextMeshFromBuildString,
                -1,
                null,
                null,
                "Sheets/RallyResults/PlayerResults/Class",
                "GAME Rally class"),
            new FsmTextTarget(
                "Sheets/RallyRegistration/Functions/Class",
                "Data",
                "State 1",
                3,
                FsmTextTargetKind.TranslateAllBuildStringAndDisplayStrings,
                -1,
                null,
                null,
                null,
                "GAME Rally class"),
            new FsmTextTarget(
                "Sheets/RallyRegistration/Functions/Class",
                "Data",
                "State 1",
                3,
                FsmTextTargetKind.TextMeshFromBuildString,
                -1,
                null,
                null,
                "Sheets/RallyRegistration/Functions/Class",
                "GAME Rally class"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                "State 1",
                6,
                FsmTextTargetKind.BuildStringPart,
                0,
                null,
                "Time penalty:",
                null,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                "State 1",
                6,
                FsmTextTargetKind.BuildStringPart,
                2,
                null,
                "sec.",
                null,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                "State 1",
                7,
                FsmTextTargetKind.BuildStringPart,
                0,
                null,
                "Parc Ferme violation:",
                null,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                "State 1",
                8,
                FsmTextTargetKind.BuildStringPart,
                0,
                null,
                "Jump start violation:",
                null,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                null,
                -1,
                FsmTextTargetKind.FsmVariable,
                -1,
                "StringTimePenalty",
                null,
                null,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                null,
                -1,
                FsmTextTargetKind.FsmVariable,
                -1,
                "StringParcferme",
                null,
                null,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                null,
                -1,
                FsmTextTargetKind.FsmVariable,
                -1,
                "StringJumpstart",
                null,
                null,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                null,
                -1,
                FsmTextTargetKind.TextMeshFromVariable,
                -1,
                "StringTimePenalty",
                null,
                RallyPenaltyPath,
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                null,
                -1,
                FsmTextTargetKind.TextMeshFromVariable,
                -1,
                "StringParcferme",
                null,
                "Sheets/RallyResults/PlayerPenalties/Parcferme",
                "GAME Rally penalties"),
            new FsmTextTarget(
                RallyPenaltyPath,
                "Data",
                null,
                -1,
                FsmTextTargetKind.TextMeshFromVariable,
                -1,
                "StringJumpstart",
                null,
                "Sheets/RallyResults/PlayerPenalties/Jumpstart",
                "GAME Rally penalties")
        };

        private static readonly FsmTextTarget[] GameTicketTargets = new FsmTextTarget[]
        {
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "Calc fine 2", 4, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "Calc fine 2", 5, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "100kmh", 4, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "100kmh", 5, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "80kmh", 4, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "80kmh", 5, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "45kmh", 4, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "45kmh", 5, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/TrafficTicket/TicketData", "Fetch data", 11, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/EnviroCrime/TicketData", "Calc fine 5", 8, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/EnviroCrime/TicketData", "Calc fine 5", 9, "GAME Traffic ticket"),
            BuildStringActionTarget("Sheets/EnviroCrime/TicketData", "Fetch data", 11, "GAME Traffic ticket")
        };

        private static FsmTextTarget BuildStringActionTarget(string objectPath, string stateName, int actionIndex, string appliedLabel)
        {
            return new FsmTextTarget(
                objectPath,
                null,
                stateName,
                actionIndex,
                FsmTextTargetKind.TranslateAllBuildStringAndDisplayStrings,
                -1,
                null,
                null,
                null,
                appliedLabel);
        }

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
                bool immediateChanged = ApplyImmediateRallyTranslations();
                immediateChanged |= ApplyImmediateMechanicServiceTranslations();
                if (!force && !ShouldPoll(ref lastGamePollTime, MaintenancePollInterval))
                    return immediateChanged;

                return TryApplyGameTranslations() || immediateChanged;
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
            anyChanged |= TryApplyGameTvGraphicsScheduleTranslations();
            anyChanged |= TryApplyGameRallyTemplateTranslations();
            anyChanged |= TryApplyGameTicketTranslations();
            anyChanged |= TryApplyGameMechanicServiceTranslations();
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

        private bool TryApplyGameTvGraphicsScheduleTranslations()
        {
            bool anyChanged = false;
            string translatedPrefix;
            Dictionary<string, string> translatedDays;

            if (!TryBuildTvScheduleTranslationParts(out translatedPrefix, out translatedDays))
                return false;

            List<PlayMakerFSM> scheduleFsms = GetFsmsByPathPrefix(TvSchedulePathPrefix);
            for (int i = 0; i < scheduleFsms.Count; i++)
            {
                PlayMakerFSM fsm = scheduleFsms[i];
                if (!IsFsmReady(fsm) || fsm.FsmName != "Text")
                    continue;

                anyChanged |= ApplyTvScheduleTitlePrefixTranslation(fsm, translatedPrefix);
            }

            anyChanged |= TranslateTvScheduleDaysArrays(translatedDays);

            if (anyChanged)
            {
                appliedTarget = "GAME TV schedule";
            }

            return anyChanged;
        }

        private bool TryApplyGameRallyTemplateTranslations()
        {
            return ApplyFsmTextTargets(GameRallyTargets);
        }

        private bool TryApplyGameTicketTranslations()
        {
            return ApplyFsmTextTargets(GameTicketTargets);
        }

        private bool TryApplyGameMechanicServiceTranslations()
        {
            bool anyChanged = false;
            bool hasActiveBreakdown;
            HashSet<string> activeLineKeys = new HashSet<string>();
            anyChanged |= TranslateMechanicServiceBreakdownArrays(activeLineKeys, out hasActiveBreakdown);
            anyChanged |= TranslateMechanicServiceLineFsms(hasActiveBreakdown ? activeLineKeys : null);

            if (anyChanged)
            {
                appliedTarget = "GAME mechanic service payment";
            }

            return anyChanged;
        }

        private bool ApplyImmediateMechanicServiceTranslations()
        {
            bool anyChanged = false;
            bool hasActiveBreakdown;
            bool hasCachedBreakdown;
            HashSet<string> activeLineKeys = new HashSet<string>();
            anyChanged |= TranslateActiveMechanicServiceBreakdownArrays(activeLineKeys, out hasActiveBreakdown);
            anyChanged |= TranslateMechanicServiceBreakdownArrays(activeLineKeys, out hasCachedBreakdown);
            hasActiveBreakdown |= hasCachedBreakdown;

            for (int i = 1; i <= ServicePaymentLineCount; i++)
            {
                anyChanged |= TranslateMechanicServiceLineFsmByPath(
                    ServicePaymentLinePathPrefix + i.ToString(),
                    hasActiveBreakdown ? activeLineKeys : null);
            }

            if (anyChanged)
            {
                appliedTarget = "GAME mechanic service payment";
            }

            return anyChanged;
        }

        private bool TranslateActiveMechanicServiceBreakdownArrays(HashSet<string> activeLineKeys, out bool foundBreakdown)
        {
            foundBreakdown = false;
            GameObject servicePayment = MLCUtils.FindGameObjectCached(ServicePaymentPathPrefix);
            if (servicePayment == null)
                return false;

            PlayMakerArrayListProxy[] proxies = servicePayment.GetComponentsInChildren<PlayMakerArrayListProxy>(true);
            if (proxies == null || proxies.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < proxies.Length; i++)
            {
                changed |= TranslateMechanicServiceBreakdownProxy(proxies[i], activeLineKeys, ref foundBreakdown);
            }

            return changed;
        }

        private bool TranslateMechanicServiceBreakdownArrays(HashSet<string> activeLineKeys, out bool foundBreakdown)
        {
            foundBreakdown = false;
            EnsureArrayListProxyPathCache();

            bool changed = false;
            foreach (KeyValuePair<string, PlayMakerArrayListProxy> pair in arrayListProxyPathCache)
            {
                changed |= TranslateMechanicServiceBreakdownProxy(pair.Value, activeLineKeys, ref foundBreakdown);
            }

            return changed;
        }

        private bool TranslateMechanicServiceBreakdownProxy(PlayMakerArrayListProxy proxy, HashSet<string> activeLineKeys, ref bool foundBreakdown)
        {
            if (proxy == null
                || proxy.gameObject == null
                || !string.Equals(proxy.referenceName, ServicePaymentBreakdownReference, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string path = MLCUtils.GetGameObjectPath(proxy.gameObject);
            if (!IsMechanicServiceBreakdownProxy(proxy, path))
                return false;

            foundBreakdown = true;
            return TranslateMechanicServiceArrayList(proxy._arrayList, activeLineKeys);
        }

        private bool IsMechanicServiceBreakdownProxy(PlayMakerArrayListProxy proxy, string path)
        {
            if (proxy == null || proxy._arrayList == null || proxy._arrayList.Count == 0)
                return false;

            bool isServicePaymentPath = !string.IsNullOrEmpty(path)
                && path.StartsWith(ServicePaymentPathPrefix, System.StringComparison.Ordinal);
            for (int i = 0; i < proxy._arrayList.Count; i++)
            {
                if (proxy._arrayList[i] == null)
                    continue;

                if (IsMechanicServiceLine(proxy._arrayList[i].ToString()))
                    return true;
            }

            return isServicePaymentPath;
        }

        private bool TranslateMechanicServiceLineFsms(HashSet<string> activeLineKeys)
        {
            List<PlayMakerFSM> lineFsms = GetFsmsByPathPrefix(ServicePaymentLinePathPrefix);
            if (lineFsms == null || lineFsms.Count == 0)
                return false;

            bool anyChanged = false;
            for (int i = 0; i < lineFsms.Count; i++)
            {
                PlayMakerFSM fsm = lineFsms[i];
                anyChanged |= TranslateMechanicServiceLineFsm(fsm, activeLineKeys);
            }

            return anyChanged;
        }

        private bool TranslateMechanicServiceLineFsmByPath(string objectPath, HashSet<string> activeLineKeys)
        {
            PlayMakerFSM fsm = FindFsmIncludingInactiveByPathAndName(objectPath, "GetLine");
            return TranslateMechanicServiceLineFsm(fsm, activeLineKeys);
        }

        private bool TranslateMechanicServiceLineFsm(PlayMakerFSM fsm, HashSet<string> activeLineKeys)
        {
            if (!IsFsmReady(fsm) || fsm.FsmName != "GetLine")
                return false;

            bool changed = false;
            changed |= TranslateMechanicServiceFsmVariables(fsm, activeLineKeys);
            changed |= TranslateMechanicServiceFsmActions(fsm, activeLineKeys);
            changed |= SyncMechanicServiceLineTextMesh(fsm, activeLineKeys);

            return changed;
        }

        private bool TranslateMechanicServiceFsmVariables(PlayMakerFSM fsm, HashSet<string> activeLineKeys)
        {
            if (fsm == null || fsm.FsmVariables == null || fsm.FsmVariables.StringVariables == null)
                return false;

            bool changed = false;
            HutongGames.PlayMaker.FsmString[] variables = fsm.FsmVariables.StringVariables;
            for (int i = 0; i < variables.Length; i++)
            {
                changed |= TranslateMechanicServiceFsmStringValue(variables[i], activeLineKeys);
            }

            return changed;
        }

        private bool TranslateMechanicServiceFsmActions(PlayMakerFSM fsm, HashSet<string> activeLineKeys)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            bool changed = false;
            for (int stateIndex = 0; stateIndex < fsm.FsmStates.Length; stateIndex++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[stateIndex];
                if (state == null || state.Actions == null)
                    continue;

                for (int actionIndex = 0; actionIndex < state.Actions.Length; actionIndex++)
                {
                    changed |= TranslateMechanicServiceObjectFsmStringFields(state.Actions[actionIndex], 0, activeLineKeys);
                }
            }

            return changed;
        }

        private bool TranslateMechanicServiceArrayList(ArrayList values, HashSet<string> activeLineKeys)
        {
            if (values == null || values.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == null)
                    continue;

                AddMechanicServiceActiveLine(values[i].ToString(), activeLineKeys);

                string translated;
                if (TryTranslateMechanicServiceLine(values[i].ToString(), out translated))
                {
                    values[i] = translated;
                    changed = true;
                }

                AddMechanicServiceActiveLine(values[i].ToString(), activeLineKeys);
            }

            return changed;
        }

        private bool TranslateMechanicServiceStringList(List<string> values)
        {
            if (values == null || values.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                string translated;
                if (TryTranslateMechanicServiceLine(values[i], out translated))
                {
                    values[i] = translated;
                    changed = true;
                }
            }

            return changed;
        }

        private bool TranslateMechanicServiceFsmStringValue(HutongGames.PlayMaker.FsmString target, HashSet<string> activeLineKeys)
        {
            if (target == null || string.IsNullOrEmpty(target.Value))
                return false;

            string original = target.Value;
            string translated;
            if (TryTranslateMechanicServiceLine(original, out translated))
            {
                if (ShouldClearInactiveMechanicServiceLine(translated, activeLineKeys))
                {
                    target.Value = string.Empty;
                    return true;
                }

                target.Value = translated;
                return true;
            }

            if (!ShouldClearInactiveMechanicServiceLine(original, activeLineKeys))
                return false;

            target.Value = string.Empty;
            return true;
        }

        private bool TranslateMechanicServiceObjectFsmStringFields(object value, int depth, HashSet<string> activeLineKeys)
        {
            if (value == null || depth > 2)
                return false;

            HutongGames.PlayMaker.FsmString fsmString = value as HutongGames.PlayMaker.FsmString;
            if (fsmString != null)
                return TranslateMechanicServiceFsmStringValue(fsmString, activeLineKeys);

            bool changed = false;
            HutongGames.PlayMaker.FsmString[] fsmStrings = value as HutongGames.PlayMaker.FsmString[];
            if (fsmStrings != null)
            {
                for (int i = 0; i < fsmStrings.Length; i++)
                {
                    changed |= TranslateMechanicServiceFsmStringValue(fsmStrings[i], activeLineKeys);
                }

                return changed;
            }

            System.Type type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fields == null)
                return false;

            for (int i = 0; i < fields.Length; i++)
            {
                object fieldValue = fields[i].GetValue(value);
                changed |= TranslateMechanicServiceObjectFsmStringFields(fieldValue, depth + 1, activeLineKeys);
            }

            return changed;
        }

        private bool SyncMechanicServiceLineTextMesh(PlayMakerFSM fsm, HashSet<string> activeLineKeys)
        {
            if (fsm == null || fsm.FsmVariables == null || fsm.gameObject == null)
                return false;

            string path = MLCUtils.GetGameObjectPath(fsm.gameObject);
            if (string.IsNullOrEmpty(path))
                return false;

            HutongGames.PlayMaker.FsmString text = fsm.FsmVariables.GetFsmString("Text");
            string fsmText = text == null ? null : text.Value;

            if (activeLineKeys != null
                && !string.IsNullOrEmpty(fsmText)
                && IsMechanicServiceLine(fsmText)
                && IsActiveMechanicServiceLine(fsmText, activeLineKeys))
            {
                return SetTextMeshTextByPath(path, fsmText);
            }

            TextMesh textMesh = FindTextMeshByPath(path);
            if (textMesh == null)
                return false;

            string currentText = textMesh.text;
            if (string.IsNullOrEmpty(currentText))
                return false;

            if (activeLineKeys != null
                && IsMechanicServiceLine(currentText)
                && IsActiveMechanicServiceLine(currentText, activeLineKeys))
            {
                string translated;
                if (TryTranslateMechanicServiceLine(currentText, out translated) && textMesh.text != translated)
                {
                    textMesh.text = translated;
                    return true;
                }
            }

            if (activeLineKeys == null && IsMechanicServiceLine(currentText))
            {
                string translated;
                if (TryTranslateMechanicServiceLine(currentText, out translated) && textMesh.text != translated)
                {
                    textMesh.text = translated;
                    return true;
                }
            }

            if (ShouldClearInactiveMechanicServiceLine(currentText, activeLineKeys))
            {
                textMesh.text = string.Empty;
                return true;
            }

            return false;
        }

        private bool TryTranslateMechanicServiceLine(string original, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(original) || translations == null)
                return false;

            for (int i = 0; i < MechanicServiceOriginalKeys.Length; i++)
            {
                string originalKey = MechanicServiceOriginalKeys[i];
                if (!StartsWithMechanicServiceKey(original, originalKey))
                    continue;

                string translatedPrefix;
                if (!TryGetTranslationValue(originalKey, out translatedPrefix)
                    || string.IsNullOrEmpty(translatedPrefix)
                    || TextMatchesExact(translatedPrefix, originalKey))
                {
                    return false;
                }

                translated = BuildMechanicServiceLineTranslation(original, originalKey, translatedPrefix);
                return translated != original;
            }

            return false;
        }

        private void AddMechanicServiceActiveLine(string line, HashSet<string> activeLineKeys)
        {
            if (activeLineKeys == null || string.IsNullOrEmpty(line) || !IsMechanicServiceLine(line))
                return;

            activeLineKeys.Add(MLCUtils.FormatUpperKey(line));
        }

        private bool ShouldClearInactiveMechanicServiceLine(string line, HashSet<string> activeLineKeys)
        {
            return activeLineKeys != null
                && !string.IsNullOrEmpty(line)
                && IsMechanicServiceLine(line)
                && !IsActiveMechanicServiceLine(line, activeLineKeys);
        }

        private bool IsActiveMechanicServiceLine(string line, HashSet<string> activeLineKeys)
        {
            if (activeLineKeys == null || string.IsNullOrEmpty(line))
                return false;

            return activeLineKeys.Contains(MLCUtils.FormatUpperKey(line));
        }

        private bool IsMechanicServiceLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < MechanicServiceOriginalKeys.Length; i++)
            {
                string originalKey = MechanicServiceOriginalKeys[i];
                if (StartsWithMechanicServiceKey(value, originalKey))
                    return true;

                string translatedPrefix;
                if (TryGetTranslationValue(originalKey, out translatedPrefix)
                    && !string.IsNullOrEmpty(translatedPrefix)
                    && StartsWithMechanicServiceKey(value, translatedPrefix))
                {
                    return true;
                }
            }

            return false;
        }

        private bool StartsWithMechanicServiceKey(string value, string originalKey)
        {
            if (string.IsNullOrEmpty(value)
                || string.IsNullOrEmpty(originalKey)
                || !value.StartsWith(originalKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return value.Length == originalKey.Length || char.IsWhiteSpace(value[originalKey.Length]);
        }

        private string BuildMechanicServiceLineTranslation(string original, string originalKey, string translatedPrefix)
        {
            string suffix = original.Length > originalKey.Length ? original.Substring(originalKey.Length) : string.Empty;
            if (string.IsNullOrEmpty(suffix))
                return translatedPrefix;

            int suffixContentIndex = 0;
            while (suffixContentIndex < suffix.Length && char.IsWhiteSpace(suffix[suffixContentIndex]))
            {
                suffixContentIndex++;
            }

            if (suffixContentIndex >= suffix.Length)
                return translatedPrefix + suffix;

            int originalPriceColumn = originalKey.Length + suffixContentIndex;
            int spaces = originalPriceColumn - translatedPrefix.Length;
            if (spaces < 1)
                spaces = suffixContentIndex > 0 ? suffixContentIndex : 1;

            return translatedPrefix + new string(' ', spaces) + suffix.Substring(suffixContentIndex);
        }

        private bool ApplyImmediateRallyTranslations()
        {
            return ApplyFsmTextTargets(GameRallyTargets);
        }

        private bool ApplyFsmTextTargets(FsmTextTarget[] targets)
        {
            if (targets == null || targets.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < targets.Length; i++)
            {
                changed |= ApplyFsmTextTarget(targets[i]);
            }

            return changed;
        }

        private bool ApplyFsmTextTarget(FsmTextTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.ObjectPath))
                return false;

            PlayMakerFSM fsm = string.IsNullOrEmpty(target.FsmName)
                ? FindFsmIncludingInactiveByPathAndState(target.ObjectPath, target.StateName)
                : FindFsmIncludingInactiveByPathAndName(target.ObjectPath, target.FsmName);
            if (!IsFsmReady(fsm))
                return false;

            bool changed = false;
            switch (target.Kind)
            {
                case FsmTextTargetKind.BuildStringPart:
                    changed = ApplyFsmTextBuildStringPartTarget(fsm, target);
                    break;
                case FsmTextTargetKind.FsmVariable:
                    changed = ApplyFsmTextVariableTarget(fsm, target);
                    break;
                case FsmTextTargetKind.TextMeshFromVariable:
                    changed = ApplyFsmTextMeshFromVariableTarget(fsm, target);
                    break;
                case FsmTextTargetKind.TextMeshFromBuildString:
                    changed = ApplyFsmTextMeshFromBuildStringTarget(fsm, target);
                    break;
                case FsmTextTargetKind.TranslateAllBuildStringAndDisplayStrings:
                    changed = ApplyFsmTextActionTranslationTarget(fsm, target);
                    break;
            }

            if (changed && !string.IsNullOrEmpty(target.AppliedLabel))
            {
                appliedTarget = target.AppliedLabel;
            }

            return changed;
        }

        private bool ApplyFsmTextBuildStringPartTarget(PlayMakerFSM fsm, FsmTextTarget target)
        {
            HutongGames.PlayMaker.FsmState state = FindState(fsm, target.StateName);
            if (state == null || state.Actions == null || target.ActionIndex < 0 || target.ActionIndex >= state.Actions.Length)
                return false;

            object action = state.Actions[target.ActionIndex];
            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(action);
            if (parts == null || target.StringPartIndex < 0 || target.StringPartIndex >= parts.Length)
                return false;

            bool changed = TranslateFsmStringWithOriginalKey(parts[target.StringPartIndex], target.OriginalKey);
            if (changed)
            {
                ApplyBuildStringStoreResultFromParts(action, parts);
            }

            return changed;
        }

        private bool ApplyFsmTextVariableTarget(PlayMakerFSM fsm, FsmTextTarget target)
        {
            if (fsm == null || fsm.FsmVariables == null || string.IsNullOrEmpty(target.VariableName))
                return false;

            HutongGames.PlayMaker.FsmString variable = fsm.FsmVariables.GetFsmString(target.VariableName);
            if (variable == null || string.IsNullOrEmpty(variable.Value))
                return false;

            string patternPath = target.ObjectPath + "|" + target.FsmName + "|variable|" + target.VariableName;
            return TranslateRallyClassStringValue(variable) || TranslateFsmStringValueWithPattern(variable, patternPath);
        }

        private bool ApplyFsmTextMeshFromVariableTarget(PlayMakerFSM fsm, FsmTextTarget target)
        {
            if (fsm == null || fsm.FsmVariables == null || string.IsNullOrEmpty(target.VariableName) || string.IsNullOrEmpty(target.TextMeshPath))
                return false;

            HutongGames.PlayMaker.FsmString variable = fsm.FsmVariables.GetFsmString(target.VariableName);
            if (variable == null || string.IsNullOrEmpty(variable.Value))
                return false;

            return SetTextMeshTextByPath(target.TextMeshPath, variable.Value);
        }

        private bool ApplyFsmTextMeshFromBuildStringTarget(PlayMakerFSM fsm, FsmTextTarget target)
        {
            if (string.IsNullOrEmpty(target.TextMeshPath))
                return false;

            HutongGames.PlayMaker.FsmState state = FindState(fsm, target.StateName);
            if (state == null || state.Actions == null || target.ActionIndex < 0 || target.ActionIndex >= state.Actions.Length)
                return false;

            object action = state.Actions[target.ActionIndex];
            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(action);
            if (parts == null || parts.Length == 0)
                return false;

            bool changed = false;
            if (parts.Length >= 3)
            {
                changed |= ApplyBuildStringFastPatternTranslation(fsm, state.Name, target.ActionIndex, parts);
            }

            for (int i = 0; i < parts.Length; i++)
            {
                bool rallyChanged = TranslateRallyClassStringValue(parts[i]);
                changed |= rallyChanged;
                if (!rallyChanged)
                {
                    changed |= TranslateStringPart(parts[i]);
                }
            }

            changed |= TranslateActionFsmStringFieldsWithPattern(
                action,
                BuildFsmActionPatternPath(fsm, state.Name, target.ActionIndex));
            changed |= ApplyBuildStringStoreResultFromParts(action, parts);

            string combined = BuildCombinedText(parts);
            if (string.IsNullOrEmpty(combined))
                return changed;

            return SetTextMeshTextByPath(target.TextMeshPath, combined) || changed;
        }

        private bool ApplyFsmTextActionTranslationTarget(PlayMakerFSM fsm, FsmTextTarget target)
        {
            HutongGames.PlayMaker.FsmState state = FindState(fsm, target.StateName);
            if (state == null || state.Actions == null || target.ActionIndex < 0 || target.ActionIndex >= state.Actions.Length)
                return false;

            object action = state.Actions[target.ActionIndex];
            if (action == null)
                return false;

            bool changed = false;
            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(action);
            if (parts != null && parts.Length > 0)
            {
                if (parts.Length >= 3)
                {
                    changed |= ApplyBuildStringFastPatternTranslation(fsm, state.Name, target.ActionIndex, parts);
                }

                for (int i = 0; i < parts.Length; i++)
                {
                    bool rallyChanged = TranslateRallyClassStringValue(parts[i]);
                    changed |= rallyChanged;
                    if (!rallyChanged)
                    {
                        changed |= TranslateStringPart(parts[i]);
                    }
                }

                changed |= TranslateActionFsmStringFieldsWithPattern(
                    action,
                    BuildFsmActionPatternPath(fsm, state.Name, target.ActionIndex));
                changed |= ApplyBuildStringStoreResultFromParts(action, parts);
                return changed;
            }

            HutongGames.PlayMaker.Actions.SetStringValue setStringAction = action as HutongGames.PlayMaker.Actions.SetStringValue;
            if (setStringAction != null)
            {
                return TranslateSetStringValue(setStringAction);
            }

            changed |= TranslateSetPropertyStringParameter(action);
            changed |= TranslateActionFsmStringFieldsWithPattern(
                action,
                BuildFsmActionPatternPath(fsm, state.Name, target.ActionIndex));
            return changed;
        }

        private bool TranslateFsmStringWithOriginalKey(HutongGames.PlayMaker.FsmString target, string originalKey)
        {
            if (target == null || string.IsNullOrEmpty(target.Value) || string.IsNullOrEmpty(originalKey))
                return false;

            if (!TextMatchesExact(target.Value, originalKey))
                return false;

            string translated;
            if (!TryGetTranslationValue(originalKey, out translated) || string.IsNullOrEmpty(translated) || TextMatchesExact(translated, originalKey))
                return false;

            target.Value = ApplyOuterWhitespaceFromOriginal(target.Value, translated);
            return true;
        }

        private bool SetTextMeshTextByPath(string objectPath, string text)
        {
            if (string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(text))
                return false;

            TextMesh textMesh = FindTextMeshByPath(objectPath);
            if (textMesh == null || textMesh.text == text)
                return false;

            textMesh.text = text;
            return true;
        }

        private TextMesh FindTextMeshByPath(string objectPath)
        {
            TextMesh cachedTextMesh;
            if (textMeshPathCache.TryGetValue(objectPath, out cachedTextMesh)
                && cachedTextMesh != null
                && cachedTextMesh.gameObject != null)
            {
                return cachedTextMesh;
            }

            GameObject activeObject = MLCUtils.FindGameObjectCached(objectPath);
            if (activeObject != null)
            {
                TextMesh activeTextMesh = activeObject.GetComponent<TextMesh>();
                if (activeTextMesh != null)
                {
                    textMeshPathCache[objectPath] = activeTextMesh;
                    return activeTextMesh;
                }
            }

            EnsureTextMeshPathCache();

            if (textMeshPathCache.TryGetValue(objectPath, out cachedTextMesh)
                && cachedTextMesh != null
                && cachedTextMesh.gameObject != null)
            {
                return cachedTextMesh;
            }

            return null;
        }

        private bool TryBuildTvScheduleTranslationParts(out string translatedPrefix, out Dictionary<string, string> translatedDays)
        {
            translatedPrefix = null;
            translatedDays = new Dictionary<string, string>();

            if (translations == null)
                return false;

            Dictionary<string, string> translatedTitlesByDay = new Dictionary<string, string>();
            for (int i = 0; i < TvScheduleOriginalDays.Length; i++)
            {
                string originalDay = TvScheduleOriginalDays[i];
                string translatedTitle;
                if (TryGetTranslationValue(TvSchedulePrefixOriginal + originalDay, out translatedTitle)
                    && !TextMatchesExact(translatedTitle, TvSchedulePrefixOriginal + originalDay))
                {
                    translatedTitlesByDay[MLCUtils.FormatUpperKey(originalDay)] = translatedTitle;
                }
            }

            if (!TryGetTranslationValue(TvSchedulePrefixOriginal, out translatedPrefix)
                || TextMatchesExact(translatedPrefix, TvSchedulePrefixOriginal))
            {
                translatedPrefix = FindCommonTvScheduleTitlePrefix(translatedTitlesByDay);
            }

            if (string.IsNullOrEmpty(translatedPrefix))
                return false;

            for (int i = 0; i < TvScheduleOriginalDays.Length; i++)
            {
                string originalDay = TvScheduleOriginalDays[i];
                string normalizedDay = MLCUtils.FormatUpperKey(originalDay);
                string translatedDay;

                if (TryGetTranslationValue(originalDay, out translatedDay) && !TextMatchesExact(translatedDay, originalDay))
                {
                    translatedDays[normalizedDay] = translatedDay;
                    continue;
                }

                string translatedTitle;
                if (translatedTitlesByDay.TryGetValue(normalizedDay, out translatedTitle)
                    && translatedTitle.StartsWith(translatedPrefix, System.StringComparison.OrdinalIgnoreCase)
                    && translatedTitle.Length > translatedPrefix.Length)
                {
                    translatedDay = translatedTitle.Substring(translatedPrefix.Length).Trim();
                    if (!string.IsNullOrEmpty(translatedDay))
                    {
                        translatedDays[normalizedDay] = translatedDay;
                    }
                }
            }

            return translatedDays.Count > 0;
        }

        private string FindCommonTvScheduleTitlePrefix(Dictionary<string, string> translatedTitlesByDay)
        {
            if (translatedTitlesByDay == null || translatedTitlesByDay.Count < 2)
                return null;

            string commonPrefix = null;
            foreach (KeyValuePair<string, string> pair in translatedTitlesByDay)
            {
                if (string.IsNullOrEmpty(pair.Value))
                    continue;

                if (commonPrefix == null)
                {
                    commonPrefix = pair.Value;
                    continue;
                }

                commonPrefix = GetCommonPrefix(commonPrefix, pair.Value);
                if (string.IsNullOrEmpty(commonPrefix))
                    return null;
            }

            if (string.IsNullOrEmpty(commonPrefix))
                return null;

            foreach (KeyValuePair<string, string> pair in translatedTitlesByDay)
            {
                if (string.IsNullOrEmpty(pair.Value) || pair.Value.Length <= commonPrefix.Length)
                    return null;
            }

            return commonPrefix;
        }

        private string GetCommonPrefix(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
                return null;

            int length = System.Math.Min(first.Length, second.Length);
            int index = 0;
            while (index < length && first[index] == second[index])
            {
                index++;
            }

            return index > 0 ? first.Substring(0, index) : null;
        }

        private bool ApplyTvScheduleTitlePrefixTranslation(PlayMakerFSM fsm, string translatedPrefix)
        {
            HutongGames.PlayMaker.FsmState state = FindState(fsm, "State 11");
            if (state == null || state.Actions == null || state.Actions.Length == 0)
                return false;

            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(state.Actions[0]);
            if (parts == null || parts.Length == 0 || parts[0] == null)
                return false;

            string current = parts[0].Value;
            if (current == translatedPrefix)
                return false;

            if (!TextMatchesExact(current, TvSchedulePrefixOriginal))
                return false;

            parts[0].Value = translatedPrefix;
            return true;
        }

        private bool TranslateTvScheduleDaysArrays(Dictionary<string, string> translatedDays)
        {
            if (translatedDays == null || translatedDays.Count == 0)
                return false;

            bool changed = false;

            PlayMakerArrayListProxy[] allProxies = Resources.FindObjectsOfTypeAll<PlayMakerArrayListProxy>();
            if (allProxies == null)
                return false;

            for (int i = 0; i < allProxies.Length; i++)
            {
                PlayMakerArrayListProxy proxy = allProxies[i];
                if (proxy == null || proxy.gameObject == null || proxy.referenceName != "Days")
                    continue;

                string path = MLCUtils.GetGameObjectPath(proxy.gameObject);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(TvSchedulePathPrefix, System.StringComparison.Ordinal))
                    continue;

                changed |= TranslateTvScheduleDayArrayList(proxy._arrayList, translatedDays);
                changed |= TranslateTvScheduleDayStringList(proxy.preFillStringList, translatedDays);
            }

            return changed;
        }

        private bool TranslateTvScheduleDayArrayList(ArrayList values, Dictionary<string, string> translatedDays)
        {
            if (values == null || values.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == null)
                    continue;

                string translated;
                if (TryGetTvScheduleDayTranslation(values[i].ToString(), translatedDays, out translated))
                {
                    values[i] = translated;
                    changed = true;
                }
            }

            return changed;
        }

        private bool TranslateTvScheduleDayStringList(List<string> values, Dictionary<string, string> translatedDays)
        {
            if (values == null || values.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < values.Count; i++)
            {
                string translated;
                if (TryGetTvScheduleDayTranslation(values[i], translatedDays, out translated))
                {
                    values[i] = translated;
                    changed = true;
                }
            }

            return changed;
        }

        private bool TryGetTvScheduleDayTranslation(string original, Dictionary<string, string> translatedDays, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(original) || translatedDays == null)
                return false;

            string normalized = MLCUtils.FormatUpperKey(original);
            if (!translatedDays.TryGetValue(normalized, out translated))
                return false;

            return !string.IsNullOrEmpty(translated) && original != translated;
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

        private bool TranslateFsmStringVariableWithPattern(PlayMakerFSM fsm, string variableName, string patternPath)
        {
            if (fsm == null || fsm.FsmVariables == null || string.IsNullOrEmpty(variableName))
                return false;

            HutongGames.PlayMaker.FsmString target = fsm.FsmVariables.GetFsmString(variableName);
            return TranslateFsmStringValueWithPattern(target, patternPath);
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

        private bool TranslateFsmStringValueWithPattern(HutongGames.PlayMaker.FsmString target, string patternPath)
        {
            if (target == null || string.IsNullOrEmpty(target.Value))
                return false;

            string original = target.Value;
            if (patternMatcher != null)
            {
                string translatedPattern = patternMatcher.TryTranslateWithPattern(original, patternPath ?? string.Empty);
                if (!string.IsNullOrEmpty(translatedPattern) && translatedPattern != original)
                {
                    target.Value = translatedPattern;
                    return true;
                }
            }

            return TranslateFsmStringValue(target);
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

        private bool ApplyBuildStringStoreResultFromParts(object action, HutongGames.PlayMaker.FsmString[] parts)
        {
            if (action == null || parts == null || parts.Length == 0)
                return false;

            FieldInfo storeResultField = GetCachedField(action.GetType(), "storeResult");
            if (storeResultField == null)
                return false;

            HutongGames.PlayMaker.FsmString storeResult = storeResultField.GetValue(action) as HutongGames.PlayMaker.FsmString;
            if (storeResult == null)
                return false;

            string combined = BuildCombinedText(parts);
            if (string.IsNullOrEmpty(combined) || storeResult.Value == combined)
                return false;

            storeResult.Value = combined;
            return true;
        }

        private bool TranslateActionFsmStringFieldsWithPattern(object action, string patternPath)
        {
            return TranslateObjectFsmStringFieldsWithPattern(action, 0, patternPath);
        }

        private bool TranslateObjectFsmStringFieldsWithPattern(object value, int depth, string patternPath)
        {
            if (value == null || depth > 2)
                return false;

            bool changed = false;

            HutongGames.PlayMaker.FsmString fsmString = value as HutongGames.PlayMaker.FsmString;
            if (fsmString != null)
                return TranslateRallyClassStringValue(fsmString) | TranslateFsmStringValueWithPattern(fsmString, patternPath);

            HutongGames.PlayMaker.FsmString[] fsmStrings = value as HutongGames.PlayMaker.FsmString[];
            if (fsmStrings != null)
            {
                for (int i = 0; i < fsmStrings.Length; i++)
                {
                    bool rallyChanged = TranslateRallyClassStringValue(fsmStrings[i]);
                    changed |= rallyChanged;
                    if (!rallyChanged)
                    {
                        changed |= TranslateFsmStringValueWithPattern(fsmStrings[i], patternPath);
                    }
                }

                return changed;
            }

            System.Type type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fields == null)
                return false;

            for (int i = 0; i < fields.Length; i++)
            {
                object fieldValue = fields[i].GetValue(value);
                changed |= TranslateObjectFsmStringFieldsWithPattern(fieldValue, depth + 1, patternPath);
            }

            return changed;
        }

        private string BuildFsmActionPatternPath(PlayMakerFSM fsm, string stateName, int actionIndex)
        {
            if (fsm == null || fsm.gameObject == null)
                return string.Empty;

            return MLCUtils.GetGameObjectPath(fsm.gameObject)
                + "|"
                + fsm.FsmName
                + "|"
                + (stateName ?? string.Empty)
                + "|"
                + actionIndex.ToString();
        }

        private bool TranslateRallyClassStringValue(HutongGames.PlayMaker.FsmString target)
        {
            if (target == null || string.IsNullOrEmpty(target.Value))
                return false;

            string translated;
            if (!TryTranslateRallyClassText(target.Value, out translated))
                return false;

            if (target.Value == translated)
                return false;

            target.Value = translated;
            return true;
        }

        private bool TryTranslateRallyClassText(string original, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(original))
                return false;

            return TryTranslateRallyClassSegment(original, out translated)
                || TryTranslateRallyClassPrefix(original, "Junior", out translated)
                || TryTranslateRallyClassPrefix(original, "Amateur", out translated);
        }

        private bool TryTranslateRallyClassSegment(string original, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(original) || !TextMatchesExact(original.Trim(), "- Class"))
                return false;

            string translatedCore;
            if (!TryGetTranslationValue("- Class", out translatedCore) || TextMatchesExact(translatedCore, "- Class"))
                return false;

            translated = GetLeadingWhitespace(original) + translatedCore.Trim() + GetTrailingWhitespace(original);
            return translated != original;
        }

        private bool TryTranslateRallyClassPrefix(string original, string className, out string translated)
        {
            translated = null;
            string originalPrefix = className + " - Class";
            string translatedClassName;
            string translatedClassSegment;
            if (string.IsNullOrEmpty(original)
                || !original.StartsWith(originalPrefix, System.StringComparison.OrdinalIgnoreCase)
                || !TryGetTranslationValue(className, out translatedClassName)
                || TextMatchesExact(translatedClassName, className)
                || !TryTranslateRallyClassSegment(" - Class", out translatedClassSegment))
            {
                return false;
            }

            string translatedPrefix = translatedClassName + translatedClassSegment;
            string remainder = original.Substring(originalPrefix.Length);
            if (translatedPrefix.Length > 0
                && remainder.Length > 0
                && char.IsWhiteSpace(translatedPrefix[translatedPrefix.Length - 1])
                && char.IsWhiteSpace(remainder[0]))
            {
                remainder = remainder.Substring(1);
            }

            translated = translatedPrefix + remainder;
            return translated != original;
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
                part.Value = ApplyOuterWhitespaceFromOriginal(original, translated);
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

        private string ApplyOuterWhitespaceFromOriginal(string original, string translated)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translated))
                return translated;

            string leadingWhitespace = GetLeadingWhitespace(original);
            string trailingWhitespace = GetTrailingWhitespace(original);
            if (string.IsNullOrEmpty(leadingWhitespace) && string.IsNullOrEmpty(trailingWhitespace))
                return translated;

            bool translatedHasLeadingWhitespace = char.IsWhiteSpace(translated[0]);
            bool translatedHasTrailingWhitespace = char.IsWhiteSpace(translated[translated.Length - 1]);

            if (!string.IsNullOrEmpty(leadingWhitespace) && !translatedHasLeadingWhitespace)
            {
                translated = leadingWhitespace + translated;
            }

            if (!string.IsNullOrEmpty(trailingWhitespace) && !translatedHasTrailingWhitespace)
            {
                translated = translated + trailingWhitespace;
            }

            return translated;
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
            string value;
            if (TryGetTranslationValue(key, out value))
                return value;

            return fallback;
        }

        private bool TryGetTranslationValue(string key, out string value)
        {
            value = null;
            if (translations == null || string.IsNullOrEmpty(key))
                return false;

            string normalizedKey = MLCUtils.FormatUpperKey(key);
            return translations.TryGetValue(normalizedKey, out value);
        }

    }
}
