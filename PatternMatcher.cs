// Pattern matching system for Translation Strings

using MSCLoader;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Text.RegularExpressions;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Unified pattern matching system for all translation types
    /// Replaces separate FSM, Magazine, and Price pattern logic
    /// </summary>
    public class PatternMatcher
    {
        private List<TranslationPattern> patterns = new List<TranslationPattern>();
        private Dictionary<string, string> translations;

        public PatternMatcher(Dictionary<string, string> translations)
        {
            this.translations = translations;
            
            InitializeBuiltInPatterns();
        }

        /// <summary>
        /// Initialize built-in patterns (can be overridden by config file)
        /// </summary>
        private void InitializeBuiltInPatterns()
        {
            // Fleetari services (helper)
            AddFleetariService(
            	"RimPolish",
            	"Vanteiden kiilotus",
            	"Rim polish",
            	"RIMPOLISH"
            );
            
            AddFleetariService(
            	"TireJob",
            	"Rengastyöt",
            	"tire job",
            	"TIREJOB"
            );
            
            AddFleetariService(
            	"CustomPaint",
            	"Custom automaalaus",
            	"Custom paint",
            	"CUSTOMPAINT"
            );
            
            AddFleetariService(
            	"MetallicPaint",
            	"Metalliväri",
            	"Metallic color",
            	"METALLICPAINT"
            );
            
            AddFleetariService(
            	"OriginalPaint",
            	"Alkuperäisväri",
            	"Original color",
            	"ORIGINALPAINT"
            );
            
            AddFleetariService(
            	"FactorySpecialPaint",
            	"Tehtaan erikoismaalaus",
            	"Factory special paint",
            	"FACTORYSPECIALPAINT"
            );
            
            AddFleetariService(
            	"RimMetallic",
            	"Vanteet metalliväri",
            	"Rim metallic",
            	"RIMMETALLIC"
            );
            
            AddFleetariService(
            	"RimPaint",
            	"Vanteet maalattuna",
            	"Rim paint",
            	"RIMPAINT"
            );
            
            AddFleetariService(
            	"EngineAdjust",
            	"Moottorin säätö",
            	"Engine adjust",
            	"ENGINEADJUST"
            );
            
            AddFleetariService(
            	"ToeAdjust",
            	"Aurauskulmien säätö",
            	"Toe adjust",
            	"TOEADJUST"
            );
            
            AddFleetariService(
            	"BrakeService",
            	"Jarruhuolto",
            	"brake service",
            	"BRAKESERVICE"
            );
            
            AddFleetariService(
            	"EngineTune",
            	"Moottorin viritys",
            	"engine tune up",
            	"ENGINETUNE"
            );
            
            AddFleetariService(
            	"SuspensionRepair",
            	"Ripustusten suoristus",
            	"Suspension repair",
            	"SUSPENSIONREPAIR"
            );
            
            AddFleetariService(
            	"DoorSafetyNets",
            	"Ovien turvaverkot",
            	"door safety nets",
            	"DOORSAFETYNETS"
            );
            
            AddFleetariService(
            	"RollCageInstall",
            	"Turvakehikon asennus",
            	"rollcage install",
            	"ROLLCAGEINSTALL"
            );
            
            AddFleetariService(
            	"WindshieldReplacement",
            	"Tuulilasin vaihto",
            	"windshield replacement",
            	"WINDSHIELDREPLACEMENT"
            );
            
            AddFleetariService(
            	"RatioChange",
            	"Perävälityksen vaihto",
            	"ratio change",
            	"RATIOCHANGE"
            );
            
            AddFleetariService(
            	"RollcageRemoval",
            	"Turvakehikon poisto",
            	"rollcage removal",
            	"ROLLCAGEREMOVAL"
            );
            
            AddFleetariService(
            	"SheetMetalWork",
            	"Peltityöt",
            	"sheet metal work",
            	"SHEETMETALWORK"
            );
            
            AddFleetariService(
                "VinylRemoval",
                "Vinyylikaton poisto",
                "vinyl removal",
                "VINYLREMOVAL"
            );
            
            // Price Total pattern (regex)
            var pricePattern = new TranslationPattern(
                "PriceTotal",
                TranslationMode.RegexExtract,
                @"PRICE TOTAL:\s*([\d.]+)\s*MK",
                "{PRICETOTAL}: {0} MK"
            );
            pricePattern.PathMatcher = path => path.Contains("GUI/Indicators/Interaction");
            patterns.Add(pricePattern);

            // Take Money pattern (regex)
            var takeMoneyPattern = new TranslationPattern(
                "TakeMoney",
                TranslationMode.RegexExtract,
                @"TAKE MONEY \s*([\d.]+)\s*MK",
                "{TAKEMONEY} {0} MK"
            );
            takeMoneyPattern.PathMatcher = path => path.Contains("GUI/Indicators/Interaction");
            patterns.Add(takeMoneyPattern);

            // Pay Post Order pattern (regex)
            var payPostOrderPattern = new TranslationPattern(
                "PayPostOrder",
                TranslationMode.RegexExtract,
                @"PAY POST ORDER \s*([\d.]+)\s*MK",
                "{PAYPOSTORDER} {0} MK"
            );
            payPostOrderPattern.PathMatcher = path => path.Contains("GUI/Indicators/Interaction");
            patterns.Add(payPostOrderPattern);

            // Unpaid Fine pattern (regex)
            var unpaidFinePattern = new TranslationPattern(
                "UnpaidFine",
                TranslationMode.RegexExtract,
                @"UNPAID FINES, \s*([\d.]+)\s*MK",
                "{UNPAIDFINES} {0} MK"
            );
            unpaidFinePattern.PathMatcher = path => path.Contains("GUI/Indicators/Interaction");
            patterns.Add(unpaidFinePattern);

            // Scrap Payment pattern (regex)
			var scrapPaymentPattern = new TranslationPattern(
				"ScrapPayment",
				TranslationMode.RegexExtract,
				@"SCRAP PAYMENT,\s*([\d.]+)\s*MK",
				"{SCRAPPAYMENT} {0} MK"
			);
			scrapPaymentPattern.PathMatcher = path => path.Contains("GUI/Indicators/Interaction");
			patterns.Add(scrapPaymentPattern);
            
            // TV Chat moderator pattern (regex)
            var tvChatModeratorPattern = new TranslationPattern(
                "TVChatModerator",
                TranslationMode.RegexExtract,
                @"Valvojana: \s*(.+)",
                "{VALVOJANA}: {0}"
            );
            tvChatModeratorPattern.PathMatcher = path => path.Contains("Systems/TV/TVGraphics/CHAT/Moderator");
            patterns.Add(tvChatModeratorPattern);

            // Magazine price/phone pattern (custom handler)
            var magazinePricePattern = new TranslationPattern(
                "MagazinePrice",
                TranslationMode.CustomHandler,
                "h.{price},- puh.{phone}",
                "{price} MK, {PHONE} - {phone}"
            );
            magazinePricePattern.PathMatcher = path => path.Contains("YellowPagesMagazine");
            magazinePricePattern.TextMatcher = text => text.StartsWith("h.") && text.Contains(",- puh.");
            magazinePricePattern.CustomHandler = TranslateMagazinePriceLine;
            patterns.Add(magazinePricePattern);

            // Magazine comma-separated words
            var magazineWordsPattern = new TranslationPattern(
                "MagazineWords",
                TranslationMode.CommaSeparated,
                "",
                ""
            );
            magazineWordsPattern.PathMatcher = path => path.Contains("YellowPagesMagazine");
            magazineWordsPattern.TextMatcher = text => text.Split(',').Length == 3;
            patterns.Add(magazineWordsPattern);
        }

        /// <summary>
        /// Load patterns from file (FSM patterns, custom patterns, etc.)
        /// </summary>
        public void LoadPatternsFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                CoreConsole.Print("No pattern file found, using built-in patterns only");
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                string currentSection = null;
                int loadedCount = 0;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();

                    // Skip comments and empty lines
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    // Check for section header
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2);
                        continue;
                    }

                    // Parse pattern - auto-detects FsmPattern vs FsmPatternWithTranslation
                    if (TryParseFsmPattern(trimmed, out TranslationPattern pattern))
                    {
                        patterns.Insert(0, pattern);  // Insert at beginning to override built-in patterns
                        loadedCount++;
                    }
                }

                CoreConsole.Print($"Loaded {loadedCount} patterns from file");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"Failed to load patterns: {ex.Message}");
            }
        }

        private bool TryParseFsmPattern(string line, out TranslationPattern pattern)
        {
            pattern = null;
            
            int equalsIndex = FindUnescapedEquals(line);
            if (equalsIndex <= 0)
                return false;

            string original = line.Substring(0, equalsIndex).Trim();
            string translation = line.Substring(equalsIndex + 1).Trim();

            // Unescape special characters
            original = UnescapeString(original);
            translation = UnescapeString(translation);

            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translation))
                return false;

            // Check if this is a pattern (contains {0}, {1}, etc.)
            // Auto-detect: if translation has placeholders, use FsmPatternWithTranslation (translate the params)
            // If translation has NO placeholders, use FsmPattern (just substitute - rare case)
            if (original.Contains("{0}") || original.Contains("{1}") || original.Contains("{2}"))
            {
                bool translationHasPlaceholders = translation.Contains("{0}") || translation.Contains("{1}") || translation.Contains("{2}");
                
                pattern = new TranslationPattern(
                    "FSM_" + original.Substring(0, System.Math.Min(20, original.Length)),
                    translationHasPlaceholders ? TranslationMode.FsmPatternWithTranslation : TranslationMode.FsmPattern,
                    original,
                    translation
                );
                return true;
            }

            return false;
        }

        private void AddFleetariService(
            string id,
            string finnish,
            string english,
            string tokenKey
        )
        {
        	var pattern = new TranslationPattern(
        		id,
        		TranslationMode.RegexExtract,
        		$@"(?i){System.Text.RegularExpressions.Regex.Escape(finnish)}\s*/.*?\s+(\d+)\s*,\s*[-–−]",
        		$"{{{tokenKey}}}      {{0}},-"
        	);
        
        	patterns.Add(pattern);
        }

        /// <summary>
        /// Try to translate text using pattern matching
        /// Returns null if no pattern matched
        /// </summary>
        public string TryTranslateWithPattern(string text, string path)
        {
            foreach (var pattern in patterns)
            {
                string result = pattern.TryTranslate(text, path, translations);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Add a pattern programmatically
        /// </summary>
        public void AddPattern(TranslationPattern pattern)
        {
            patterns.Add(pattern);
        }

        /// <summary>
        /// Clear all patterns
        /// </summary>
        public void ClearPatterns()
        {
            patterns.Clear();
        }

        /// <summary>
        /// Custom handler for magazine price/phone lines
        /// Format: "h.149,- puh.123456" -> "149 MK, PHONE - 123456"
        /// </summary>
        private TranslationPattern.CustomHandlerResult TranslateMagazinePriceLine(string text, string path, Dictionary<string, string> translations)
        {
            try
            {
                // Remove "h." prefix and split by ",- puh."
                if (!text.StartsWith("h."))
                    return new TranslationPattern.CustomHandlerResult(false, null);

                string withoutPrefix = text.Substring(2);
                string[] parts = withoutPrefix.Split(new string[] { ",- puh." }, System.StringSplitOptions.None);

                if (parts.Length == 2)
                {
                    string pricePart = parts[0].Trim();
                    string phonePart = parts[1].Trim();

                    // Get phone label from translations
                    string phoneLabel = translations.TryGetValue("PHONE", out string translation)
                        ? translation
                        : "PHONE";

                    return new TranslationPattern.CustomHandlerResult(true, $"{pricePart} MK, {phoneLabel} - {phonePart}");
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Warning($"Failed to parse magazine price/phone line: {text} - {ex.Message}");
            }

            return new TranslationPattern.CustomHandlerResult(false, null);
        }

        // Utility methods from TeletextHandler
        private int FindUnescapedEquals(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '=')
                {
                    // Check if escaped
                    if (i > 0 && line[i - 1] == '\\')
                        continue;
                    return i;
                }
            }
            return -1;
        }

        private string UnescapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            return str.Replace("\\=", "=").Replace("\\n", "\n");
        }
    }
}
