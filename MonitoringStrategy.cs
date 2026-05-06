namespace MWC_Localization_Core
{
    /// <summary>
    /// Defines how frequently a TextMesh should be monitored for changes.
    /// </summary>
    public enum MonitoringStrategy
    {
        /// <summary>
        /// Monitor every frame without throttling.
        /// Use for interaction prompts, subtitles, and other critical real-time UI.
        /// </summary>
        EveryFrame,

        /// <summary>
        /// Monitor at a fast polling interval for responsive UI.
        /// Use for active HUD elements that update frequently.
        /// </summary>
        FastPolling,

        /// <summary>
        /// Keep checking even after translation for content that is frequently rebuilt.
        /// Use for magazine text and similar regenerated content.
        /// </summary>
        Persistent,

        /// <summary>
        /// Keep font and layout adjustments enforced for content whose transform is rebuilt by the game.
        /// Use only for small, known text groups.
        /// </summary>
        PersistentLayout,

        /// <summary>
        /// Translate only when a GameObject becomes active in the hierarchy.
        /// Use for menus, dialogs, and other show/hide UI panels.
        /// </summary>
        OnVisibilityChange,

        /// <summary>
        /// Translate once when the TextMesh becomes available later.
        /// Use for dynamically created TextMeshes that appear after the initial scan.
        /// </summary>
        LateTranslateOnce,

        /// <summary>
        /// Apply the localized font once when the TextMesh becomes available, then stop monitoring.
        /// Use for TextMeshes whose text is already translated upstream at the data source
        /// (e.g. TeletextHandler / ArrayListProxyHandler / FsmTextHook mutate the backing data),
        /// so no translation is needed - only the font swap.
        /// </summary>
        LateApplyFontOnce,
    }

    /// <summary>
    /// Defines which translation method to use.
    /// </summary>
    public enum TranslationMode
    {
        /// <summary>
        /// Pattern matching with {0}, {1}, and similar placeholders.
        /// </summary>
        FsmPattern,

        /// <summary>
        /// Use a custom handler for more complex translation logic.
        /// </summary>
        CustomHandler,

        /// <summary>
        /// Pattern matching with placeholders plus translation of extracted parameters.
        /// </summary>
        FsmPatternWithTranslation
    }
}
