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
        public List<PositionAdjustment> PositionAdjustments { get; private set; } = new List<PositionAdjustment>();

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
                bool inPositionAdjustmentsSection = false;

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
                        inPositionAdjustmentsSection = false;
                        continue;
                    }
                    else if (trimmedLine == "[POSITION_ADJUSTMENTS]")
                    {
                        inPositionAdjustmentsSection = true;
                        inFontsSection = false;
                        continue;
                    }

                    // Parse based on current section
                    if (inFontsSection)
                    {
                        ParseFontMapping(trimmedLine);
                    }
                    else if (inPositionAdjustmentsSection)
                    {
                        ParsePositionAdjustment(trimmedLine);
                    }
                    else
                    {
                        ParseConfigLine(trimmedLine);
                    }
                }

                logger.LogInfo($"Configuration loaded: {LanguageName} ({LanguageCode})");
                logger.LogInfo($"Font mappings: {FontMappings.Count}");
                logger.LogInfo($"Unicode ranges: {UnicodeRanges.Count}");
                logger.LogInfo($"Position adjustments: {PositionAdjustments.Count}");

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
        /// Parse position adjustment line (Conditions = X,Y,Z)
        /// Example: Contains(GUI/HUD) & EndsWith(/HUDLabel) = 0,-0.05,0
        /// </summary>
        private void ParsePositionAdjustment(string line)
        {
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                return;

            string conditionsString = line.Substring(0, equalsIndex).Trim();
            string offsetString = line.Substring(equalsIndex + 1).Trim();

            if (string.IsNullOrEmpty(conditionsString) || string.IsNullOrEmpty(offsetString))
                return;

            // Parse offset (X,Y,Z)
            string[] offsetParts = offsetString.Split(',');
            if (offsetParts.Length != 3)
            {
                logger.LogWarning($"Invalid position offset format: '{offsetString}'. Expected X,Y,Z");
                return;
            }

            try
            {
                float x = float.Parse(offsetParts[0].Trim());
                float y = float.Parse(offsetParts[1].Trim());
                float z = float.Parse(offsetParts[2].Trim());

                UnityEngine.Vector3 offset = new UnityEngine.Vector3(x, y, z);
                PositionAdjustment adjustment = new PositionAdjustment(conditionsString, offset);

                PositionAdjustments.Add(adjustment);
            }
            catch (System.Exception ex)
            {
                logger.LogWarning($"Failed to parse position adjustment '{line}': {ex.Message}");
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

                        // Guard clause: Warn if range includes ANY Latin Unicode blocks (0000-024F)
                        // This includes Basic Latin, Latin-1 Supplement, Latin Extended-A/B
                        // The game contains Finnish characters (Ä, Ö, Å) in original text!
                        if (start <= 0x024F && end >= 0x0000)
                        {
                            logger.LogError("═══════════════════════════════════════════════════════════════");
                            logger.LogError($"ERROR: Unicode range '{trimmedRange}' includes Latin characters (0000-024F)!");
                            logger.LogError("This range contains English AND Finnish characters (Ä, Ö, Å, etc.)");
                            logger.LogError("from the original game and will BREAK translation!");
                            logger.LogError("");
                            logger.LogError("For Latin-based languages (Polish, Spanish, French, German, etc.):");
                            logger.LogError("Leave UNICODE_RANGES empty or commented out in config.txt");
                            logger.LogError("");
                            logger.LogError("Unicode ranges are ONLY for non-Latin languages like:");
                            logger.LogError("- Korean (AC00-D7AF,1100-11FF,3130-318F)");
                            logger.LogError("- Japanese (3040-309F,30A0-30FF,4E00-9FFF)");
                            logger.LogError("- Chinese (4E00-9FFF,3400-4DBF)");
                            logger.LogError("═══════════════════════════════════════════════════════════════");
                            
                            // Clear all ranges to prevent translation breaking
                            UnicodeRanges.Clear();
                            return;
                        }

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
        /// DEPRECATED: Use HashSet-based TextMesh tracking instead for better performance
        /// Kept for backwards compatibility with Unicode-based detection (Korean, Japanese, Chinese)
        /// Returns false if no ranges configured (for Latin languages)
        /// </summary>
        public bool ContainsLocalizedCharacters(string text)
        {
            // Skip detection if no Unicode ranges configured (Latin languages)
            if (UnicodeRanges.Count == 0)
                return false;

            if (string.IsNullOrEmpty(text))
                return false;

            // Simple check without caching (rarely used now)
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

        /// <summary>
        /// Get position offset for the given path based on configured adjustments
        /// Returns Vector3.zero if no matching adjustment found
        /// </summary>
        public UnityEngine.Vector3 GetPositionOffset(string path)
        {
            foreach (PositionAdjustment adjustment in PositionAdjustments)
            {
                if (adjustment.Matches(path))
                {
                    return adjustment.Offset;
                }
            }

            return UnityEngine.Vector3.zero;
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
