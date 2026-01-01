// My Winter Car - Simple Localization Plugin
// BepInEx Plugin for Korean translation support
// Installation: Place compiled DLL in BepInEx/plugins/

using BepInEx;
using BepInEx.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MWC_Localization_Core
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class SimpleLocalization : BaseUnityPlugin
    {
        public const string GUID = "com.potatosalad.mwc_localization_core";
        public const string PluginName = "MWC Localization Core";
        public const string Version = "0.2.0";

        // Constants for dynamic element scanning
        private const float MAINMENU_SCAN_INTERVAL = 0.5f;

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
        private Dictionary<string, Font> koreanFonts = new Dictionary<string, Font>();
        private bool usingAssetBundleFonts = false;

        private Dictionary<string, string> koreanFontPair = new Dictionary<string, string>
        {
            { "FugazOne-Regular", "NanumSquareRoundEB"},
            { "Heebo-Black", "PaperlogyExtraBold"},
            { "AlfaSlabOne-Regular", "ROKAFSlabSerifBold"},
            { "ArchivoBlack-Regular", "PaperlogyExtraBold"},
            { "Cour10Bd", "D2Coding"},
            { "RAGE", "KyoboHandwriting2019"},
            { "Dosis-SemiBold", "TmoneyRoundWindExtraBold"},
            { "Dosis-SemiBold LowCase", "TmoneyRoundWindExtraBold"},
            { "VT323-Regular", "GalmuriMono11"},
            { "DroidSerif-Bold", "MaruBuri-Bold"},
            { "PlayfairDisplay-Bold", "MaruBuri-Bold"},
            { "WalterTurncoat-Regular", "MaruBuri-Bold"},
            { "BLUEHIGD", "D2Coding"}
        };

        // Dynamic UI element tracking
        private List<TextMesh> dynamicTextMeshes = new List<TextMesh>();
        private List<TextMesh> priorityTextMeshes = new List<TextMesh>();  // High-priority elements (checked every frame)
        private HashSet<string> translatedPaths = new HashSet<string>();
        private float lastMainMenuScanTime = 0f;

        // GameObject path cache for performance
        private Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();

        void Awake()
        {
            _logger = Logger;
            _logger.LogInfo($"{PluginName} v{Version} loaded!");

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
            LoadKoreanFonts();

            // Initialize translator after fonts are loaded
            translator = new TextMeshTranslator(translations, koreanFonts, magazineHandler, _logger);
        }

        bool LoadKoreanFonts()
        {
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

                // Load fonts for different original fonts
                foreach (var pair in koreanFontPair)
                {
                    string originalFontName = pair.Key;
                    string assetFontName = pair.Value;

                    Font font = fontBundle.LoadAsset(assetFontName, typeof(Font)) as Font;
                    if (font != null)
                    {
                        koreanFonts[originalFontName] = font;
                        _logger.LogInfo($"Loaded {assetFontName} for {originalFontName}");
                    }
                }

                if (koreanFonts.Count > 0)
                {
                    _logger.LogInfo($"Successfully loaded {koreanFonts.Count} Korean fonts from asset bundle");
                    usingAssetBundleFonts = true;
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

        void LoadTranslations()
        {
            string translationPath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate.txt");

            if (!File.Exists(translationPath))
            {
                _logger.LogWarning($"Translation file not found: {translationPath}");
                return;
            }

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

                _logger.LogInfo($"Loaded {translations.Count} translations");
                hasLoadedTranslations = true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Failed to load translations: {ex.Message}");
            }
        }

        void ReloadTranslations()
        {
            _logger.LogInfo("[F9] Reloading translations...");

            // Clear existing translations
            translations.Clear();
            magazineHandler.ClearTranslations();

            // Reload from file
            LoadTranslations();

            // Reload magazine translations
            string magazinePath = Path.Combine(Path.Combine(Paths.PluginPath, "l10n_assets"), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);

            // Clear caches to force re-translation
            translatedPaths.Clear();
            pathCache.Clear();
            dynamicTextMeshes.Clear();
            priorityTextMeshes.Clear();

            // Reset scene translation flags
            hasTranslatedSplashScreen = false;
            hasTranslatedMainMenu = false;
            hasTranslatedGameScene = false;

            _logger.LogInfo($"[F9] Reloaded {translations.Count} translations. Current scene will be re-translated.");
        }
        void Update()
        {
            if (!hasLoadedTranslations)
                return;

            // F9 key: Reload translations at runtime
            if (Input.GetKeyDown(KeyCode.F9))
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
                pathCache.Clear();  // Clear path cache on scene change
            }
            else if (currentScene == "GAME")
            {
                hasTranslatedMainMenu = false;
                translatedPaths.Clear();
                priorityTextMeshes.Clear();
                pathCache.Clear();  // Clear path cache on scene change
            }
        }

        void LateUpdate()
        {
            string currentScene = Application.loadedLevelName;

            if (currentScene == "GAME" && hasTranslatedGameScene)
            {
                // Check priority elements every frame (no throttling)
                UpdatePriorityTextMeshes();

                // Scan for new dynamic UI elements and update existing ones
                UpdateDynamicTextMeshes();
            }
            else if (currentScene == "MainMenu" && hasTranslatedMainMenu)
            {
                // Handle late-loading MainMenu elements
                ScanForNewMainMenuElements();
                UpdateDynamicTextMeshes();
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
                if (translator.TranslateAndApplyFont(textMesh, path))
                {
                    // Check if this element needs continuous monitoring
                    if (path.Contains("Interface/Songs/") && !dynamicTextMeshes.Contains(textMesh))
                    {
                        dynamicTextMeshes.Add(textMesh);
                    }

                    translatedPaths.Add(path);
                }
            }
        }

        void UpdatePriorityTextMeshes()
        {
            // Check priority elements every frame - no throttling for instant response
            // Use GameObject.Find for specific paths instead of scanning all TextMeshes (performance!)
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
                GameObject obj = GameObject.Find(path);
                if (obj != null)
                {
                    TextMesh tm = obj.GetComponent<TextMesh>();
                    if (tm != null)
                    {
                        // Add to priority list if not already there
                        if (!priorityTextMeshes.Contains(tm))
                        {
                            priorityTextMeshes.Add(tm);
                        }

                        // Translate if needed
                        translator.TranslateAndApplyFont(tm, path);
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
                    continue;
                }

                if (string.IsNullOrEmpty(textMesh.text))
                    continue;

                string path = GetGameObjectPath(textMesh.gameObject);

                // Check if this is magazine text that needs persistent monitoring
                bool isMagazineText = magazineHandler.IsMagazineText(path);

                // Skip if already in Korean - remove from monitoring UNLESS it's magazine text
                // (magazine text can be regenerated by the game, so we need to keep monitoring it)
                if (StringHelper.ContainsKorean(textMesh.text) && !isMagazineText)
                {
                    dynamicTextMeshes.RemoveAt(i);
                    continue;
                }

                // Translate and apply font using unified helper
                translator.TranslateAndApplyFont(textMesh, path);
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
                if (translator.TranslateAndApplyFont(textMesh, path))
                {
                    translatedCount++;
                    translatedPaths.Add(path);
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

