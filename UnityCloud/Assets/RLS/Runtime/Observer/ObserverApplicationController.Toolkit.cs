using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController
    {
        private VisualElement toolkitRoot;
        private PanelSettings toolkitPanel;
        private ApplicationState toolkitState = (ApplicationState)(-1);
        private bool toolkitPicker;
        private bool inspectorOpen = true;
        private Label clockLabel, transportLabel, saveLabel, errorLabel, inspectorTitle, inspectorSubtitle;
        private Button pauseButton;
        private ProgressBar loadProgress;
        private ScrollView eventList, workList;
        private readonly Dictionary<string, Label> metricLabels = new Dictionary<string, Label>();
        private float nextUiRefresh;
        private long renderedStep = -1;
        private string renderedLabel, renderedFilter;
        private string renderedEventsKey;
        private bool toolkitCompactViewport, toolkitNarrowViewport;

        private void InitializeToolkit()
        {
            var panelSettingsAsset = Resources.Load<PanelSettings>("ObserverPanelSettings");
            if (panelSettingsAsset != null)
            {
                toolkitPanel = Instantiate(panelSettingsAsset);
            }
            else if (Application.isEditor)
            {
                toolkitPanel = ScriptableObject.CreateInstance<PanelSettings>();
            }
            else
            {
                throw new InvalidOperationException(
                    "Missing required Resources/ObserverPanelSettings.asset. Add a saved Panel Settings asset there so ICU data is included in player builds.");
            }
            toolkitPanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            toolkitPanel.referenceResolution = new Vector2Int(1600, 900);
            toolkitPanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            toolkitPanel.match = 0.5f;
            toolkitPanel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ObserverTheme");
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = toolkitPanel;
            toolkitRoot = document.rootVisualElement;
            toolkitRoot.pickingMode = PickingMode.Ignore;
            var sheet = Resources.Load<StyleSheet>("ObserverShell");
            if (sheet != null) toolkitRoot.styleSheets.Add(sheet);
            toolkitRoot.AddToClassList("root");
        }

        private void OnDestroy()
        {
            if (toolkitPanel != null) Destroy(toolkitPanel);
        }

        private bool ToolkitHasFocus() => toolkitRoot?.panel?.focusController?.focusedElement != null;

        private bool ToolkitContainsPointer(Vector2 screenPoint)
        {
            if (showSnapshotPicker) return true;
            var panel = toolkitRoot.panel;
            if (panel == null) return false;
            VisualElement picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, screenPoint));
            return picked != null && picked != toolkitRoot;
        }

        private void UpdateToolkit()
        {
            if (toolkitRoot == null) return;
            toolkitPanel.scale = textScale;
            toolkitRoot.EnableInClassList("high-contrast", highContrast);
            bool compactViewport = Screen.width < 960 || Screen.height < 640;
            bool narrowViewport = Screen.width < 640;
            if (toolkitCompactViewport != compactViewport)
            {
                toolkitCompactViewport = compactViewport;
                toolkitRoot.EnableInClassList("compact-viewport", compactViewport);
            }
            if (toolkitNarrowViewport != narrowViewport)
            {
                toolkitNarrowViewport = narrowViewport;
                toolkitRoot.EnableInClassList("narrow-viewport", narrowViewport);
            }
            if (toolkitState != state || toolkitPicker != showSnapshotPicker)
            {
                toolkitState = state;
                toolkitPicker = showSnapshotPicker;
                RebuildToolkit();
            }
            if (Time.unscaledTime < nextUiRefresh) return;
            nextUiRefresh = Time.unscaledTime + 0.15f;
            if (errorLabel != null)
            {
                errorLabel.text = plainLanguageError;
                errorLabel.style.display = string.IsNullOrEmpty(plainLanguageError) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (state == ApplicationState.Loading && loadProgress != null)
            {
                loadProgress.value = loadingProgress * 100;
                loadProgress.title = loadingMessage;
            }
            if (state != ApplicationState.Observer || simulation == null || showSnapshotPicker) return;
            clockLabel.text = simulation.World.Clock.CurrentUtc.ToString("MMM dd, yyyy   HH:mm") + " GST";
            transportLabel.text = simulation.World.Clock.Paused ? "PAUSED" : "OBSERVING  ×" + simulation.World.Clock.Speed;
            pauseButton.text = simulation.World.Clock.Paused ? "Play" : "Pause";
            saveLabel.text = statusMessage;
            if (renderedStep == simulation.World.Clock.StepIndex && renderedLabel == selectedLabelId && renderedFilter == eventFilter) return;
            renderedStep = simulation.World.Clock.StepIndex;
            renderedLabel = selectedLabelId;
            renderedFilter = eventFilter;
            RefreshToolkitInspection();
        }

        private void RebuildToolkit()
        {
            toolkitRoot.Clear();
            metricLabels.Clear();
            renderedStep = -1;
            renderedLabel = null;
            renderedEventsKey = null;
            if (state == ApplicationState.Observer) BuildObserverToolkit();
            else BuildApplicationToolkit();
            if (showSnapshotPicker) BuildSnapshotToolkit();
            errorLabel = Text(toolkitRoot, plainLanguageError, "error-banner");
            errorLabel.style.display = string.IsNullOrEmpty(plainLanguageError) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void BuildApplicationToolkit()
        {
            var backdrop = Box(toolkitRoot, "backdrop");
            var card = Box(backdrop, "menu-card");
            Text(card, "TÓPODA CHARTS STUDIOS", "eyebrow");
            Text(card, "Record Label Simulator", "hero-title");
            Text(card, "A living world of music", "subtitle");
            switch (state)
            {
                case ApplicationState.Splash:
                    if (logo != null) { var art = new Image { image = logo, scaleMode = ScaleMode.ScaleToFit }; art.AddToClassList("logo"); card.Add(art); }
                    break;
                case ApplicationState.MainMenu:
                    ActionButton(card, "Watch the World Grow", () => BeginObserver(ObserverMode.WatchPreSimulation), "primary");
                    ActionButton(card, "Open Aug 31 Handoff", () => BeginObserver(ObserverMode.OpenHandoff));
                    ActionButton(card, "Load Snapshot", OpenSnapshotToolkit);
                    var row = Box(card, "row");
                    ActionButton(row, "Settings", () => state = ApplicationState.Settings);
                    ActionButton(row, "Credits", () => state = ApplicationState.Credits);
                    ActionButton(card, "Exit", ExitCleanly);
                    Text(card, "CENTRAL BLOOMVILLE  /  2425\nOffline play · Local snapshots", "muted");
                    break;
                case ApplicationState.Loading:
                    Text(card, "Opening Central Bloomville", "section-title");
                    loadProgress = new ProgressBar { lowValue = 0, highValue = 100, value = 0 };
                    card.Add(loadProgress);
                    Text(card, "Preparing your world. You will enter with time paused.", "muted");
                    break;
                case ApplicationState.Settings:
                    Text(card, "Presentation", "section-title");
                    var scale = new Slider("Text size", 0.9f, 1.35f) { value = textScale };
                    scale.RegisterValueChangedCallback(e => textScale = e.newValue); card.Add(scale);
                    var motion = new Toggle("Reduced motion") { value = reducedMotion };
                    motion.RegisterValueChangedCallback(e => reducedMotion = e.newValue); card.Add(motion);
                    var contrast = new Toggle("High contrast") { value = highContrast };
                    contrast.RegisterValueChangedCallback(e => highContrast = e.newValue); card.Add(contrast);
                    Text(card, "Tab selects controls · Enter activates\nWorld: WASD / arrows pan · Q/E orbit · wheel zoom\nSpace pauses · N steps · Home returns to overview", "muted");
                    ActionButton(card, "Back", () => state = ApplicationState.MainMenu);
                    break;
                case ApplicationState.Credits:
                    Text(card, "Created by JL / Tópoda Charts Studios\n\nImplementation support: Codex / OpenAI\nPowered by Unity", "body-copy");
                    ActionButton(card, "Back", () => state = ApplicationState.MainMenu);
                    break;
                case ApplicationState.Exiting: Text(card, "Saving your world…", "section-title"); break;
            }
        }

        private void BuildObserverToolkit()
        {
            var bar = Box(toolkitRoot, "topbar row");
            var identity = Box(bar, "identity");
            Text(identity, "CENTRAL BLOOMVILLE", "brand");
            Text(identity, mode == ObserverMode.OpenHandoff ? "Aug 31 handoff" : "Watch the world grow", "muted");
            var clock = Box(bar, "clock");
            clockLabel = Text(clock, "", "clock-text");
            transportLabel = Text(clock, "", "eyebrow");
            pauseButton = ActionButton(bar, "Play", TogglePause, "primary");
            ActionButton(bar, "Step", () => { SafeAutosave("before single step"); simulation.SingleStep(); RefreshWorldPresenter(true); });
            foreach (int speed in new[] { 1, 4, 16 }) { int value = speed; ActionButton(bar, "×" + speed, () => SetSpeed(value)); }
            ActionButton(bar, "Inspect", () => { inspectorOpen = !inspectorOpen; RebuildToolkit(); });
            ActionButton(bar, "Menu", ReturnToMenu);
            if (inspectorOpen)
            {
                var panel = Box(toolkitRoot, "inspector light-panel");
                inspectorTitle = Text(panel, "Record Label", "section-title");
                inspectorSubtitle = Text(panel, "", "muted");
                var scroll = new ScrollView(ScrollViewMode.Vertical); panel.Add(scroll);
                foreach (string key in new[] { "Cash", "Reputation", "Catalog value", "Chart score", "Audience", "Structure slots" })
                {
                    var metric = Box(scroll, "metric row"); Text(metric, key, "metric-name"); metricLabels[key] = Text(metric, "—", "metric-value");
                }
                var preferences = new Foldout { text = "Strategy preferences", value = false }; scroll.Add(preferences);
                foreach (string key in new[] { "Release cadence", "Quality target", "Promotion spend", "Cash reserve", "Audience growth", "Trend response" })
                { var metric = Box(preferences, "metric row"); Text(metric, key, "metric-name"); metricLabels[key] = Text(metric, "—", "metric-value"); }
                Text(scroll, "Recent work", "section-title");
                workList = new ScrollView(ScrollViewMode.Vertical); workList.AddToClassList("works"); scroll.Add(workList);
                var saves = new Foldout { text = "Snapshots & time", value = false }; scroll.Add(saves);
                var name = new TextField("Name") { value = snapshotName };
                name.RegisterValueChangedCallback(e => snapshotName = e.newValue); saves.Add(name);
                ActionButton(saves, "Create named snapshot", SaveNamedSnapshot, "primary");
                var skip = Box(saves, "row");
                ActionButton(skip, "Skip 1 day", () => Skip(TimeSpan.FromDays(1)));
                ActionButton(skip, "Skip 1 week", () => Skip(TimeSpan.FromDays(7)));
                var tools = Box(saves, "row"); ActionButton(tools, "Load", OpenSnapshotToolkit);
                ActionButton(tools, "Reset", ResetWorld);
            }
            else { inspectorTitle = null; inspectorSubtitle = null; workList = null; }
            var feed = Box(toolkitRoot, "event-feed light-panel");
            feed.EnableInClassList("wide-feed", !inspectorOpen);
            var filters = Box(feed, "row");
            Text(filters, "WORLD EVENTS", "feed-title");
            foreach (string filter in new[] { "All", "Creation", "Production", "Release", "Promotion", "Chart" })
            {
                string category = filter;
                ActionButton(filters, filter, () => { eventFilter = category; renderedFilter = null; });
            }
            eventList = new ScrollView(ScrollViewMode.Vertical); feed.Add(eventList);
            saveLabel = Text(toolkitRoot, statusMessage, "save-status");
        }

        private void RefreshToolkitInspection()
        {
            LabelRecord label = simulation.FindLabel(selectedLabelId);
            if (label != null && inspectorTitle != null)
            {
                inspectorTitle.text = label.Name;
                inspectorSubtitle.text = label.MarketCode + "  /  " + label.Nation;
                SetMetric("Cash", "$" + label.Cash.ToString("N0")); SetMetric("Reputation", label.Reputation.ToString());
                SetMetric("Catalog value", label.CatalogValue.ToString("N0")); SetMetric("Chart score", label.ChartScore.ToString("N0"));
                var act = simulation.World.Acts.FirstOrDefault(x => x.LabelId == label.Id);
                var structure = simulation.World.Structures.FirstOrDefault(x => x.LabelId == label.Id);
                SetMetric("Audience", act == null ? "—" : act.Audience.ToString("N0"));
                SetMetric("Structure slots", structure == null ? "—" : structure.OccupiedSlots + " / " + structure.SlotCount);
                SetMetric("Release cadence", ObserverDisplayFormat.Preference(label.ReleaseCadencePreference)); SetMetric("Quality target", ObserverDisplayFormat.Preference(label.QualityPreference));
                SetMetric("Promotion spend", ObserverDisplayFormat.Preference(label.PromotionPreference)); SetMetric("Cash reserve", ObserverDisplayFormat.Preference(label.CashReservePreference));
                SetMetric("Audience growth", ObserverDisplayFormat.Preference(label.AudienceGrowthPreference)); SetMetric("Trend response", ObserverDisplayFormat.Preference(label.TrendResponsePreference));
                workList.Clear();
                foreach (var work in simulation.World.Works.Where(x => x.LabelId == label.Id).OrderByDescending(x => x.CreatedTicks).Take(4))
                { var item = Box(workList, "work-card"); Text(item, work.Title, "work-title"); Text(item, ObserverDisplayFormat.StageLabel(work), "muted"); }
            }
            var recent = simulation.RecentEvents(20, eventFilter);
            string eventsKey = eventFilter + "|" + string.Join("|", recent.Select(item => item.OccurredTicks + ":" + item.Headline));
            if (eventsKey == renderedEventsKey) return;
            renderedEventsKey = eventsKey;
            var expanded = new HashSet<string>(eventList.Query<Foldout>().ToList().Where(item => item.value).Select(item => item.text));
            eventList.Clear();
            foreach (var item in recent)
            {
                var entry = new Foldout { text = new DateTime(item.OccurredTicks).ToString("MMM d HH:mm") + "   " + item.Headline, value = false };
                entry.value = expanded.Contains(entry.text);
                entry.AddToClassList("event-item"); Text(entry, item.Explanation, "body-copy"); eventList.Add(entry);
            }
        }

        private void OpenSnapshotToolkit()
        {
            if (simulation != null) { simulation.World.Clock.Paused = true; simulation.RefreshDigest(); }
            snapshotList = snapshots.List(); showSnapshotPicker = true;
        }

        private void BuildSnapshotToolkit()
        {
            var modal = Box(toolkitRoot, "backdrop modal");
            var card = Box(modal, "menu-card light-panel");
            Text(card, "Load a local snapshot", "section-title");
            Text(card, "Your current world is protected before loading.", "muted");
            var list = new ScrollView(ScrollViewMode.Vertical); list.AddToClassList("snapshot-list"); card.Add(list);
            if (snapshotList.Count == 0) Text(list, "No snapshots yet. Watch the world grow to create one.", "body-copy");
            foreach (var item in snapshotList)
            {
                string file = item.FileName;
                var button = ActionButton(list, item.DisplayName + "\n" + ObserverDisplayFormat.SnapshotDate(item) + " · " + item.Compatibility, () => LoadSnapshot(file));
                button.SetEnabled(item.IntegrityValid);
            }
            ActionButton(card, "Close", () => showSnapshotPicker = false);
        }

        private void SetMetric(string key, string value) { if (metricLabels.TryGetValue(key, out var label)) label.text = value; }
        private static VisualElement Box(VisualElement parent, string classes)
        { var box = new VisualElement(); foreach (var c in classes.Split(' ')) box.AddToClassList(c); parent.Add(box); return box; }
        private static Label Text(VisualElement parent, string value, string classes)
        { var label = new Label(value); foreach (var c in classes.Split(' ')) label.AddToClassList(c); parent.Add(label); return label; }
        private static Button ActionButton(VisualElement parent, string text, Action action, string classes = "")
        {
            var button = new Button(action) { text = text }; button.AddToClassList("action");
            if (!string.IsNullOrEmpty(classes)) button.AddToClassList(classes);
            parent.Add(button); return button;
        }
    }
}
