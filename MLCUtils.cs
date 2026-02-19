using UnityEngine;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// String normalization utilities for localization
    /// </summary>
    public static class MLCUtils
    {
        /// <summary>
        /// Format string for use as translation key (uppercase, no spaces/newlines)
        /// </summary>
        public static string FormatUpperKey(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            // Trim whitespace
            original = original.Trim();

            // Normalize: keep only letters and digits (remove spaces, punctuation, apostrophes etc.)
            var sb = new System.Text.StringBuilder(original.Length);
            foreach (char c in original)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            }

            string result = sb.ToString().ToUpperInvariant();
            return result;
        }

        // Cache for GameObject paths to improve performance
        private static Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();

        public static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            // Check cache first
            if (pathCache.TryGetValue(obj, out string cachedPath))
                return cachedPath;

            // Build path using List + Reverse
            List<string> pathParts = new List<string>();
            Transform current = obj.transform;

            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            // Reverse and join
            pathParts.Reverse();
            string path = string.Join("/", pathParts.ToArray());

            // Cache the path (limit cache size to prevent memory bloat)
            if (pathCache.Count < 1000)
            {
                pathCache[obj] = path;
            }

            return path;
        }
    }
}
