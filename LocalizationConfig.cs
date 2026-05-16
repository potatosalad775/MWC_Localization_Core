// Configuration file loader

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Centralized timing constants for the localization system.
    /// </summary>
    public static class LocalizationConstants
    {
        public const float ARRAY_MONITOR_INTERVAL = 4.0f;                                  // Check arrays every 4 seconds
        public const float ARRAY_MONITOR_STEP_INTERVAL = ARRAY_MONITOR_INTERVAL / 4f;      // Four staggered passes share one full array/proxy monitoring cycle.
        public const float CHAT_MONITOR_INTERVAL = 1.0f;                                   // Check dynamic TV chat messages once per second
        public const float FSM_SOURCE_POLL_INTERVAL = 5.0f;                                // Check Fleetari/ATM FSM dynamic sources once every 5 seconds
        public const float GUI_MONITOR_RETRY_INTERVAL = 1.0f;                              // Retry missing GUI TextMesh references once per second
    }

    /// <summary>
    /// Loads and manages localization configuration from config.txt
    /// Supports language metadata, font mappings, and text adjustment rules.
    /// </summary>
    public class LocalizationConfig
    {
        /// <summary>
        /// Path prefixes that always get the localized font applied, regardless of whether
        /// the displayed text is in the translation dictionary. Used for teletext, computer
        /// POS, TV graphics, etc., where the original text stays in-game but the font must
        /// be swapped so non-ASCII glyphs render correctly.
        /// </summary>
        public static readonly string[] ForcedFontPathPrefixes = new string[]
        {
            // Computer
            "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS",
            // GUI
            "GUI/Indicators/Partname",
            "GUI/Indicators/Interaction",
            "GUI/Indicators/RallyCountdown",
            "GUI/Indicators/Gear",
            "GUI/Indicators/Subtitles",
            // Sheets
            "Sheets/ServicePayment",
            "Sheets/TrafficTicket",
            "Sheets/RallyResults",
            "Sheets/Arrestwarrant",
            "Sheets/EnviroCrime",
            // TV / Teletext
            "Systems/Teletext/VKTekstiTV/PAGES",
            "Systems/Teletext/VKTekstiTV/HEADER",
        };

        public static bool IsForcedFontPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            for (int i = 0; i < ForcedFontPathPrefixes.Length; i++)
            {
                if (path.StartsWith(ForcedFontPathPrefixes[i]))
                    return true;
            }
            return false;
        }

        public string LanguageName { get; private set; } = "Unknown";
        public string LanguageCode { get; private set; } = "en-US";
        public Dictionary<string, string> FontMappings { get; private set; } = new Dictionary<string, string>();
        public List<TextAdjustment> TextAdjustments { get; private set; } = new List<TextAdjustment>();
        public List<TextAdjustment> GameObjectAdjustments { get; private set; } = new List<TextAdjustment>();
        public LocalizationConfig()
        {
        }

        /// <summary>
        /// Load configuration from config.txt
        /// </summary>
        public bool LoadConfig(string configPath)
        {
            // Make reload idempotent by resetting previously loaded values.
            FontMappings.Clear();
            TextAdjustments.Clear();
            GameObjectAdjustments.Clear();
            LanguageName = "Unknown";
            LanguageCode = "en-US";

            if (!File.Exists(configPath))
            {
                CoreConsole.Warning($"[LocalizationConfig] Config file not found: {configPath}");
                CoreConsole.Warning("[LocalizationConfig] Using default configuration (no font mappings)");
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(configPath, Encoding.UTF8);
                bool inFontsSection = false;
                bool inTextAdjustmentsSection = false;

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
                        inTextAdjustmentsSection = false;
                        continue;
                    }
                    else if (trimmedLine == "[POSITION_ADJUSTMENTS]")
                    {
                        inTextAdjustmentsSection = true;
                        inFontsSection = false;
                        continue;
                    }

                    // Parse based on current section
                    if (inFontsSection)
                    {
                        ParseFontMapping(trimmedLine);
                    }
                    else if (inTextAdjustmentsSection)
                    {
                        ParseTextAdjustment(trimmedLine);
                    }
                    else
                    {
                        ParseConfigLine(trimmedLine);
                    }
                }

                CoreConsole.Print($"[LocalizationConfig] Configuration loaded: {LanguageName} ({LanguageCode})");
                CoreConsole.Print($"[LocalizationConfig] Font mappings: {FontMappings.Count}");
                CoreConsole.Print($"[LocalizationConfig] Position adjustments: {TextAdjustments.Count} TextMesh, {GameObjectAdjustments.Count} GameObject");

                return true;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[LocalizationConfig] Failed to load config: {ex.Message}");
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
        /// Parse position adjustment line with support for position, font size, line spacing, and width scale
        /// Format: Conditions = X,Y,Z[,FontSize,LineSpacing,WidthScale]
        /// Examples:
        ///   Contains(GUI/HUD) = 0,-0.05,0                    (position only)
        ///   Contains(Speedometer) = 0,0,0,0.15               (position + font size)
        ///   Contains(Menu) = 0,0,0,0.12,1.0                  (position + font size + line spacing)
        ///   Contains(Narrow) = 0,0,0,,,1.2                   (position + skip font/spacing + width scale 1.2x)
        ///   Contains(Wide) = 0,0,0,0.1,1.0,1.5               (all adjustments: make wider with 1.5x scale)
        /// </summary>
        private void ParseTextAdjustment(string line)
        {
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                return;

            string conditionsString = line.Substring(0, equalsIndex).Trim();
            string offsetString = line.Substring(equalsIndex + 1).Trim();

            if (string.IsNullOrEmpty(conditionsString) || string.IsNullOrEmpty(offsetString))
                return;

            // Parse adjustment values (X,Y,Z[,FontSize,LineSpacing,WidthScale])
            string[] parts = offsetString.Split(',');
            if (parts.Length < 3)
            {
                CoreConsole.Warning($"[LocalizationConfig] Invalid adjustment format: '{offsetString}'. Expected at least X,Y,Z");
                return;
            }

            try
            {
                // Parse position offset (required)
                float x = float.Parse(parts[0].Trim());
                float y = float.Parse(parts[1].Trim());
                float z = float.Parse(parts[2].Trim());
                UnityEngine.Vector3 offset = new UnityEngine.Vector3(x, y, z);

                // Parse optional font properties
                float? fontSize = null;
                float? lineSpacing = null;
                float? widthScale = null;

                if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3].Trim()))
                {
                    fontSize = float.Parse(parts[3].Trim());
                }

                if (parts.Length > 4 && !string.IsNullOrEmpty(parts[4].Trim()))
                {
                    lineSpacing = float.Parse(parts[4].Trim());
                }

                if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5].Trim()))
                {
                    widthScale = float.Parse(parts[5].Trim());
                }

                TextAdjustment adjustment = new TextAdjustment(conditionsString, offset, fontSize, lineSpacing, widthScale);
                if (adjustment.TargetsGameObject)
                    GameObjectAdjustments.Add(adjustment);
                else
                    TextAdjustments.Add(adjustment);
            }
            catch (System.Exception ex)
            {
                CoreConsole.Warning($"[LocalizationConfig] Failed to parse position adjustment '{line}': {ex.Message}");
            }
        }

        /// <summary>
        /// Apply position adjustment to TextMesh based on path matching
        /// Uses HashSet-based caching to prevent duplicate adjustments
        /// </summary>
        /// <returns>True if adjustment was applied, false if no match or already adjusted</returns>
        public bool ApplyTextAdjustment(TextMesh textMesh, string path)
        {
            if (textMesh == null || string.IsNullOrEmpty(path))
                return false;

            if (path.Contains("GUI/Indicators/Subtitles"))
            {
                // Special handling for subtitles to center-align and lower position
                textMesh.alignment = TextAlignment.Center;
                textMesh.anchor = TextAnchor.LowerCenter;
                if (!path.Contains("/Shadow"))
                    textMesh.transform.localPosition = new Vector3(textMesh.transform.localPosition.x, -1.0f, textMesh.transform.localPosition.z);
            }

            // Find matching adjustment and apply it
            foreach (TextAdjustment adjustment in TextAdjustments)
            {
                if (adjustment.Matches(path))
                {
                    return adjustment.ApplyAdjustment(textMesh);
                }
            }

            return false; // No matching adjustment found
        }

        /// <summary>
        /// Find each GameObjectEquals adjustment's target and apply its offset.
        /// Call after TranslateScene / InitialPass on each scene load and F8 reload.
        /// </summary>
        public void ApplyGameObjectAdjustments()
        {
            for (int i = 0; i < GameObjectAdjustments.Count; i++)
            {
                TextAdjustment adj = GameObjectAdjustments[i];
                for (int j = 0; j < adj.Conditions.Count; j++)
                {
                    PathCondition cond = adj.Conditions[j];
                    if (cond.Type != ConditionType.GameObjectEquals || cond.Negate)
                        continue;

                    GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(cond.Value);
                    if (go == null)
                    {
                        CoreConsole.Print($"[LocalizationConfig] GameObjectEquals: '{cond.Value}' not present in this scene, skipping");
                        continue;
                    }

                    adj.ApplyToGameObject(go);
                    break; // one lookup per rule is sufficient
                }
            }
        }

        /// <summary>
        /// Clear all position adjustment caches.
        /// Useful for F8 reload functionality to reapply adjustments.
        /// </summary>
        public void ClearTextAdjustmentCaches()
        {
            foreach (TextAdjustment adjustment in TextAdjustments)
                adjustment.ClearCache();

            foreach (TextAdjustment adjustment in GameObjectAdjustments)
                adjustment.ClearCache();
        }
    }
}
