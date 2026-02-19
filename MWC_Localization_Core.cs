using MSCLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MWC_Localization_Core
{
    public class MWC_Localization_Core : Mod
    {
        // Mod metadata
        public override string ID => "MWC_Localization_Core_BR";
        public override string Name => "MWC_Localization_Core";
        public override string Author => "potatosalad775&LucasMonOficial";
        public override string Version => "1.0.6";
        public override string Description => "Multi-language core localization framework for My Winter Car";
        public override Game SupportedGames => Game.MyWinterCar;

        // Translation data
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        private bool hasLoadedTranslations = false;

        // Core handlers
        private MagazineTextHandler magazineHandler;
        private TeletextHandler teletextHandler;
        private ArrayListProxyHandler arrayListHandler;
        private TextMeshTranslator translator;

        // Unified managers
        private SceneTranslationManager sceneManager;
        private UnifiedTextMeshMonitor textMeshMonitor;
        
        // Critical UI monitor (MonoBehaviour for LateUpdate access)
        private GameObject lateUpdateHandlerObject;
        private LateUpdateHandler lateUpdateHandler;

        // Font management
        private static AssetBundle fontBundle;  // Static to persist across MSCLoader instance recreation
        private Dictionary<string, Font> customFonts = new Dictionary<string, Font>();

        // Localization configuration
        private LocalizationConfig config;

        // MSCLoader settings
        private SettingsKeybind reloadKey;
        private SettingsCheckBox showDebugLogs;
        private SettingsCheckBox showWarningLogs;

        // Registration phase - NO logic here, only SetupFunction calls!
        public override void ModSetup()
        {
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
            SetupFunction(Setup.PostLoad, Mod_PostLoad);
            SetupFunction(Setup.Update, Mod_Update);
        }

        // Define settings UI
        private void Mod_Settings()
        {
            // Keybind for reloading translations
            Keybind.AddHeader("Localization Plugin Hotkeys");
            reloadKey = Keybind.Add("reloadKey", "Reload Translations", KeyCode.F8);

            // Show debug console messages
            Settings.AddHeader("Miscellaneous Options");
            showDebugLogs = Settings.AddCheckBox("showDebugLogs", "Show debug messages in console", false);
            showWarningLogs = Settings.AddCheckBox("showWarningLogs", "Show warning / error messages in console", false);
        }

        // Main menu loaded - initialize everything
        private void Mod_OnMenuLoad()
        {
            ModConsole.Print($"[{Name}] Main Menu loaded - initializing localization core...");
            // Initialize collections
            translations = new Dictionary<string, string>();
            customFonts = new Dictionary<string, Font>();

            // Initialize configuration
            config = new LocalizationConfig();
            string configPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt");
            config.LoadConfig(configPath);

            // Initialize core console
            CoreConsole.Initialize(showDebugLogs, showWarningLogs);

            // Initialize handlers
            magazineHandler = new MagazineTextHandler();
            teletextHandler = new TeletextHandler();

            // Initialize scene manager
            sceneManager = new SceneTranslationManager();

            // Load fonts
            LoadCustomFonts();

            // Initialize translator after fonts are loaded
            translator = new TextMeshTranslator(translations, customFonts, magazineHandler, config);

            // Load translations immediately
            LoadTranslations();

            // Load magazine translations from separate file
            string magazinePath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);
            
            // Load teletext translations from separate file
            string teletextPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_teletext.txt");
            teletextHandler.LoadTeletextTranslations(teletextPath);
            translator.LoadFsmPatterns(teletextPath); // Load additional FSM patterns
            
            // Initialize array handler with translation dictionaries and translator
            arrayListHandler = new ArrayListProxyHandler(translations, magazineHandler, translator);
            arrayListHandler.InitializeArrayPaths();
            
            // Initialize unified text mesh monitor
            textMeshMonitor = new UnifiedTextMeshMonitor(translator);

            // Translate main menu
            CoreConsole.Print($"[{Name}] Translating Main Menu...");
            TranslateScene();
            sceneManager.MarkSceneTranslated("MainMenu");

            // Initialize UpdateMainMenu for radio and CD translations
            InitializeUpdateMainMenu();
        }

        // Game fully loaded - translate everything
        private void Mod_PostLoad()
        {
            // Translate game scene
            ModConsole.Print($"[{Name}] Game fully loaded - translating...");
            TranslateScene();
            sceneManager.MarkSceneTranslated("GAME");
            
            // Reset handlers
            teletextHandler.Reset();
            arrayListHandler.Reset();

            // Translate static arrays immediately
            int arrayTranslated = arrayListHandler.TranslateAllArrays();
            if (arrayTranslated > 0)
            {
                CoreConsole.Print($"[{Name}] Translated {arrayTranslated} array items");
                arrayListHandler.ApplyFontsToArrayElements();
            }
            
            // Create MonoBehaviour for LateUpdate monitoring
            // ALL continuous monitoring logic runs in LateUpdate to ensure correct timing
            lateUpdateHandlerObject = new GameObject("MWC_LateUpdateHandler");
            lateUpdateHandler = lateUpdateHandlerObject.AddComponent<LateUpdateHandler>();
            lateUpdateHandler.Initialize(
                this, 
                translator, 
                textMeshMonitor, 
                teletextHandler, 
                arrayListHandler, 
                sceneManager
            );
        }

        // Every frame - scheduling and scene management ONLY
        // All monitoring logic moved to LateUpdateHandler.LateUpdate()
        private void Mod_Update()
        {
            if (!hasLoadedTranslations)
                return;

            // Hotkey check - F8 to reload translations
            if (reloadKey != null && reloadKey.GetKeybindDown())
            {
                ReloadTranslations();
                return;
            }

            string currentScene = Application.loadedLevelName;
            
            // Update scene manager and handle scene changes
            bool sceneChanged = sceneManager.UpdateScene(currentScene);
            
            // Clear caches when entering a new scene
            if (sceneChanged)
            {
                // Clear MonoBehaviour cache. Do NOT destroy the GameObject to avoid
                // unnecessary allocations and potential loss of monitoring. If the
                // handler object is missing, recreate it so monitoring continues.
                if (lateUpdateHandler != null)
                {
                    lateUpdateHandler.ClearCache();
                }

                if (lateUpdateHandlerObject == null)
                {
                    // Recreate the late update handler only if core dependencies exist
                    if (translator != null && textMeshMonitor != null && teletextHandler != null && arrayListHandler != null && sceneManager != null)
                    {
                        lateUpdateHandlerObject = new GameObject("MWC_LateUpdateHandler");
                        lateUpdateHandler = lateUpdateHandlerObject.AddComponent<LateUpdateHandler>();
                        lateUpdateHandler.Initialize(
                            this,
                            translator,
                            textMeshMonitor,
                            teletextHandler,
                            arrayListHandler,
                            sceneManager
                        );
                    }
                }

                CoreConsole.Print($"[{Name}] Scene changed to '{currentScene}' - cleared caches");
            }

            // Initial translation pass for Main Menu (Required for hot reloads)
            if (currentScene == "MainMenu" && sceneManager.ShouldTranslateScene("MainMenu"))
            {
                CoreConsole.Print($"[{Name}] Translating Main Menu...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("MainMenu");
            }

            // Initial translation pass for Game scene (Required for hot reloads)
            if (currentScene == "GAME" && sceneManager.ShouldTranslateScene("GAME"))
            {
                CoreConsole.Print($"[{Name}] Translating Game scene...");
                TranslateScene();
                sceneManager.MarkSceneTranslated("GAME");
                
                // Reset teletext handler and retry tracking for new scene
                teletextHandler.Reset();
                arrayListHandler.Reset();
                
                // Translate static arrays immediately (HUD, menus, etc.)
                int arrayTranslated = arrayListHandler.TranslateAllArrays();
                if (arrayTranslated > 0)
                {
                    CoreConsole.Print($"[{Name}] [Arrays] Initial translation: {arrayTranslated} items");
                    // Apply Korean fonts to TextMesh components using array data
                    arrayListHandler.ApplyFontsToArrayElements();
                }
            }
        }

        bool LoadCustomFonts()
        {
            CoreConsole.Print($"[{Name}] Loading fonts...");

            // Skip font loading if no font mappings configured
            if (config.FontMappings.Count == 0)
            {
                CoreConsole.Print($"[{Name}] No font mappings configured - using default fonts");
                return false;
            }

            try
            {
                // Load bundle only if not already loaded
                if (fontBundle == null)
                {
                    fontBundle = LoadAssets.LoadBundle(this, "fonts.unity3d");
                    CoreConsole.Print($"[{Name}] Bundle loaded, result: {(fontBundle == null ? "NULL" : "NOT NULL")}");
                }
                
                if (fontBundle == null)
                {
                    CoreConsole.Warning($"[{Name}] Failed to load font bundle");
                    return false;
                }

                foreach (var pair in config.FontMappings)
                {
                    Font font = fontBundle.LoadAsset(pair.Value, typeof(Font)) as Font;
                    if (font != null)
                    {
                        customFonts[pair.Key] = font;
                        CoreConsole.Print($"[{Name}] Loaded font: {pair.Value} for {pair.Key}");
                    }
                    else
                    {
                        CoreConsole.Warning($"[{Name}] Failed to load font asset: {pair.Value}");
                    }
                }

                if (customFonts.Count > 0)
                {
                    CoreConsole.Print($"[{Name}] Loaded {customFonts.Count} custom fonts");
                    return true;
                }
                else
                {
                    CoreConsole.Warning($"[{Name}] No fonts loaded from bundle");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Font loading failed: {ex.Message}");
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
                    if (string.IsNullOrEmpty(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex > 0)
                    {
                        string key = line.Substring(0, separatorIndex).Trim();
                        // Preserve leading spaces in the value (they may be significant for layout)
                        // TrimEnd to remove accidental trailing whitespace but keep leading spaces after '='
                        string value = line.Substring(separatorIndex + 1).TrimEnd();
                        // If file authors used the common formatting "key = value" (exactly one space after '='),
                        // remove that single separator space. If there are multiple spaces after '=', treat them as intentional
                        // (do not remove), so authors can add leading spaces for layout.
                        if (line.Length > separatorIndex + 1 && line[separatorIndex + 1] == ' ')
                        {
                            bool hasSecondSpace = (line.Length > separatorIndex + 2 && line[separatorIndex + 2] == ' ');
                            if (!hasSecondSpace && value.Length > 0 && value[0] == ' ')
                            {
                                // only remove the single separator space
                                value = value.Substring(1);
                            }
                        }

                        string normalizedKey = MLCUtils.FormatUpperKey(key);
                        string processedValue = value.Replace("\\n", "\n");

                        if (!string.IsNullOrEmpty(normalizedKey))
                        {
                            translations[normalizedKey] = processedValue;
                        }
                    }
                }

                hasLoadedTranslations = true;
                CoreConsole.Print($"[{Name}] Loaded {translations.Count} translations from {Path.GetFileName(translationPath)}");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Failed to load translations: {ex.Message}");
            }
        }

        void LoadTranslations()
        {
            // Load translation file used in My Summer Car first
            string mscTranslationPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_msc.txt");
            
            if (!File.Exists(mscTranslationPath))
            {
                CoreConsole.Warning($"[{Name}] Translation file not found: {mscTranslationPath}");
            }
            else 
            {
                InsertTranslationLines(mscTranslationPath);
                translator.LoadFsmPatterns(mscTranslationPath);
            }

            // Load main translation file for My Winter Car
            string translationPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate.txt");

            if (!File.Exists(translationPath))
            {
                CoreConsole.Warning($"[{Name}] Translation file not found: {translationPath}");
            }
            else 
            {
                InsertTranslationLines(translationPath);
                translator.LoadFsmPatterns(translationPath);
            }

            // Load mod translation file for My Winter Car
            string modTranslationPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_mod.txt");

            if (!File.Exists(modTranslationPath))
            {
                CoreConsole.Warning($"[{Name}] Translation file not found: {modTranslationPath}");
            }
            else 
            {
                InsertTranslationLines(modTranslationPath);
                translator.LoadFsmPatterns(modTranslationPath);
            }
        }

        void ReloadTranslations()
        {
            CoreConsole.Print($"[{Name}] [F8] Reloading translations...");

            // Clear existing translations
            translations.Clear();
            magazineHandler.ClearTranslations();
            arrayListHandler.ClearTranslations();

            // Reset text adjustment caches and reload config
            config.ClearTextAdjustmentCaches();
            string configPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt");
            config.LoadConfig(configPath);

            // Reload from file
            LoadTranslations();

            // Reload magazine translations
            string magazinePath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_magazine.txt");
            magazineHandler.LoadMagazineTranslations(magazinePath);
            
            // Reload teletext translations
            string teletextPath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_teletext.txt");
            teletextHandler.LoadTeletextTranslations(teletextPath);
            
            // Reload FSM patterns from main file first
            string mainTranslatePath = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate.txt");
            translator.LoadFsmPatterns(mainTranslatePath);
            
            // Reload additional FSM patterns from teletext file
            translator.LoadFsmPatterns(teletextPath);
            
            // Reset and re-initialize LateUpdateHandler to find critical UI again
            if (lateUpdateHandler != null)
            {
                lateUpdateHandler.ClearCache();
                // Re-initialize to find critical UI components again
                lateUpdateHandler.Initialize(
                    this, 
                    translator, 
                    textMeshMonitor, 
                    teletextHandler, 
                    arrayListHandler, 
                    sceneManager
                );
            }
            else
            {
                // If the handler object was destroyed elsewhere, recreate it so monitoring continues
                if (lateUpdateHandlerObject == null)
                {
                    lateUpdateHandlerObject = new GameObject("MWC_LateUpdateHandler");
                    lateUpdateHandler = lateUpdateHandlerObject.AddComponent<LateUpdateHandler>();
                    lateUpdateHandler.Initialize(
                        this,
                        translator,
                        textMeshMonitor,
                        teletextHandler,
                        arrayListHandler,
                        sceneManager
                    );
                }
            }

            // Reset managers
            sceneManager.ResetAll();
            textMeshMonitor.Clear();
            
            // Reset teletext handler
            teletextHandler.Reset();
            arrayListHandler.Reset();

            // Reapply fonts and adjustments to all TextMeshes (after restore)
            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();
            int reappliedCount = 0;
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm != null && !string.IsNullOrEmpty(tm.text))
                {
                    string path = MLCUtils.GetGameObjectPath(tm.gameObject);
                    translator.ApplyCustomFont(tm, path);
                    reappliedCount++;
                }
            }

            CoreConsole.Print($"[{Name}] [F8] Reloaded {translations.Count} translations. Reapplied fonts/adjustments to {reappliedCount} TextMeshes.");
        }

        void TranslateScene()
        {
            textMeshMonitor.RegisterAllPathRuleElements();

            // If we have the LateUpdateHandler (MonoBehaviour), use its incremental scanner to
            // avoid doing a large synchronous scan in a single frame. Otherwise fall back to
            // immediate scanning (used during MainMenu initialization where coverage is small).
            if (lateUpdateHandler != null)
            {
                lateUpdateHandler.StartSceneTranslationIncremental(LocalizationConstants.TRANSLATION_BATCH_SIZE);
                return;
            }

            // Fallback synchronous scan (menu or no handler available)
            TextMesh[] allTextMeshes = Resources.FindObjectsOfTypeAll<TextMesh>();
            int translatedCount = 0;

            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;

                string path = MLCUtils.GetGameObjectPath(tm.gameObject);
                if (translator.TranslateAndApplyFont(tm, path, null))
                    translatedCount++;
            }

            CoreConsole.Print($"[{Name}] Scene translation complete: {translatedCount}/{allTextMeshes.Length} TextMesh objects translated");
        }

        void InitializeUpdateMainMenu()
        {
            try
            {
                // Create a temporary GameObject to attach the UpdateMainMenu component
                GameObject tempObject = new GameObject("UpdateMainMenuHost");
                UpdateMainMenu updateMainMenu = tempObject.AddComponent<UpdateMainMenu>();
                
                // Initialize with translation dictionary
                updateMainMenu.Initialize(translations);
                
                CoreConsole.Print($"[{Name}] UpdateMainMenu initialized successfully");
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Failed to initialize UpdateMainMenu: {ex.Message}");
            }
        }
    }
}
