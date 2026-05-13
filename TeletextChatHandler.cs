using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Dynamic TV-chat monitor. Runs at Medium cadence (CHAT_MONITOR_INTERVAL) so chat messages
    /// stay responsive while the slower static-teletext loop in TeletextHandler can retire once done.
    /// Per-slot value cache skips slots that haven't changed since the previous tick.
    ///
    /// Translation data is owned by TeletextHandler (loaded from translate_teletext.txt) and read
    /// through TeletextHandler.TryGetChatTranslations.
    /// </summary>
    public class TeletextChatHandler : ITranslationSurface
    {
        public string Name { get { return "TeletextChatHandler"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Medium; } }
        public bool IsComplete { get { return false; } }

        private readonly TeletextHandler teletext;

        private PlayMakerArrayListProxy[] chatProxies;
        private bool chatProxiesResolved;

        // Last processed value per chat slot, keyed by proxy instance id.
        private readonly Dictionary<int, List<string>> chatLastValuesByProxy = new Dictionary<int, List<string>>();

        public TeletextChatHandler(TeletextHandler teletext)
        {
            this.teletext = teletext;
        }

        public void Initialize(TranslationContext ctx)
        {
            // Translation data lives on TeletextHandler; nothing to wire up here.
        }

        public int InitialPass()
        {
            return MonitorAndTranslateChatMessages();
        }

        public int MonitorTick(float deltaTime)
        {
            return MonitorAndTranslateChatMessages();
        }

        public void Reset()
        {
            chatProxies = null;
            chatProxiesResolved = false;
            chatLastValuesByProxy.Clear();
        }

        public void ClearTranslations()
        {
            // Translation data is owned by TeletextHandler; runtime caches reset via Reset().
        }

        private int MonitorAndTranslateChatMessages()
        {
            try
            {
                if (!chatProxiesResolved)
                {
                    GameObject dataObject = LocalizationUtils.FindGameObjectCached(TeletextHandler.ChatMessagesPath);
                    if (dataObject == null)
                        return 0;

                    chatProxies = dataObject.GetComponents<PlayMakerArrayListProxy>();
                    chatProxiesResolved = true;
                }

                if (chatProxies == null || chatProxies.Length == 0)
                    return 0;

                Dictionary<string, string> exact;
                List<string> indexed;
                if (!teletext.TryGetChatTranslations(out exact, out indexed))
                    return 0;

                int totalTranslated = 0;
                for (int i = 0; i < chatProxies.Length; i++)
                {
                    PlayMakerArrayListProxy proxy = chatProxies[i];
                    if (proxy == null || !string.Equals(proxy.referenceName, "Messages", System.StringComparison.Ordinal))
                        continue;

                    int translated = TranslateChangedChatMessagesProxy(proxy, exact, indexed);
                    if (translated > 0)
                    {
                        CoreConsole.Print($"[TeletextChatHandler] Translated '{TeletextHandler.ChatMessagesCategoryName}' with {translated} items");
                        totalTranslated += translated;
                    }
                }

                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[TeletextChatHandler] Error monitoring TV chat messages: {ex.Message}");
                return 0;
            }
        }

        private int TranslateChangedChatMessagesProxy(
            PlayMakerArrayListProxy proxy,
            Dictionary<string, string> translations,
            List<string> indexedFallback)
        {
            if (proxy == null || proxy._arrayList == null)
                return 0;

            ArrayList arrayList = proxy._arrayList;
            int instanceId = proxy.GetInstanceID();
            List<string> lastValues;
            if (!chatLastValuesByProxy.TryGetValue(instanceId, out lastValues))
            {
                lastValues = new List<string>(arrayList.Count);
                chatLastValuesByProxy[instanceId] = lastValues;
            }

            while (lastValues.Count < arrayList.Count)
                lastValues.Add(null);
            while (lastValues.Count > arrayList.Count)
                lastValues.RemoveAt(lastValues.Count - 1);

            bool hasIndexFallback = indexedFallback != null && indexedFallback.Count > 0;

            int translatedCount = 0;
            int fallbackCount = 0;
            int nonEmptySourceIndex = -1;

            for (int i = 0; i < arrayList.Count; i++)
            {
                if (arrayList[i] == null)
                {
                    lastValues[i] = null;
                    continue;
                }

                string original = arrayList[i].ToString();
                string normalizedOriginal = original.Trim();
                if (string.IsNullOrEmpty(normalizedOriginal))
                {
                    lastValues[i] = original;
                    continue;
                }

                nonEmptySourceIndex++;
                if (lastValues[i] == original)
                    continue;

                string translationKey = TranslationFileParser.UnescapeString(normalizedOriginal);
                string finalValue = original;

                string translation;
                if (translations.TryGetValue(translationKey, out translation))
                {
                    if (original != translation)
                    {
                        finalValue = translation;
                        arrayList[i] = translation;
                        translatedCount++;
                    }
                }
                else if (hasIndexFallback && nonEmptySourceIndex < indexedFallback.Count)
                {
                    string indexTranslation = indexedFallback[nonEmptySourceIndex];
                    if (!string.IsNullOrEmpty(indexTranslation) && original != indexTranslation)
                    {
                        finalValue = indexTranslation;
                        arrayList[i] = indexTranslation;
                        translatedCount++;
                        fallbackCount++;
                    }
                }

                lastValues[i] = finalValue;
            }

            if (fallbackCount > 0)
            {
                CoreConsole.Print($"[TeletextChatHandler] '{TeletextHandler.ChatMessagesCategoryName}': Used index fallback for {fallbackCount} changed items");
            }

            return translatedCount;
        }
    }
}
