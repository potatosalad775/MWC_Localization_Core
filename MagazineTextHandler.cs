// 'Classified Magazine' Text Handler

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Handles Yellow Pages magazine text translation.
    /// Performs direct line lookup and price/phone line formatting.
    /// </summary>
    public class MagazineTextHandler : ITranslationSurface
    {
        public string Name { get { return "MagazineTextHandler"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return yellowPagesSourcesResolved; } }

        private Dictionary<string, string> magazineTranslations = new Dictionary<string, string>();
        private bool yellowPagesSourcesResolved;
        private readonly List<PlayMakerFSM> yellowLineFsmCache = new List<PlayMakerFSM>();
        private string assetsFolder;

        public void Initialize(TranslationContext ctx)
        {
            assetsFolder = ctx.AssetsFolder;
            string path = Path.Combine(assetsFolder, "translate_magazine.txt");
            LoadMagazineTranslations(path);
        }

        public int InitialPass()
        {
            return TranslateAllSources();
        }

        public int MonitorTick(float deltaTime)
        {
            return MonitorAndTranslateSources();
        }

        public void Reset()
        {
            ResetRuntimeState();
        }

        /// <summary>
        /// Load magazine-specific translations from separate file
        /// </summary>
        public void LoadMagazineTranslations(string translationPath)
        {
            magazineTranslations = TranslationFileParser.ParseKeyValueFile(translationPath, normalizeKeys: true);
            CoreConsole.Print($"[MagazineTextHandler] Loaded {magazineTranslations.Count} magazine translations");
        }

        /// <summary>
        /// Get total count of loaded magazine translations
        /// </summary>
        public int GetTranslationCount()
        {
            return magazineTranslations.Count;
        }

        /// <summary>
        /// Check if path is a magazine text element
        /// </summary>
        public bool IsMagazineText(string path)
        {
            return path.Contains("Sheets/YellowPagesMagazine/Page") && path.EndsWith("/Lines/YellowLine");
        }

        /// <summary>
        /// Translate price and phone number line (e.g., "h.149,- puh.123456" -> "149 MK, PHONE - 123456")
        /// Uses PHONE key from translate_magazine.txt for the phone label
        /// </summary>
        private string FormatPricePhoneLine(string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value) || !value.StartsWith("h.") || !value.Contains(",- puh."))
                    return null;

                // Remove "h." prefix and split by ",- puh."
                string withoutPrefix = value.Substring(2);
                string[] parts = withoutPrefix.Split(new string[] { ",- puh." }, System.StringSplitOptions.None);

                if (parts.Length == 2)
                {
                    string pricePart = parts[0].Trim();
                    string phonePart = parts[1].Trim();

                    // Get phone label from translations (default to "PHONE" if not found)
                    string phoneLabel = magazineTranslations.TryGetValue("PHONE", out string translation)
                        ? translation
                        : "PHONE";

                    return $"{pricePart} MK, {phoneLabel} - {phonePart}";
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Warning($"[MagazineTextHandler] Failed to parse magazine price/phone line: {value} - {ex.Message}");
            }

            return null;
        }

        public void ResetRuntimeState()
        {
            yellowPagesSourcesResolved = false;
            yellowLineFsmCache.Clear();
        }

        public int TranslateAllSources()
        {
            yellowPagesSourcesResolved = false;
            return MonitorAndTranslateSources();
        }

        public int MonitorAndTranslateSources()
        {
            if (yellowPagesSourcesResolved)
                return 0;

            bool foundSourceFsm;
            int translated = TranslateYellowPagesMagazineFsmSources(out foundSourceFsm);
            if (foundSourceFsm && translated == 0)
                yellowPagesSourcesResolved = true;

            return translated;
        }

        private int TranslateYellowPagesMagazineFsmSources(out bool foundSourceFsm)
        {
            foundSourceFsm = false;

            GameObject root = LocalizationUtils.FindGameObjectCached("Sheets/YellowPagesMagazine");
            if (root == null)
                return 0;

            PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
            if (fsms == null || fsms.Length == 0)
                return 0;

            int translated = 0;
            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || FsmUtils.GetFsmName(fsm) != "Generate")
                    continue;

                foundSourceFsm = true;
                translated += TranslateMagazineGenerateFsm(fsm);
            }

            if (translated > 0)
                CoreConsole.Print($"[MagazineTextHandler] Translated {translated} Yellow Pages magazine FSM source values");

            return translated;
        }

        private int TranslateMagazineGenerateFsm(PlayMakerFSM fsm)
        {
            int translated = 0;

            if (fsm.FsmVariables != null && fsm.FsmVariables.StringVariables != null)
            {
                HutongGames.PlayMaker.FsmString[] variables = fsm.FsmVariables.StringVariables;
                for (int i = 0; i < variables.Length; i++)
                {
                    if (TranslateMagazineFsmString(variables[i]))
                        translated++;
                }
            }

            if (fsm.FsmStates == null)
                return translated;

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

                    translated += TranslateMagazineBuildStringParts(action);
                }
            }

            translated += SyncMagazineYellowLineDisplay(fsm);
            return translated;
        }

        internal int SyncYellowLineDisplaysForCurrentScene(out bool foundDisplayLine)
        {
            foundDisplayLine = false;
            if (Application.loadedLevelName != "GAME")
                return 0;

            List<PlayMakerFSM> fsms = GetYellowLineFsms();
            int changed = 0;
            for (int i = 0; i < fsms.Count; i++)
            {
                if (HasYellowLineDisplayValue(fsms[i]))
                    foundDisplayLine = true;

                changed += SyncMagazineYellowLineDisplay(fsms[i]);
            }

            return changed;
        }

        private List<PlayMakerFSM> GetYellowLineFsms()
        {
            if (AreYellowLineFsmsValid())
                return yellowLineFsmCache;

            yellowLineFsmCache.Clear();

            GameObject root = LocalizationUtils.FindGameObjectCached("Sheets/YellowPagesMagazine");
            if (root == null)
                return yellowLineFsmCache;

            PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
            if (fsms == null || fsms.Length == 0)
                return yellowLineFsmCache;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || fsm.gameObject == null || FsmUtils.GetFsmName(fsm) != "Generate")
                    continue;

                string path = LocalizationUtils.GetGameObjectPath(fsm.gameObject);
                if (string.IsNullOrEmpty(path) || path.IndexOf("/Lines/YellowLine", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                yellowLineFsmCache.Add(fsm);
            }

            return yellowLineFsmCache;
        }

        private bool AreYellowLineFsmsValid()
        {
            if (yellowLineFsmCache.Count == 0)
                return false;

            for (int i = 0; i < yellowLineFsmCache.Count; i++)
            {
                PlayMakerFSM fsm = yellowLineFsmCache[i];
                if (fsm == null || fsm.gameObject == null)
                    return false;
            }

            return true;
        }

        private bool HasYellowLineDisplayValue(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.gameObject == null || fsm.FsmVariables == null)
                return false;

            HutongGames.PlayMaker.FsmString line = fsm.FsmVariables.GetFsmString("Line");
            return line != null && !string.IsNullOrEmpty(line.Value);
        }

        private int SyncMagazineYellowLineDisplay(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.gameObject == null || fsm.FsmVariables == null)
                return 0;

            string path = LocalizationUtils.GetGameObjectPath(fsm.gameObject);
            if (string.IsNullOrEmpty(path) || path.IndexOf("/Lines/YellowLine", System.StringComparison.OrdinalIgnoreCase) < 0)
                return 0;

            HutongGames.PlayMaker.FsmString line = fsm.FsmVariables.GetFsmString("Line");
            if (line == null || string.IsNullOrEmpty(line.Value))
                return 0;

            string translatedLine = TranslateMagazineSourceValue(line.Value);
            if (string.IsNullOrEmpty(translatedLine))
                return 0;

            int changed = 0;
            if (line.Value != translatedLine)
            {
                line.Value = translatedLine;
                changed++;
            }

            if (fsm.FsmStates != null)
            {
                for (int stateIndex = 0; stateIndex < fsm.FsmStates.Length; stateIndex++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[stateIndex];
                    if (state == null || state.Actions == null || state.Name != "State 4")
                        continue;

                    if (state.Actions.Length > 0 && FsmUtils.SetNestedStringValue(state.Actions[0], translatedLine, "storeValue"))
                        changed++;

                    if (state.Actions.Length > 1 && FsmUtils.SetNestedStringValue(state.Actions[1], translatedLine, "targetProperty", "StringParameter"))
                        changed++;
                }
            }

            TextMesh textMesh = fsm.GetComponent<TextMesh>();
            if (textMesh != null && textMesh.text != translatedLine)
            {
                textMesh.text = translatedLine;
                changed++;
            }

            return changed;
        }

        private int TranslateMagazineBuildStringParts(object action)
        {
            if (!FsmUtils.IsBuildStringAction(action))
                return 0;

            FieldInfo field = FsmUtils.GetStringPartsField(action);
            if (field == null)
                return 0;

            int translated = 0;
            object value = field.GetValue(action);

            HutongGames.PlayMaker.FsmString[] fsmStrings = value as HutongGames.PlayMaker.FsmString[];
            if (fsmStrings != null)
            {
                for (int i = 0; i < fsmStrings.Length; i++)
                {
                    if (TranslateMagazineFsmString(fsmStrings[i]))
                        translated++;
                }

                return translated;
            }

            string[] stringParts = value as string[];
            if (stringParts == null)
                return 0;

            for (int i = 0; i < stringParts.Length; i++)
            {
                string translatedValue = TranslateMagazineSourceValue(stringParts[i]);
                if (translatedValue != stringParts[i])
                {
                    stringParts[i] = translatedValue;
                    translated++;
                }
            }

            return translated;
        }

        private bool TranslateMagazineFsmString(HutongGames.PlayMaker.FsmString fsmString)
        {
            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value))
                return false;

            string translated = TranslateMagazineSourceValue(fsmString.Value);
            if (translated == null || translated == fsmString.Value)
                return false;

            fsmString.Value = translated;
            return true;
        }

        private string TranslateMagazineSourceValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value == "h.")
                return string.Empty;

            if (value.Trim() == ",- puh.")
                return " MK, " + GetPhoneLabel() + " - ";

            string formatted = FormatPricePhoneLine(value);
            if (!string.IsNullOrEmpty(formatted))
                return formatted;

            // Piece and title text is translated earlier in the backing array/hash table sources.
            return value;
        }

        private string GetPhoneLabel()
        {
            return magazineTranslations.TryGetValue("PHONE", out string translation)
                ? translation
                : "PHONE";
        }

        /// <summary>
        /// Clear all magazine translations
        /// </summary>
        public void ClearTranslations()
        {
            magazineTranslations.Clear();
            ResetRuntimeState();
        }

        /// <summary>
        /// Get translation for a magazine text (case-insensitive lookup)
        /// Returns null if no translation found
        /// </summary>
        public string GetTranslation(string original)
        {
            if (string.IsNullOrEmpty(original))
                return null;

            string normalizedKey = LocalizationUtils.FormatUpperKey(original);
            if (magazineTranslations.TryGetValue(normalizedKey, out string translation))
            {
                return translation;
            }

            return null;
        }
    }

    /// <summary>
    /// Per-frame repair pass for Yellow Pages display lines that are rebuilt while the magazine is open.
    /// Keeps the broader magazine source scan on its normal slow cadence.
    /// </summary>
    public class MagazineYellowLineHook : ITranslationSurface
    {
        public string Name { get { return "MagazineYellowLineHook"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.PerFrame; } }
        public bool IsComplete { get { return isComplete; } }

        private readonly MagazineTextHandler magazineHandler;
        private bool isComplete;
        private bool hasFoundDisplayLine;
        private float settleTimer;
        private const float SettleSeconds = 5.0f;

        public MagazineYellowLineHook(MagazineTextHandler magazineHandler)
        {
            this.magazineHandler = magazineHandler;
        }

        public void Initialize(TranslationContext ctx)
        {
        }

        public int InitialPass()
        {
            return MonitorTick(0f);
        }

        public int MonitorTick(float deltaTime)
        {
            if (isComplete || magazineHandler == null)
                return 0;

            bool foundDisplayLine;
            int changed = magazineHandler.SyncYellowLineDisplaysForCurrentScene(out foundDisplayLine);
            if (foundDisplayLine)
                hasFoundDisplayLine = true;

            if (!hasFoundDisplayLine)
                return changed;

            if (changed > 0)
            {
                settleTimer = 0f;
                return changed;
            }

            settleTimer += deltaTime > 0f ? deltaTime : 0.02f;
            if (settleTimer >= SettleSeconds)
                isComplete = true;

            return changed;
        }

        public void Reset()
        {
            isComplete = false;
            hasFoundDisplayLine = false;
            settleTimer = 0f;
        }

        public void ClearTranslations()
        {
            isComplete = false;
            hasFoundDisplayLine = false;
            settleTimer = 0f;
        }
    }
}
