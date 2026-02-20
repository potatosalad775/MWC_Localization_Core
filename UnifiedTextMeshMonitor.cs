using MSCLoader;
using System.Collections.Generic;
using System.Linq;
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

        // Throttling timers
        private float fastPollingTimer;
        private float slowPollingTimer;

        // OPTIMIZATION: Budget-based processing to reduce per-frame cost
        private int translationBudgetRemaining;

        // Path-based monitoring rules (pattern -> strategy mapping)
        private Dictionary<string, MonitoringStrategy> pathRules;
        
        // Instance-based storage (supports multiple TextMeshes per path)
        private Dictionary<int, TextMeshEntry> instanceEntries;  // instanceID -> entry
        private Dictionary<string, HashSet<int>> pathToInstances;  // path -> instanceIDs
        private Dictionary<MonitoringStrategy, HashSet<int>> strategyGroups;  // strategy -> instanceIDs
        private List<string> monitoredPaths = new List<string>();

        // OPTIMIZATION: Reusable list to avoid allocations per frame
        private List<int> toRemoveBuffer = new List<int>(128);

        public UnifiedTextMeshMonitor(TextMeshTranslator translator)
        {
            this.translator = translator;
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

            // Main Menu Radio/CD - every frame (to ensure fonts apply)
            AddPathRule("Radio/Folk", MonitoringStrategy.EveryFrame);
            AddPathRule("Radio/CD", MonitoringStrategy.EveryFrame);

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
            AddPathRule("Sheets/UnemployPaper", MonitoringStrategy.FastPolling);

            // Teletext/FSM - slow polling (1 FPS)
            AddPathRule("Systems/TV/Teletext/VKTekstiTV", MonitoringStrategy.SlowPolling);

            // Magazine / Sheets - on visibility change
            AddPathRule("Sheets/ServiceBrochure", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/ServicePayment", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/YellowPagesMagazine/Page1", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/YellowPagesMagazine/Page2", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("PERAPORTTI/ATMs/MoneyATM/Screen/Tapahtumat", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/TrafficTicket", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("COMPUTER", MonitoringStrategy.OnVisibilityChange);
        }

        public void AddPathRule(string pathPattern, MonitoringStrategy strategy)
        {
            pathRules[pathPattern] = strategy;
        }

        /// <summary>
        /// Determine monitoring strategy for a given path
        /// Matches most specific (longest) rule first to avoid ambiguity
        /// </summary>
        public MonitoringStrategy DetermineStrategy(string path)
        {
            string longestMatch = null;
            MonitoringStrategy matchedStrategy = MonitoringStrategy.TranslateOnce;

            // Find the longest (most specific) matching rule
            foreach (var rule in pathRules)
            {
                if (path.Contains(rule.Key))
                {
                    // Use longest match (most specific)
                    if (longestMatch == null || rule.Key.Length > longestMatch.Length)
                    {
                        longestMatch = rule.Key;
                        matchedStrategy = rule.Value;
                    }
                }
            }

            return matchedStrategy;
        }

        /// <summary>
        /// Register all TextMeshes under defined path rules
        /// </summary>
        public void RegisterAllPathRuleElements()
        {
            foreach (string parentPath in pathRules.Keys)
            {
                MonitoringStrategy strategy = pathRules[parentPath];
                
                // Late-register sempre para Sheets/* (muitos papers spawnam depois)
                if (parentPath.StartsWith("Sheets/") ||
                    strategy == MonitoringStrategy.LateTranslateOnce ||
                    strategy == MonitoringStrategy.OnVisibilityChange)
                {
                    if (!monitoredPaths.Contains(parentPath))
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
            // Use ToList() to avoid modifying collection during iteration
            foreach (string parentPath in monitoredPaths.ToList())
            {
                MonitoringStrategy strategy = pathRules.ContainsKey(parentPath) ? pathRules[parentPath] : MonitoringStrategy.LateTranslateOnce;

                int registered = Register(parentPath, strategy);

                // Remove from monitoring list if successfully registered
                if (registered > 0)
                {
                    monitoredPaths.Remove(parentPath);
                }
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

            GameObject parent = GameObject.Find(parentPath);
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
                if (!pathToInstances.ContainsKey(textMeshPath))
                    pathToInstances[textMeshPath] = new HashSet<int>();
                pathToInstances[textMeshPath].Add(instanceID);
                
                // Group by strategy
                strategyGroups[finalStrategy].Add(instanceID);

                registeredCount++;
            }
            return registeredCount;
        }

        /// <summary>
        /// Unregister a TextMesh from monitoring by path
        /// Removes all TextMesh instances at the given path
        /// </summary>
        public void Unregister(string path)
        {
            if (path == null || !pathToInstances.ContainsKey(path))
                return;

            var instanceIDs = pathToInstances[path];
            foreach (int instanceID in instanceIDs)
            {
                if (!instanceEntries.ContainsKey(instanceID))
                    continue;

                var entry = instanceEntries[instanceID];
                strategyGroups[entry.Strategy].Remove(instanceID);
                instanceEntries.Remove(instanceID);
            }
            
            pathToInstances.Remove(path);
        }

        /// <summary>
        /// Unregister a specific TextMesh instance
        /// </summary>
        public void UnregisterInstance(int instanceID)
        {
            if (!instanceEntries.ContainsKey(instanceID))
                return;

            var entry = instanceEntries[instanceID];
            
            // Remove from strategy group
            strategyGroups[entry.Strategy].Remove(instanceID);
            
            // Remove from path index
            if (pathToInstances.ContainsKey(entry.Path))
            {
                pathToInstances[entry.Path].Remove(instanceID);
                if (pathToInstances[entry.Path].Count == 0)
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
            // OPTIMIZATION: Reset budget each frame
            translationBudgetRemaining = LocalizationConstants.TRANSLATIONS_PER_CYCLE;

            // Always update EveryFrame and Persistent
            UpdateGroup(MonitoringStrategy.EveryFrame);
            if (translationBudgetRemaining <= 0) return;
            
            UpdateGroup(MonitoringStrategy.Persistent);
            if (translationBudgetRemaining <= 0) return;

            // Throttled fast polling (0.1s)
            fastPollingTimer += deltaTime;
            if (fastPollingTimer >= LocalizationConstants.FAST_POLLING_INTERVAL)
            {
                UpdateGroup(MonitoringStrategy.FastPolling);
                fastPollingTimer = 0f;
                if (translationBudgetRemaining <= 0) return;
            }

            // Throttled slow polling (1.0s)
            slowPollingTimer += deltaTime;
            if (slowPollingTimer >= LocalizationConstants.SLOW_POLLING_INTERVAL)
            {
                UpdateGroup(MonitoringStrategy.SlowPolling);
                if (translationBudgetRemaining <= 0) return;

                MonitorLateRegister(); // Also check for late registrations
                slowPollingTimer = 0f;
            }

            // OnVisibilityChange - check when visibility changes
            UpdateVisibilityChangeGroup();
        }

        private void UpdateGroup(MonitoringStrategy strategy)
        {
            var instanceIDs = strategyGroups[strategy];
            toRemoveBuffer.Clear();  // OPTIMIZATION: Reuse buffer instead of new List each call

            foreach (int instanceID in instanceIDs)
            {
                // OPTIMIZATION: Budget check to reduce per-frame cost
                if (translationBudgetRemaining <= 0)
                    break;

                if (!instanceEntries.TryGetValue(instanceID, out var entry))
                {
                    toRemoveBuffer.Add(instanceID);
                    continue;
                }

                if (!entry.IsValid())
                {
                    toRemoveBuffer.Add(instanceID);
                    continue;
                }

                // OPTIMIZATION: Avoid repeated property access
                bool textChanged = entry.HasTextChanged();
                bool wasTranslated = entry.WasTranslated;
                
                // Persistent strategy: Check regardless of status
                // Other strategies: Only check if text changed
                bool shouldCheck = textChanged || !wasTranslated || strategy == MonitoringStrategy.Persistent;
                
                if (shouldCheck)
                {
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path, null);
                    if (translated)
                    {
                        entry.WasTranslated = true;
                        entry.UpdateLastText();
                        
                        translationBudgetRemaining--;  // OPTIMIZATION: Deduct from budget

                        // Mark for removal if TranslateOnce
                        if (strategy == MonitoringStrategy.TranslateOnce)
                        {
                            toRemoveBuffer.Add(instanceID);
                        }
                    }
                }
            }

            // OPTIMIZATION: Clean up removed instances
            for (int i = 0; i < toRemoveBuffer.Count; i++)
            {
                UnregisterInstance(toRemoveBuffer[i]);
            }
        }

        private void UpdateVisibilityChangeGroup()
        {
            var instanceIDs = strategyGroups[MonitoringStrategy.OnVisibilityChange];

            foreach (int instanceID in instanceIDs)
            {
                if (!instanceEntries.ContainsKey(instanceID))
                    continue;

                var entry = instanceEntries[instanceID];

                if (!entry.IsValid())
                    continue;

                bool wasVisible = entry.IsVisible;
                entry.UpdateVisibility();

                // Only translate when visibility changes from hidden to visible
                if (!wasVisible && entry.IsVisible)
                {
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
            fastPollingTimer = 0f;
            slowPollingTimer = 0f;
        }
    }
}
