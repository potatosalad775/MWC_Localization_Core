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
        public bool HasProcessedText;
        public bool WasTranslated;
        public bool IsVisible;

        public TextMeshEntry(TextMesh textMesh, string path, MonitoringStrategy strategy)
        {
            TextMesh = textMesh;
            GameObject = textMesh != null ? textMesh.gameObject : null;
            Path = path;
            Strategy = strategy;
            LastText = textMesh != null ? textMesh.text : "";
            HasProcessedText = false;
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
        private float visibilityPollingTimer;

        // Path-based monitoring rules (pattern -> strategy mapping)
        private Dictionary<string, MonitoringStrategy> pathRules;
        
        // Instance-based storage (supports multiple TextMeshes per path)
        private Dictionary<int, TextMeshEntry> instanceEntries;  // instanceID -> entry
        private Dictionary<MonitoringStrategy, HashSet<int>> strategyGroups;  // strategy -> instanceIDs
        private HashSet<string> monitoredPaths = new HashSet<string>();
        // Subset of pathRules whose strategy is PersistentRebuilding. Re-scanned every
        // frame so newly-spawned child TextMeshes are picked up the same frame they
        // appear. Kept small on purpose - every entry pays a per-frame
        // GetComponentsInChildren walk.
        private HashSet<string> rebuildingPaths = new HashSet<string>();
        private List<int> removalBuffer = new List<int>(64);
        private List<string> stringRemovalBuffer = new List<string>(16);

        public UnifiedTextMeshMonitor(TextMeshTranslator translator)
        {
            this.translator = translator;
            pathRules = new Dictionary<string, MonitoringStrategy>();
            instanceEntries = new Dictionary<int, TextMeshEntry>();
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

            // Teletext/FSM displays are primarily translated at array/FSM source level.
            // Use one-shot late registration to avoid rescanning large TV trees continuously.
            AddPathRule("Systems/TV/Teletext/VKTekstiTV/PAGES", MonitoringStrategy.LateTranslateOnce);
            // CHAT/Generated/Lines is already translated upstream by TeletextHandler, 
            // so the TextMesh text never matches a dictionary key on this end. 
            // Apply the font once and stop monitoring - no translation pass would ever succeed here.
            AddPathRule("Systems/TV/TVGraphics/CHAT/Generated", MonitoringStrategy.LateApplyFontOnce);
            AddPathRule("Systems/TV/TVGraphics/CHAT/Day/Time", MonitoringStrategy.LateApplyFontOnce);

            // Magazine / Sheets - on visibility change
            AddPathRule("Sheets/UnemployPaper", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/ServiceBrochure", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("Sheets/ServicePayment", MonitoringStrategy.OnVisibilityChange);
            AddPathRule("COMPUTER/SYSTEM/TELEBBS/CONLINE/CommandLine", MonitoringStrategy.OnVisibilityChange);

            // Magazine / Sheets - persistent monitoring due to dynamic content changes and rebuilds
            AddPathRule("Sheets/YellowPagesMagazine/Page1", MonitoringStrategy.Persistent);
            AddPathRule("Sheets/YellowPagesMagazine/Page2", MonitoringStrategy.Persistent);
            AddPathRule("PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat", MonitoringStrategy.Persistent);
            AddPathRule("COMPUTER/SYSTEM/POS/NoOS", MonitoringStrategy.Persistent);

            // Magazine product list - parent rebuilds children while sheet is open.
            AddPathRule("Sheets/Magazine/Products", MonitoringStrategy.PersistentRebuilding);
        }

        public void AddPathRule(string pathPattern, MonitoringStrategy strategy)
        {
            pathRules[pathPattern] = strategy;
            if (strategy == MonitoringStrategy.PersistentRebuilding)
                rebuildingPaths.Add(pathPattern);
        }

        // A parent scan (e.g. FastPolling on "CHAT/Day") must not absorb TextMeshes that have
        // their own more-specific rule (e.g. LateApplyFontOnce on "CHAT/Day/Time"), or the
        // one-shot rule gets silently demoted into continuous polling.
        private bool HasMoreSpecificRule(string textMeshPath, string parentPath)
        {
            int parentLen = parentPath.Length;
            foreach (var rule in pathRules.Keys)
            {
                if (rule.Length <= parentLen)
                    continue;
                if (rule == textMeshPath || textMeshPath.StartsWith(rule + "/"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Register all TextMeshes under defined path rules
        /// </summary>
        public void RegisterAllPathRuleElements()
        {
            foreach (string parentPath in pathRules.Keys)
            {
                MonitoringStrategy strategy = pathRules[parentPath];
                int registered = Register(parentPath, strategy);

                // Only queue for late retry if the initial Register couldn't find the parent yet
                // (registered == 0). Persistent and OnVisibilityChange stay queued so rebuilt
                // children get picked up.
                bool needsLateQueue = strategy == MonitoringStrategy.LateTranslateOnce ||
                                      strategy == MonitoringStrategy.LateApplyFontOnce ||
                                      strategy == MonitoringStrategy.OnVisibilityChange ||
                                      strategy == MonitoringStrategy.Persistent;
                if (needsLateQueue && (registered == 0 ||
                                       strategy == MonitoringStrategy.Persistent ||
                                       strategy == MonitoringStrategy.OnVisibilityChange))
                {
                    monitoredPaths.Add(parentPath);
                }
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
                // Persistent and OnVisibilityChange paths stay in the scan because their
                // child TextMeshes can be rebuilt.
                if (registered > 0 &&
                    strategy != MonitoringStrategy.Persistent &&
                    strategy != MonitoringStrategy.OnVisibilityChange)
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

            // Font-only fast path: these TextMeshes are translated upstream at the data
            // source, so we only need to swap the font once and then forget about them.
            // No per-instance tracking or follow-up monitoring is needed - the translator's
            // appliedFontCache already prevents redundant re-application.
            if (finalStrategy == MonitoringStrategy.LateApplyFontOnce)
            {
                foreach (var textMesh in textMeshes)
                {
                    if (textMesh == null)
                        continue;

                    string textMeshPath = MLCUtils.GetGameObjectPath(textMesh.gameObject);
                    if (HasMoreSpecificRule(textMeshPath, parentPath))
                        continue;
                    translator.ApplyFontOnly(textMesh, textMeshPath);
                    registeredCount++;
                }
                return registeredCount;
            }

            foreach (var textMesh in textMeshes)
            {
                if (textMesh == null)
                    continue;

                int instanceID = textMesh.GetInstanceID();

                // Skip if this specific instance is already registered
                if (instanceEntries.ContainsKey(instanceID))
                    continue;

                string textMeshPath = MLCUtils.GetGameObjectPath(textMesh.gameObject);

                // A more-specific path rule (e.g. child Late/OnVisibility rule) must handle this
                // TextMesh instead of it being captured by this parent scan.
                if (HasMoreSpecificRule(textMeshPath, parentPath))
                    continue;

                // Translate first to reduce visible unlocalized text
                bool translated = translator.TranslateAndApplyFont(textMesh, textMeshPath);

                TextMeshEntry entry = new TextMeshEntry(textMesh, textMeshPath, finalStrategy);
                entry.HasProcessedText = true;
                entry.UpdateLastText();
                if (translated)
                {
                    entry.WasTranslated = true;
                }

                if (finalStrategy == MonitoringStrategy.LateTranslateOnce && entry.WasTranslated)
                {
                    registeredCount++;
                    continue;
                }

                // Register instance
                instanceEntries[instanceID] = entry;
                
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

            // Remove instance
            instanceEntries.Remove(instanceID);
        }

        /// <summary>
        /// Re-run Register() for PersistentRebuilding parents so newly-spawned child
        /// TextMeshes are picked up the same frame they appear. Register() already
        /// deduplicates by instanceID; the dominant cost is one
        /// GetComponentsInChildren&lt;TextMesh&gt;(true) per parent, so rebuildingPaths
        /// must stay tiny.
        /// </summary>
        private void ScanRebuildingPaths()
        {
            foreach (string parentPath in rebuildingPaths)
            {
                Register(parentPath, MonitoringStrategy.PersistentRebuilding);
            }
        }

        /// <summary>
        /// Main update loop - called every frame
        /// </summary>
        public void Update(float deltaTime)
        {
            // Per-frame work for content the game rebuilds in place.
            // ScanRebuildingPaths catches new child TextMeshes; the translator's
            // drift-tracked refresh re-pins meshes whose transforms the game rewrites
            // every frame. Both lists are deliberately tiny.
            ScanRebuildingPaths();
            translator.RefreshDriftTrackedAdjustments();

            // Always update EveryFrame
            UpdateGroup(MonitoringStrategy.EveryFrame);

            // Throttled fast polling (0.1s)
            fastPollingTimer += deltaTime;
            if (fastPollingTimer >= LocalizationConstants.FAST_POLLING_INTERVAL)
            {
                UpdateGroup(MonitoringStrategy.FastPolling);
                UpdateGroup(MonitoringStrategy.Persistent);
                UpdateGroup(MonitoringStrategy.PersistentRebuilding);
                fastPollingTimer = 0f;
            }

            // Throttled slow polling (1.0s)
            slowPollingTimer += deltaTime;
            if (slowPollingTimer >= LocalizationConstants.SLOW_POLLING_INTERVAL)
            {
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
                    removalBuffer.Add(instanceID);
                    continue;
                }

                if (strategy == MonitoringStrategy.LateTranslateOnce && entry.WasTranslated)
                {
                    removalBuffer.Add(instanceID);
                    continue;
                }

                // Skip inactive entries for polling strategies.
                // OnVisibilityChange has its own dedicated pass.
                if (!entry.GameObject.activeInHierarchy)
                    continue;

                bool textChanged = entry.HasTextChanged();
                if (textChanged)
                {
                    entry.HasProcessedText = false;
                }
                
                if (textChanged || !entry.HasProcessedText)
                {
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path);
                    entry.HasProcessedText = true;
                    entry.UpdateLastText();
                    if (translated)
                    {
                        entry.WasTranslated = true;

                        // Late one-shot entries can stop monitoring after the first successful translation.
                        if (strategy == MonitoringStrategy.LateTranslateOnce)
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
            removalBuffer.Clear();

            foreach (int instanceID in instanceIDs)
            {
                TextMeshEntry entry;
                if (!instanceEntries.TryGetValue(instanceID, out entry))
                    continue;

                if (!entry.IsValid())
                {
                    removalBuffer.Add(instanceID);
                    continue;
                }

                bool wasVisible = entry.IsVisible;
                entry.UpdateVisibility();

                // Only translate when visibility changes from hidden to visible
                if (!wasVisible && entry.IsVisible)
                {
                    bool translated = translator.TranslateAndApplyFont(entry.TextMesh, entry.Path);
                    entry.HasProcessedText = true;
                    entry.UpdateLastText();
                    if (translated)
                    {
                        entry.WasTranslated = true;
                    }
                }
            }

            foreach (int instanceID in removalBuffer)
            {
                UnregisterInstance(instanceID);
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
            pathRules.Clear();
            monitoredPaths.Clear();
            rebuildingPaths.Clear();
            fastPollingTimer = 0f;
            slowPollingTimer = 0f;
            visibilityPollingTimer = 0f;
            InitializeDefaultPathRules();
        }
    }
}
