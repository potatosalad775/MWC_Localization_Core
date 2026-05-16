using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MSC_Localization_Core
{
    /// <summary>
    /// String normalization and GameObject path/find caching used throughout the localization pipeline.
    /// </summary>
    public static class LocalizationUtils
    {
        /// <summary>
        /// Format string for use as translation key (uppercase, no spaces/newlines)
        /// </summary>
        public static string FormatUpperKey(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

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

            return new string(buffer, 0, len);
        }

        // Cache for GameObject paths to improve performance
        private static Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();
        // Cache for expensive GameObject.Find(path) lookups
        private static Dictionary<string, GameObject> gameObjectFindCache = new Dictionary<string, GameObject>();
        // Paths confirmed absent after a full inactive-object scan; cleared on scene change / F8.
        private static HashSet<string> notFoundPaths = new HashSet<string>();

        // Reusable scratch buffers for path construction. Unity is single-threaded so no locking needed.
        private static Transform[] pathChain = new Transform[32];
        private static readonly StringBuilder pathBuilder = new StringBuilder(128);

        public static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            // Check cache first
            if (pathCache.TryGetValue(obj, out string cachedPath))
                return cachedPath;

            // Walk parents into a reusable buffer, then build forward into a reusable StringBuilder.
            // Avoids List<string>/Reverse/ToArray/string.Join allocations on every uncached call.
            int depth = 0;
            Transform current = obj.transform;
            while (current != null)
            {
                if (depth >= pathChain.Length)
                {
                    Transform[] bigger = new Transform[pathChain.Length * 2];
                    System.Array.Copy(pathChain, bigger, depth);
                    pathChain = bigger;
                }
                pathChain[depth++] = current;
                current = current.parent;
            }

            pathBuilder.Length = 0;
            for (int i = depth - 1; i >= 0; i--)
            {
                if (pathBuilder.Length > 0)
                    pathBuilder.Append('/');
                pathBuilder.Append(pathChain[i].name);
                pathChain[i] = null;
            }

            string path = pathBuilder.ToString();

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
        /// Like FindGameObjectCached but also searches inactive GameObjects.
        /// Falls back to Resources.FindObjectsOfTypeAll when the active-only
        /// GameObject.Find misses. The result is cached for future calls.
        /// </summary>
        public static GameObject FindGameObjectIncludingInactive(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            GameObject found = FindGameObjectCached(path);
            if (found != null)
                return found;

            // Skip the expensive scan if a previous call already confirmed this path absent.
            if (notFoundPaths.Contains(path))
                return null;

            // Pre-filter by leaf name before building full paths.
            string leafName = path.Substring(path.LastIndexOf('/') + 1);
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != leafName)
                    continue;
                if (GetGameObjectPath(t.gameObject) == path)
                {
                    gameObjectFindCache[path] = t.gameObject;
                    return t.gameObject;
                }
            }

            notFoundPaths.Add(path);
            return null;
        }

        /// <summary>
        /// Like FindGameObjectIncludingInactive but returns every GameObject whose
        /// full hierarchy path equals <paramref name="path"/>. The game has sibling
        /// objects sharing an identical path (e.g. TV forecast page 188 slots), and
        /// the single-object find/cache cannot represent them. Not globally cached;
        /// callers should cache the result.
        /// </summary>
        public static List<GameObject> FindAllGameObjectsIncludingInactive(string path)
        {
            List<GameObject> result = new List<GameObject>();
            if (string.IsNullOrEmpty(path))
                return result;

            // Pre-filter by leaf name before building full paths.
            string leafName = path.Substring(path.LastIndexOf('/') + 1);
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != leafName)
                    continue;
                if (GetGameObjectPath(t.gameObject) == path)
                    result.Add(t.gameObject);
            }

            return result;
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
        /// Use on F8 reload. For scene changes prefer PruneCaches so static paths stay warm.
        /// </summary>
        public static void ClearCaches()
        {
            pathCache.Clear();
            gameObjectFindCache.Clear();
            notFoundPaths.Clear();
        }

        /// <summary>
        /// Drop only entries that reference destroyed Unity objects.
        /// Lets stable paths (HUD, indicators) survive a MainMenu->GAME transition cold-start free.
        /// </summary>
        public static void PruneCaches()
        {
            // Path cache: Unity overrides == to return true for destroyed objects, so we can detect them.
            // Dictionary still tracks them by reference identity, so Remove(key) works.
            List<GameObject> stalePathKeys = null;
            foreach (KeyValuePair<GameObject, string> entry in pathCache)
            {
                if (entry.Key == null)
                {
                    if (stalePathKeys == null) stalePathKeys = new List<GameObject>();
                    stalePathKeys.Add(entry.Key);
                }
            }
            if (stalePathKeys != null)
            {
                for (int i = 0; i < stalePathKeys.Count; i++)
                    pathCache.Remove(stalePathKeys[i]);
            }

            List<string> staleFindKeys = null;
            foreach (KeyValuePair<string, GameObject> entry in gameObjectFindCache)
            {
                if (entry.Value == null)
                {
                    if (staleFindKeys == null) staleFindKeys = new List<string>();
                    staleFindKeys.Add(entry.Key);
                }
            }
            if (staleFindKeys != null)
            {
                for (int i = 0; i < staleFindKeys.Count; i++)
                    gameObjectFindCache.Remove(staleFindKeys[i]);
            }

            // A path absent in one scene may exist in the next; always reset on scene change.
            notFoundPaths.Clear();
        }
    }

    /// <summary>
    /// PlayMaker FSM reflection helpers shared by translation hooks.
    /// Caches FieldInfo lookups to avoid repeated reflection costs during scene scans.
    /// </summary>
    internal static class FsmUtils
    {
        private static readonly Dictionary<System.Type, FieldInfo[]> fieldCache = new Dictionary<System.Type, FieldInfo[]>();
        private static readonly Dictionary<System.Type, FieldInfo> stringPartsFieldCache = new Dictionary<System.Type, FieldInfo>();

        public static string GetFsmName(PlayMakerFSM fsm)
        {
            if (fsm == null)
                return string.Empty;

            if (fsm.Fsm != null && !string.IsNullOrEmpty(fsm.Fsm.Name))
                return fsm.Fsm.Name;

            return fsm.FsmName ?? string.Empty;
        }

        public static FieldInfo GetField(System.Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static FieldInfo[] GetFields(System.Type type)
        {
            FieldInfo[] fields;
            if (!fieldCache.TryGetValue(type, out fields))
            {
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                fieldCache[type] = fields;
            }

            return fields;
        }

        public static bool SetFsmStringValue(HutongGames.PlayMaker.FsmString target, string value)
        {
            if (target == null)
                return false;

            string safeValue = value ?? string.Empty;
            if (target.Value == safeValue)
                return false;

            target.Value = safeValue;
            return true;
        }

        public static bool SetNestedStringValue(object root, string value, params string[] fieldPath)
        {
            if (root == null || fieldPath == null || fieldPath.Length == 0)
                return false;

            object current = root;
            FieldInfo field = null;
            for (int i = 0; i < fieldPath.Length; i++)
            {
                field = GetField(current.GetType(), fieldPath[i]);
                if (field == null)
                    return false;

                if (i == fieldPath.Length - 1)
                    break;

                current = field.GetValue(current);
                if (current == null)
                    return false;
            }

            object existing = field.GetValue(current);
            HutongGames.PlayMaker.FsmString fsmString = existing as HutongGames.PlayMaker.FsmString;
            if (fsmString != null)
                return SetFsmStringValue(fsmString, value);

            if (field.FieldType == typeof(string))
            {
                string safeValue = value ?? string.Empty;
                if ((string)existing == safeValue)
                    return false;

                field.SetValue(current, safeValue);
                return true;
            }

            return false;
        }

        public static bool IsBuildStringAction(object action)
        {
            if (action == null)
                return false;

            string typeName = action.GetType().Name;
            return typeName == "BuildString" || typeName == "BuildStringFast" || typeName == "StringAddNewLine";
        }

        public static FieldInfo GetStringPartsField(object action)
        {
            if (action == null)
                return null;

            System.Type type = action.GetType();
            FieldInfo field;
            if (!stringPartsFieldCache.TryGetValue(type, out field))
            {
                field = GetField(type, "stringParts");
                stringPartsFieldCache[type] = field;
            }

            return field;
        }
    }
}
