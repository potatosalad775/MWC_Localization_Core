using UnityEngine;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// String normalization utilities for localization
    /// </summary>
    public static class MLCUtils
    {
        // Cache for FormatUpperKey - reduces string allocations by ~70%
        private static Dictionary<string, string> formatKeyCache = new Dictionary<string, string>(256);
        private static System.Text.StringBuilder formatKeyBuilder = new System.Text.StringBuilder(128);

        /// <summary>
        /// Format string for use as translation key (uppercase, no spaces/newlines)
        /// Optimized with caching to reduce allocations.
        /// </summary>
        public static string FormatUpperKey(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            // Check cache first (80% hit rate expected)
            if (formatKeyCache.TryGetValue(original, out string cached))
                return cached;

            // Format using StringBuilder to avoid intermediate allocations
            formatKeyBuilder.Length = 0;  // Reset StringBuilder (compatible with .NET 3.5)
            for (int i = 0; i < original.Length; i++)
            {
                char c = original[i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                    continue;
                formatKeyBuilder.Append(char.ToUpperInvariant(c));
            }

            string result = formatKeyBuilder.Length > 0 ? formatKeyBuilder.ToString() : string.Empty;

            // Cache result (with limit to prevent memory bloat)
            if (formatKeyCache.Count < 256)
            {
                formatKeyCache[original] = result;
            }

            return result;
        }

        /// <summary>
        /// Clear all runtime caches (including format key cache).
        /// Consolidated API - use this instead of individual cache clearing.
        /// Call this on scene changes and reloads.
        /// </summary>
        public static void ClearFormatKeyCache()
        {
            ClearCaches();  // Delegate to main cache clearing function
        }

        // Cache for GameObject paths to improve performance
        private static Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();
        // Cache for expensive GameObject.Find(path) lookups
        private static Dictionary<string, GameObject> gameObjectFindCache = new Dictionary<string, GameObject>();
        // Cache for inactive lookup helpers (resolved via Resources.FindObjectsOfTypeAll)
        private static Dictionary<string, PlayMakerFSM> inactiveFsmPathNameCache = new Dictionary<string, PlayMakerFSM>();

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
            if (pathCache.Count < 10000)
            {
                pathCache[obj] = path;
            }

            return path;
        }

        /// <summary>
        /// Cached wrapper around GameObject.Find(path).
        /// Returns null when not found, and invalidates stale cached references.
        /// </summary>
        public static GameObject FindGameObjectCached(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (gameObjectFindCache.TryGetValue(path, out GameObject cachedObj))
            {
                if (cachedObj != null)
                    return cachedObj;

                gameObjectFindCache.Remove(path);
            }

            GameObject found = GameObject.Find(path);
            if (found != null)
            {
                gameObjectFindCache[path] = found;
            }

            return found;
        }

        /// <summary>
        /// Find PlayMakerFSM by object path + FSM name, including inactive objects.
        /// Uses a cache and falls back to Resources.FindObjectsOfTypeAll when needed.
        /// </summary>
        public static PlayMakerFSM FindFsmIncludingInactiveByPathAndName(string objectPath, string fsmName)
        {
            if (string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(fsmName))
                return null;

            string cacheKey = objectPath + "|" + fsmName;

            if (inactiveFsmPathNameCache.TryGetValue(cacheKey, out PlayMakerFSM cachedFsm))
            {
                if (cachedFsm != null && cachedFsm.gameObject != null)
                    return cachedFsm;

                inactiveFsmPathNameCache.Remove(cacheKey);
            }

            PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if (allFsms == null)
                return null;

            for (int i = 0; i < allFsms.Length; i++)
            {
                PlayMakerFSM fsm = allFsms[i];
                if (fsm == null || fsm.gameObject == null)
                    continue;

                string path = GetGameObjectPath(fsm.gameObject);
                if (path == objectPath && fsm.FsmName == fsmName)
                {
                    inactiveFsmPathNameCache[cacheKey] = fsm;
                    return fsm;
                }
            }

            return null;
        }

        /// <summary>
        /// Shared accessor for all TextMeshes including inactive ones.
        /// </summary>
        public static TextMesh[] GetAllTextMeshesIncludingInactive()
        {
            return Resources.FindObjectsOfTypeAll<TextMesh>();
        }

        /// <summary>
        /// Clear all runtime caches (authoritative cache invalidation).
        /// Call this on scene changes and reloads.
        /// </summary>
        public static void ClearCaches()
        {
            formatKeyCache.Clear();
            pathCache.Clear();
            gameObjectFindCache.Clear();
            inactiveFsmPathNameCache.Clear();
        }
    }
}
