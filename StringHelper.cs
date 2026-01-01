namespace MWC_Localization_Core
{
    /// <summary>
    /// String normalization and Korean text detection utilities
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

        /// <summary>
        /// Check if text contains Korean characters (Hangul)
        /// </summary>
        public static bool ContainsKorean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                // Hangul Syllables: AC00–D7AF
                // Hangul Jamo: 1100–11FF
                // Hangul Compatibility Jamo: 3130–318F
                if ((c >= 0xAC00 && c <= 0xD7AF) ||
                    (c >= 0x1100 && c <= 0x11FF) ||
                    (c >= 0x3130 && c <= 0x318F))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
