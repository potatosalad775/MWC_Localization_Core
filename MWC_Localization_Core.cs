using MSCLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace MWC_Localization_Core
{
    public class MWC_Localization_Core : Mod
    {
        // Mod metadata
        public override string ID => "MWC_Localization_Core";
        public override string Name => "MWC_Localization_Core";
        public override string Author => "potatosalad775";
        public override string Version => "1.1.3";
        public override string Description => "Multi-language core localization framework for My Winter Car";
        public override Game SupportedGames => Game.MyWinterCar;

        private static readonly string[] MainTranslationFiles = new string[]
        {
            "translate_msc.txt",
            "translate.txt",
            "translate_mod.txt"
        };

        // translate_magazine.txt entries are restricted to these object-path prefixes
        // so that short magazine-only tokens (e.g. "h.") cannot leak into unrelated
        // game text. See TranslationDictionary.AddScoped.
        private const string MagazineScopeId = "magazine";
        private static readonly string[] MagazineScopePathPrefixes = new string[]
        {
            "CARPARTS/PARTSYSTEM/PostSystem/",
            "Sheets/YellowPagesMagazine/",
        };

        // Shared state
        private TranslationDictionary translations = new TranslationDictionary();
        private Dictionary<string, Font> customFonts = new Dictionary<string, Font>();
        private LocalizationConfig config;
        private TextMeshTranslator translator;
        private TranslationContext ctx;
        private List<ITranslationSurface> surfaces;

        // Scene state (formerly SceneTranslationManager).
        // Leaving a scene clears its translated flag so we re-translate on return.
        private string currentScene = string.Empty;
        private readonly HashSet<string> translatedScenes = new HashSet<string>();

        // LateUpdate driver
        private GameObject lateUpdateHandlerObject;
        private LateUpdateHandler lateUpdateHandler;

        // Font bundle (static so it persists across MSCLoader instance recreation)
        private static AssetBundle fontBundle;
        private bool hasLoadedTranslations = false;

        // MSCLoader settings
        private SettingsKeybind reloadKey;
        private SettingsCheckBox showDebugLogs;
        private SettingsCheckBox showWarningLogs;

        public override void ModSetup()
        {
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
            SetupFunction(Setup.PostLoad, Mod_PostLoad);
            SetupFunction(Setup.Update, Mod_Update);
        }

        private void Mod_Settings()
        {
            Keybind.AddHeader("Localization Plugin Hotkeys");
            reloadKey = Keybind.Add("reloadKey", "Reload Translations", KeyCode.F8);

            Settings.AddHeader("Miscellaneous Options");
            showDebugLogs = Settings.AddCheckBox("showDebugLogs", "Show debug messages in console", false);
            showWarningLogs = Settings.AddCheckBox("showWarningLogs", "Show warning / error messages in console", false);
        }

        private void Mod_OnMenuLoad()
        {
            ModConsole.Print($"[{Name}] Main Menu loaded - initializing localization core...");

            translations = new TranslationDictionary();
            customFonts = new Dictionary<string, Font>();

            config = new LocalizationConfig();
            config.LoadConfig(Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt"));

            CoreConsole.Initialize(showDebugLogs, showWarningLogs);

            translatedScenes.Clear();
            currentScene = string.Empty;

            LoadCustomFonts();

            translator = new TextMeshTranslator(translations, customFonts, config);

            LoadAllMainTranslationFiles();
            LoadMagazineFormatTranslations();

            ctx = new TranslationContext(
                translations,
                customFonts,
                config,
                translator,
                ModLoader.GetModAssetsFolder(this));

            surfaces = new List<ITranslationSurface>
            {
                new FsmGuiTranslator(),
                new FsmArrayTranslator(),
                new ArrayListProxyHandler(),
                new HashTableProxyHandler(),
                new FsmTextHook(),
                new TextureReplacementSurface(),
            };

            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Initialize(ctx);

            CoreConsole.Print($"[{Name}] Translating Main Menu...");
            TranslateScene();
            MarkSceneTranslated("MainMenu");
            RunSurfaceInitialPasses(null);
            config.ApplyGameObjectAdjustments();
        }

        private void Mod_PostLoad()
        {
            ModConsole.Print($"[{Name}] Game fully loaded - translating...");
            TranslateScene();
            MarkSceneTranslated("GAME");
            RunSurfaceInitialPasses(null);
            config.ApplyGameObjectAdjustments();

            // ALL continuous monitoring runs in LateUpdate to get correct timing relative to game updates.
            lateUpdateHandlerObject = new GameObject("MWC_LateUpdateHandler");
            lateUpdateHandler = lateUpdateHandlerObject.AddComponent<LateUpdateHandler>();
            lateUpdateHandler.Initialize(surfaces, () => HasSceneBeenTranslated("GAME"));
        }

        private void Mod_Update()
        {
            if (!hasLoadedTranslations)
                return;

            if (reloadKey != null && reloadKey.GetKeybindDown())
            {
                ReloadTranslations();
                return;
            }

            string sceneName = Application.loadedLevelName;
            bool sceneChanged = UpdateScene(sceneName);

            if (sceneChanged)
            {
                LocalizationUtils.PruneCaches();

                if (lateUpdateHandler != null)
                    lateUpdateHandler.ClearCache();
                if (translator != null)
                    translator.ClearRuntimeCaches();

                if (surfaces != null)
                {
                    for (int i = 0; i < surfaces.Count; i++)
                        surfaces[i].Reset();
                }

                if (lateUpdateHandlerObject != null)
                {
                    Object.Destroy(lateUpdateHandlerObject);
                    lateUpdateHandlerObject = null;
                    lateUpdateHandler = null;
                }

                CoreConsole.Print($"[{Name}] Scene changed to '{sceneName}' - cleared caches");
            }

            // Initial translation pass for the current scene (covers hot reloads where
            // OnMenuLoad / PostLoad already fired but the scene was reset).
            if (sceneName == "MainMenu" && ShouldTranslateScene("MainMenu"))
            {
                CoreConsole.Print($"[{Name}] Translating Main Menu...");
                TranslateScene();
                MarkSceneTranslated("MainMenu");
                RunSurfaceInitialPasses(null);
                config.ApplyGameObjectAdjustments();
            }
            else if (sceneName == "GAME" && ShouldTranslateScene("GAME"))
            {
                CoreConsole.Print($"[{Name}] Translating Game scene...");
                TranslateScene();
                MarkSceneTranslated("GAME");
                RunSurfaceInitialPasses("Initial ");
                config.ApplyGameObjectAdjustments();
            }
        }

        // Scene tracking (formerly SceneTranslationManager). Leaving a known scene
        // clears its translated flag so returning to it re-runs the initial pass.
        private bool UpdateScene(string newScene)
        {
            if (string.IsNullOrEmpty(newScene) || currentScene == newScene)
                return false;

            if (!string.IsNullOrEmpty(currentScene))
                translatedScenes.Remove(currentScene);

            currentScene = newScene;
            return true;
        }

        private bool ShouldTranslateScene(string scene) { return !translatedScenes.Contains(scene); }
        private bool HasSceneBeenTranslated(string scene) { return translatedScenes.Contains(scene); }
        private void MarkSceneTranslated(string scene) { translatedScenes.Add(scene); }

        private void RunSurfaceInitialPasses(string logPrefix)
        {
            if (surfaces == null) return;
            for (int i = 0; i < surfaces.Count; i++)
            {
                int count = surfaces[i].InitialPass();
                if (count > 0)
                    CoreConsole.Print($"[{Name}] {logPrefix ?? string.Empty}{surfaces[i].Name}: translated {count}");
            }
        }

        private void LoadAllMainTranslationFiles()
        {
            string assets = ModLoader.GetModAssetsFolder(this);
            foreach (string fileName in MainTranslationFiles)
            {
                string path = Path.Combine(assets, fileName);
                if (!File.Exists(path))
                {
                    CoreConsole.Warning($"[{Name}] Translation file not found: {path}");
                    continue;
                }

                Dictionary<string, string> loaded = TranslationFileParser.ParseKeyValueFile(
                    path,
                    normalizeKeys: true,
                    overwriteExisting: true);
                translations.AddAll(loaded);
                translations.LoadPatternsFromFile(path);

                hasLoadedTranslations = true;
                CoreConsole.Print($"[{Name}] Loaded {loaded.Count} translations from {fileName} ({translations.Count} total)");
            }
        }

        private void LoadMagazineFormatTranslations()
        {
            string path = Path.Combine(ModLoader.GetModAssetsFolder(this), "translate_magazine.txt");
            Dictionary<string, string> loaded = TranslationFileParser.ParseKeyValueFile(
                path,
                normalizeKeys: true,
                overwriteExisting: true);

            string phoneLabel;
            if (!loaded.TryGetValue("PHONE", out phoneLabel) || string.IsNullOrEmpty(phoneLabel))
                phoneLabel = "PHONE";

            translations.AddScoped(MagazineScopeId, MagazineScopePathPrefixes, loaded);
            translations.AddScopedEntry(MagazineScopeId, "h.", string.Empty);
            translations.AddScopedEntry(MagazineScopeId, ",- puh.", " MK, " + phoneLabel + " -");
            hasLoadedTranslations = true;

            if (File.Exists(path))
                CoreConsole.Print($"[{Name}] Loaded {loaded.Count} magazine translations from translate_magazine.txt (scoped)");
            else
                CoreConsole.Warning($"[{Name}] Magazine format file not found: {path}; using default phone label");
        }

        bool LoadCustomFonts()
        {
            CoreConsole.Print($"[{Name}] Loading fonts...");

            if (config.FontMappings.Count == 0)
            {
                CoreConsole.Print($"[{Name}] No font mappings configured - using default fonts");
                return false;
            }

            try
            {
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
                    if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                        continue;

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

                CoreConsole.Warning($"[{Name}] No fonts loaded from bundle");
                return false;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[{Name}] Font loading failed: {ex.Message}");
                return false;
            }
        }

        void ReloadTranslations()
        {
            CoreConsole.Print($"[{Name}] [F8] Reloading translations...");

            // Drop translation data
            translations.Clear();
            translations.ResetPatterns();
            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].ClearTranslations();

            // Clear global + service caches; let surfaces reset their own runtime state.
            LocalizationUtils.ClearCaches();
            translator.ClearRuntimeCaches();
            config.ClearTextAdjustmentCaches();
            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Reset();

            // Reload config + fonts + main translation files
            config.LoadConfig(Path.Combine(ModLoader.GetModAssetsFolder(this), "config.txt"));
            customFonts.Clear();
            LoadCustomFonts();
            LoadAllMainTranslationFiles();
            LoadMagazineFormatTranslations();

            // Re-init surfaces (each loads its own translation files in Initialize)
            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Initialize(ctx);

            translatedScenes.Clear();

            // Reapply fonts to existing TextMeshes (re-translate happens via the per-scene initial pass)
            TextMesh[] allTextMeshes = LocalizationUtils.GetAllTextMeshesIncludingInactive();
            int reappliedCount = 0;
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null || string.IsNullOrEmpty(tm.text))
                    continue;
                string path = LocalizationUtils.GetGameObjectPath(tm.gameObject);
                translator.ApplyCustomFont(tm, path);
                reappliedCount++;
            }

            // Force initial passes for the current scene if applicable
            string sceneName = Application.loadedLevelName;
            if (sceneName == "MainMenu" || sceneName == "GAME")
            {
                currentScene = sceneName;
                MarkSceneTranslated(sceneName);
                RunSurfaceInitialPasses("Reload ");
                config.ApplyGameObjectAdjustments();
            }

            // Restart LateUpdate driver with the new surface list (instance hasn't changed but its tick state has)
            if (lateUpdateHandler != null)
            {
                lateUpdateHandler.ClearCache();
                lateUpdateHandler.Initialize(surfaces, () => HasSceneBeenTranslated("GAME"));
            }

            CoreConsole.Print($"[{Name}] [F8] Reloaded {translations.Count} translations. Reapplied fonts/adjustments to {reappliedCount} TextMeshes.");
        }

        void TranslateScene()
        {
            // Find all TextMesh components in the scene, including inactive objects.
            TextMesh[] allTextMeshes = LocalizationUtils.GetAllTextMeshesIncludingInactive();
            int translatedCount = 0;
            int forcedFontAppliedCount = 0;

            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm == null)
                    continue;

                string path = LocalizationUtils.GetGameObjectPath(tm.gameObject);

                if (!string.IsNullOrEmpty(tm.text))
                {
                    if (translator.TranslateAndApplyFont(tm, path))
                        translatedCount++;
                }

                if (LocalizationConfig.IsForcedFontPath(path) && translator.ApplyFontOnly(tm, path))
                    forcedFontAppliedCount++;
            }

            CoreConsole.Print($"[{Name}] Scene translation complete: {translatedCount}/{allTextMeshes.Length} TextMesh objects translated, forced font pass: {forcedFontAppliedCount}");
        }
    }
}
