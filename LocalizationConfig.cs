using BepInEx.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Loads and manages localization configuration from config.txt
    /// Supports language metadata, font mappings, and Unicode range detection
    /// </summary>
    public class LocalizationConfig
    {
        public string LanguageName { get; private set; } = "Unknown";
        public string LanguageCode { get; private set; } = "en-US";
        public Dictionary<string, string> FontMappings { get; private set; } = new Dictionary<string, string>();
        public List<UnicodeRange> UnicodeRanges { get; private set; } = new List<UnicodeRange>();

        private ManualLogSource logger;

        public LocalizationConfig(ManualLogSource logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Load configuration from config.txt
        /// </summary>
        public bool LoadConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                logger.LogWarning($"Config file not found: {configPath}");
                logger.LogInfo("Using default configuration (no character detection, no font mappings)");
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(configPath, Encoding.UTF8);
                bool inFontsSection = false;

                foreach (string line in lines)
                {
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(line) || line.Trim().StartsWith("#"))
                        continue;

                    string trimmedLine = line.Trim();

                    // Check for section headers
                    if (trimmedLine == "[FONTS]")
                    {
                        inFontsSection = true;
                        continue;
                    }

                    // Parse based on current section
                    if (inFontsSection)
                    {
                        ParseFontMapping(trimmedLine);
                    }
                    else
                    {
                        ParseConfigLine(trimmedLine);
                    }
                }

                logger.LogInfo($"Configuration loaded: {LanguageName} ({LanguageCode})");
                logger.LogInfo($"Font mappings: {FontMappings.Count}");
                logger.LogInfo($"Unicode ranges: {UnicodeRanges.Count}");

                return true;
            }
            catch (System.Exception ex)
            {
                logger.LogError($"Failed to load config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parse configuration line (KEY = VALUE format)
        /// </summary>
        private void ParseConfigLine(string line)
        {
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                return;

            string key = line.Substring(0, equalsIndex).Trim();
            string value = line.Substring(equalsIndex + 1).Trim();

            switch (key.ToUpper())
            {
                case "LANGUAGE_NAME":
                    LanguageName = value;
                    break;

                case "LANGUAGE_CODE":
                    LanguageCode = value;
                    break;

                case "UNICODE_RANGES":
                    ParseUnicodeRanges(value);
                    break;
            }
        }

        /// <summary>
        /// Parse font mapping line (OriginalFont = LocalizedFont)
        /// </summary>
        private void ParseFontMapping(string line)
        {
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                return;

            string originalFont = line.Substring(0, equalsIndex).Trim();
            string localizedFont = line.Substring(equalsIndex + 1).Trim();

            if (!string.IsNullOrEmpty(originalFont) && !string.IsNullOrEmpty(localizedFont))
            {
                FontMappings[originalFont] = localizedFont;
            }
        }

        /// <summary>
        /// Parse Unicode ranges (format: START-END,START-END)
        /// Example: AC00-D7AF,1100-11FF,3130-318F
        /// </summary>
        private void ParseUnicodeRanges(string rangesString)
        {
            if (string.IsNullOrEmpty(rangesString))
                return;

            string[] ranges = rangesString.Split(',');

            foreach (string range in ranges)
            {
                string trimmedRange = range.Trim();
                string[] parts = trimmedRange.Split('-');

                if (parts.Length == 2)
                {
                    try
                    {
                        int start = System.Convert.ToInt32(parts[0].Trim(), 16);
                        int end = System.Convert.ToInt32(parts[1].Trim(), 16);

                        UnicodeRanges.Add(new UnicodeRange(start, end));
                    }
                    catch (System.Exception ex)
                    {
                        logger.LogWarning($"Failed to parse Unicode range '{trimmedRange}': {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Check if text contains characters from configured Unicode ranges
        /// Returns false if no ranges configured (for Latin languages)
        /// </summary>
        public bool ContainsLocalizedCharacters(string text)
        {
            // Skip detection if no Unicode ranges configured (Latin languages)
            if (UnicodeRanges.Count == 0)
                return false;

            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                foreach (UnicodeRange range in UnicodeRanges)
                {
                    if (c >= range.Start && c <= range.End)
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Represents a Unicode character range for language detection
    /// </summary>
    public struct UnicodeRange
    {
        public int Start;
        public int End;

        public UnicodeRange(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}
