namespace MWC_Localization_Core
{
    /// <summary>
    /// String normalization utilities for localization
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// Format string for use as translation key (uppercase, no spaces/newlines)
        /// </summary>
        public static string FormatUpperKey(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            // Trim whitespace
            original = original.Trim();
            // Remove spaces, newlines, carriage returns
            original = original.Replace(" ", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
            // Convert to uppercase
            original = original.ToUpper();
            return original;
        }
    }
}
