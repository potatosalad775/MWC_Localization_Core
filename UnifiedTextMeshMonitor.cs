using MSCLoader;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Represents a TextMesh component being monitored for translation
    /// </summary>
    public class TextMeshEntry
    {
        public TextMesh TextMesh;
        public GameObject GameObject;
        public string Path;
        public MonitoringStrategy Strategy;
        public string LastText;
        public bool WasTranslated;
        public bool IsVisible;

        public TextMeshEntry(TextMesh textMesh, string path, MonitoringStrategy strategy)
        {
            TextMesh = textMesh;
            GameObject = textMesh != null ? textMesh.gameObject : null;
            Path = path;
            Strategy = strategy;
            LastText = textMesh != null ? textMesh.text : "";
            WasTranslated = false;
            IsVisible = GameObject != null && GameObject.activeInHierarchy;
        }

        public bool IsValid()
        {
            return TextMesh != null && GameObject != null;
        }

        public bool HasTextChanged()
        {
            if (TextMesh == null || string.IsNullOrEmpty(TextMesh.text))
                return false;

            return LastText != TextMesh.text;
        }

        public void UpdateLastText()
        {
            if (TextMesh != null)
            {
                LastText = TextMesh.text;
            }
        }

        public void UpdateVisibility()
        {
            IsVisible = GameObject != null && GameObject.activeInHierarchy;
        }
    }

    /// <summary>
    /// Unified monitoring system for all TextMesh translation
    /// Replaces UpdatePriorityTextMeshes, UpdateDynamicTextMeshes, and FSM monitoring
    /// </summary>
    public class UnifiedTextMeshMonitor
    {
        private TextMeshTranslator translator;
        private MWC_Localization_Core plugin;  // Reference to plugin for registering original text

        // Throttling timers
        private float fastPollingTimer;
        private float slowPollingTimer;
        private float visibilityPollingTimer;

        // Path-based monitoring rules (pattern -> strategy mapping)
        private Dictionary<string, MonitoringStrategy> pathRules;
        
        // Instance-based storage (supports multiple TextMeshes per path)
        private Dictionary<int, TextMeshEntry> instanceEntries;  // instanceID -> entry
        private Dictionary<string, HashSet<int>> pathToInstances;  // path -> instanceIDs
        private Dictionary<MonitoringStrategy, HashSet<int>> strategyGroups;  // strategy -> instanceIDs
        private HashSet<string> monitoredPaths = new HashSet<string>();
        private List<int> removalBuffer = new List<int>(64);
        private List<string> stringRemovalBuffer = new List<string>(16);

        public UnifiedTextMeshMonitor(TextMeshTranslator translator, MWC_Localization_Core plugin = null)
        {
            this.translator = translator;
            this.plugin = plugin;
            pathRules = new Dictionary<string, MonitoringStrategy>();
            instanceEntries = new Dictionary<int, TextMeshEntry>();
            pathToInstances = new Dictionary<string, HashSet<int>>();
            strategyGroups = new Dictionary<MonitoringStrategy, HashSet<int>>();

            // Initialize strategy groups
            foreach (MonitoringStrategy strategy in System.Enum.GetValues(typeof(MonitoringStrategy)))
            {
                strategyGroups[strategy] = new HashSet<int>();
            }

            InitializeDefaultPathRules();
        }

        private void InitializeDefaultPathRules()
        {
            // Critical UI - every frame
            AddPathRule("GUI/Indicators/Interaction", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Interaction/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Partname", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Partname/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Subtitles", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Subtitles/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/TaxiGUI", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/TaxiGUI/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Gear", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/Indicators/Gear/Shadow", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/HUD/Thrist/HUDLabel", MonitoringStrategy.EveryFrame);
            AddPathRule("GUI/HUD/Thrist/HUDLabel/Shadow", MonitoringStrategy.EveryFrame);

            // Active HUD - fast polling (10 FPS)
            AddPathRule("GUI/HUD/Mortal/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Day/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Thirst/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Hunger/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Stress/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Urine/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Fatigue/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Money/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Bodytemp/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Sweat/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("GUI/HUD/Jailtime/HUDValue", MonitoringStrategy.FastPolling);
            AddPathRule("Systems/TV/TVGraphics/CHAT/Day", MonitoringStrategy.FastPolling);
            AddPathRule("Systems/TV/TVGraphics/CHAT/Moderator", MonitoringStrategy.FastPolling);
            AddPathRule("Systems/TV/Teletext/VKTekstiTV/HEADER/Texts/Status", MonitoringStrategy.FastPolling);

            // Teletext/FSM displays are primarily translated at array/FSM source level.
            // Use one-shot late registration to avoid rescanning large TV trees continuously.
            AddPathRule("Systems/TV/Teletext/VKTekstiTV/PAGES", MonitoringStrategy.LateTranslateOnce);
            AddPathRule("Systems/TV/TVGraphics/CHAT/Generated", MonitoringStrategy.LateTranslateOnce);

            // Magazine / Sheets - on visibility change
            AddPathRule("Sheets/UnemployPaper", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/ServiceBrochure", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/ServicePayment", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/TrafficTicket", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("COMPUTER/SYSTEM/TELEBBS/CONLINE/CommandLine", MonitoringStrategy.OnVisibilityChange);

            // Magazine / Sheets - persistent monitoring due to dynamic content changes and rebuilds
            AddPathRule("Sheets/YellowPagesMagazine/Page1", MonitoringStrategy.Persistent);
            AddPathRule("Sheets/YellowPagesMagazine/Page2", MonitoringStrategy.Persistent);
            AddPathRule("PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat", MonitoringStrategy.Persistent);
            AddPathRule("COMPUTER/SYSTEM/POS/NoOS", MonitoringStrategy.Persistent);
        }

        public void AddPathRule(string pathPattern, MonitoringStrategy strategy)
        {
            pathRules[pathPattern] = strategy;
        }

        /// <summary>
        /// Register all TextMeshes under defined path rules
        /// </summary>
        public void RegisterAllPathRuleElements()
        {
            foreach (string parentPath in pathRules.Keys)
            {
                MonitoringStrategy strategy = pathRules[parentPath];
                if (strategy == MonitoringStrategy.LateTranslateOnce ||
                    strategy == MonitoringStrategy.OnVisibilityChange ||
                    strategy == MonitoringStrategy.Persistent)
                {
                    monitoredPaths.Add(parentPath);
                }

                Register(parentPath, strategy);
            }
        }

        /// <summary>
        /// Periodically monitor TextMeshes to be available for monitoring
        /// </summary>
        public void MonitorLateRegister()
        {
            stringRemovalBuffer.Clear();
            foreach (string parentPath in monitoredPaths)
            {
                MonitoringStrategy strategy;
                if (!pathRules.TryGetValue(parentPath, out strategy))
                    strategy = MonitoringStrategy.LateTranslateOnce;

                int registered = Register(parentPath, strategy);

                // Remove from monitoring list if successfully registered.
                // Persistent paths stay in the scan because their child TextMeshes can be rebuilt.
                if (registered > 0 && strategy != MonitoringStrategy.Persistent)
                {
                    stringRemovalBuffer.Add(parentPath);
                }
            }
            for (int i = 0; i < stringRemovalBuffer.Count; i++)
            {
                monitoredPaths.Remove(stringRemovalBuffer[i]);
            }
        }

        /// <summary>
        /// Register a TextMesh for monitoring
        /// Supports multiple TextMeshes at the same path
        /// </summary>
        public int Register(string parentPath, MonitoringStrategy? strategy = null)
        {
            int registeredCount = 0;

            // Determine strategy if not provided
            MonitoringStrategy finalStrategy = strategy ?? pathRules[parentPath];

            GameObject parent = MLCUtils.FindGameObjectCached(parentPath);
            if (parent == null)
                return registeredCount;

            // Get all TextMesh components under this parent
            TextMesh[] textMeshes = parent.GetComponentsInChildren<TextMesh>(true);
            foreach (var textMesh in textMeshes)
            {
                if (textMesh == null)
                    continue;

                int instanceID = textMesh.GetInstanceID();
                
                // Skip if this specific instance is already registered
                if (instanceEntries.ContainsKey(instanceID))
                    continue;

                string textMeshPath = MLCUtils.GetGameObjectPath(textMesh.gameObject);
                
                // Register original text before translation for reload support
                if (plugin != null && !string.IsNullOrEmpty(textMesh.text))
                {
                    plugin.RegisterOriginalMeshText(textMeshPath, textMesh.text);
                }
                
                // Translate first to reduce visible unlocalized text
                bool translated = translator.TranslateAndApplyFont(textMesh, textMeshPath, null);

                TextMeshEntry entry = new TextMeshEntry(textMesh, textMeshPath, finalStrategy);
                if (translated)
                {
                    entry.WasTranslated = true;
                    entry.UpdateLastText();
                }

                // Register instance
                instanceEntries[instanceID] = entry;
                
                // Index by path
                HashSet<int> pathSet;
                if (!pathToInstances.TryGetValue(textMeshPath, out pathSet))
                {
                    pathSet = new HashSet<int>();
                    pathToInstances[textMeshPath] = pathSet;
                }
                pathSet.Add(instanceID);
                
                // Group by strategy
                strategyGroups[finalStrategy].Add(instanceID);

                registeredCount++;
            }
            return registeredCount;
        }

        /// <summary>
        /// Unregister a specific TextMesh instance
        /// </summary>
        public void UnregisterInstance(int instanceID)
        {
            TextMeshEntry entry;
            if (!instanceEntries.TryGetValue(instanceID, out entry))
                return;

            // Remove from strategy group
            strategyGroups[entry.Strategy].Remove(instanceID);

            // Remove from path index
            HashSet<int> pathSet;
            if (pathToInstances.TryGetValue(entry.Path, out pathSet))
            {
                pathSet.Remove(instanceID);
                if (pathSet.Count == 0)
                    pathToInstances.Remove(entry.Path);
            }
            
            // Remove instance
            instanceEntries.Remove(instanceID);
        }

        /// <summary>
        /// Main update loop - called every frame
        /// </summary>
        public void Update(float deltaTime)
        {
            // Always update EveryFrame
            UpdateGroup(MonitoringStrategy.EveryFrame);

            // Throttled fast polling (0.1s)
            fastPollingTimer += deltaTime;
            if (fastPollingTimer >= LocalizationConstants.FAST_POLLING_INTERVAL)
            {
                UpdateGroup(MonitoringStrategy.FastPolling);
                UpdateGroup(MonitoringStrategy.Persistent);
                fastPollingTimer = 0f;
            }

            // Throttled slow polling (1.0s)
            slowPollingTimer += deltaTime;
            if (slowPollingTimer >= LocalizationConstants.SLOW_POLLING_INTERVAL)
            {
                UpdateGroup(MonitoringStrategy.SlowPolling);
                UpdateGroup(MonitoringStrategy.LateTranslateOnce);
                MonitorLateRegister(); // Also check for late registrations
                slowPollingTimer = 0f;
            }

            // OnVisibilityChange - check at a throttled interval
            visibilityPollingTimer += deltaTime;
            if (visibilityPollingTimer >= LocalizationConstants.VISIBILITY_POLLING_INTERVAL)
            {
                UpdateVisibilityChangeGroup();
                visibilityPollingTimer = 0f;
            }
        }

        private void UpdateGroup(MonitoringStrategy strategy)
        {
            var instanceIDs = strategyGroups[strategy];
            removalBuffer.Clear();

            foreach (int instanceID in instanceIDs)
            {
                TextMeshEntry entry;
                if (!instanceEntries.TryGetValue(instanceID, out entry))
                    continue;

                // Check validity first to avoid NullReferenceException on destroyed objects
                if (!entry.IsValid())
                {
                    // Persistent strategy: Keep checking even if entry is not valid
                    if (strategy != MonitoringStrategy.Persistent)
                        removalBuffer.Add(instanceID);
                    continue;
                }

                // Skip inactive entries for polling strategies.
                // OnVisibilityChange has its own dedicated pass.
                if (!entry.GameObject.activeInHierarchy)
                    continue;

                bool textChanged = entry.HasTextChanged();
                
                if (textChanged || !entry.WasTranslated)
                {
                    // Register original text if this is first translation attempt
                    if (!entry.WasTranslated && plugin != null && !string.IsNullOrEmpty(entry.TextMesh.text))
                    {
                        plugin.RegisterOriginalMeshText(entry.Path, entry.TextMesh.text);
                    }
                    
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path, null);
                    if (translated)
                    {
                        entry.WasTranslated = true;
                        entry.UpdateLastText();

                        // Mark for removal if TranslateOnce
                        if (strategy == MonitoringStrategy.TranslateOnce || strategy == MonitoringStrategy.LateTranslateOnce)
                        {
                            removalBuffer.Add(instanceID);
                        }
                    }
                }
            }

            // Clean up removed instances
            foreach (int instanceID in removalBuffer)
            {
                UnregisterInstance(instanceID);
            }
        }

        private void UpdateVisibilityChangeGroup()
        {
            var instanceIDs = strategyGroups[MonitoringStrategy.OnVisibilityChange];

            foreach (int instanceID in instanceIDs)
            {
                TextMeshEntry entry;
                if (!instanceEntries.TryGetValue(instanceID, out entry))
                    continue;

                if (!entry.IsValid())
                    continue;

                bool wasVisible = entry.IsVisible;
                entry.UpdateVisibility();

                // Only translate when visibility changes from hidden to visible
                if (!wasVisible && entry.IsVisible)
                {
                    // Register original text if this is first translation attempt
                    if (!entry.WasTranslated && plugin != null && !string.IsNullOrEmpty(entry.TextMesh.text))
                    {
                        plugin.RegisterOriginalMeshText(entry.Path, entry.TextMesh.text);
                    }
                    
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path, null);
                    if (translated)
                    {
                        entry.WasTranslated = true;
                        entry.UpdateLastText();
                    }
                }
            }
        }

        /// <summary>
        /// Clear all monitored entries
        /// </summary>
        public void Clear()
        {
            foreach (var group in strategyGroups.Values)
            {
                group.Clear();
            }
            instanceEntries.Clear();
            pathToInstances.Clear();
            pathRules.Clear();
            monitoredPaths.Clear();
            fastPollingTimer = 0f;
            slowPollingTimer = 0f;
            visibilityPollingTimer = 0f;
            InitializeDefaultPathRules();
        }
    }
}
