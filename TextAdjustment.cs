using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Stores original TextMesh state before adjustment
    /// </summary>
    public struct OriginalTextMeshState
    {
        public Vector3 LocalPosition;
        public float CharacterSize;
        public float LineSpacing;
        public float WidthScale;

        public OriginalTextMeshState(TextMesh textMesh)
        {
            LocalPosition = textMesh.transform.localPosition;
            CharacterSize = textMesh.characterSize;
            LineSpacing = textMesh.lineSpacing;
            WidthScale = textMesh.transform.localScale.x;
        }
    }

    /// <summary>
    /// Represents a position adjustment rule with path matching conditions
    /// Supports Contains, EndsWith, StartsWith, and negation (!Contains)
    /// </summary>
    public class TextAdjustment
    {
        public List<PathCondition> Conditions { get; private set; } = new List<PathCondition>();
        public Vector3 Offset { get; private set; }
        public float? FontSize { get; private set; }
        public float? LineSpacing { get; private set; }
        public float? WidthScale { get; private set; }

        // Track which TextMesh objects have been adjusted to prevent duplicate adjustments
        private HashSet<TextMesh> adjustedTextMeshes = new HashSet<TextMesh>();

        // Store original state of adjusted TextMesh objects for restoration
        private Dictionary<TextMesh, OriginalTextMeshState> originalStates = new Dictionary<TextMesh, OriginalTextMeshState>();

        // Last values written by this rule. If the game rewrites a transform later,
        // the next monitor tick can apply the offset again without accumulating it.
        private Dictionary<TextMesh, Vector3> lastAppliedLocalPositions = new Dictionary<TextMesh, Vector3>();
        // PositionToleranceSqr is a squared Vector3.sqrMagnitude threshold, not a raw
        // distance; 1e-7 means about 3.16e-4 units of linear drift.
        private const float PositionToleranceSqr = 0.0000001f;

        public TextAdjustment(string conditionsString, Vector3 offset, float? fontSize = null, float? lineSpacing = null, float? widthScale = null)
        {
            Offset = offset;
            FontSize = fontSize;
            LineSpacing = lineSpacing;
            WidthScale = widthScale;
            ParseConditions(conditionsString);
        }

        /// <summary>
        /// Apply position adjustment to TextMesh if not already adjusted
        /// Uses HashSet to prevent duplicate adjustments
        /// </summary>
        /// <returns>True if adjustment was applied, false if already adjusted</returns>
        public bool ApplyAdjustment(TextMesh textMesh)
        {
            if (textMesh == null)
                return false;

            bool alreadyAdjusted = adjustedTextMeshes.Contains(textMesh);

            // Store original state before applying adjustments (only once, on first application)
            if (!originalStates.ContainsKey(textMesh))
                originalStates[textMesh] = new OriginalTextMeshState(textMesh);

            if (alreadyAdjusted)
            {
                Vector3 lastAppliedPosition;
                if (lastAppliedLocalPositions.TryGetValue(textMesh, out lastAppliedPosition))
                {
                    Vector3 positionDelta = textMesh.transform.localPosition - lastAppliedPosition;
                    if (positionDelta.sqrMagnitude <= PositionToleranceSqr)
                        return false;
                }

                // The game moved this TextMesh after our last adjustment. Keep the
                // original sizing values, but use the current transform as the new base.
                OriginalTextMeshState updatedState = originalStates[textMesh];
                updatedState.LocalPosition = textMesh.transform.localPosition;
                originalStates[textMesh] = updatedState;
            }

            // Apply the position offset
            OriginalTextMeshState originalState = originalStates[textMesh];
            Vector3 targetPosition = originalState.LocalPosition + Offset;
            textMesh.transform.localPosition = targetPosition;

            // Apply font size if specified
            if (FontSize.HasValue)
            {
                textMesh.characterSize = FontSize.Value;
            }

            // Apply line spacing if specified
            if (LineSpacing.HasValue)
            {
                textMesh.lineSpacing = LineSpacing.Value;
            }

            // Apply width scale if specified (scales X axis to make text wider/narrower)
            if (WidthScale.HasValue)
            {
                Vector3 scale = textMesh.transform.localScale;
                scale.x = WidthScale.Value;
                textMesh.transform.localScale = scale;
            }

            // Note: Unity 5.x TextMesh supports: characterSize, fontSize, lineSpacing, transform.localScale
            // Character width is controlled via transform.localScale.x

            // Mark as adjusted
            adjustedTextMeshes.Add(textMesh);
            lastAppliedLocalPositions[textMesh] = targetPosition;

            return true;
        }

        /// <summary>
        /// Restore TextMesh to its original state before adjustment
        /// </summary>
        /// <returns>True if restored, false if no original state exists</returns>
        public bool RestoreOriginal(TextMesh textMesh)
        {
            if (textMesh == null || !originalStates.ContainsKey(textMesh))
                return false;

            OriginalTextMeshState originalState = originalStates[textMesh];

            // Restore original values
            textMesh.transform.localPosition = originalState.LocalPosition;
            textMesh.characterSize = originalState.CharacterSize;
            textMesh.lineSpacing = originalState.LineSpacing;

            Vector3 scale = textMesh.transform.localScale;
            scale.x = originalState.WidthScale;
            textMesh.transform.localScale = scale;

            // Remove from adjusted set so it can be re-adjusted
            adjustedTextMeshes.Remove(textMesh);
            lastAppliedLocalPositions.Remove(textMesh);

            return true;
        }

        /// <summary>
        /// Clear the cache of adjusted TextMesh objects and restore all to original state
        /// Useful for F8/F9 reload functionality
        /// </summary>
        public void ClearCache()
        {
            // Restore all adjusted TextMeshes to their original state before clearing
            foreach (var textMesh in new List<TextMesh>(originalStates.Keys))
            {
                if (textMesh != null)
                    RestoreOriginal(textMesh);
            }
            // adjustedTextMeshes is already cleared by RestoreOriginal() calls
            originalStates.Clear();
            lastAppliedLocalPositions.Clear();
        }

        /// <summary>
        /// Parse condition string (e.g., "Contains(GUI/HUD) & EndsWith(/HUDLabel)")
        /// </summary>
        private void ParseConditions(string conditionsString)
        {
            string[] parts = conditionsString.Split('&');

            foreach (string part in parts)
            {
                string trimmed = part.Trim();

                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Check for negation
                bool negate = trimmed.StartsWith("!");
                if (negate)
                    trimmed = trimmed.Substring(1).Trim();

                // Parse condition type and value
                if (trimmed.StartsWith("Contains(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(9, trimmed.Length - 10);
                    Conditions.Add(new PathCondition(ConditionType.Contains, value, negate));
                }
                else if (trimmed.StartsWith("EndsWith(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(9, trimmed.Length - 10);
                    Conditions.Add(new PathCondition(ConditionType.EndsWith, value, negate));
                }
                else if (trimmed.StartsWith("StartsWith(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(11, trimmed.Length - 12);
                    Conditions.Add(new PathCondition(ConditionType.StartsWith, value, negate));
                }
                else if (trimmed.StartsWith("Equals(") && trimmed.EndsWith(")"))
                {
                    string value = trimmed.Substring(7, trimmed.Length - 8);
                    Conditions.Add(new PathCondition(ConditionType.Equals, value, negate));
                }
            }
        }

        /// <summary>
        /// Check if the given path matches all conditions
        /// </summary>
        public bool Matches(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            foreach (PathCondition condition in Conditions)
            {
                if (!condition.Evaluate(path))
                    return false;
            }

            return Conditions.Count > 0; // At least one condition must exist
        }
    }

    /// <summary>
    /// Represents a single path matching condition
    /// </summary>
    public class PathCondition
    {
        public ConditionType Type { get; private set; }
        public string Value { get; private set; }
        public bool Negate { get; private set; }

        public PathCondition(ConditionType type, string value, bool negate = false)
        {
            Type = type;
            Value = value;
            Negate = negate;
        }

        /// <summary>
        /// Evaluate if the path satisfies this condition
        /// </summary>
        public bool Evaluate(string path)
        {
            bool result = false;

            switch (Type)
            {
                case ConditionType.Contains:
                    result = path.Contains(Value);
                    break;

                case ConditionType.EndsWith:
                    result = path.EndsWith(Value);
                    break;

                case ConditionType.StartsWith:
                    result = path.StartsWith(Value);
                    break;

                case ConditionType.Equals:
                    result = path == Value;
                    break;
            }

            // Apply negation if needed
            return Negate ? !result : result;
        }
    }

    /// <summary>
    /// Types of path matching conditions
    /// </summary>
    public enum ConditionType
    {
        Contains,
        EndsWith,
        StartsWith,
        Equals
    }
}
