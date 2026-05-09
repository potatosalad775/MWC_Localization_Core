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
        private List<PlayMakerFSM> cachedAtmTransactionDescriptionFsms = new List<PlayMakerFSM>();
        private List<PlayMakerFSM> cachedTvScheduleTextFsms = new List<PlayMakerFSM>();
        private List<PlayMakerArrayListProxy> cachedTvScheduleDayProxies = new List<PlayMakerArrayListProxy>();
        private List<TextMesh> cachedTvScheduleTitleTextMeshes = new List<TextMesh>();
        private Dictionary<string, PlayMakerFSM> fsmPathStateCache = new Dictionary<string, PlayMakerFSM>();
        private Dictionary<string, PlayMakerFSM> fsmPathNameCache = new Dictionary<string, PlayMakerFSM>();
        private Dictionary<string, PlayMakerFSM> fsmPathComponentCache = new Dictionary<string, PlayMakerFSM>();
        private Dictionary<string, TextMesh> textMeshPathCache = new Dictionary<string, TextMesh>();
        private Dictionary<string, PlayMakerArrayListProxy> arrayListProxyPathCache = new Dictionary<string, PlayMakerArrayListProxy>();
        private float lastMainMenuPollTime = -10f;
        private float lastGamePollTime = -10f;
        private float lastImmediateGamePollTime = -10f;
        private bool mainMenuApplied;
        private bool mainMenuRadioApplied;
        private bool mainMenuCreditsApplied;

        private const float BootstrapPollInterval = 0.5f;
        private const float MaintenancePollInterval = 5.0f;
        private const float VisibleImmediatePollInterval = 0.20f;
        private const string TvSchedulePathPrefix = "Systems/TV/TVGraphics/GFXTanaan";
        private const string TvSchedulePrefixOriginal = "ohjelmat ";
        private const string RallyPenaltyPath = "Sheets/RallyResults/PlayerPenalties";
        private const string AtmMoneyPathPrefix = "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM";
        private const string AtmTransactionDescriptionPath = "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite";
        private const string AtmTransactionDescriptionReference = "Selite";
        private const string AtmTransactionLabel = "GAME ATM transactions";
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

        private enum FsmTextTargetKind
        {
            BuildStringPart,
            StringAddNewLinePart,
            FsmVariable,
            TextMeshFromVariable,
            TextMeshFromBuildString,
            ActionFsmStringField,
            FirstStateSetPropertyStringParameter,
            SetPropertyStringParameter,
            BuildStringTemplate,
            ExactFsmTranslation,
            TranslateAllBuildStringAndDisplayStrings
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
            public int ComponentIndex;

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
                string appliedLabel,
                int componentIndex = -1)
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
                ComponentIndex = componentIndex;
            }
        }

        private const string GamePosLabel = "GAME POS";
        private const string GameTeletextBottomlineLabel = "GAME Teletext Bottomline";
        private const string GameTeletextWeatherLabel = "GAME Teletext Weather";
        private const string GameConlineLabel = "GAME Conline";
        private const string GameComputerLabel = "GAME Computer";
        private const string GameFishgameLabel = "GAME Computer Fishgame";
        private const string GameProPilkkiLabel = "GAME Computer ProPilkki";

        private static readonly FsmTextTarget[] GamePosTargets = new FsmTextTarget[]
        {
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 1", 0, 0, GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 3", 0, 2, GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 4", 0, 1, GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 5", 0, 1, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Error", 0, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Format disk", 0, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Format drive", 0, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Copy disk", 1, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Data error", 1, GamePosLabel),
            FsmStringFieldTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Write new line 2", 0, "setValue", GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Reset POS 2", 0, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Error 2", 0, GamePosLabel),
            FsmStringFieldTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Calling...", 0, "setValue", GamePosLabel),
            FsmStringFieldTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Waiting...", 0, "setValue", GamePosLabel),
            FsmStringFieldTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Calling....", 0, "setValue", GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Wrong number", 0, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Incorrect", 0, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "New baud", 0, GamePosLabel),
            ActionTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Mem error", 1, GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Copyying", 4, 0, GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Remove mem", 3, 0, GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Remove mem 2", 3, 0, GamePosLabel),
            StringAddNewLinePartTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Dir list A", 3, 1, GamePosLabel),
            StringAddNewLinePartTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Dir list C", 3, 1, GamePosLabel),
            ExactFsmTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Volume in drive A is A", GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "Spezzer", 1, 2, GamePosLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/POS/Command", "Typer", "State 3", 1, 2, GamePosLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/POS/NoOS", null, "State 1", 0, GamePosLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/POS/NoOS", null, "State 3", 0, GamePosLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", null, "State 1", 0, GamePosLabel)
        };

        private static readonly FsmTextTarget[] GameTeletextTargets = new FsmTextTarget[]
        {
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1", "Data", "State 1", 2, GameTeletextBottomlineLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1", "Data", "State 1", 2, GameTeletextBottomlineLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1", "Data", "State 1", 2, GameTeletextBottomlineLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1", "Data", "State 1", 3, GameTeletextBottomlineLabel),
            BuildStringPartTarget("Systems/TV/TVGraphics/CHAT/Day/Time", "Clock", "State 3", 2, 0, "GAME Teletext Clock")
        };

        private static readonly FsmTextTarget[] GameTeletextSportsTemplateTargets = new FsmTextTarget[]
        {
            TemplateTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1", "Data", "State 1", 2, "Sarjatilanne kun pelattu {0} ottelua.", "GAME Teletext Sports"),
            TemplateTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1",
                "Data",
                "State 1",
                2,
                "Pääsarjan kierroksen {0} tulokset.",
                "GAME Teletext Sports"),
            TemplateTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1", "Data", "State 1", 2, "Kierros {0} tulokset", "GAME Teletext Sports"),
            TemplateTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1", "Data", "State 1", 3, "Kierros {0} pelikohteet", "GAME Teletext Sports")
        };

        private static readonly FsmTextTarget[] GameRallyClassTargets = new FsmTextTarget[]
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
                "GAME Rally class")
        };

        private static readonly FsmTextTarget[] GameRallyPenaltyTargets = new FsmTextTarget[]
        {
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

        private static readonly FsmTextTarget[] GameAtmTransactionTargets = new FsmTextTarget[]
        {
            new FsmTextTarget(AtmTransactionDescriptionPath, "GetData", null, -1, FsmTextTargetKind.FsmVariable, -1, "Data", null, null, AtmTransactionLabel),
            new FsmTextTarget(AtmTransactionDescriptionPath, "GetData", "State 1", 0, FsmTextTargetKind.ActionFsmStringField, -1, "result", null, null, AtmTransactionLabel),
            new FsmTextTarget(AtmTransactionDescriptionPath, "GetData", "State 1", 1, FsmTextTargetKind.SetPropertyStringParameter, -1, null, null, null, AtmTransactionLabel),
            new FsmTextTarget(AtmTransactionDescriptionPath, "GetData", null, -1, FsmTextTargetKind.TextMeshFromVariable, -1, "Data", null, null, AtmTransactionLabel)
        };

        private static FsmTextTarget BuildStringActionTarget(string objectPath, string stateName, int actionIndex, string appliedLabel)
        {
            return ActionTarget(objectPath, null, stateName, actionIndex, appliedLabel);
        }

        private static FsmTextTarget ActionTarget(string objectPath, string fsmName, string stateName, int actionIndex, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, stateName, actionIndex, FsmTextTargetKind.TranslateAllBuildStringAndDisplayStrings, -1, null, null, null, appliedLabel);
        }

        private static FsmTextTarget BuildStringPartTarget(string objectPath, string fsmName, string stateName, int actionIndex, int stringPartIndex, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, stateName, actionIndex, FsmTextTargetKind.BuildStringPart, stringPartIndex, null, null, null, appliedLabel);
        }

        private static FsmTextTarget StringAddNewLinePartTarget(string objectPath, string fsmName, string stateName, int actionIndex, int stringPartIndex, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, stateName, actionIndex, FsmTextTargetKind.StringAddNewLinePart, stringPartIndex, null, null, null, appliedLabel);
        }

        private static FsmTextTarget FsmStringFieldTarget(string objectPath, string fsmName, string stateName, int actionIndex, string fieldName, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, stateName, actionIndex, FsmTextTargetKind.ActionFsmStringField, -1, fieldName, null, null, appliedLabel);
        }

        private static FsmTextTarget SetPropertyTarget(string objectPath, string fsmName, string stateName, int actionIndex, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, stateName, actionIndex, FsmTextTargetKind.SetPropertyStringParameter, -1, null, null, null, appliedLabel);
        }

        private static FsmTextTarget FirstSetPropertyTarget(string objectPath, string fsmName, int actionIndex, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, null, actionIndex, FsmTextTargetKind.FirstStateSetPropertyStringParameter, -1, null, null, null, appliedLabel);
        }

        private static FsmTextTarget FirstSetPropertyTargetByState(string objectPath, string stateName, int actionIndex, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, null, stateName, actionIndex, FsmTextTargetKind.FirstStateSetPropertyStringParameter, -1, null, null, null, appliedLabel);
        }

        private static FsmTextTarget TemplateTarget(string objectPath, string fsmName, string stateName, int actionIndex, string originalPattern, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, stateName, actionIndex, FsmTextTargetKind.BuildStringTemplate, -1, null, originalPattern, null, appliedLabel);
        }

        private static FsmTextTarget ExactFsmTarget(string objectPath, string fsmName, string original, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, fsmName, null, -1, FsmTextTargetKind.ExactFsmTranslation, -1, null, original, null, appliedLabel);
        }

        private static FsmTextTarget ComponentVariableTarget(string objectPath, int componentIndex, string variableName, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, null, null, -1, FsmTextTargetKind.FsmVariable, -1, variableName, null, null, appliedLabel, componentIndex);
        }

        private static FsmTextTarget ComponentFsmStringFieldTarget(string objectPath, int componentIndex, string stateName, int actionIndex, string fieldName, string appliedLabel)
        {
            return new FsmTextTarget(objectPath, null, stateName, actionIndex, FsmTextTargetKind.ActionFsmStringField, -1, fieldName, null, null, appliedLabel, componentIndex);
        }

        private static readonly string TeletextEnnusteUpdaterPrefix = "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste/";

        private static readonly FsmTextTarget[] GameTeletextWeatherTargets = new FsmTextTarget[]
        {
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Nyt", "Logic", "State 4", 0, GameTeletextWeatherLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Nyt", "Logic", "State 4", 1, GameTeletextWeatherLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Nyt", "Logic", "State 6", 0, GameTeletextWeatherLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Nyt", "Logic", "State 6", 1, GameTeletextWeatherLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste", "Logic", "State 4", 0, GameTeletextWeatherLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste", "Logic", "State 4", 1, GameTeletextWeatherLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste", "Logic", "State 6", 0, GameTeletextWeatherLabel),
            ActionTarget("Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste", "Logic", "State 6", 1, GameTeletextWeatherLabel)
        };

        private static readonly FsmTextTarget[] GameUnemployPaperTargets = BuildUnemployPaperTargets();
        private static FsmTextTarget[] BuildUnemployPaperTargets()
        {
            List<FsmTextTarget> targets = new List<FsmTextTarget>();
            string[] groups = new string[] { "2A", "2B", "2C", "2D" };

            for (int g = 0; g < groups.Length; g++)
            {
                string group = groups[g];
                for (int i = 1; i <= 7; i++)
                {
                    string path = "Sheets/UnemployPaper/" + group + "/" + i.ToString();
                    targets.Add(new FsmTextTarget(path, "Button", null, -1, FsmTextTargetKind.FsmVariable, -1, "jobNo", null, null, "GAME UnemployPaper"));
                    targets.Add(new FsmTextTarget(path, "Button", null, -1, FsmTextTargetKind.FsmVariable, -1, "JobNo", null, null, "GAME UnemployPaper"));
                    targets.Add(new FsmTextTarget(path, "Button", null, -1, FsmTextTargetKind.FsmVariable, -1, "jobYes", null, null, "GAME UnemployPaper"));
                    targets.Add(new FsmTextTarget(path, "Button", null, -1, FsmTextTargetKind.FsmVariable, -1, "JobYes", null, null, "GAME UnemployPaper"));
                }
            }

            return targets.ToArray();
        }

        private static readonly FsmTextTarget[] GameConlineTargets = new FsmTextTarget[]
        {
            SetPropertyTarget("COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", null, "Wait", 0, GameConlineLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", null, "Too short", 0, GameConlineLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Download", 1, "GAME Conline Chat"),
            SetPropertyTarget("COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Fail", 0, "GAME Conline Chat"),
            SetPropertyTarget("COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Upload", 1, "GAME Conline Chat")
        };

        private static readonly FsmTextTarget[] GameFishgameTargets = new FsmTextTarget[]
        {
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Reset", 1, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "kÃ¤nnikala 6", 1, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Kalja 1", 1, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Kalja 2", 1, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Kalja 3", 1, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Kalja 4", 1, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Kalja 5", 1, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Peruskala", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Karkasi", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Ahven", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Hauki", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "SÃ¤rki", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Lahna", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Kalakukko", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Sakko", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "KÃ¤nnikala", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Erikoiskala", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "UKK", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Kultakala", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "RahasÃ¤kki", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Tonnikala", 0, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Rosvo", 0, GameFishgameLabel),
            FirstSetPropertyTargetByState("COMPUTER/SYSTEM/Kaappis-Fishgame", "Play", 5, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Fishgame", null, "Play", 2, GameFishgameLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Grilli", null, "Game over", 0, GameComputerLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", null, "State 2", 0, GameComputerLabel),
            SetPropertyTarget("COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", null, "Lose", 0, GameComputerLabel)
        };

        private static readonly FsmTextTarget[] GameProPilkkiTargets = new FsmTextTarget[]
        {
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", null, "State 7", 1, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", null, "State 8", 1, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", null, "State 11", 1, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", null, "State 16", 1, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", null, "State 21", 1, GameProPilkkiLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetPelaaja/SuurinKala", null, "State 1", 5, 0, GameProPilkkiLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetPelaaja/SuurinKala", null, "State 1", 5, 4, GameProPilkkiLabel),
            ComponentVariableTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Alkuvalikko/Asetukset", 1, "Name", GameProPilkkiLabel),
            ComponentFsmStringFieldTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Alkuvalikko/Asetukset", 1, "State 2", 0, "storeValue", GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin", null, "State 7", 0, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin", null, "State 8", 0, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin", null, "State 11", 0, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin", null, "State 16", 0, GameProPilkkiLabel),
            ActionTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Osoitin", null, "State 21", 0, GameProPilkkiLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", null, "Grammat", 3, 1, GameProPilkkiLabel),
            BuildStringPartTarget("COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", null, "Kalan paino", 2, 1, GameProPilkkiLabel)
        };

        private static readonly string[] GameComputerTextMeshTargets = new string[]
        {
            "COMPUTER/SYSTEM/TELEBBS/Software/text",
            "COMPUTER/SYSTEM/TELEBBS/CONLINE/GFX/text",
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
            "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetPelaaja/Yhteispaino",
            "COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Antenna",
            "COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle",
            "COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Oven"
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
            lastImmediateGamePollTime = -10f;
            cachedEnnusteDataFsms.Clear();
            cachedAtmTransactionDescriptionFsms.Clear();
            cachedTvScheduleTextFsms.Clear();
            cachedTvScheduleDayProxies.Clear();
            cachedTvScheduleTitleTextMeshes.Clear();
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
            fsmPathStateCache.Clear();
            fsmPathNameCache.Clear();
            fsmPathComponentCache.Clear();
            textMeshPathCache.Clear();
            arrayListProxyPathCache.Clear();
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
                bool immediateChanged = ApplyImmediateRallyClassTranslations();
                if (force || ShouldPoll(ref lastImmediateGamePollTime, VisibleImmediatePollInterval))
                {
                    immediateChanged |= ApplyVisibleImmediateTvScheduleTranslations();
                    immediateChanged |= ApplyVisibleImmediateRallyTranslations();
                    immediateChanged |= ApplyVisibleImmediateAtmTransactionTranslations();
                    immediateChanged |= ApplyVisibleImmediateMechanicServiceTranslations();
                }

                if (!force && !ShouldPoll(ref lastGamePollTime, MaintenancePollInterval))
                    return immediateChanged;

                return TryApplyGameTranslations() || immediateChanged;
            }

            return false;
        }

        private bool ApplyVisibleImmediateTvScheduleTranslations()
        {
            return IsAnyPathActiveInHierarchy(TvSchedulePathPrefix)
                ? TryApplyGameTvGraphicsScheduleTranslations()
                : false;
        }

        private bool ApplyVisibleImmediateRallyTranslations()
        {
            return IsAnyPathActiveInHierarchy(RallyPenaltyPath, "Sheets/RallyResults")
                ? ApplyImmediateRallyPenaltyTranslations()
                : false;
        }

        private bool ApplyVisibleImmediateAtmTransactionTranslations()
        {
            return IsAnyPathActiveInHierarchy("PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat")
                ? ApplyImmediateAtmTransactionTranslations()
                : false;
        }

        private bool ApplyVisibleImmediateMechanicServiceTranslations()
        {
            return IsAnyPathActiveInHierarchy(ServicePaymentPathPrefix)
                ? ApplyImmediateMechanicServiceTranslations()
                : false;
        }

        private bool IsAnyPathActiveInHierarchy(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return false;

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject obj = FindGameObjectByPathIncludingInactive(paths[i]);
                if (obj != null && obj.activeInHierarchy)
                    return true;
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
            anyChanged |= TryApplyGameAtmTransactionTranslations();
            anyChanged |= TryApplyGameMechanicServiceTranslations();
            anyChanged |= TryApplyGameTeletextSportsTemplateTranslations();
            anyChanged |= TryApplyGameTeletextBottomlineFsmTranslations();
            anyChanged |= TryApplyGameTeletextControlTranslations();
            anyChanged |= TryApplyGameTeletextWeatherUpdaterFsmTranslations();
            anyChanged |= TryApplyGameUnemployPaperFsmTranslations();
            anyChanged |= TryApplyGameConlineInitializeTranslations();
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

            List<PlayMakerFSM> scheduleFsms = GetTvScheduleTextFsms();
            for (int i = 0; i < scheduleFsms.Count; i++)
            {
                PlayMakerFSM fsm = scheduleFsms[i];
                if (!IsFsmReady(fsm) || fsm.FsmName != "Text")
                    continue;

                anyChanged |= ApplyTvScheduleTitleTranslation(fsm, translatedPrefix, translatedDays);
            }

            anyChanged |= TranslateTvScheduleDaysArrays(translatedDays);
            anyChanged |= TranslateTvScheduleTitleTextMeshes(translatedPrefix, translatedDays);

            if (anyChanged)
            {
                appliedTarget = "GAME TV schedule";
            }

            return anyChanged;
        }

        private bool TryApplyGameRallyTemplateTranslations()
        {
            bool anyChanged = ApplyImmediateRallyClassTranslations();

            if (IsAnyPathActiveInHierarchy(RallyPenaltyPath, "Sheets/RallyResults"))
            {
                anyChanged |= ApplyImmediateRallyPenaltyTranslations();
            }

            return anyChanged;
        }

        private bool TryApplyGameTicketTranslations()
        {
            return IsAnyPathActiveInHierarchy("Sheets/TrafficTicket", "Sheets/EnviroCrime")
                ? ApplyFsmTextTargets(GameTicketTargets)
                : false;
        }

        private bool TryApplyGameAtmTransactionTranslations()
        {
            if (!IsAnyPathActiveInHierarchy("PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat"))
                return false;

            bool anyChanged = false;
            anyChanged |= TranslateArrayListProxiesByReference(AtmMoneyPathPrefix, AtmTransactionDescriptionReference);
            anyChanged |= TranslateArrayListProxiesFromArrayListGetActions(AtmMoneyPathPrefix, AtmTransactionDescriptionReference);
            anyChanged |= ApplyAtmTransactionDescriptionFsmTargets();

            if (anyChanged)
            {
                appliedTarget = AtmTransactionLabel;
            }

            return anyChanged;
        }

        private bool ApplyImmediateAtmTransactionTranslations()
        {
            bool anyChanged = false;
            anyChanged |= TranslateArrayListProxiesByReference(AtmMoneyPathPrefix, AtmTransactionDescriptionReference);
            anyChanged |= TranslateArrayListProxiesFromArrayListGetActions(AtmMoneyPathPrefix, AtmTransactionDescriptionReference);
            anyChanged |= ApplyAtmTransactionDescriptionFsmTargets();

            if (anyChanged)
            {
                appliedTarget = AtmTransactionLabel;
            }

            return anyChanged;
        }

        private bool ApplyAtmTransactionDescriptionFsmTargets()
        {
            List<PlayMakerFSM> fsms = GetAtmTransactionDescriptionFsms();
            if (fsms == null || fsms.Count == 0)
                return ApplyFsmTextTargets(GameAtmTransactionTargets);

            bool changed = false;
            for (int i = 0; i < fsms.Count; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (!IsFsmReady(fsm))
                    continue;

                for (int targetIndex = 0; targetIndex < GameAtmTransactionTargets.Length; targetIndex++)
                {
                    changed |= ApplyFsmTextTargetToFsm(fsm, GameAtmTransactionTargets[targetIndex]);
                }
            }

            if (changed)
            {
                appliedTarget = AtmTransactionLabel;
            }

            return changed;
        }

        private List<PlayMakerFSM> GetAtmTransactionDescriptionFsms()
        {
            if (AreCachedFsmsValid(cachedAtmTransactionDescriptionFsms))
                return cachedAtmTransactionDescriptionFsms;

            cachedAtmTransactionDescriptionFsms.Clear();

            GameObject root = FindGameObjectByPathIncludingInactive(AtmMoneyPathPrefix);
            if (root == null)
                return cachedAtmTransactionDescriptionFsms;

            PlayMakerFSM[] allFsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
            if (allFsms == null)
                return cachedAtmTransactionDescriptionFsms;

            for (int i = 0; i < allFsms.Length; i++)
            {
                PlayMakerFSM fsm = allFsms[i];
                if (fsm == null || fsm.gameObject == null || fsm.FsmName != "GetData")
                    continue;

                string path = MLCUtils.GetGameObjectPath(fsm.gameObject);
                if (path == AtmTransactionDescriptionPath)
                {
                    cachedAtmTransactionDescriptionFsms.Add(fsm);
                }
            }

            return cachedAtmTransactionDescriptionFsms;
        }

        private bool TranslateArrayListProxiesFromArrayListGetActions(string pathPrefix, string referenceName)
        {
            if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(referenceName))
                return false;

            bool changed = false;
            List<PlayMakerFSM> fsms = GetAtmTransactionDescriptionFsms();
            for (int i = 0; i < fsms.Count; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (!IsFsmReady(fsm) || fsm.FsmStates == null)
                    continue;

                for (int stateIndex = 0; stateIndex < fsm.FsmStates.Length; stateIndex++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[stateIndex];
                    if (state == null || state.Actions == null)
                        continue;

                    for (int actionIndex = 0; actionIndex < state.Actions.Length; actionIndex++)
                    {
                        HutongGames.PlayMaker.Actions.ArrayListGet action = state.Actions[actionIndex] as HutongGames.PlayMaker.Actions.ArrayListGet;
                        PlayMakerArrayListProxy proxy = GetArrayListGetProxy(action);
                        if (action == null
                            || action.reference == null
                            || !TextMatchesExact(action.reference.Value, referenceName)
                            || proxy == null)
                        {
                            continue;
                        }

                        changed |= TranslateArrayListValues(proxy._arrayList);
                        changed |= TranslateStringListValues(proxy.preFillStringList);
                    }
                }
            }

            return changed;
        }

        private bool TranslateArrayListProxiesByReference(string pathPrefix, string referenceName)
        {
            if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(referenceName))
                return false;

            GameObject root = FindGameObjectByPathIncludingInactive(pathPrefix);
            if (root == null)
                return false;

            PlayMakerArrayListProxy[] proxies = root.GetComponentsInChildren<PlayMakerArrayListProxy>(true);
            if (proxies == null || proxies.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < proxies.Length; i++)
            {
                changed |= TranslateArrayListProxyIfReferenceMatches(proxies[i], pathPrefix, referenceName, null);
            }

            return changed;
        }

        private bool TranslateArrayListProxyIfReferenceMatches(
            PlayMakerArrayListProxy proxy,
            string pathPrefix,
            string referenceName,
            string cachePath)
        {
            if (proxy == null
                || proxy.gameObject == null
                || !string.Equals(proxy.referenceName, referenceName, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string path = string.IsNullOrEmpty(cachePath) ? MLCUtils.GetGameObjectPath(proxy.gameObject) : cachePath;
            if (string.IsNullOrEmpty(path) || !path.StartsWith(pathPrefix, System.StringComparison.Ordinal))
                return false;

            bool changed = false;
            changed |= TranslateArrayListValues(proxy._arrayList);
            changed |= TranslateStringListValues(proxy.preFillStringList);
            return changed;
        }

        private GameObject FindGameObjectByPathIncludingInactive(string objectPath)
        {
            if (string.IsNullOrEmpty(objectPath))
                return null;

            string normalizedPath = objectPath.TrimEnd('/');
            GameObject activeObject = MLCUtils.FindGameObjectCached(normalizedPath);
            if (activeObject != null)
                return activeObject;

            int slashIndex = normalizedPath.IndexOf('/');
            if (slashIndex < 0)
                return null;

            GameObject root = MLCUtils.FindGameObjectCached(normalizedPath.Substring(0, slashIndex));
            if (root == null)
                return null;

            Transform child = root.transform.Find(normalizedPath.Substring(slashIndex + 1));
            return child != null ? child.gameObject : null;
        }

        private GameObject FindNearestGameObjectForPathPrefix(string pathPrefix)
        {
            if (string.IsNullOrEmpty(pathPrefix))
                return null;

            string candidate = pathPrefix.TrimEnd('/');
            while (!string.IsNullOrEmpty(candidate))
            {
                GameObject obj = FindGameObjectByPathIncludingInactive(candidate);
                if (obj != null)
                    return obj;

                int slashIndex = candidate.LastIndexOf('/');
                if (slashIndex < 0)
                    break;

                candidate = candidate.Substring(0, slashIndex);
            }

            return null;
        }

        private bool IsObjectPathUnderPrefix(GameObject obj, string pathPrefix)
        {
            if (obj == null || string.IsNullOrEmpty(pathPrefix))
                return false;

            string path = MLCUtils.GetGameObjectPath(obj);
            return !string.IsNullOrEmpty(path)
                && path.StartsWith(pathPrefix.TrimEnd('/'), System.StringComparison.Ordinal);
        }

        private bool TryApplyGameMechanicServiceTranslations()
        {
            if (!IsAnyPathActiveInHierarchy(ServicePaymentPathPrefix))
                return false;

            bool anyChanged = false;
            bool hasActiveBreakdown;
            bool hasCachedBreakdown;
            HashSet<string> activeLineKeys = new HashSet<string>();
            anyChanged |= TranslateActiveMechanicServiceBreakdownArrays(activeLineKeys, out hasActiveBreakdown);
            anyChanged |= TranslateMechanicServiceBreakdownArrays(null, out hasCachedBreakdown);
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
            anyChanged |= TranslateMechanicServiceBreakdownArrays(null, out hasCachedBreakdown);

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
            GameObject servicePayment = FindGameObjectByPathIncludingInactive(ServicePaymentPathPrefix);
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
            GameObject sheets = FindGameObjectByPathIncludingInactive("Sheets");
            if (sheets == null)
                return false;

            PlayMakerArrayListProxy[] proxies = sheets.GetComponentsInChildren<PlayMakerArrayListProxy>(true);
            if (proxies == null || proxies.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < proxies.Length; i++)
            {
                changed |= TranslateMechanicServiceBreakdownProxy(proxies[i], activeLineKeys, ref foundBreakdown);
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

            bool syncedFromArrayList;
            if (TrySyncMechanicServiceLineFromArrayListGet(fsm, out syncedFromArrayList))
                return syncedFromArrayList;

            if (activeLineKeys == null)
                return false;

            bool changed = false;
            changed |= TranslateMechanicServiceFsmVariables(fsm, activeLineKeys);
            changed |= TranslateMechanicServiceFsmActions(fsm, activeLineKeys);
            changed |= SyncMechanicServiceLineTextMesh(fsm, activeLineKeys);

            return changed;
        }

        private bool TrySyncMechanicServiceLineFromArrayListGet(PlayMakerFSM fsm, out bool changed)
        {
            changed = false;
            HutongGames.PlayMaker.Actions.ArrayListGet action = GetMechanicServiceArrayListGetAction(fsm);
            if (action == null)
                return false;

            int index = action.atIndex == null ? -1 : action.atIndex.Value;
            PlayMakerArrayListProxy proxy = GetArrayListGetProxy(action);
            if (proxy == null || proxy._arrayList == null)
                return false;

            if (index < 0 || index >= proxy._arrayList.Count || proxy._arrayList[index] == null)
            {
                changed = ClearMechanicServiceLine(fsm);
                return true;
            }

            string sourceLine = proxy._arrayList[index].ToString();
            if (string.IsNullOrEmpty(sourceLine))
            {
                changed = ClearMechanicServiceLine(fsm);
                return true;
            }

            string displayLine = sourceLine;
            string translated;
            if (TryTranslateMechanicServiceLine(sourceLine, out translated))
            {
                displayLine = translated;
                if (proxy._arrayList[index].ToString() != translated)
                {
                    proxy._arrayList[index] = translated;
                    changed = true;
                }
            }

            changed |= SetMechanicServiceLineValue(fsm, displayLine);
            return true;
        }

        private HutongGames.PlayMaker.Actions.ArrayListGet GetMechanicServiceArrayListGetAction(PlayMakerFSM fsm)
        {
            HutongGames.PlayMaker.FsmState state = FindState(fsm, "State 1");
            if (state == null || state.Actions == null || state.Actions.Length == 0)
                return null;

            HutongGames.PlayMaker.Actions.ArrayListGet action = state.Actions[0] as HutongGames.PlayMaker.Actions.ArrayListGet;
            if (action == null || action.reference == null || !TextMatchesExact(action.reference.Value, ServicePaymentBreakdownReference))
                return null;

            return action;
        }

        private PlayMakerArrayListProxy GetArrayListGetProxy(HutongGames.PlayMaker.Actions.ArrayListGet action)
        {
            if (action == null)
                return null;

            FieldInfo proxyField = GetCachedField(action.GetType(), "proxy");
            return proxyField == null ? null : proxyField.GetValue(action) as PlayMakerArrayListProxy;
        }

        private bool ClearMechanicServiceLine(PlayMakerFSM fsm)
        {
            return SetMechanicServiceLineValue(fsm, string.Empty);
        }

        private bool SetMechanicServiceLineValue(PlayMakerFSM fsm, string value)
        {
            if (fsm == null || fsm.gameObject == null)
                return false;

            bool changed = false;
            HutongGames.PlayMaker.FsmString text = fsm.FsmVariables != null ? fsm.FsmVariables.GetFsmString("Text") : null;
            changed |= SetFsmStringValue(text, value);

            HutongGames.PlayMaker.FsmState state = FindState(fsm, "State 1");
            if (state != null && state.Actions != null)
            {
                if (state.Actions.Length > 0)
                {
                    changed |= SetNestedFsmStringValue(state.Actions[0], value, "result", "namedVar");
                }

                if (state.Actions.Length > 1)
                {
                    changed |= SetNestedFsmStringValue(state.Actions[1], value, "targetProperty", "StringParameter");
                }
            }

            TextMesh textMesh = fsm.gameObject.GetComponent<TextMesh>();
            if (textMesh != null && textMesh.text != value)
            {
                textMesh.text = value;
                changed = true;
            }

            return changed;
        }

        private bool SetFsmStringValue(HutongGames.PlayMaker.FsmString target, string value)
        {
            if (target == null)
                return false;

            string safeValue = value ?? string.Empty;
            if (target.Value == safeValue)
                return false;

            target.Value = safeValue;
            return true;
        }

        private bool SetNestedFsmStringValue(object root, string value, params string[] fieldPath)
        {
            HutongGames.PlayMaker.FsmString target = GetNestedFsmString(root, fieldPath);
            return SetFsmStringValue(target, value);
        }

        private HutongGames.PlayMaker.FsmString GetNestedFsmString(object root, params string[] fieldPath)
        {
            object current = root;
            if (current == null || fieldPath == null || fieldPath.Length == 0)
                return null;

            for (int i = 0; i < fieldPath.Length; i++)
            {
                FieldInfo field = GetCachedField(current.GetType(), fieldPath[i]);
                if (field == null)
                    return null;

                current = field.GetValue(current);
                if (current == null)
                    return null;
            }

            return current as HutongGames.PlayMaker.FsmString;
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

        private bool ApplyImmediateRallyClassTranslations()
        {
            return ApplyFsmTextTargets(GameRallyClassTargets);
        }

        private bool ApplyImmediateRallyPenaltyTranslations()
        {
            return ApplyFsmTextTargets(GameRallyPenaltyTargets);
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

            PlayMakerFSM fsm = target.ComponentIndex >= 0
                ? FindFsmIncludingInactiveByPathAndComponentIndex(target.ObjectPath, target.ComponentIndex)
                : string.IsNullOrEmpty(target.FsmName)
                    ? FindFsmIncludingInactiveByPathAndState(target.ObjectPath, target.StateName)
                    : FindFsmIncludingInactiveByPathAndName(target.ObjectPath, target.FsmName);
            if (!IsFsmReady(fsm))
            {
                fsm = FindFsmByTargetPathPrefix(target);
            }

            if (!IsFsmReady(fsm))
                return false;

            return ApplyFsmTextTargetToFsm(fsm, target);
        }

        private bool ApplyFsmTextTargetToFsm(PlayMakerFSM fsm, FsmTextTarget target)
        {
            if (!IsFsmReady(fsm) || target == null)
                return false;

            bool changed = false;
            switch (target.Kind)
            {
                case FsmTextTargetKind.BuildStringPart:
                    changed = ApplyFsmTextBuildStringPartTarget(fsm, target);
                    break;
                case FsmTextTargetKind.StringAddNewLinePart:
                    changed = ApplyStringAddNewLineActionStringPartTranslation(fsm, target.StateName, target.ActionIndex, target.StringPartIndex);
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
                case FsmTextTargetKind.ActionFsmStringField:
                    changed = ApplyStateActionFsmStringFieldTranslation(
                        fsm,
                        target.StateName,
                        target.ActionIndex,
                        target.VariableName);
                    break;
                case FsmTextTargetKind.FirstStateSetPropertyStringParameter:
                    changed = ApplyFirstStateSetPropertyStringParameterTranslation(fsm, target.ActionIndex);
                    break;
                case FsmTextTargetKind.SetPropertyStringParameter:
                    changed = ApplyStateSetPropertyStringParameterTranslation(
                        fsm,
                        target.StateName,
                        target.ActionIndex);
                    break;
                case FsmTextTargetKind.BuildStringTemplate:
                    changed = ApplyBuildStringTemplateTranslation(
                        fsm,
                        target.StateName,
                        target.ActionIndex,
                        target.OriginalKey);
                    break;
                case FsmTextTargetKind.ExactFsmTranslation:
                    changed = ApplyExactFsmTranslation(fsm, target.OriginalKey);
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

        private PlayMakerFSM FindFsmByTargetPathPrefix(FsmTextTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.ObjectPath))
                return null;

            List<PlayMakerFSM> fsms = GetFsmsByPathPrefix(target.ObjectPath);
            if (fsms == null || fsms.Count == 0)
                return null;

            for (int i = 0; i < fsms.Count; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (!IsFsmReady(fsm))
                    continue;

                if (!string.IsNullOrEmpty(target.FsmName) && fsm.FsmName != target.FsmName)
                    continue;

                if (string.IsNullOrEmpty(target.FsmName)
                    && !string.IsNullOrEmpty(target.StateName)
                    && !HasState(fsm, target.StateName))
                {
                    continue;
                }

                return fsm;
            }

            return null;
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

            bool changed = string.IsNullOrEmpty(target.OriginalKey)
                ? TranslateStringPart(parts[target.StringPartIndex])
                : TranslateFsmStringWithOriginalKey(parts[target.StringPartIndex], target.OriginalKey);
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
            if (fsm == null || fsm.FsmVariables == null || string.IsNullOrEmpty(target.VariableName))
                return false;

            HutongGames.PlayMaker.FsmString variable = fsm.FsmVariables.GetFsmString(target.VariableName);
            if (variable == null || string.IsNullOrEmpty(variable.Value))
                return false;

            if (string.IsNullOrEmpty(target.TextMeshPath))
            {
                TextMesh textMesh = fsm.gameObject != null ? fsm.gameObject.GetComponent<TextMesh>() : null;
                if (textMesh == null || textMesh.text == variable.Value)
                    return false;

                textMesh.text = variable.Value;
                return true;
            }

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

                changed |= ApplyBuildStringStoreResultFromParts(action, parts);
                return changed;
            }

            HutongGames.PlayMaker.Actions.SetStringValue setStringAction = action as HutongGames.PlayMaker.Actions.SetStringValue;
            if (setStringAction != null)
            {
                return TranslateSetStringValue(setStringAction);
            }

            changed |= TranslateSetPropertyStringParameter(action);
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

            GameObject activeObject = FindGameObjectByPathIncludingInactive(objectPath);
            if (activeObject != null)
            {
                TextMesh activeTextMesh = activeObject.GetComponent<TextMesh>();
                if (activeTextMesh != null)
                {
                    textMeshPathCache[objectPath] = activeTextMesh;
                    return activeTextMesh;
                }
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

        private bool ApplyTvScheduleTitleTranslation(PlayMakerFSM fsm, string translatedPrefix, Dictionary<string, string> translatedDays)
        {
            HutongGames.PlayMaker.FsmState state = FindState(fsm, "State 11");
            if (state == null || state.Actions == null || state.Actions.Length == 0)
                return false;

            object action = state.Actions[0];
            HutongGames.PlayMaker.FsmString[] parts = GetBuildStringParts(action);
            if (parts == null || parts.Length == 0 || parts[0] == null)
                return false;

            bool changed = false;
            for (int i = 0; i < parts.Length; i++)
            {
                changed |= TranslateTvScheduleTitlePart(parts[i], translatedPrefix, translatedDays);
            }

            if (changed)
            {
                ApplyBuildStringStoreResultFromParts(action, parts);
            }

            changed |= TranslateTvScheduleBuildStringStoreResult(action, translatedPrefix, translatedDays);
            changed |= TranslateTvScheduleTextMesh(fsm != null && fsm.gameObject != null ? fsm.gameObject.GetComponent<TextMesh>() : null, translatedPrefix, translatedDays);

            return changed;
        }

        private bool TranslateTvScheduleTitlePart(HutongGames.PlayMaker.FsmString part, string translatedPrefix, Dictionary<string, string> translatedDays)
        {
            if (part == null || string.IsNullOrEmpty(part.Value))
                return false;

            string translated;
            if (!TryTranslateTvScheduleTitleText(part.Value, translatedPrefix, translatedDays, out translated) || part.Value == translated)
                return false;

            part.Value = translated;
            return true;
        }

        private bool TranslateTvScheduleBuildStringStoreResult(object action, string translatedPrefix, Dictionary<string, string> translatedDays)
        {
            if (action == null)
                return false;

            FieldInfo storeResultField = GetCachedField(action.GetType(), "storeResult");
            if (storeResultField == null)
                return false;

            HutongGames.PlayMaker.FsmString storeResult = storeResultField.GetValue(action) as HutongGames.PlayMaker.FsmString;
            if (storeResult == null || string.IsNullOrEmpty(storeResult.Value))
                return false;

            string translated;
            if (!TryTranslateTvScheduleTitleText(storeResult.Value, translatedPrefix, translatedDays, out translated) || storeResult.Value == translated)
                return false;

            storeResult.Value = translated;
            return true;
        }

        private bool TranslateTvScheduleTitleTextMeshes(string translatedPrefix, Dictionary<string, string> translatedDays)
        {
            bool changed = false;
            List<TextMesh> textMeshes = GetTvScheduleTitleTextMeshes();

            for (int i = 0; i < textMeshes.Count; i++)
            {
                changed |= TranslateTvScheduleTextMesh(textMeshes[i], translatedPrefix, translatedDays);
            }

            return changed;
        }

        private List<PlayMakerFSM> GetTvScheduleTextFsms()
        {
            if (AreCachedFsmsValid(cachedTvScheduleTextFsms))
                return cachedTvScheduleTextFsms;

            cachedTvScheduleTextFsms.Clear();

            GameObject root = FindNearestGameObjectForPathPrefix(TvSchedulePathPrefix);
            if (root == null)
                return cachedTvScheduleTextFsms;

            PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
            if (fsms == null)
                return cachedTvScheduleTextFsms;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null
                    || fsm.gameObject == null
                    || fsm.FsmName != "Text"
                    || !IsObjectPathUnderPrefix(fsm.gameObject, TvSchedulePathPrefix))
                {
                    continue;
                }

                cachedTvScheduleTextFsms.Add(fsm);
            }

            return cachedTvScheduleTextFsms;
        }

        private List<TextMesh> GetTvScheduleTitleTextMeshes()
        {
            if (AreCachedTextMeshesValid(cachedTvScheduleTitleTextMeshes))
                return cachedTvScheduleTitleTextMeshes;

            cachedTvScheduleTitleTextMeshes.Clear();

            GameObject root = FindGameObjectByPathIncludingInactive(TvSchedulePathPrefix);
            if (root == null)
                return cachedTvScheduleTitleTextMeshes;

            TextMesh[] textMeshes = root.GetComponentsInChildren<TextMesh>(true);
            if (textMeshes == null)
                return cachedTvScheduleTitleTextMeshes;

            for (int i = 0; i < textMeshes.Length; i++)
            {
                TextMesh textMesh = textMeshes[i];
                if (textMesh == null || textMesh.gameObject == null)
                    continue;

                cachedTvScheduleTitleTextMeshes.Add(textMesh);
            }

            return cachedTvScheduleTitleTextMeshes;
        }

        private bool TranslateTvScheduleTextMesh(TextMesh textMesh, string translatedPrefix, Dictionary<string, string> translatedDays)
        {
            if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            string translated;
            if (!TryTranslateTvScheduleTitleText(textMesh.text, translatedPrefix, translatedDays, out translated) || textMesh.text == translated)
                return false;

            textMesh.text = translated;
            return true;
        }

        private bool TryTranslateTvScheduleTitleText(string original, string translatedPrefix, Dictionary<string, string> translatedDays, out string translated)
        {
            translated = original;
            if (string.IsNullOrEmpty(original))
                return false;

            bool changed = false;
            if (!string.IsNullOrEmpty(translatedPrefix))
            {
                translated = ReplaceOrdinalIgnoreCase(translated, TvSchedulePrefixOriginal, translatedPrefix, ref changed);
            }

            if (translatedDays != null && translatedDays.Count > 0)
            {
                for (int i = 0; i < TvScheduleOriginalDays.Length; i++)
                {
                    string translatedDay;
                    if (!translatedDays.TryGetValue(MLCUtils.FormatUpperKey(TvScheduleOriginalDays[i]), out translatedDay)
                        || string.IsNullOrEmpty(translatedDay))
                    {
                        continue;
                    }

                    translated = ReplaceOrdinalIgnoreCase(translated, TvScheduleOriginalDays[i], translatedDay, ref changed);
                }
            }

            return changed;
        }

        private string ReplaceOrdinalIgnoreCase(string source, string search, string replacement, ref bool changed)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search) || replacement == null)
                return source;

            int index = source.IndexOf(search, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return source;

            int start = 0;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(source.Length);
            while (index >= 0)
            {
                sb.Append(source, start, index - start);
                sb.Append(replacement);
                start = index + search.Length;
                changed = true;
                index = source.IndexOf(search, start, System.StringComparison.OrdinalIgnoreCase);
            }

            sb.Append(source, start, source.Length - start);
            return sb.ToString();
        }

        private bool TranslateTvScheduleDaysArrays(Dictionary<string, string> translatedDays)
        {
            if (translatedDays == null || translatedDays.Count == 0)
                return false;

            bool changed = false;
            List<PlayMakerArrayListProxy> allProxies = GetTvScheduleDayProxies();

            for (int i = 0; i < allProxies.Count; i++)
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

        private List<PlayMakerArrayListProxy> GetTvScheduleDayProxies()
        {
            if (AreCachedArrayListProxiesValid(cachedTvScheduleDayProxies))
                return cachedTvScheduleDayProxies;

            cachedTvScheduleDayProxies.Clear();

            PlayMakerArrayListProxy[] allProxies = Resources.FindObjectsOfTypeAll<PlayMakerArrayListProxy>();
            if (allProxies == null)
                return cachedTvScheduleDayProxies;

            for (int i = 0; i < allProxies.Length; i++)
            {
                PlayMakerArrayListProxy proxy = allProxies[i];
                if (proxy == null || proxy.gameObject == null || proxy.referenceName != "Days")
                    continue;

                string path = MLCUtils.GetGameObjectPath(proxy.gameObject);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(TvSchedulePathPrefix, System.StringComparison.Ordinal))
                    continue;

                cachedTvScheduleDayProxies.Add(proxy);
            }

            return cachedTvScheduleDayProxies;
        }

        private bool AreCachedFsmsValid(List<PlayMakerFSM> fsms)
        {
            if (fsms == null || fsms.Count == 0)
                return false;

            for (int i = 0; i < fsms.Count; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || fsm.gameObject == null)
                    return false;
            }

            return true;
        }

        private bool AreCachedArrayListProxiesValid(List<PlayMakerArrayListProxy> proxies)
        {
            if (proxies == null || proxies.Count == 0)
                return false;

            for (int i = 0; i < proxies.Count; i++)
            {
                PlayMakerArrayListProxy proxy = proxies[i];
                if (proxy == null || proxy.gameObject == null)
                    return false;
            }

            return true;
        }

        private bool AreCachedTextMeshesValid(List<TextMesh> textMeshes)
        {
            if (textMeshes == null || textMeshes.Count == 0)
                return false;

            for (int i = 0; i < textMeshes.Count; i++)
            {
                TextMesh textMesh = textMeshes[i];
                if (textMesh == null || textMesh.gameObject == null)
                    return false;
            }

            return true;
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
            return ApplyFsmTextTargets(GamePosTargets);
        }

        private bool TryApplyGamePosFsmMappings()
        {
            bool anyChanged = false;
            anyChanged |= ApplyTextMeshTranslationByPath("COMPUTER/SYSTEM/TELEBBS/Software/text");

            if (anyChanged)
            {
                appliedTarget = GamePosLabel;
            }

            return anyChanged;
        }

        private bool TryApplyGameTeletextBottomlineFsmTranslations()
        {
            return ApplyFsmTextTargets(GameTeletextTargets);
        }

        private bool TryApplyGameTeletextSportsTemplateTranslations()
        {
            return ApplyFsmTextTargets(GameTeletextSportsTemplateTargets);
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
            bool anyChanged = ApplyFsmTextTargets(GameTeletextWeatherTargets);

            List<PlayMakerFSM> ennusteDataFsms = GetEnnusteDataFsms();
            for (int i = 0; i < ennusteDataFsms.Count; i++)
            {
                PlayMakerFSM fsm = ennusteDataFsms[i];
                if (!IsFsmReady(fsm))
                    continue;

                LogReadyOnce("TTX_WX_READY", "[FsmTextHook] Teletext weather updater FSM targets are ready.");
                bool hasAnyTarget = false;
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
            return ApplyFsmTextTargets(GameUnemployPaperTargets);
        }

        private bool TryApplyGameConlineInitializeTranslations()
        {
            return ApplyFsmTextTargets(GameConlineTargets);
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

            anyChanged |= ApplyFsmTextTargets(GameFishgameTargets);
            anyChanged |= ApplyFsmTextTargets(GameProPilkkiTargets);
            anyChanged |= TranslateArrayListProxyByPathAndIndex("COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saaliit", 2);
            anyChanged |= ApplyTextMeshTranslationByPaths(GameComputerTextMeshTargets);

            if (anyChanged)
            {
                appliedTarget = GameComputerLabel;
            }

            return anyChanged;
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
            if (cachedEnnusteDataFsms.Count > 0)
                return cachedEnnusteDataFsms;

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

            GameObject activeObject = FindGameObjectByPathIncludingInactive(objectPath);
            PlayMakerFSM activeFsm = FindFsmWithState(activeObject, stateName);
            if (activeFsm != null)
            {
                fsmPathStateCache[cacheKey] = activeFsm;
                return activeFsm;
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

            GameObject activeObject = FindGameObjectByPathIncludingInactive(objectPath);
            PlayMakerFSM activeFsm = FindFsmWithName(activeObject, fsmName);
            if (activeFsm != null)
            {
                fsmPathNameCache[cacheKey] = activeFsm;
                return activeFsm;
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

            GameObject root = FindNearestGameObjectForPathPrefix(pathPrefix);
            if (root == null)
                return;

            PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
            if (fsms == null)
                return;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm != null
                    && fsm.gameObject != null
                    && fsm.FsmName == fsmName
                    && IsObjectPathUnderPrefix(fsm.gameObject, pathPrefix))
                {
                    results.Add(fsm);
                }
            }
        }

        private List<PlayMakerFSM> GetFsmsByPathPrefix(string pathPrefix)
        {
            List<PlayMakerFSM> results = new List<PlayMakerFSM>();
            if (string.IsNullOrEmpty(pathPrefix))
                return results;

            GameObject root = FindNearestGameObjectForPathPrefix(pathPrefix);
            if (root == null)
                return results;

            PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
            if (fsms == null)
                return results;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm != null && fsm.gameObject != null && IsObjectPathUnderPrefix(fsm.gameObject, pathPrefix))
                {
                    results.Add(fsm);
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

            GameObject activeObject = FindGameObjectByPathIncludingInactive(objectPath);
            PlayMakerFSM indexedFsm = GetComponentAtIndex<PlayMakerFSM>(activeObject, componentIndex);
            if (indexedFsm != null)
            {
                fsmPathComponentCache[cacheKey] = indexedFsm;
                return indexedFsm;
            }

            return null;
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

            GameObject activeObject = FindGameObjectByPathIncludingInactive(objectPath);
            if (activeObject != null)
            {
                TextMesh activeTextMesh = activeObject.GetComponent<TextMesh>();
                if (activeTextMesh != null)
                    textMeshPathCache[objectPath] = activeTextMesh;

                return TranslateTextMesh(activeTextMesh);
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

            GameObject activeObject = FindGameObjectByPathIncludingInactive(objectPath);
            PlayMakerArrayListProxy indexedProxy = GetComponentAtIndex<PlayMakerArrayListProxy>(activeObject, componentIndex);
            if (indexedProxy != null)
            {
                arrayListProxyPathCache[cacheKey] = indexedProxy;
                return indexedProxy;
            }

            return null;
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
                || TryTranslateRallyClassPrefix(original, "Amateur", out translated)
                || TryTranslateRallyClassNamePrefix(original, "Junior", out translated)
                || TryTranslateRallyClassNamePrefix(original, "Amateur", out translated);
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

        private bool TryTranslateRallyClassNamePrefix(string original, string className, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(className))
                return false;

            string translatedClassName;
            if (!TryGetTranslationValue(className, out translatedClassName)
                || TextMatchesExact(translatedClassName, className))
            {
                return false;
            }

            string leadingWhitespace = GetLeadingWhitespace(original);
            string core = original.Substring(leadingWhitespace.Length);
            if (!core.StartsWith(className, System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (core.Length > className.Length
                && char.IsLetterOrDigit(core[className.Length]))
            {
                return false;
            }

            string remainder = core.Substring(className.Length);
            if (!string.IsNullOrEmpty(remainder) && !HasRallyClassContext(remainder))
                return false;

            translated = leadingWhitespace + translatedClassName + remainder;
            return translated != original;
        }

        private bool HasRallyClassContext(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            string normalized = MLCUtils.FormatUpperKey(text);
            if (normalized.IndexOf(MLCUtils.FormatUpperKey("Class"), System.StringComparison.Ordinal) >= 0)
                return true;

            string translatedClassSegment;
            if (TryTranslateRallyClassSegment(" - Class", out translatedClassSegment)
                && normalized.IndexOf(MLCUtils.FormatUpperKey(translatedClassSegment), System.StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return false;
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
