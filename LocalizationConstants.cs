namespace MWC_Localization_Core
{
    /// <summary>
    /// Centralized constants for localization system
    /// All timing, retry, and threshold values in one place
    /// </summary>
    public static class LocalizationConstants
    {
        // Monitoring strategies timing
        public const float FAST_POLLING_INTERVAL = 0.15f;           // ~6.6 times per second
        public const float SLOW_POLLING_INTERVAL = 1.0f;            // Once per second
        public const float VISIBILITY_POLLING_INTERVAL = 0.35f;     // ~3 times per second
        public const float ARRAY_MONITOR_INTERVAL = 2.0f;           // Check arrays every 2 seconds
        public const float ARRAY_MONITOR_STEP_INTERVAL = ARRAY_MONITOR_INTERVAL / 4f;
    }
}
