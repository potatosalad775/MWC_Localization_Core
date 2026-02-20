namespace MWC_Localization_Core
{
    /// <summary>
    /// Centralized constants for localization system
    /// All timing, retry, and threshold values in one place
    /// </summary>
    public static class LocalizationConstants
    {
        // Monitoring strategies timing
        public const float FAST_POLLING_INTERVAL = 0.1f;            // 10 times per second
        public const float SLOW_POLLING_INTERVAL = 1.0f;            // Once per second
        public const float ARRAY_MONITOR_INTERVAL = 1.0f;           // Check arrays every 1 second (was every frame)
        // Max translations performed by monitor update loop per cycle/frame
        public const int TRANSLATIONS_PER_CYCLE = 5;
        // Translation batch size for incremental scene translation
        public const int TRANSLATION_BATCH_SIZE = 64;
    }
}
