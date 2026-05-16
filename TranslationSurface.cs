using System.Collections.Generic;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// How often a surface's MonitorTick should fire after its initial pass.
    /// LateUpdateHandler reads this to drive the tick scheduler.
    /// </summary>
    public enum SurfaceCadence
    {
        /// <summary>InitialPass only; MonitorTick is never called.</summary>
        OncePerScene,
        /// <summary>Every LateUpdate frame. For HUD-style monitors that mirror text per frame.</summary>
        PerFrame,
        /// <summary>FSM_SOURCE_POLL_INTERVAL. For small dynamic FSM sources that the game rebuilds per screen open.</summary>
        Fast,
        /// <summary>CHAT_MONITOR_INTERVAL (~1s). For dynamic-but-cheap monitors like TV chat with per-slot caching.</summary>
        Medium,
        /// <summary>ARRAY_MONITOR_INTERVAL, staggered across surfaces. For ArrayList/HashTable proxies that lazy-load.</summary>
        Slow,
    }

    /// <summary>
    /// Read-only carrier handed to every surface during Initialize.
    /// Bundles the shared dependencies so the main class no longer threads them
    /// through constructor parameters individually.
    /// </summary>
    public sealed class TranslationContext
    {
        public TranslationDictionary Translations { get; private set; }
        public Dictionary<string, Font> CustomFonts { get; private set; }
        public LocalizationConfig Config { get; private set; }
        public TextMeshTranslator Translator { get; private set; }
        public string AssetsFolder { get; private set; }

        public TranslationContext(
            TranslationDictionary translations,
            Dictionary<string, Font> customFonts,
            LocalizationConfig config,
            TextMeshTranslator translator,
            string assetsFolder)
        {
            Translations = translations;
            CustomFonts = customFonts;
            Config = config;
            Translator = translator;
            AssetsFolder = assetsFolder;
        }
    }

    /// <summary>
    /// Common lifecycle contract for translation surfaces (HUD, teletext,
    /// ArrayList/HashTable proxies, FSM hook). Implementing this lets the main class
    /// treat them as a uniform List&lt;ITranslationSurface&gt; instead of seven named fields.
    /// </summary>
    public interface ITranslationSurface
    {
        /// <summary>Used in log lines; should be unique within the surface set.</summary>
        string Name { get; }

        /// <summary>How often MonitorTick should fire after InitialPass.</summary>
        SurfaceCadence Cadence { get; }

        /// <summary>True once the surface has nothing more to do; scheduler stops ticking it.
        /// Surfaces that monitor inherently-live data (HUD, dynamic FSM sources, chat) return false.</summary>
        bool IsComplete { get; }

        /// <summary>Wire up dependencies. Called once after construction and again on F8 reload.</summary>
        void Initialize(TranslationContext ctx);

        /// <summary>Initial translate pass after scene load or reload. Returns count translated (used for log lines).</summary>
        int InitialPass();

        /// <summary>Periodic monitor pass. No-op return 0 is fine for surfaces whose work is in InitialPass only.</summary>
        int MonitorTick(float deltaTime);

        /// <summary>Clear runtime caches + resolved-status tracking. Called on scene change.</summary>
        void Reset();

        /// <summary>Drop translation data (separate from Reset); called before reloading from disk on F8.</summary>
        void ClearTranslations();
    }
}
