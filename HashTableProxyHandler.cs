using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Translates PlayMakerHashTableProxy data used by the magazine keyword system.
    /// Updates the live hash table, snapshot, and prefill string list so runtime and reset paths stay consistent.
    /// </summary>
    public class HashTableProxyHandler : ITranslationSurface
    {
        public string Name { get { return "HashTableProxyHandler"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return IsMonitoringComplete; } }
        public bool IsMonitoringComplete { get { return translatedPaths.Count >= targetPaths.Count; } }

        private TranslationDictionary translations;
        private readonly HashSet<string> targetPaths = new HashSet<string>();
        private readonly HashSet<string> translatedPaths = new HashSet<string>();
        private readonly Dictionary<string, PlayMakerHashTableProxy[]> proxyCache = new Dictionary<string, PlayMakerHashTableProxy[]>();

        private static readonly FieldInfo PreFillStringListField =
            typeof(PlayMakerHashTableProxy).GetField("preFillStringList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public void Initialize(TranslationContext ctx)
        {
            translations = ctx.Translations;
            InitializeTargetPaths();
        }

        public int InitialPass()
        {
            return TranslateAllHashTables();
        }

        public int MonitorTick(float deltaTime)
        {
            return MonitorAndTranslateHashTables();
        }

        public void InitializeTargetPaths()
        {
            targetPaths.Clear();
            targetPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsFI");
            targetPaths.Add("CARPARTS/PARTSYSTEM/PostSystem/KeywordsEN");
        }

        public void ClearTranslations()
        {
            Reset();
        }

        public void Reset()
        {
            translatedPaths.Clear();
            proxyCache.Clear();
        }

        public int TranslateAllHashTables()
        {
            int totalTranslated = 0;

            foreach (string path in targetPaths)
            {
                if (translatedPaths.Contains(path))
                    continue;

                int translated = TranslateHashTablePath(path, out bool isReady);
                if (translated > 0)
                {
                    totalTranslated += translated;
                }

                if (isReady)
                {
                    translatedPaths.Add(path);
                }
            }

            return totalTranslated;
        }

        public int MonitorAndTranslateHashTables()
        {
            return TranslateAllHashTables();
        }

        private int TranslateHashTablePath(string objectPath, out bool isReady)
        {
            isReady = false;

            PlayMakerHashTableProxy[] proxies = GetProxies(objectPath);
            if (proxies == null || proxies.Length == 0)
                return 0;

            isReady = true;
            int translatedCount = 0;

            for (int i = 0; i < proxies.Length; i++)
            {
                PlayMakerHashTableProxy proxy = proxies[i];
                if (proxy == null)
                    continue;

                translatedCount += TranslateProxy(proxy, objectPath, i);
            }

            return translatedCount;
        }

        private PlayMakerHashTableProxy[] GetProxies(string objectPath)
        {
            if (proxyCache.TryGetValue(objectPath, out PlayMakerHashTableProxy[] cached))
            {
                bool valid = true;
                for (int i = 0; i < cached.Length; i++)
                {
                    if (cached[i] == null)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                    return cached;

                proxyCache.Remove(objectPath);
            }

            GameObject obj = LocalizationUtils.FindGameObjectCached(objectPath);
            if (obj == null)
                return null;

            PlayMakerHashTableProxy[] proxies = obj.GetComponents<PlayMakerHashTableProxy>();
            if (proxies == null || proxies.Length == 0)
            {
                CoreConsole.Warning($"PlayMakerHashTableProxy not found at {objectPath}");
                return null;
            }

            proxyCache[objectPath] = proxies;
            return proxies;
        }

        private int TranslateProxy(PlayMakerHashTableProxy proxy, string objectPath, int componentIndex)
        {
            int translatedCount = 0;
            Hashtable hashTable = proxy.hashTable;
            if (hashTable == null)
                return 0;

            List<object> keys = new List<object>();
            foreach (object key in hashTable.Keys)
            {
                if (key == null)
                    continue;
                keys.Add(key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                object key = keys[i];
                if (key == null)
                    continue;
                string original = hashTable[key] as string;
                if (string.IsNullOrEmpty(original))
                    continue;

                string translated = FindTranslation(original);
                if (string.IsNullOrEmpty(translated) || translated == original)
                    continue;

                hashTable[key] = translated;
                translatedCount++;
            }

            translatedCount += TranslatePreFillStringList(proxy);

            if (translatedCount > 0)
            {
                proxy.TakeSnapShot();
                CoreConsole.Print($"[HashTableProxyHandler] Translated {translatedCount} string values in {objectPath}[{componentIndex}]");
            }

            return translatedCount;
        }

        private int TranslatePreFillStringList(PlayMakerHashTableProxy proxy)
        {
            if (PreFillStringListField == null)
                return 0;

            List<string> preFillStringList = PreFillStringListField.GetValue(proxy) as List<string>;
            if (preFillStringList == null || preFillStringList.Count == 0)
                return 0;

            int translatedCount = 0;

            for (int i = 0; i < preFillStringList.Count; i++)
            {
                string original = preFillStringList[i];
                if (string.IsNullOrEmpty(original))
                    continue;

                string translated = FindTranslation(original);
                if (string.IsNullOrEmpty(translated) || translated == original)
                    continue;

                preFillStringList[i] = translated;
                translatedCount++;
            }

            return translatedCount;
        }

        private string FindTranslation(string original)
        {
            string translated;
            return translations != null && translations.TryGetExact(original, out translated)
                ? translated
                : null;
        }
    }
}
