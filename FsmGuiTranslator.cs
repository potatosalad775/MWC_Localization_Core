using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Translates the HUD Interaction and Subtitles meshes by splicing a translation
    /// action right after each PlayMaker SetProperty that writes a TextMesh's "text".
    ///
    /// Both indicators share the same FSM shape: an "SetText" FSM contains a state
    /// with two SetProperty actions - one writing the visible TextMesh, the other the
    /// child Shadow TextMesh - both sourced from a global FsmString (GUIinteraction
    /// or GUIsubtitle). Splicing once per state injects translation after both calls,
    /// so primary + shadow are covered without any per-frame polling.
    ///
    /// Strategy:
    ///   1. At InitialPass, for each target (ObjectPath + FsmName + StateName), find
    ///      the GameObject, the named FSM, and the named state. For every SetProperty
    ///      action in that state whose property is "text" and whose target resolves
    ///      to a TextMesh, splice a TranslateTextMeshAction directly after it. When
    ///      the FSM runs the state, SetProperty writes the source string, then our
    ///      injected action immediately translates the TextMesh's text in place.
    ///   2. Once a target's state has been walked, it is never revisited (the splice
    ///      is idempotent under F8 reload via the in-state guard below).
    /// </summary>
    public class FsmGuiTranslator : ITranslationSurface
    {
        public string Name { get { return "FsmGuiTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return processedTargets.Count >= targets.Length; } }

        private TranslationDictionary translations;

        private sealed class IndicatorTarget
        {
            public readonly string ObjectPath;
            public readonly string FsmName;
            public readonly string StateName;
            public readonly string Key;

            public IndicatorTarget(string objectPath, string fsmName, string stateName)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                StateName = stateName;
                Key = objectPath + "|" + fsmName + "|" + stateName;
            }
        }

        private readonly IndicatorTarget[] targets = new IndicatorTarget[]
        {   
            new IndicatorTarget("GUI/Indicators/Partname",       "Partname", "On"),
            new IndicatorTarget("GUI/Indicators/Interaction",    "SetText",  "State 1"),
            new IndicatorTarget("GUI/Indicators/RallyCountdown", "SetText",  "State 1"),
            new IndicatorTarget("GUI/Indicators/Gear",           "SetText",  "State 1"),
            new IndicatorTarget("GUI/Indicators/Subtitles",      "SetText",  "State 3"),
        };

        private readonly HashSet<string> processedTargets = new HashSet<string>();

        // Tracks FSM instances we've already asked PlayMaker to deserialize. Inactive
        // GameObjects' FSMs leave action runtime data uninitialized until first use,
        // so reflection on SetProperty.targetProperty NRE-s without an explicit
        // InitData() call. Same pattern as FsmTextHook.IsFsmReady.
        private readonly HashSet<int> initializedFsmIds = new HashSet<int>();

        // Subset of target keys whose state should also get a RefreshPartnameLayoutAction
        // appended at the end of the action list. These are the indicators whose visible
        // line count contributes to Partname's vertical offset.
        private readonly HashSet<string> layoutContributorKeys = new HashSet<string>
        {
            "GUI/Indicators/Partname|Partname|On",
            "GUI/Indicators/Subtitles|SetText|State 3",
        };

        // Shared helper that captures Partname's base position once and applies the
        // line-count-driven Y offset. All injected RefreshPartnameLayoutAction
        // instances share this single helper so they see a consistent base position.
        private readonly PartnameLayoutHelper layoutHelper = new PartnameLayoutHelper();

        public void Initialize(TranslationContext ctx)
        {
            translations = ctx.Translations;
        }

        public int InitialPass()
        {
            return ProcessAllTargets();
        }

        public int MonitorTick(float deltaTime)
        {
            return ProcessAllTargets();
        }

        public void Reset()
        {
            processedTargets.Clear();
            initializedFsmIds.Clear();
            layoutHelper.Reset();
        }

        public void ClearTranslations()
        {
            // No owned translation data; just drop runtime state so the next pass
            // re-walks and re-confirms the splice is in place.
            Reset();
        }

        private int ProcessAllTargets()
        {
            int totalInjected = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                IndicatorTarget t = targets[i];
                if (processedTargets.Contains(t.Key))
                    continue;

                try
                {
                    GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(t.ObjectPath);
                    if (go == null) continue;

                    PlayMakerFSM fsm = FindFsmByName(go, t.FsmName);
                    if (fsm == null) continue;

                    // Inactive FSMs leave serialized action fields uninitialized; force
                    // deserialization so reflection on targetProperty etc. is safe.
                    if (!EnsureFsmInitialized(fsm) || fsm.FsmStates == null) continue;

                    HutongGames.PlayMaker.FsmState state = FindState(fsm, t.StateName);
                    if (state == null || state.Actions == null) continue;

                    bool appendLayout = layoutContributorKeys.Contains(t.Key);
                    int injected = SpliceTranslationActionsInto(state, appendLayout);
                    if (injected > 0)
                        CoreConsole.Print($"[FsmGuiTranslator] Injected {injected} action(s) into {t.ObjectPath} ({t.FsmName}/{t.StateName})");

                    processedTargets.Add(t.Key);
                    totalInjected += injected;
                }
                catch (System.Exception ex)
                {
                    // Per-target catch so one bad target doesn't abort the rest of the loop.
                    // Don't mark processed: we'll retry next tick once the FSM is ready.
                    CoreConsole.Error($"[FsmGuiTranslator] Failed processing {t.ObjectPath} ({t.FsmName}/{t.StateName}): {ex.Message}");
                }
            }
            return totalInjected;
        }

        private static PlayMakerFSM FindFsmByName(GameObject go, string fsmName)
        {
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] != null && FsmUtils.GetFsmName(fsms[i]) == fsmName)
                    return fsms[i];
            }
            return null;
        }

        private bool EnsureFsmInitialized(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null) return false;
            if (fsm.Fsm.Initialized) return true;

            int id = fsm.GetInstanceID();
            if (!initializedFsmIds.Contains(id))
            {
                initializedFsmIds.Add(id);
                try
                {
                    fsm.Fsm.InitData();
                }
                catch
                {
                    // Some FSMs are not safe to early-init; skip and retry next tick
                    // (id stays in the set so we don't loop forever).
                }
            }
            return fsm.FsmStates != null;
        }

        private static HutongGames.PlayMaker.FsmState FindState(PlayMakerFSM fsm, string stateName)
        {
            HutongGames.PlayMaker.FsmState[] states = fsm.FsmStates;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].Name == stateName)
                    return states[i];
            }
            return null;
        }

        private int SpliceTranslationActionsInto(HutongGames.PlayMaker.FsmState state, bool appendLayoutAction)
        {
            HutongGames.PlayMaker.FsmStateAction[] oldActions = state.Actions;

            // F8-reload guard: if this state already carries one of our injected
            // actions (translation or layout), the previous injection is still in
            // place. The translations reference and layoutHelper instance are stable
            // across reloads, so updated translations and base positions are picked
            // up automatically.
            for (int i = 0; i < oldActions.Length; i++)
            {
                if (oldActions[i] is TranslateTextMeshAction || oldActions[i] is RefreshPartnameLayoutAction)
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
                if (action.GetType().Name != "SetProperty") continue;

                TranslateTextMeshAction inj = BuildInjectionAction(action);
                if (inj == null) continue;

                // First match in this state - lazy-init the new action list with
                // everything seen so far.
                if (newActions == null)
                {
                    newActions = new List<HutongGames.PlayMaker.FsmStateAction>(oldActions.Length + 3);
                    for (int j = 0; j <= ai; j++)
                        newActions.Add(oldActions[j]);
                }
                newActions.Add(inj);
                injected++;
            }

            // Append the layout-refresh action at the very end for layout-contributing
            // states. Runs after every other action in the state, so the TextMesh
            // contents it reads are final (post-translation). Injected even when no
            // translation splices fired, since this state may still need to drive
            // Partname's offset when its line count changes.
            if (appendLayoutAction)
            {
                if (newActions == null)
                {
                    newActions = new List<HutongGames.PlayMaker.FsmStateAction>(oldActions.Length + 1);
                    for (int j = 0; j < oldActions.Length; j++)
                        newActions.Add(oldActions[j]);
                }
                newActions.Add(new RefreshPartnameLayoutAction { helper = layoutHelper });
                injected++;
            }

            if (newActions != null)
                state.Actions = newActions.ToArray();

            return injected;
        }

        private TranslateTextMeshAction BuildInjectionAction(HutongGames.PlayMaker.FsmStateAction setPropertyAction)
        {
            FieldInfo targetPropertyField = FsmUtils.GetField(setPropertyAction.GetType(), "targetProperty");
            if (targetPropertyField == null) return null;

            object targetProperty = targetPropertyField.GetValue(setPropertyAction);
            if (targetProperty == null) return null;

            FieldInfo propertyNameField = FsmUtils.GetField(targetProperty.GetType(), "PropertyName");
            if (propertyNameField == null) return null;

            string propertyName = propertyNameField.GetValue(targetProperty) as string;
            if (string.IsNullOrEmpty(propertyName)
                || !string.Equals(propertyName, "text", System.StringComparison.OrdinalIgnoreCase))
                return null;

            TextMesh textMesh = ResolveTargetTextMesh(targetProperty);
            if (textMesh == null) return null;

            return new TranslateTextMeshAction
            {
                target = textMesh,
                translations = translations,
                label = textMesh.gameObject != null ? textMesh.gameObject.name : "?",
            };
        }

        // PlayMaker's FsmProperty.TargetObject can resolve to either the component
        // directly (when the user picks a TextMesh in the editor) or to the parent
        // GameObject. Handle both shapes.
        private static TextMesh ResolveTargetTextMesh(object targetProperty)
        {
            FieldInfo targetObjectField = FsmUtils.GetField(targetProperty.GetType(), "TargetObject");
            if (targetObjectField == null) return null;

            HutongGames.PlayMaker.FsmObject fsmObject = targetObjectField.GetValue(targetProperty) as HutongGames.PlayMaker.FsmObject;
            if (fsmObject == null) return null;

            UnityEngine.Object unityObject = fsmObject.Value;
            if (unityObject == null) return null;

            TextMesh asTextMesh = unityObject as TextMesh;
            if (asTextMesh != null) return asTextMesh;

            GameObject asGameObject = unityObject as GameObject;
            return asGameObject != null ? asGameObject.GetComponent<TextMesh>() : null;
        }

        /// <summary>
        /// Custom FsmStateAction spliced after each SetProperty that writes a
        /// TextMesh's "text". When the state runs, SetProperty writes the original
        /// Finnish source from a global FsmString; this action then immediately
        /// translates the TextMesh's text in place before any subsequent action or
        /// reader sees it.
        ///
        /// Both OnEnter and OnUpdate translate, and Finish() is deliberately not
        /// called: if the preceding SetProperty has everyFrame=true (Interaction's
        /// FSM does), it rewrites Finnish into the TextMesh every frame the state
        /// is active, so we have to re-translate every frame too. The dict lookup
        /// is LRU-cached so the per-frame cost is a hash hit plus a TextMesh.text
        /// assignment.
        /// </summary>
        public class TranslateTextMeshAction : HutongGames.PlayMaker.FsmStateAction
        {
            public TextMesh target;
            public TranslationDictionary translations;
            public string label;

            public override void OnEnter()
            {
                TranslateNow();
            }

            public override void OnUpdate()
            {
                TranslateNow();
            }

            private void TranslateNow()
            {
                try
                {
                    if (target == null || target.gameObject == null) return;

                    string source = target.text;
                    if (string.IsNullOrEmpty(source) || translations == null) return;

                    if (!translations.TryTranslate(source, null, out string translated)) return;
                    if (string.IsNullOrEmpty(translated) || translated == source) return;

                    target.text = translated;
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Error($"[FsmGuiTranslator] Translate error ({label}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Holds Partname's base local position and the three TextMesh refs whose
        /// line counts drive the multi-line Y offset. Owned by FsmGuiTranslator
        /// and shared across every injected RefreshPartnameLayoutAction so they see a
        /// consistent base position. Replaces the per-frame loop the old
        /// GuiTextMonitor.UpdatePartnameMultilineLayout used to run.
        /// </summary>
        public class PartnameLayoutHelper
        {
            private const string PartnamePath = "GUI/Indicators/Partname";
            private const string InteractionPath = "GUI/Indicators/Interaction";
            private const string SubtitlesPath = "GUI/Indicators/Subtitles";

            // Same magic numbers the old GuiTextMonitor used; preserving the existing
            // tuned offsets keeps the visual behavior identical for translated text.
            private const float SingleLineOffset = 0f;
            private const float TwoLineOffset = 0.74f;
            private const float PerExtraLineStep = 0.50f;
            private const float MaxOffset = 2.24f;

            private TextMesh partnamePrimary;
            private TextMesh interactionMesh;
            private TextMesh subtitlesMesh;
            private Vector3 partnameBaseLocalPosition;
            private bool hasBasePosition;

            public void Refresh()
            {
                if (partnamePrimary == null || partnamePrimary.gameObject == null)
                {
                    partnamePrimary = ResolveTextMesh(PartnamePath);
                    if (partnamePrimary == null) return;
                }
                if (interactionMesh == null || interactionMesh.gameObject == null)
                    interactionMesh = ResolveTextMesh(InteractionPath);
                if (subtitlesMesh == null || subtitlesMesh.gameObject == null)
                    subtitlesMesh = ResolveTextMesh(SubtitlesPath);

                Transform t = partnamePrimary.transform;
                if (!hasBasePosition)
                {
                    // First call - capture Partname's current local position as the
                    // neutral baseline. Assumes no other code has nudged Partname yet
                    // (matches the assumption the old GuiTextMonitor made).
                    partnameBaseLocalPosition = t.localPosition;
                    hasBasePosition = true;
                }

                int maxLines = CountLines(partnamePrimary);
                int interactionLines = CountLines(interactionMesh);
                if (interactionLines > maxLines) maxLines = interactionLines;
                int subtitleLines = CountLines(subtitlesMesh);
                if (subtitleLines > maxLines) maxLines = subtitleLines;

                float offsetY;
                if (maxLines <= 1)
                {
                    offsetY = SingleLineOffset;
                }
                else
                {
                    offsetY = TwoLineOffset + (maxLines - 2) * PerExtraLineStep;
                    if (offsetY > MaxOffset) offsetY = MaxOffset;
                }

                Vector3 target = new Vector3(
                    partnameBaseLocalPosition.x,
                    partnameBaseLocalPosition.y + offsetY,
                    partnameBaseLocalPosition.z);

                // Skip the write if Partname is already at the target position to
                // avoid triggering unnecessary transform-changed callbacks. Shadow is
                // a child of Partname (per the GameObject hierarchy path) so it rides
                // along automatically when the parent's local position changes.
                if (t.localPosition != target)
                    t.localPosition = target;
            }

            public void Reset()
            {
                partnamePrimary = null;
                interactionMesh = null;
                subtitlesMesh = null;
                hasBasePosition = false;
            }

            private static TextMesh ResolveTextMesh(string path)
            {
                GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(path);
                return go != null ? go.GetComponent<TextMesh>() : null;
            }

            private static int CountLines(TextMesh textMesh)
            {
                if (textMesh == null || textMesh.gameObject == null || !textMesh.gameObject.activeInHierarchy)
                    return 1;

                string text = textMesh.text;
                if (string.IsNullOrEmpty(text)) return 1;

                int lines = 1;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n') lines++;
                }
                return lines;
            }
        }

        /// <summary>
        /// Appended at the end of each layout-contributing state (Partname/On,
        /// Interaction/State 1, Subtitles/State 3). Whenever the state enters, ticks,
        /// or exits, recomputes Partname's Y offset against the current live line
        /// counts of all three indicators. OnExit ensures the offset relaxes when an
        /// indicator's display state transitions out.
        /// </summary>
        public class RefreshPartnameLayoutAction : HutongGames.PlayMaker.FsmStateAction
        {
            public PartnameLayoutHelper helper;

            public override void OnEnter()
            {
                RefreshSafely();
            }

            public override void OnUpdate()
            {
                RefreshSafely();
            }

            public override void OnExit()
            {
                RefreshSafely();
            }

            private void RefreshSafely()
            {
                try
                {
                    if (helper != null) helper.Refresh();
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Error($"[FsmGuiTranslator] Layout refresh error: {ex.Message}");
                }
            }
        }
    }
}
