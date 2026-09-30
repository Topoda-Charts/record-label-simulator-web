using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.UIElements;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController
    {
        private static readonly WorkStage[] ProductionStageOrder =
        {
            WorkStage.SheetMusic,
            WorkStage.DemoRecording,
            WorkStage.Master,
            WorkStage.Track
        };

        private bool productionTrayOpen;
        private string productionSelectedRootId;
        private string productionSelectedStageId;
        private string productionRenderedKey;
        private VisualElement productionTrayRoot;
        private VisualElement productionWorkList;
        private VisualElement productionChain;
        private VisualElement productionDetails;
        private VisualElement productionHistory;
        private Label productionSubtitle;

        private void BuildProductionTray(VisualElement parent)
        {
            productionTrayRoot = Box(parent, "inspector production-tray");
            productionTrayRoot.style.display = DisplayStyle.None;

            VisualElement header = Box(productionTrayRoot, "inspector-header");
            Text(header, "Production", "section-title");
            productionSubtitle = Text(header, "Select a label to inspect its work.", "muted");

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.AddToClassList("inspector-content");
            productionTrayRoot.Add(content);

            productionWorkList = Box(content, "production-work-list");
            productionChain = Box(content, "production-chain");
            productionDetails = Box(content, "production-detail");
            productionHistory = Box(content, "production-history");
            productionRenderedKey = null;
        }

        private void RefreshProductionTray()
        {
            if (productionTrayRoot == null)
            {
                return;
            }

            productionTrayRoot.style.display = productionTrayOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!productionTrayOpen)
            {
                return;
            }

            if (simulation == null || simulation.World == null)
            {
                ClearProductionTray("Waiting for observer data.");
                return;
            }

            LabelRecord label = simulation.FindLabel(selectedLabelId);
            if (label == null)
            {
                ClearProductionTray("Select a label to inspect its work.");
                return;
            }

            List<CreativeWorkRecord> roots = simulation.World.Works
                .Where(work => work.LabelId == label.Id && work.Stage == WorkStage.SheetMusic)
                .OrderByDescending(work => work.CreatedTicks)
                .ThenBy(work => work.Id, StringComparer.Ordinal)
                .Take(4)
                .ToList();

            if (roots.Count == 0)
            {
                string emptyKey = label.Id + "|" + simulation.World.Clock.StepIndex + "|empty";
                if (emptyKey != productionRenderedKey)
                {
                    productionRenderedKey = emptyKey;
                    productionSubtitle.text = label.Name + " · " + label.MarketCode;
                    productionWorkList.Clear();
                    productionChain.Clear();
                    productionDetails.Clear();
                    productionHistory.Clear();
                    Text(productionWorkList, "Recent work", "section-title");
                    Text(productionWorkList, "No sheet music has been recorded for this label yet.", "muted");
                }

                return;
            }

            CreativeWorkRecord selectedRoot = roots.FirstOrDefault(work => work.Id == productionSelectedRootId);
            if (selectedRoot == null)
            {
                selectedRoot = roots[0];
                productionSelectedRootId = selectedRoot.Id;
                productionSelectedStageId = null;
            }

            List<CreativeWorkRecord> selectedChain = BuildProductionChain(selectedRoot, simulation.World.Works);
            CreativeWorkRecord selectedStage = selectedChain.FirstOrDefault(work => work.Id == productionSelectedStageId);
            if (selectedStage == null)
            {
                selectedStage = selectedChain[selectedChain.Count - 1];
                productionSelectedStageId = selectedStage.Id;
            }

            string renderKey = label.Id + "|" + simulation.World.Clock.StepIndex + "|" + selectedRoot.Id + "|" + selectedStage.Id;
            if (renderKey == productionRenderedKey)
            {
                return;
            }

            productionRenderedKey = renderKey;
            productionSubtitle.text = label.Name + " · " + label.MarketCode;
            BuildProductionWorkList(roots, selectedRoot);
            BuildProductionStageChain(selectedChain, selectedStage);
            BuildProductionDetails(selectedStage, selectedChain, simulation.World);
            BuildProductionHistory(selectedChain);
        }

        private void ClearProductionTray(string message)
        {
            if (message == null)
            {
                return;
            }

            if (productionSubtitle != null && productionSubtitle.text != message)
            {
                productionSubtitle.text = message;
                productionWorkList.Clear();
                productionChain.Clear();
                productionDetails.Clear();
                productionHistory.Clear();
                productionRenderedKey = null;
            }
        }

        private void BuildProductionWorkList(List<CreativeWorkRecord> roots, CreativeWorkRecord selectedRoot)
        {
            productionWorkList.Clear();
            Text(productionWorkList, "Recent works", "section-title");
            for (int index = 0; index < roots.Count; index++)
            {
                CreativeWorkRecord root = roots[index];
                List<CreativeWorkRecord> chain = BuildProductionChain(root, simulation.World.Works);
                CreativeWorkRecord latest = chain[chain.Count - 1];
                string text = ProductionTitle(root) + "\n" + ProductionStageName(latest.Stage) + " · " + ProductionStatus(latest, chain, simulation.World.Clock.CurrentTicks);
                Button button = ActionButton(productionWorkList, text, () => SelectProductionRoot(root.Id));
                button.AddToClassList("production-work");
                button.EnableInClassList("is-selected", root.Id == selectedRoot.Id);
            }
        }

        private void BuildProductionStageChain(List<CreativeWorkRecord> chain, CreativeWorkRecord selectedStage)
        {
            productionChain.Clear();
            Text(productionChain, "Production chain", "section-title");
            for (int index = 0; index < ProductionStageOrder.Length; index++)
            {
                WorkStage stage = ProductionStageOrder[index];
                CreativeWorkRecord work = chain.FirstOrDefault(item => item.Stage == stage);
                VisualElement step = Box(productionChain, "production-step");
                if (work == null)
                {
                    Text(step, ProductionStageName(stage), "production-stage");
                    Text(step, "Not created in this chain", "muted");
                    continue;
                }

                Button select = ActionButton(step, ProductionStageName(stage), () => SelectProductionStage(work.Id));
                select.AddToClassList("production-select");
                select.EnableInClassList("is-selected", work.Id == selectedStage.Id);
                Text(step, ProductionStatus(work, chain, simulation.World.Clock.CurrentTicks), "muted");
            }
        }

        private void BuildProductionDetails(CreativeWorkRecord work, List<CreativeWorkRecord> chain, WorldSnapshot world)
        {
            productionDetails.Clear();
            Text(productionDetails, "Selected output", "section-title");
            Text(productionDetails, ProductionTitle(work), "production-title");
            Text(productionDetails, ProductionStageName(work.Stage) + " · " + ProductionStatus(work, chain, world.Clock.CurrentTicks), "production-stage muted");
            AddProductionFact(productionDetails, "Created", FormatProductionDate(work.CreatedTicks));
            AddProductionFact(productionDetails, "Theme", RecordedValue(work.Theme));
            AddProductionFact(productionDetails, "Mood", RecordedValue(work.Mood));
            AddProductionFact(productionDetails, "Genre", RecordedValue(work.ContentGenre));
            AddProductionFact(productionDetails, "Quality", work.Quality.ToString(CultureInfo.InvariantCulture) + "/100");

            if (work.Stage == WorkStage.Track)
            {
                AddProductionFact(productionDetails, "Release", work.Released ? "Released" : "Not released");
                AddProductionFact(productionDetails, "Lifetime streams", work.LifetimeStreams.ToString("N0", CultureInfo.InvariantCulture));
            }
            else if (work.Stage == WorkStage.Master)
            {
                AddProductionFact(productionDetails, "Release", "Unreleased master");
            }

            AddProductionCreator(productionDetails, world, "Songwriter", work.SongwriterCreatorId);
            AddProductionCreator(productionDetails, world, "Recording artist", work.RecordingArtistCreatorId);
            AddProductionCreator(productionDetails, world, "Producer", work.ProducerCreatorId);

            StructureRecord structure = world.Structures.FirstOrDefault(item => item.Id == work.StructureId);
            if (structure != null)
            {
                AddProductionFact(productionDetails, "Facility", structure.Name);
            }

            if (!string.IsNullOrWhiteSpace(work.SlotId))
            {
                AddProductionFact(productionDetails, "Slot", work.SlotId);
            }
        }

        private void BuildProductionHistory(List<CreativeWorkRecord> chain)
        {
            productionHistory.Clear();
            Text(productionHistory, "Linked history", "section-title");
            var workIds = new HashSet<string>(chain.Select(work => work.Id), StringComparer.Ordinal);
            List<WorldEventRecord> events = simulation.World.Events
                .OrderByDescending(item => item.OccurredTicks)
                .ThenByDescending(item => item.Priority)
                .ToList();
            var directEventIds = new HashSet<string>(events
                .Where(item => workIds.Contains(item.SubjectId) || workIds.Contains(item.RelatedId))
                .Select(item => item.Id), StringComparer.Ordinal);
            List<WorldEventRecord> linked = events
                .Where(item => directEventIds.Contains(item.Id) || directEventIds.Contains(item.ParentEventId))
                .Take(6)
                .ToList();

            if (linked.Count == 0)
            {
                Text(productionHistory, "No linked events are recorded for this chain.", "muted");
                return;
            }

            for (int index = 0; index < linked.Count; index++)
            {
                WorldEventRecord item = linked[index];
                VisualElement entry = Box(productionHistory, "production-history-item");
                Text(entry, FormatProductionDate(item.OccurredTicks) + " · " + item.Category, "muted");
                Text(entry, item.Headline, "body-copy");
                if (!string.IsNullOrWhiteSpace(item.Explanation))
                {
                    Text(entry, item.Explanation, "muted");
                }
            }
        }

        private List<CreativeWorkRecord> BuildProductionChain(CreativeWorkRecord root, List<CreativeWorkRecord> works)
        {
            var chain = new List<CreativeWorkRecord> { root };
            CreativeWorkRecord current = root;
            var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id };

            while (chain.Count < ProductionStageOrder.Length)
            {
                WorkStage? expectedStage = NextProductionStage(current.Stage);
                if (!expectedStage.HasValue)
                {
                    break;
                }

                CreativeWorkRecord next = works
                    .Where(item => item.LabelId == root.LabelId && item.SourceOutputId == current.Id && item.Stage == expectedStage.Value)
                    .OrderByDescending(item => item.CreatedTicks)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (next == null || string.IsNullOrWhiteSpace(next.Id) || !visited.Add(next.Id))
                {
                    break;
                }

                chain.Add(next);
                current = next;
            }

            return chain;
        }

        private void SelectProductionRoot(string rootId)
        {
            productionSelectedRootId = rootId;
            productionSelectedStageId = null;
            productionRenderedKey = null;
            RefreshProductionTray();
        }

        private void SelectProductionStage(string workId)
        {
            productionSelectedStageId = workId;
            productionRenderedKey = null;
            RefreshProductionTray();
        }

        private string ProductionStatus(CreativeWorkRecord work, List<CreativeWorkRecord> chain, long currentTicks)
        {
            if (work.Stage == WorkStage.Track)
            {
                return work.Released
                    ? "Released · " + work.LifetimeStreams.ToString("N0", CultureInfo.InvariantCulture) + " streams"
                    : "Track not released";
            }

            bool hasLinkedSuccessor = work.HasDownstreamOutput || chain.Any(item => item.SourceOutputId == work.Id);
            if (work.Stage == WorkStage.Master && hasLinkedSuccessor)
            {
                return "Unreleased master · Track linked";
            }

            if (hasLinkedSuccessor)
            {
                return "Stage complete";
            }

            if (work.ReadyForNextStageTicks <= 0)
            {
                return "Readiness not recorded";
            }

            string progress = currentTicks >= work.ReadyForNextStageTicks
                ? "Ready for next stage"
                : "In progress · ready " + FormatProductionDate(work.ReadyForNextStageTicks);
            return work.Stage == WorkStage.Master ? progress + " · unreleased" : progress;
        }

        private void AddProductionCreator(VisualElement parent, WorldSnapshot world, string role, string creatorId)
        {
            if (string.IsNullOrWhiteSpace(creatorId))
            {
                return;
            }

            MemberRecord creator = world.Members.FirstOrDefault(item => item.Id == creatorId);
            string name = creator == null ? null : creator.Name;
            Text(parent, role + " · " + (string.IsNullOrWhiteSpace(name) ? "Name unavailable" : name), "production-fact");
        }

        private static void AddProductionFact(VisualElement parent, string label, string value)
        {
            Text(parent, label + " · " + value, "production-fact");
        }

        private static string ProductionTitle(CreativeWorkRecord work)
        {
            return string.IsNullOrWhiteSpace(work.Title) ? "Title not recorded" : work.Title;
        }

        private static string RecordedValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Not recorded" : value;
        }

        private static string ProductionStageName(WorkStage stage)
        {
            switch (stage)
            {
                case WorkStage.SheetMusic: return "Sheet Music";
                case WorkStage.DemoRecording: return "Demo Recording";
                case WorkStage.Master: return "Master";
                case WorkStage.Track: return "Track";
                default: return stage.ToString();
            }
        }

        private static WorkStage? NextProductionStage(WorkStage stage)
        {
            switch (stage)
            {
                case WorkStage.SheetMusic: return WorkStage.DemoRecording;
                case WorkStage.DemoRecording: return WorkStage.Master;
                case WorkStage.Master: return WorkStage.Track;
                default: return null;
            }
        }

        private static string FormatProductionDate(long ticks)
        {
            if (ticks <= DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                return "Date not recorded";
            }

            return new DateTime(ticks, DateTimeKind.Utc).ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }
    }
}
