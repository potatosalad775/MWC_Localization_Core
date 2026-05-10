using MSCLoader;
using System;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Core console utilities for MWC Localization Core mod
    /// </summary>
    public static class CoreConsole
    {
        // Use delegates to defer value lookup, avoiding timing issues
        private static Func<bool> getShowDebugLogs;
        private static Func<bool> getShowWarningLogs;

        public static void Initialize(SettingsCheckBox showDebugLogs, SettingsCheckBox showWarningLogs)
        {
            // Store lambdas that safely get current values
            getShowDebugLogs = () => showDebugLogs?.GetValue() ?? true;  // Default true if null
            getShowWarningLogs = () => showWarningLogs?.GetValue() ?? true;  // Default true if null
        }

        /// <summary>
        /// Print a debug message to the ModConsole
        /// </summary>
        public static void Print(string message)
        {
            if (getShowDebugLogs == null || getShowDebugLogs())
                ModConsole.Print("[MWC_LC] " + message);
        }

        /// <summary>
        /// Print a warning message to the ModConsole
        /// </summary>
        public static void Warning(string message)
        {
            if (getShowWarningLogs == null || getShowWarningLogs())
                ModConsole.Warning("[MWC_LC] " + message);
        }

        /// <summary>
        /// Print an error message to the ModConsole
        /// </summary>
        public static void Error(string message)
        {
            if (getShowWarningLogs == null || getShowWarningLogs())
                ModConsole.Error("[MWC_LC] " + message);
        }
    }
}
