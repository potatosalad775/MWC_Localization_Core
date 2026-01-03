// My Winter Car - Localization Plugin
// BepInEx Plugin for multi-language translation support
// Installation: Place compiled DLL in BepInEx/plugins/

using BepInEx;
using BepInEx.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MWC_Localization_Core
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class LocalizationPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.potatosalad.mwc_localization_core";
        public const string PluginName = "MWC Localization Core";
        public const string Version = "0.3.1";

        // Constants for dynamic element scanning
        private const float MAINMENU_SCAN_INTERVAL = 2.0f;  // Reduced frequency: 0.5s -> 2.0s
        private const float DYNAMIC_UPDATE_INTERVAL = 0.1f;  // Throttle dynamic updates to 10 FPS

        private static ManualLogSource _logger;

        // Translation data
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        private bool hasLoadedTranslations = false;

        // Magazine text handler
        private MagazineTextHandler magazineHandler;

        // Translation handler
        private TextMeshTranslator translator;

        // Scene translation tracking
        private bool hasTranslatedSplashScreen = false;
        private bool hasTranslatedMainMenu = false;
        private bool hasTranslatedGameScene = false;

        // Font management
        private AssetBundle fontBundle;
        private Dictionary<string, Font> customFonts = new Dictionary<string, Font>();

        // Localization configuration
        private LocalizationConfig config;

        // Dynamic UI element tracking
        private List<TextMesh> dynamicTextMeshes = new List<TextMesh>();
        private List<TextMesh> priorityTextMeshes = new List<TextMesh>();  // High-priority elements (checked every frame)
        private HashSet<string> translatedPaths = new HashSet<string>();
        private float lastMainMenuScanTime = 0f;
        private float lastDynamicUpdateTime = 0f;

        // GameObject path cache for performance
        private Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();
        
        // Cached GameObject references for priority elements (avoid GameObject.Find)
        private Dictionary<string, GameObject> priorityObjectCache = new Dictionary<string, GameObject>();
        
        // Track last text content to detect changes (dirty flag system)
        private Dictionary<TextMesh, string> lastTextContent = new Dictionary<TextMesh, string>();
        
        // Track which TextMesh objects have been translated (language-agnostic detection)
        private HashSet<TextMesh> translatedTextMeshes = new HashSet<TextMesh>();

        void Awake()
        {
            _logger = Logger;
            _logger.LogInfo($"{PluginName} v{Version} loaded!");

            // Initialize configuration
            config = new LocalizationConfig(_logger);
            string configPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "config.txt");
            config.LoadConfig(configPath);

            // Initialize magazine handler
            magazineHandler = new MagazineTextHandler(_logger);

            // Load translations immediately - this is safe
            LoadTranslations();

            // Load magazine translations from separate file
            string magazinePath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);
        }

        void Start()
        {
            // Load fonts in Start() instead of Awake() - Unity's AssetBundle system
            // needs to be fully initialized before CreateFromMemoryImmediate works
            _logger.LogInfo("Start() - Loading fonts...");

            // Try asset bundle first, fallback to Default font
            LoadCustomFonts();

            // Initialize translator after fonts are loaded
            translator = new TextMeshTranslator(translations, customFonts, magazineHandler, config, _logger);
        }

        bool LoadCustomFonts()
        {
            // Skip font loading if no font mappings configured
            if (config.FontMappings.Count == 0)
            {
                _logger.LogInfo("No font mappings configured - using default fonts");
                return false;
            }

            string bundlePath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "fonts.unity3d");

            if (!File.Exists(bundlePath))
            {
                _logger.LogWarning($"Font bundle not found: {bundlePath}");
                return false;
            }

            try
            {
                _logger.LogInfo($"Loading font bundle from: {bundlePath}");
                fontBundle = LoadBundle(bundlePath);

                if (fontBundle == null)
                {
                    _logger.LogError("Failed to create AssetBundle from file");
                    return false;
                }

                _logger.LogInfo($"AssetBundle loaded successfully");

                // Load fonts from config mappings
                foreach (var pair in config.FontMappings)
                {
                    string originalFontName = pair.Key;
                    string assetFontName = pair.Value;

                    Font font = fontBundle.LoadAsset(assetFontName, typeof(Font)) as Font;
                    if (font != null)
                    {
                        customFonts[originalFontName] = font;
                        _logger.LogInfo($"Loaded {assetFontName} for {originalFontName}");
                    }
                }

                if (customFonts.Count > 0)
                {
                    _logger.LogInfo($"Successfully loaded {customFonts.Count} custom fonts from asset bundle");
                    return true;
                }
                else
                {
                    _logger.LogWarning("No fonts were loaded from asset bundle");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Failed to load font bundle: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        void InsertTranslationLines(string translationPath)
        {
            try
            {
                string[] lines = File.ReadAllLines(translationPath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    // Skip empty lines and comments
                    if (line.IsNullOrWhiteSpace() || line.TrimStart().StartsWith("#"))
                        continue;

                    // Parse KEY=VALUE format
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1).Trim();

                        // Normalize key (remove spaces, convert to uppercase)
                        key = StringHelper.FormatUpperKey(key);

                        // Handle escaped newlines in value
                        value = value.Replace("\\n", "\n");

                        if (!translations.ContainsKey(key))
                        {
                            translations[key] = value;
                        }
                    }
                }
                _logger.LogInfo($"Loaded {translations.Count} translations from {translationPath}");
                hasLoadedTranslations = true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Failed to load translations: {ex.Message}");
            }
        }

        void LoadTranslations()
        {
            // Load translation file used in My Summer Car first
            string mscTranslationPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_msc.txt");
            
            if (!File.Exists(mscTranslationPath))
            {
                _logger.LogWarning($"Translation file not found: {mscTranslationPath}");
            }
            else 
            {
                InsertTranslationLines(mscTranslationPath);
            }

            // Load main translation file for My Winter Car
            string translationPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate.txt");

            if (!File.Exists(translationPath))
            {
                _logger.LogWarning($"Translation file not found: {translationPath}");
                return;
            }
            else 
            {
                InsertTranslationLines(translationPath);
            }
        }

        void ReloadTranslations()
        {
            _logger.LogInfo("[F8] Reloading translations...");

            // Clear existing translations
            translations.Clear();
            magazineHandler.ClearTranslations();

            // Reload from file
            LoadTranslations();

            // Reload magazine translations
            string magazinePath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);

            // Clear all caches to force re-translation
            translatedPaths.Clear();
            pathCache.Clear();
            dynamicTextMeshes.Clear();
            priorityTextMeshes.Clear();
            priorityObjectCache.Clear();
            lastTextContent.Clear();
            translatedTextMeshes.Clear();

            // Reset scene translation flags
            hasTranslatedSplashScreen = false;
            hasTranslatedMainMenu = false;
            hasTranslatedGameScene = false;

            _logger.LogInfo($"[F8] Reloaded {translations.Count} translations. Current scene will be re-translated.");
        }

        void Update()
        {
            if (!hasLoadedTranslations)
                return;

            // F8 key: Reload translations at runtime
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ReloadTranslations();
                return;
            }

            string currentScene = Application.loadedLevelName;

            // Initial translation pass for Main Menu
            if (currentScene == "SplashScreen" && !hasTranslatedSplashScreen)
            {
                _logger.LogInfo("Translating Splash Screen...");
                TranslateScene();
                hasTranslatedSplashScreen = true;
            }

            // Initial translation pass for Main Menu
            if (currentScene == "MainMenu" && !hasTranslatedMainMenu)
            {
                _logger.LogInfo("Translating Main Menu...");
                TranslateScene();
                hasTranslatedMainMenu = true;
            }

            // Initial translation pass for Game scene
            if (currentScene == "GAME" && !hasTranslatedGameScene)
            {
                _logger.LogInfo("Translating Game scene...");
                TranslateScene();
                hasTranslatedGameScene = true;
            }

            // Reset flags on scene change
            if (currentScene == "MainMenu")
            {
                hasTranslatedGameScene = false;
                dynamicTextMeshes.Clear();
                priorityTextMeshes.Clear();
                priorityObjectCache.Clear();  // Clear GameObject cache
                lastTextContent.Clear();  // Clear text tracking
                translatedTextMeshes.Clear();  // Clear translation tracking
                // Keep pathCache - reuse across scenes for performance
            }
            else if (currentScene == "GAME")
            {
                hasTranslatedMainMenu = false;
                translatedPaths.Clear();
                priorityTextMeshes.Clear();
                priorityObjectCache.Clear();  // Clear GameObject cache
                lastTextContent.Clear();  // Clear text tracking
                translatedTextMeshes.Clear();  // Clear translation tracking
                // Keep pathCache - reuse across scenes for performance
            }
        }

        void LateUpdate()
        {
            string currentScene = Application.loadedLevelName;

            if (currentScene == "GAME" && hasTranslatedGameScene)
            {
                // Check priority elements every frame (no throttling)
                UpdatePriorityTextMeshes();

                // Throttle dynamic updates to reduce CPU load
                if (Time.time - lastDynamicUpdateTime >= DYNAMIC_UPDATE_INTERVAL)
                {
                    lastDynamicUpdateTime = Time.time;
                    UpdateDynamicTextMeshes();
                }
            }
            else if (currentScene == "MainMenu" && hasTranslatedMainMenu)
            {
                // Handle late-loading MainMenu elements
                ScanForNewMainMenuElements();
                
                // Throttle dynamic updates
                if (Time.time - lastDynamicUpdateTime >= DYNAMIC_UPDATE_INTERVAL)
                {
                    lastDynamicUpdateTime = Time.time;
                    UpdateDynamicTextMeshes();
                }
            }
        }

        void ScanForNewMainMenuElements()
        {
            // Throttle scanning
            if (Time.time - lastMainMenuScanTime < MAINMENU_SCAN_INTERVAL)
                return;

            lastMainMenuScanTime = Time.time;

            // Find all TextMesh components
            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();

            foreach (TextMesh textMesh in allTextMeshes)
            {
                if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                    continue;

                string path = GetGameObjectPath(textMesh.gameObject);

                // Skip if we've already translated this path
                if (translatedPaths.Contains(path))
                    continue;

                // Try to translate
                if (translator.TranslateAndApplyFont(textMesh, path, translatedTextMeshes))
                {
                    // Check if this element needs continuous monitoring
                    if (path.Contains("Interface/Songs/") && !dynamicTextMeshes.Contains(textMesh))
                    {
                        dynamicTextMeshes.Add(textMesh);
                    }

                    translatedPaths.Add(path);
                    translatedTextMeshes.Add(textMesh);  // Mark as translated
                }
            }
        }

        void UpdatePriorityTextMeshes()
        {
            // Check priority elements every frame - no throttling for instant response
            // Use cached GameObject references to avoid expensive GameObject.Find calls
            string[] priorityPaths = new string[]
            {
                "GUI/Indicators/Interaction",
                "GUI/Indicators/Interaction/Shadow",
                "GUI/Indicators/Partname",
                "GUI/Indicators/Partname/Shadow",
                "GUI/Indicators/Subtitles",
                "GUI/Indicators/Subtitles/Shadow",
                "GUI/HUD/Day/HUDValue"
            };

            foreach (string path in priorityPaths)
            {
                GameObject obj;
                
                // Try to get cached reference first
                if (!priorityObjectCache.TryGetValue(path, out obj) || obj == null)
                {
                    // Cache miss or destroyed - find and cache
                    obj = GameObject.Find(path);
                    if (obj != null)
                    {
                        priorityObjectCache[path] = obj;
                    }
                    else
                    {
                        continue;  // Object not found
                    }
                }
                
                TextMesh tm = obj.GetComponent<TextMesh>();
                if (tm != null && !string.IsNullOrEmpty(tm.text))
                {
                    // Add to priority list if not already there
                    if (!priorityTextMeshes.Contains(tm))
                    {
                        priorityTextMeshes.Add(tm);
                    }

                    // Check if text changed (dirty flag)
                    string currentText = tm.text;
                    if (!lastTextContent.TryGetValue(tm, out string lastText) || lastText != currentText)
                    {
                        // Text changed - translate it
                        if (translator.TranslateAndApplyFont(tm, path, translatedTextMeshes))
                        {
                            lastTextContent[tm] = tm.text;  // Update tracked content
                            translatedTextMeshes.Add(tm);  // Mark as translated
                        }
                    }
                }
            }
        }

        void UpdateDynamicTextMeshes()
        {
            // Monitor cached dynamic UI elements for text changes
            for (int i = dynamicTextMeshes.Count - 1; i >= 0; i--)
            {
                TextMesh textMesh = dynamicTextMeshes[i];

                // Remove null references (destroyed objects)
                if (textMesh == null)
                {
                    dynamicTextMeshes.RemoveAt(i);
                    lastTextContent.Remove(textMesh);
                    continue;
                }

                if (string.IsNullOrEmpty(textMesh.text))
                    continue;

                string path = GetGameObjectPath(textMesh.gameObject);
                string currentText = textMesh.text;

                // Check if this is magazine text that needs persistent monitoring
                bool isMagazineText = magazineHandler.IsMagazineText(path);

                // Skip if already translated - remove from monitoring UNLESS it's magazine text
                // (magazine text can be regenerated by the game, so we need to keep monitoring it)
                if (translatedTextMeshes.Contains(textMesh) && !isMagazineText)
                {
                    // Check if text changed (might have been reset by game)
                    if (lastTextContent.TryGetValue(textMesh, out string prevText) && prevText == currentText)
                    {
                        // Still translated, no change
                        dynamicTextMeshes.RemoveAt(i);
                        continue;
                    }
                    else
                    {
                        // Text changed, needs re-translation
                        translatedTextMeshes.Remove(textMesh);
                    }
                }

                // Only translate if text actually changed (dirty flag check)
                if (!lastTextContent.TryGetValue(textMesh, out string lastText) || lastText != currentText || isMagazineText)
                {
                    // Translate and apply font using unified helper
                    if (translator.TranslateAndApplyFont(textMesh, path, translatedTextMeshes))
                    {
                        lastTextContent[textMesh] = textMesh.text;  // Update tracked content
                        translatedTextMeshes.Add(textMesh);  // Mark as translated
                    }
                }
            }
        }

        void TranslateScene()
        {
            // Find all TextMesh components in the scene
            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();
            int translatedCount = 0;

            foreach (TextMesh textMesh in allTextMeshes)
            {
                if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                    continue;

                // Get GameObject path
                string path = GetGameObjectPath(textMesh.gameObject);

                // Check if this is a priority element (instant translation)
                if (IsPriorityUIElement(path) && !priorityTextMeshes.Contains(textMesh))
                {
                    priorityTextMeshes.Add(textMesh);
                }
                // Check if this is a dynamic UI element that needs continuous monitoring
                else if (IsDynamicUIElement(path) && !dynamicTextMeshes.Contains(textMesh))
                {
                    dynamicTextMeshes.Add(textMesh);
                }

                // Translate and apply font using unified helper
                if (translator.TranslateAndApplyFont(textMesh, path, translatedTextMeshes))
                {
                    translatedCount++;
                    translatedPaths.Add(path);
                    translatedTextMeshes.Add(textMesh);  // Mark as translated
                }
            }

            _logger.LogInfo($"Scene translation complete: {translatedCount} strings translated");
        }

        bool IsPriorityUIElement(string path)
        {
            // Critical UI elements that need instant translation (checked every frame)
            return path.Contains("GUI/Indicators/Interaction") ||     // Interaction prompts
                   path.Contains("GUI/Indicators/Partname") ||        // Part names
                   path.Contains("GUI/Indicators/Subtitles") ||       // Subtitles
                   path.Contains("GUI/HUD/Day/HUDValue");             // Weekday display
        }

        bool IsDynamicUIElement(string path)
        {
            // These UI elements are frequently updated and need continuous monitoring
            return path.Contains("GUI/HUD/") ||           // HUD elements (hunger, stress, etc.)
                   path.Contains("GUI/Indicators/") ||    // Subtitles, interaction prompts
                   (path.Contains("Sheets/YellowPagesMagazine/Page") && path.EndsWith("/Lines/YellowLine")) ||    // Yellow Pages Magazine
                   path.Contains("GUI/") && !path.Contains("/Debug/");
        }

        string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return "";

            // Check cache first
            if (pathCache.TryGetValue(obj, out string cachedPath))
                return cachedPath;

            // Build path using StringBuilder for better performance
            StringBuilder pathBuilder = new StringBuilder();
            Transform current = obj.transform;

            while (current != null)
            {
                if (pathBuilder.Length > 0)
                    pathBuilder.Insert(0, "/");
                pathBuilder.Insert(0, current.name);
                current = current.parent;
            }

            string path = pathBuilder.ToString();

            // Cache the path
            pathCache[obj] = path;

            return path;
        }

        AssetBundle LoadBundle(string assetBundlePath)
        {
            // Match MSCLoader exactly - keep it simple
            if (!File.Exists(assetBundlePath))
            {
                throw new System.Exception($"<b>LoadBundle() Error:</b> File not found: <b>{assetBundlePath}</b>");
            }

            _logger.LogInfo($"Loading Asset: {Path.GetFileName(assetBundlePath)}...");
            AssetBundle ab = AssetBundle.CreateFromMemoryImmediate(File.ReadAllBytes(assetBundlePath));

            if (ab == null)
            {
                throw new System.Exception("<b>LoadBundle() Error:</b> CreateFromMemoryImmediate returned null");
            }

            // Log asset names like MSCLoader does
            string[] assetNames = ab.GetAllAssetNames();
            _logger.LogInfo($"Bundle contains {assetNames.Length} assets");

            return ab;
        }
    }
}

