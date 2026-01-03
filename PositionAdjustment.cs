using BepInEx.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Represents a position adjustment rule with path matching conditions
    /// Supports Contains, EndsWith, StartsWith, and negation (!Contains)
    /// </summary>
    public class PositionAdjustment
    {
        public List<PathCondition> Conditions { get; private set; } = new List<PathCondition>();
        public Vector3 Offset { get; private set; }

        // Track which TextMesh objects have been adjusted to prevent duplicate adjustments
        private HashSet<TextMesh> adjustedTextMeshes = new HashSet<TextMesh>();

        public PositionAdjustment(string conditionsString, Vector3 offset)
        {
            Offset = offset;
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

            // Skip if already adjusted
            if (adjustedTextMeshes.Contains(textMesh))
                return false;

            // Apply the offset
            Vector3 currentPosition = textMesh.transform.localPosition;
            textMesh.transform.localPosition = currentPosition + Offset;

            // Mark as adjusted
            adjustedTextMeshes.Add(textMesh);

            return true;
        }

        /// <summary>
        /// Clear the cache of adjusted TextMesh objects
        /// Useful for F9 reload functionality
        /// </summary>
        public void ClearCache()
        {
            adjustedTextMeshes.Clear();
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
