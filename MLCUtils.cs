using UnityEngine;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// String normalization utilities for localization
    /// </summary>
    public static class MLCUtils
    {
        // LRU cache for FormatUpperKey to reduce allocations in hot path
        private static Dictionary<string, string> formatKeyCache = new Dictionary<string, string>();
        private const int FORMAT_KEY_CACHE_MAX = 256;

        /// <summary>
        /// Format string for use as translation key (uppercase, no spaces/newlines)
        /// Cached to reduce allocations in hot path
        /// </summary>
        public static string FormatUpperKey(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            // Check cache first
            if (formatKeyCache.TryGetValue(original, out string cached))
                return cached;

            // Single-pass: skip whitespace chars and uppercase in one allocation
            char[] buffer = new char[original.Length];
            int len = 0;
            for (int i = 0; i < original.Length; i++)
            {
                char c = original[i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                    continue;
                buffer[len++] = char.ToUpperInvariant(c);
            }

            if (len == 0)
                return string.Empty;

            string result = new string(buffer, 0, len);
            
            // Cache result (with limit to prevent unbounded growth)
            if (formatKeyCache.Count < FORMAT_KEY_CACHE_MAX)
                formatKeyCache[original] = result;

            return result;
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
        /// Clear all runtime caches.
        /// Call this on scene changes and reloads.
        /// </summary>
        public static void ClearCaches()
        {
            pathCache.Clear();
            gameObjectFindCache.Clear();
            inactiveFsmPathNameCache.Clear();
            ClearFormatKeyCache();
        }

        /// <summary>
        /// Clear FormatUpperKey cache.
        /// Call this on scene changes to prevent memory bloat.
        /// </summary>
        public static void ClearFormatKeyCache()
        {
            formatKeyCache.Clear();
        }
    }
}
