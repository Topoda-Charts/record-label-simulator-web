using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController
    {
        private const string StatCashPreference = "Topoda.RLS.Observer.Stats.Cash";
        private const string StatCatalogPreference = "Topoda.RLS.Observer.Stats.Catalog";
        private const string StatAudiencePreference = "Topoda.RLS.Observer.Stats.Audience";
        private const string StatReleasesPreference = "Topoda.RLS.Observer.Stats.Releases";
        private const string StatSlotsPreference = "Topoda.RLS.Observer.Stats.Slots";
        private const string DeveloperDiagnosticsPreference = "Topoda.RLS.Observer.DeveloperDiagnosticsEnabled";

        private bool statsPanelOpen;
        private bool showCashStat = true;
        private bool showCatalogStat = true;
        private bool showAudienceStat = true;
        private bool showReleasesStat = true;
        private bool showSlotUtilizationStat = true;
        private bool developerDiagnosticsEnabled;
        private bool developerDiagnosticsOpen;
        public bool DeveloperDiagnosticsEnabled { get { return developerDiagnosticsEnabled; } }
        public bool DeveloperDiagnosticsVisible { get { return developerDiagnosticsOpen && developerDiagnosticsEnabled; } }
        public bool PlayerStatisticsVisible { get { return statsPanelOpen; } }

        private readonly Dictionary<string, Label> observerStatLabels = new Dictionary<string, Label>();
        private readonly Dictionary<string, VisualElement> observerStatRows = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, Label> developerDiagnosticLines = new Dictionary<string, Label>();
        private VisualElement skipProgressPanel;
        private ProgressBar skipProgressBar;
        private Label skipProgressCaption;

        private void InitializeObserverStatistics()
        {
            showCashStat = PlayerPrefs.GetInt(StatCashPreference, 1) != 0;
            showCatalogStat = PlayerPrefs.GetInt(StatCatalogPreference, 1) != 0;
            showAudienceStat = PlayerPrefs.GetInt(StatAudiencePreference, 1) != 0;
            showReleasesStat = PlayerPrefs.GetInt(StatReleasesPreference, 1) != 0;
            showSlotUtilizationStat = PlayerPrefs.GetInt(StatSlotsPreference, 1) != 0;
            developerDiagnosticsEnabled = PlayerPrefs.GetInt(DeveloperDiagnosticsPreference, 0) != 0;
        }

        private void SetDeveloperDiagnosticsEnabled(bool enabled)
        {
            developerDiagnosticsEnabled = enabled;
            if (!enabled) developerDiagnosticsOpen = false;
            PlayerPrefs.SetInt(DeveloperDiagnosticsPreference, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void BuildObserverStatisticsPanel(VisualElement parent)
        {
            observerStatLabels.Clear();
            observerStatRows.Clear();
            var panel = Box(parent, "inspector stats-panel light-panel");
            var header = Box(panel, "inspector-header row");
            Text(header, "Observed world stats", "section-title");
            ActionButton(header, "Close", () => { statsPanelOpen = false; RebuildToolkit(); });
            var content = new ScrollView(ScrollViewMode.Vertical);
            panel.Add(content);
            Text(content, "Current values from simulation records.", "muted");

            var options = Box(content, "stats-options");
            AddStatVisibilityToggle(options, "Cash", showCashStat, value => showCashStat = value);
            AddStatVisibilityToggle(options, "Catalog", showCatalogStat, value => showCatalogStat = value);
            AddStatVisibilityToggle(options, "Audience", showAudienceStat, value => showAudienceStat = value);
            AddStatVisibilityToggle(options, "Releases", showReleasesStat, value => showReleasesStat = value);
            AddStatVisibilityToggle(options, "Slots", showSlotUtilizationStat, value => showSlotUtilizationStat = value);

            AddObserverStatRow(content, "cash", "Cash");
            AddObserverStatRow(content, "catalog", "Catalog value");
            AddObserverStatRow(content, "audience", "Act audience");
            AddObserverStatRow(content, "releases", "Released works");
            AddObserverStatRow(content, "slots", "Occupied / available slots");
            RefreshObserverStatistics();
        }

        private void AddStatVisibilityToggle(VisualElement parent, string title, bool value, System.Action<bool> setValue)
        {
            var toggle = new Toggle(title) { value = value };
            toggle.AddToClassList("stats-toggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                setValue(evt.newValue);
                SaveObserverStatisticsPreferences();
                RefreshObserverStatistics();
            });
            parent.Add(toggle);
        }

        private void AddObserverStatRow(VisualElement parent, string key, string title)
        {
            var row = Box(parent, "metric row stats-row");
            Text(row, title, "metric-name");
            observerStatLabels[key] = Text(row, "—", "metric-value");
            observerStatRows[key] = row;
        }

        private void SaveObserverStatisticsPreferences()
        {
            PlayerPrefs.SetInt(StatCashPreference, showCashStat ? 1 : 0);
            PlayerPrefs.SetInt(StatCatalogPreference, showCatalogStat ? 1 : 0);
            PlayerPrefs.SetInt(StatAudiencePreference, showAudienceStat ? 1 : 0);
            PlayerPrefs.SetInt(StatReleasesPreference, showReleasesStat ? 1 : 0);
            PlayerPrefs.SetInt(StatSlotsPreference, showSlotUtilizationStat ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void RefreshObserverStatistics()
        {
            if (simulation == null || observerStatLabels.Count == 0) return;
            var world = simulation.World;
            long cash = world.Labels.Sum(label => label.Cash);
            long catalog = world.Labels.Sum(label => (long)label.CatalogValue);
            long audience = world.Acts.Sum(act => (long)act.Audience);
            int releases = world.Works.Count(work => work.Released);
            int occupied = world.Structures.Sum(structure => structure.OccupiedSlots);
            int capacity = world.Structures.Sum(structure => structure.SlotCount);

            SetObserverStat("cash", "$" + cash.ToString("N0"));
            SetObserverStat("catalog", catalog.ToString("N0"));
            SetObserverStat("audience", audience.ToString("N0"));
            SetObserverStat("releases", releases.ToString("N0"));
            SetObserverStat("slots", capacity <= 0 ? "—" : occupied.ToString("N0") + " / " + capacity.ToString("N0") + "  (" + ((float)occupied / capacity).ToString("P0") + ")");

            SetObserverStatVisibility("cash", showCashStat);
            SetObserverStatVisibility("catalog", showCatalogStat);
            SetObserverStatVisibility("audience", showAudienceStat);
            SetObserverStatVisibility("releases", showReleasesStat);
            SetObserverStatVisibility("slots", showSlotUtilizationStat);
        }

        private void SetObserverStat(string key, string value)
        {
            if (observerStatLabels.TryGetValue(key, out var label)) label.text = value;
        }

        private void SetObserverStatVisibility(string key, bool visible)
        {
            if (observerStatRows.TryGetValue(key, out var row)) row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildDeveloperDiagnosticsOverlay(VisualElement parent)
        {
            developerDiagnosticLines.Clear();
            if (!developerDiagnosticsEnabled || !developerDiagnosticsOpen || state != ApplicationState.Observer) return;
            var panel = Box(parent, "dev-overlay light-panel");
            panel.pickingMode = PickingMode.Ignore;
            Text(panel, "Developer diagnostics", "section-title");
            Text(panel, "Read-only · Tab closes · N steps while paused", "muted");
            var lines = new ScrollView(ScrollViewMode.Vertical);
            panel.Add(lines);
            AddDeveloperDiagnosticLine(lines, "mode", "Mode");
            AddDeveloperDiagnosticLine(lines, "step", "Clock step");
            AddDeveloperDiagnosticLine(lines, "time", "Simulation time");
            AddDeveloperDiagnosticLine(lines, "digest", "State digest");
            AddDeveloperDiagnosticLine(lines, "members", "Members");
            AddDeveloperDiagnosticLine(lines, "labels", "Labels");
            AddDeveloperDiagnosticLine(lines, "structures", "Structures");
            AddDeveloperDiagnosticLine(lines, "works", "Works / released");
            AddDeveloperDiagnosticLine(lines, "skip", "Skip progress");
            RefreshDeveloperDiagnostics();
        }

        private void AddDeveloperDiagnosticLine(VisualElement parent, string key, string title)
        {
            var label = Text(parent, title + ": —", "dev-line");
            developerDiagnosticLines[key] = label;
        }

        private void RefreshDeveloperDiagnostics()
        {
            if (!developerDiagnosticsEnabled || !developerDiagnosticsOpen || simulation == null || developerDiagnosticLines.Count == 0) return;
            var world = simulation.World;
            SetDeveloperDiagnostic("mode", mode.ToString());
            SetDeveloperDiagnostic("step", world.Clock.StepIndex.ToString("N0"));
            SetDeveloperDiagnostic("time", world.Clock.CurrentUtc.ToString("yyyy-MM-dd HH:mm"));
            SetDeveloperDiagnostic("digest", string.IsNullOrEmpty(world.Digest) ? "pending" : world.Digest);
            SetDeveloperDiagnostic("members", world.Members.Count.ToString("N0"));
            SetDeveloperDiagnostic("labels", world.Labels.Count.ToString("N0"));
            SetDeveloperDiagnostic("structures", world.Structures.Count.ToString("N0"));
            SetDeveloperDiagnostic("works", world.Works.Count.ToString("N0") + " / " + world.Works.Count(work => work.Released).ToString("N0"));
            SetDeveloperDiagnostic("skip", skipInProgress ? (skipProgress * 100f).ToString("0.0") + "% · " + skipMessage : "idle");
        }

        private void SetDeveloperDiagnostic(string key, string value)
        {
            if (developerDiagnosticLines.TryGetValue(key, out var label))
            {
                string title = label.text.Substring(0, label.text.IndexOf(':'));
                label.text = title + ": " + value;
            }
        }

        private void BuildSkipProgressToolkit(VisualElement parent)
        {
            skipProgressPanel = Box(parent, "skip-progress-panel light-panel");
            skipProgressPanel.style.display = DisplayStyle.None;
            skipProgressCaption = Text(skipProgressPanel, "Simulating the selected time span…", "skip-progress-caption");
            skipProgressBar = new ProgressBar { lowValue = 0f, highValue = 100f, value = 0f };
            skipProgressBar.AddToClassList("skip-progress-bar");
            skipProgressPanel.Add(skipProgressBar);
        }

        private void RefreshSkipProgressToolkit()
        {
            if (skipProgressPanel == null) return;
            bool visible = state == ApplicationState.Observer && skipInProgress;
            skipProgressPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            if (skipProgressCaption != null)
            {
                skipProgressCaption.text = string.IsNullOrEmpty(skipMessage) ? "Simulating the selected time span…" : skipMessage;
            }
            if (skipProgressBar != null) skipProgressBar.value = Mathf.Clamp01(skipProgress) * 100f;
        }
    }
}
