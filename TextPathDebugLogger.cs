using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MWC_Localization_Core
{
    public static class TextPathDebugLogger
    {
        private const int MaxVisibleTextLogs = 12;
        private const int MaxFsmSourceLogs = 30;
        private const int MaxSubsystemTextLogs = 80;
        private const int MaxSubsystemFsmStringLogs = 120;
        private const int MaxNearbyFsmStringLogs = 160;
        private const int MaxTextPreviewLength = 140;
        private static readonly string[] SubsystemPathPrefixes = new string[]
        {
            "Systems/TV",
            "COMPUTER/SYSTEM",
            "YARD/Building/BEDROOM1/COMPUTER/SYSTEM",
            "Sheets/TrafficTicket",
            "Sheets/RallyResults",
            "Sheets/ServiceBrochure",
            "Sheets/ServicePayment",
            "Sheets/RallyRegistration"
        };

        private sealed class TextCandidate
        {
            public TextMesh TextMesh;
            public string Path;
            public string Text;
            public float Score;
        }

        public static void LogVisibleTextPaths(Dictionary<string, string> translations)
        {
            TextMesh[] allTextMeshes = MLCUtils.GetAllTextMeshesIncludingInactive();
            if (allTextMeshes == null || allTextMeshes.Length == 0)
            {
                CoreConsole.PrintAlways("[TextPathDebug] Nenhum TextMesh encontrado na cena.");
                return;
            }

            Camera camera = GetBestCamera();
            List<TextCandidate> candidates = CollectVisibleCandidates(allTextMeshes, camera);
            List<string> textsToMatch = new List<string>();

            if (candidates.Count == 0)
            {
                CoreConsole.PrintAlways("[TextPathDebug] Nenhum TextMesh visivel pela camera principal. Rodando varredura direta de TV/PC.");
            }
            else
            {
                candidates.Sort(CompareCandidates);

                int count = Mathf.Min(MaxVisibleTextLogs, candidates.Count);
                CoreConsole.PrintAlways("[TextPathDebug] Textos visiveis mais proximos do centro da tela:");

                for (int i = 0; i < count; i++)
                {
                    TextCandidate candidate = candidates[i];
                    AddUniqueText(textsToMatch, candidate.Text);

                    string normalizedKey = MLCUtils.FormatUpperKey(candidate.Text);
                    bool hasTranslation = translations != null && translations.ContainsKey(normalizedKey);
                    CoreConsole.PrintAlways(
                        "[TextPathDebug] TextMesh path=\"" + candidate.Path
                        + "\" key=\"" + normalizedKey
                        + "\" translatedKey=" + hasTranslation.ToString()
                        + " text=\"" + Preview(candidate.Text) + "\"");

                    LogNearbyFsms(candidate.TextMesh, translations);
                }
            }

            LogSubsystemTextPaths(allTextMeshes, translations, textsToMatch);
            LogMatchingFsmStringSources(textsToMatch, translations);
            LogSubsystemFsmStringSources(translations);
        }

        private static Camera GetBestCamera()
        {
            if (Camera.main != null)
                return Camera.main;

            Camera[] cameras = Object.FindObjectsOfType<Camera>();
            if (cameras == null || cameras.Length == 0)
                return null;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled)
                    return cameras[i];
            }

            return cameras[0];
        }

        private static List<TextCandidate> CollectVisibleCandidates(TextMesh[] allTextMeshes, Camera camera)
        {
            List<TextCandidate> candidates = new List<TextCandidate>();

            for (int i = 0; i < allTextMeshes.Length; i++)
            {
                TextMesh textMesh = allTextMeshes[i];
                if (textMesh == null || textMesh.gameObject == null || string.IsNullOrEmpty(textMesh.text))
                    continue;

                if (!textMesh.gameObject.activeInHierarchy)
                    continue;

                float score = 0f;
                if (camera != null)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(textMesh.transform.position);
                    if (viewport.z <= 0f || viewport.x < -0.15f || viewport.x > 1.15f || viewport.y < -0.15f || viewport.y > 1.15f)
                        continue;

                    score = Mathf.Abs(viewport.x - 0.5f) + Mathf.Abs(viewport.y - 0.5f) + Mathf.Max(0f, viewport.z) * 0.0005f;
                }

                Renderer renderer = textMesh.GetComponent<Renderer>();
                if (renderer != null && !renderer.isVisible)
                    score += 1f;

                candidates.Add(new TextCandidate
                {
                    TextMesh = textMesh,
                    Path = MLCUtils.GetGameObjectPath(textMesh.gameObject),
                    Text = textMesh.text,
                    Score = score
                });
            }

            return candidates;
        }

        private static void LogSubsystemTextPaths(TextMesh[] allTextMeshes, Dictionary<string, string> translations, List<string> textsToMatch)
        {
            List<TextCandidate> subsystemTexts = new List<TextCandidate>();

            for (int i = 0; i < allTextMeshes.Length; i++)
            {
                TextMesh textMesh = allTextMeshes[i];
                if (textMesh == null || textMesh.gameObject == null || string.IsNullOrEmpty(textMesh.text))
                    continue;

                string path = MLCUtils.GetGameObjectPath(textMesh.gameObject);
                if (!PathStartsWithAny(path, SubsystemPathPrefixes))
                    continue;

                subsystemTexts.Add(new TextCandidate
                {
                    TextMesh = textMesh,
                    Path = path,
                    Text = textMesh.text,
                    Score = textMesh.gameObject.activeInHierarchy ? 0f : 1f
                });
            }

            if (subsystemTexts.Count == 0)
            {
                CoreConsole.PrintAlways("[TextPathDebug] Nenhum TextMesh em Systems/TV ou COMPUTER/SYSTEM encontrado nesta cena.");
                return;
            }

            subsystemTexts.Sort(CompareSubsystemCandidates);
            int count = Mathf.Min(MaxSubsystemTextLogs, subsystemTexts.Count);
            CoreConsole.PrintAlways("[TextPathDebug] Varredura direta TV/PC TextMesh (nao depende de mirar):");

            for (int i = 0; i < count; i++)
            {
                TextCandidate candidate = subsystemTexts[i];
                AddUniqueText(textsToMatch, candidate.Text);

                Renderer renderer = candidate.TextMesh.GetComponent<Renderer>();
                bool rendererVisible = renderer != null && renderer.isVisible;
                string normalizedKey = MLCUtils.FormatUpperKey(candidate.Text);
                bool hasTranslation = translations != null && translations.ContainsKey(normalizedKey);

                CoreConsole.PrintAlways(
                    "[TextPathDebug] TVPCText path=\"" + candidate.Path
                    + "\" active=" + candidate.TextMesh.gameObject.activeInHierarchy.ToString()
                    + " rendererVisible=" + rendererVisible.ToString()
                    + " key=\"" + normalizedKey
                    + "\" translatedKey=" + hasTranslation.ToString()
                    + " text=\"" + Preview(candidate.Text) + "\"");

                LogNearbyFsms(candidate.TextMesh, translations);
            }

            if (subsystemTexts.Count > count)
            {
                CoreConsole.PrintAlways("[TextPathDebug] TV/PC TextMesh limit reached: " + count.ToString() + "/" + subsystemTexts.Count.ToString() + ". Envie um trecho do texto se precisar filtrar mais.");
            }
        }

        private static int CompareCandidates(TextCandidate left, TextCandidate right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            return left.Score.CompareTo(right.Score);
        }

        private static int CompareSubsystemCandidates(TextCandidate left, TextCandidate right)
        {
            int scoreCompare = CompareCandidates(left, right);
            if (scoreCompare != 0)
                return scoreCompare;

            string leftPath = left == null || left.Path == null ? string.Empty : left.Path;
            string rightPath = right == null || right.Path == null ? string.Empty : right.Path;
            return string.Compare(leftPath, rightPath);
        }

        private static void LogNearbyFsms(TextMesh textMesh, Dictionary<string, string> translations)
        {
            if (textMesh == null)
                return;

            Transform current = textMesh.transform;
            int stringLogs = 0;
            for (int depth = 0; depth < 4 && current != null; depth++)
            {
                PlayMakerFSM[] fsms = current.GetComponents<PlayMakerFSM>();
                if (fsms != null && fsms.Length > 0)
                {
                    string path = MLCUtils.GetGameObjectPath(current.gameObject);
                    for (int i = 0; i < fsms.Length; i++)
                    {
                        if (fsms[i] == null)
                            continue;

                        CoreConsole.PrintAlways("[TextPathDebug] NearbyFSM path=\"" + path + "\" fsm=\"" + fsms[i].FsmName + "\"");
                        LogNearbyFsmStringDetails(path, fsms[i], translations, ref stringLogs);
                    }
                }

                current = current.parent;
            }
        }

        private static void LogNearbyFsmStringDetails(string path, PlayMakerFSM fsm, Dictionary<string, string> translations, ref int logged)
        {
            if (fsm == null || logged >= MaxNearbyFsmStringLogs)
                return;

            TryInitFsm(fsm);

            LogFsmStringVariables(path, fsm, translations, ref logged, MaxNearbyFsmStringLogs, "NearbyFSMVar");

            if (fsm.FsmStates == null)
                return;

            for (int stateIndex = 0; stateIndex < fsm.FsmStates.Length && logged < MaxNearbyFsmStringLogs; stateIndex++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[stateIndex];
                if (state == null || state.Actions == null)
                    continue;

                for (int actionIndex = 0; actionIndex < state.Actions.Length && logged < MaxNearbyFsmStringLogs; actionIndex++)
                {
                    object action = state.Actions[actionIndex];
                    if (action == null)
                        continue;

                    ScanActionForAnyStrings(path, fsm.FsmName, state.Name, actionIndex, action, translations, ref logged, MaxNearbyFsmStringLogs, "NearbyFSMAction");
                }
            }
        }

        private static void AddUniqueText(List<string> texts, string text)
        {
            if (texts == null || string.IsNullOrEmpty(text))
                return;

            if (!texts.Contains(text))
                texts.Add(text);
        }

        private static bool PathStartsWithAny(string path, string[] prefixes)
        {
            if (string.IsNullOrEmpty(path) || prefixes == null)
                return false;

            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!string.IsNullOrEmpty(prefix) && path.StartsWith(prefix))
                    return true;
            }

            return false;
        }

        private static void LogMatchingFsmStringSources(List<string> visibleTexts, Dictionary<string, string> translations)
        {
            if (visibleTexts == null || visibleTexts.Count == 0)
                return;

            HashSet<string> targetKeys = BuildTargetKeys(visibleTexts, translations);
            PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if (allFsms == null || allFsms.Length == 0)
                return;

            int logged = 0;
            CoreConsole.PrintAlways("[TextPathDebug] Possiveis fontes FSM desses textos:");

            for (int i = 0; i < allFsms.Length && logged < MaxFsmSourceLogs; i++)
            {
                PlayMakerFSM fsm = allFsms[i];
                if (fsm == null || fsm.gameObject == null)
                    continue;

                TryInitFsm(fsm);
                if (fsm.FsmStates == null)
                    continue;

                string path = MLCUtils.GetGameObjectPath(fsm.gameObject);
                for (int stateIndex = 0; stateIndex < fsm.FsmStates.Length && logged < MaxFsmSourceLogs; stateIndex++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[stateIndex];
                    if (state == null || state.Actions == null)
                        continue;

                    for (int actionIndex = 0; actionIndex < state.Actions.Length && logged < MaxFsmSourceLogs; actionIndex++)
                    {
                        object action = state.Actions[actionIndex];
                        if (action == null)
                            continue;

                        ScanActionForMatches(path, fsm.FsmName, state.Name, actionIndex, action, targetKeys, ref logged);
                    }
                }
            }

            if (logged == 0)
            {
                CoreConsole.PrintAlways("[TextPathDebug] Nenhuma action FSM com texto igual foi encontrada. Use o TextMesh path acima.");
            }
        }

        private static void LogSubsystemFsmStringSources(Dictionary<string, string> translations)
        {
            PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if (allFsms == null || allFsms.Length == 0)
                return;

            int logged = 0;
            CoreConsole.PrintAlways("[TextPathDebug] Varredura direta TV/PC FSM strings:");

            for (int i = 0; i < allFsms.Length && logged < MaxSubsystemFsmStringLogs; i++)
            {
                PlayMakerFSM fsm = allFsms[i];
                if (fsm == null || fsm.gameObject == null)
                    continue;

                string path = MLCUtils.GetGameObjectPath(fsm.gameObject);
                if (!PathStartsWithAny(path, SubsystemPathPrefixes))
                    continue;

                TryInitFsm(fsm);
                if (fsm.FsmStates == null)
                    continue;

                for (int stateIndex = 0; stateIndex < fsm.FsmStates.Length && logged < MaxSubsystemFsmStringLogs; stateIndex++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[stateIndex];
                    if (state == null || state.Actions == null)
                        continue;

                    for (int actionIndex = 0; actionIndex < state.Actions.Length && logged < MaxSubsystemFsmStringLogs; actionIndex++)
                    {
                        object action = state.Actions[actionIndex];
                        if (action == null)
                            continue;

                        ScanActionForAnyStrings(path, fsm.FsmName, state.Name, actionIndex, action, translations, ref logged, MaxSubsystemFsmStringLogs, "TVPCFSM");
                    }
                }
            }

            if (logged == 0)
            {
                CoreConsole.PrintAlways("[TextPathDebug] Nenhuma string FSM em Systems/TV ou COMPUTER/SYSTEM encontrada nesta cena.");
            }
            else if (logged >= MaxSubsystemFsmStringLogs)
            {
                CoreConsole.PrintAlways("[TextPathDebug] TV/PC FSM string limit reached: " + MaxSubsystemFsmStringLogs.ToString() + ". Envie o trecho mais proximo do texto desejado.");
            }
        }

        private static HashSet<string> BuildTargetKeys(List<string> visibleTexts, Dictionary<string, string> translations)
        {
            HashSet<string> targetKeys = new HashSet<string>();

            for (int i = 0; i < visibleTexts.Count; i++)
            {
                string text = visibleTexts[i];
                if (string.IsNullOrEmpty(text))
                    continue;

                targetKeys.Add(MLCUtils.FormatUpperKey(text));

                string[] lines = text.Split('\n');
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex].Replace("\r", string.Empty);
                    if (!string.IsNullOrEmpty(line))
                        targetKeys.Add(MLCUtils.FormatUpperKey(line));
                }
            }

            if (translations != null)
            {
                foreach (KeyValuePair<string, string> pair in translations)
                {
                    if (string.IsNullOrEmpty(pair.Value))
                        continue;

                    for (int i = 0; i < visibleTexts.Count; i++)
                    {
                        if (pair.Value == visibleTexts[i])
                        {
                            targetKeys.Add(pair.Key);
                            break;
                        }
                    }
                }
            }

            return targetKeys;
        }

        private static void ScanActionForMatches(
            string path,
            string fsmName,
            string stateName,
            int actionIndex,
            object action,
            HashSet<string> targetKeys,
            ref int logged)
        {
            FieldInfo[] fields = action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length && logged < MaxFsmSourceLogs; i++)
            {
                FieldInfo field = fields[i];
                object value = field.GetValue(action);

                HutongGames.PlayMaker.FsmString fsmString = value as HutongGames.PlayMaker.FsmString;
                if (fsmString != null)
                {
                    LogFsmStringIfMatch(path, fsmName, stateName, actionIndex, action, field.Name, fsmString, targetKeys, ref logged);
                    continue;
                }

                HutongGames.PlayMaker.FsmString[] fsmStrings = value as HutongGames.PlayMaker.FsmString[];
                if (fsmStrings != null)
                {
                    for (int partIndex = 0; partIndex < fsmStrings.Length && logged < MaxFsmSourceLogs; partIndex++)
                    {
                        LogFsmStringIfMatch(path, fsmName, stateName, actionIndex, action, field.Name + "[" + partIndex.ToString() + "]", fsmStrings[partIndex], targetKeys, ref logged);
                    }

                    continue;
                }

                if (value != null && ShouldScanNestedValue(value))
                {
                    ScanNestedObjectForMatches(path, fsmName, stateName, actionIndex, action, field.Name, value, targetKeys, ref logged);
                }
            }
        }

        private static void ScanActionForAnyStrings(
            string path,
            string fsmName,
            string stateName,
            int actionIndex,
            object action,
            Dictionary<string, string> translations,
            ref int logged,
            int maxLogs,
            string logLabel)
        {
            FieldInfo[] fields = action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length && logged < maxLogs; i++)
            {
                FieldInfo field = fields[i];
                object value = field.GetValue(action);

                HutongGames.PlayMaker.FsmString fsmString = value as HutongGames.PlayMaker.FsmString;
                if (fsmString != null)
                {
                    LogAnyFsmString(path, fsmName, stateName, actionIndex, action, field.Name, fsmString, translations, ref logged, logLabel);
                    continue;
                }

                HutongGames.PlayMaker.FsmString[] fsmStrings = value as HutongGames.PlayMaker.FsmString[];
                if (fsmStrings != null)
                {
                    for (int partIndex = 0; partIndex < fsmStrings.Length && logged < maxLogs; partIndex++)
                    {
                        LogAnyFsmString(path, fsmName, stateName, actionIndex, action, field.Name + "[" + partIndex.ToString() + "]", fsmStrings[partIndex], translations, ref logged, logLabel);
                    }

                    continue;
                }

                if (value != null && ShouldScanNestedValue(value))
                {
                    ScanNestedObjectForAnyStrings(path, fsmName, stateName, actionIndex, action, field.Name, value, translations, ref logged, maxLogs, logLabel, 0);
                }
            }
        }

        private static void ScanNestedObjectForMatches(
            string path,
            string fsmName,
            string stateName,
            int actionIndex,
            object action,
            string parentFieldName,
            object value,
            HashSet<string> targetKeys,
            ref int logged)
        {
            FieldInfo[] fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length && logged < MaxFsmSourceLogs; i++)
            {
                FieldInfo field = fields[i];
                object nestedValue = field.GetValue(value);

                HutongGames.PlayMaker.FsmString fsmString = nestedValue as HutongGames.PlayMaker.FsmString;
                if (fsmString != null)
                {
                    LogFsmStringIfMatch(path, fsmName, stateName, actionIndex, action, parentFieldName + "." + field.Name, fsmString, targetKeys, ref logged);
                }
            }
        }

        private static void ScanNestedObjectForAnyStrings(
            string path,
            string fsmName,
            string stateName,
            int actionIndex,
            object action,
            string parentFieldName,
            object value,
            Dictionary<string, string> translations,
            ref int logged,
            int maxLogs,
            string logLabel,
            int depth)
        {
            if (value == null || depth > 3)
                return;

            FieldInfo[] fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length && logged < maxLogs; i++)
            {
                FieldInfo field = fields[i];
                object nestedValue = field.GetValue(value);

                HutongGames.PlayMaker.FsmString fsmString = nestedValue as HutongGames.PlayMaker.FsmString;
                if (fsmString != null)
                {
                    LogAnyFsmString(path, fsmName, stateName, actionIndex, action, parentFieldName + "." + field.Name, fsmString, translations, ref logged, logLabel);
                    continue;
                }

                HutongGames.PlayMaker.FsmString[] fsmStrings = nestedValue as HutongGames.PlayMaker.FsmString[];
                if (fsmStrings != null)
                {
                    for (int partIndex = 0; partIndex < fsmStrings.Length && logged < maxLogs; partIndex++)
                    {
                        LogAnyFsmString(path, fsmName, stateName, actionIndex, action, parentFieldName + "." + field.Name + "[" + partIndex.ToString() + "]", fsmStrings[partIndex], translations, ref logged, logLabel);
                    }

                    continue;
                }

                if (nestedValue != null && ShouldScanNestedValue(nestedValue))
                {
                    ScanNestedObjectForAnyStrings(path, fsmName, stateName, actionIndex, action, parentFieldName + "." + field.Name, nestedValue, translations, ref logged, maxLogs, logLabel, depth + 1);
                }
            }
        }

        private static bool ShouldScanNestedValue(object value)
        {
            if (value == null)
                return false;

            System.Type type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            string typeName = value.GetType().Name;
            return typeName.IndexOf("Property") >= 0
                || typeName.IndexOf("FsmVar") >= 0
                || typeName.IndexOf("Fsm") >= 0
                || typeName.IndexOf("String") >= 0;
        }

        private static void LogFsmStringIfMatch(
            string path,
            string fsmName,
            string stateName,
            int actionIndex,
            object action,
            string fieldPath,
            HutongGames.PlayMaker.FsmString fsmString,
            HashSet<string> targetKeys,
            ref int logged)
        {
            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value))
                return;

            string normalized = MLCUtils.FormatUpperKey(fsmString.Value);
            if (!targetKeys.Contains(normalized))
                return;

            CoreConsole.PrintAlways(
                "[TextPathDebug] FSMSource path=\"" + path
                + "\" fsm=\"" + fsmName
                + "\" state=\"" + stateName
                + "\" actionIndex=" + actionIndex.ToString()
                + " action=\"" + action.GetType().Name
                + "\" field=\"" + fieldPath
                + "\" text=\"" + Preview(fsmString.Value) + "\"");
            logged++;
        }

        private static void LogAnyFsmString(
            string path,
            string fsmName,
            string stateName,
            int actionIndex,
            object action,
            string fieldPath,
            HutongGames.PlayMaker.FsmString fsmString,
            Dictionary<string, string> translations,
            ref int logged,
            string logLabel)
        {
            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value))
                return;

            string normalized = MLCUtils.FormatUpperKey(fsmString.Value);
            bool hasTranslation = translations != null && translations.ContainsKey(normalized);

            CoreConsole.PrintAlways(
                "[TextPathDebug] " + logLabel + " path=\"" + path
                + "\" fsm=\"" + fsmName
                + "\" state=\"" + stateName
                + "\" actionIndex=" + actionIndex.ToString()
                + " action=\"" + action.GetType().Name
                + "\" field=\"" + fieldPath
                + "\" key=\"" + normalized
                + "\" translatedKey=" + hasTranslation.ToString()
                + " text=\"" + Preview(fsmString.Value) + "\"");
            logged++;
        }

        private static void LogFsmStringVariables(
            string path,
            PlayMakerFSM fsm,
            Dictionary<string, string> translations,
            ref int logged,
            int maxLogs,
            string logLabel)
        {
            if (fsm == null || fsm.FsmVariables == null || fsm.FsmVariables.StringVariables == null)
                return;

            HutongGames.PlayMaker.FsmString[] variables = fsm.FsmVariables.StringVariables;
            for (int i = 0; i < variables.Length && logged < maxLogs; i++)
            {
                HutongGames.PlayMaker.FsmString variable = variables[i];
                if (variable == null || string.IsNullOrEmpty(variable.Value))
                    continue;

                string normalized = MLCUtils.FormatUpperKey(variable.Value);
                bool hasTranslation = translations != null && translations.ContainsKey(normalized);
                string variableName = string.IsNullOrEmpty(variable.Name) ? ("#" + i.ToString()) : variable.Name;

                CoreConsole.PrintAlways(
                    "[TextPathDebug] " + logLabel + " path=\"" + path
                    + "\" fsm=\"" + fsm.FsmName
                    + "\" variable=\"" + variableName
                    + "\" key=\"" + normalized
                    + "\" translatedKey=" + hasTranslation.ToString()
                    + " text=\"" + Preview(variable.Value) + "\"");
                logged++;
            }
        }

        private static void TryInitFsm(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Initialized)
                return;

            try
            {
                fsm.Fsm.InitData();
            }
            catch
            {
            }
        }

        private static string Preview(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string preview = text.Replace("\r", "\\r").Replace("\n", "\\n");
            if (preview.Length > MaxTextPreviewLength)
                preview = preview.Substring(0, MaxTextPreviewLength) + "...";

            return preview;
        }
    }
}
