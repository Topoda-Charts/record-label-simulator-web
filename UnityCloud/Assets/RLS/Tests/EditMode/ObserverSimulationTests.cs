using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Topoda.RLS.Observer;
using UnityEditor;
using UnityEngine;

namespace Topoda.RLS.Tests.EditMode
{
    public sealed class ObserverSimulationTests
    {
        private readonly List<string> temporaryDirectories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (string directory in temporaryDirectories)
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            temporaryDirectories.Clear();
        }

        [Test]
        public void ScenarioUsesExactAnchorsAndFormulaicGenericLabels()
        {
            ScenarioDefinition scenario = ScenarioDefinition.GenericObserverSeed();
            Assert.That(scenario.StartDateUtc, Is.EqualTo(new DateTime(2425, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(scenario.EndDateUtc, Is.EqualTo(new DateTime(2425, 8, 31, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(scenario.LabelFixtures, Has.Count.EqualTo(8));
            Assert.That(scenario.BaselineReference, Is.EqualTo("generic-eight-label-validation-seed"));
            Assert.That(scenario.LabelFixtures.Count(item => item.Nation == "Annglora"), Is.EqualTo(3));
            Assert.That(scenario.LabelFixtures.Count(item => item.Nation == "Byteria"), Is.EqualTo(3));
            Assert.That(scenario.LabelFixtures.Count(item => item.Nation == "Crownia"), Is.EqualTo(2));
            Assert.That(scenario.LabelFixtures.Select(item => item.Id), Is.EqualTo(new[] { "ARL1", "ARL2", "ARL3", "BRL1", "BRL2", "BRL3", "CRL1", "CRL2" }));
            Assert.That(scenario.LabelFixtures.Select(item => item.MarketCode), Is.EqualTo(scenario.LabelFixtures.Select(item => item.Id)));
            Assert.That(scenario.LabelFixtures.All(item => item.Name == item.Nation + " Record Label " + item.MarketCode[item.MarketCode.Length - 1]), Is.True);
            Assert.That(scenario.LabelFixtures.All(item => item.Archetype.Contains("preference") || item.Archetype == "Evenly weighted preferences"), Is.True);
        }

        [Test]
        public void MainMenuIsDirectAndHasNoModeSelectionOrManagementRoute()
        {
            Assert.That(Enum.GetNames(typeof(ApplicationState)), Does.Not.Contain("ModeSelection"));
            string source = File.ReadAllText(Path.GetFullPath("Assets/RLS/Runtime/Observer/ObserverApplicationController.cs"));
            Assert.That(source, Does.Contain("WATCH THE WORLD GROW"));
            Assert.That(source, Does.Contain("OPEN AUG 31 HANDOFF"));
            Assert.That(source, Does.Contain("Load Snapshot"));
            Assert.That(source, Does.Contain("Settings"));
            Assert.That(source, Does.Contain("Credits"));
            Assert.That(source, Does.Contain("Exit"));
            Assert.That(source, Does.Not.Contain("Observe Sandbox"));
            Assert.That(source, Does.Not.Contain("ModeSelection"));
        }

        [Test]
        public void FixedStepsAndBatchedAdvancementConverge()
        {
            DeterministicSimulation stepped = Create();
            DeterministicSimulation batched = Create();
            for (int index = 0; index < 2016; index++) stepped.SingleStep();
            batched.AdvanceSteps(2016);
            Assert.That(stepped.World.Digest, Is.EqualTo(batched.World.Digest));
        }

        [Test]
        public void SameSeedRepeatsWithStablePerSystemRandomStreams()
        {
            DeterministicSimulation first = Create();
            DeterministicSimulation second = Create();
            first.AdvanceSteps(9216);
            second.AdvanceSteps(9216);
            Assert.That(first.World.Digest, Is.EqualTo(second.World.Digest));
            string[] streamIds = first.World.RandomStreams.Select(item => item.StreamId).ToArray();
            Assert.That(streamIds, Is.EqualTo(streamIds.OrderBy(item => item, StringComparer.Ordinal).ToArray()));
            Assert.That(first.World.RandomStreams.Select(item => item.StreamId).Distinct().Count(), Is.EqualTo(first.World.RandomStreams.Count));
            Assert.That(first.World.RandomStreams.Any(item => item.StreamId.StartsWith("creation/", StringComparison.Ordinal)), Is.True);
            Assert.That(first.World.RandomStreams.Any(item => item.StreamId.StartsWith("production/", StringComparison.Ordinal)), Is.True);
            Assert.That(first.World.RandomStreams.Any(item => item.StreamId.StartsWith("promotion/", StringComparison.Ordinal)), Is.True);
            Assert.That(first.World.RandomStreams.Any(item => item.StreamId.StartsWith("audience/", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void DigestIsSensitiveToEveryPersistedRecordFamily()
        {
            DeterministicSimulation simulation = Create();
            simulation.AdvanceSteps(288L * 40L);
            WorldSnapshot source = simulation.World;
            AssertDigestChanges(source, world => world.Clock.StepIndex++);
            AssertDigestChanges(source, world => world.RandomStreams[0].State++);
            AssertDigestChanges(source, world => world.Members[0].Energy--);
            AssertDigestChanges(source, world => world.Acts[0].Audience++);
            AssertDigestChanges(source, world => world.Labels[0].Cash++);
            AssertDigestChanges(source, world => world.Structures[0].Condition--);
            AssertDigestChanges(source, world => world.Slots[0].AvailableAtTicks++);
            AssertDigestChanges(source, world => world.Works[0].Quality++);
            AssertDigestChanges(source, world => world.AudienceChunks[0].MemberCount++);
            AssertDigestChanges(source, world => world.Nations[0].MarketHeat++);
            AssertDigestChanges(source, world => world.Chart[0].Score++);
            AssertDigestChanges(source, world => world.Events[0].AuthoritativePayload += ";changed=1");
            AssertDigestChanges(source, world => world.NextEventSequence++);
            AssertDigestChanges(source, world => world.TrendIndex++);
        }

        [Test]
        public void PresentationFiltersAndCameraStateAreOutsideAuthoritativeSnapshot()
        {
            string[] fieldNames = typeof(WorldSnapshot).GetFields().Select(field => field.Name).ToArray();
            Assert.That(fieldNames, Does.Not.Contain("cameraPan"));
            Assert.That(fieldNames, Does.Not.Contain("cameraZoom"));
            Assert.That(fieldNames, Does.Not.Contain("eventCategory"));
            Assert.That(fieldNames, Does.Not.Contain("selectedLabelId"));
        }

        [Test]
        public void JanToAugustAndColdHandoffConvergeWithinBudget()
        {
            DeterministicSimulation live = Create();
            DeterministicSimulation handoff = Create();
            DateTime target = ScenarioDefinition.GenericObserverSeed().EndDateUtc;
            long steps = (long)(target - live.World.Clock.CurrentUtc).TotalMinutes / live.World.Clock.FixedStepMinutes;
            for (long offset = 0; offset < steps; offset += 288) live.AdvanceSteps(Math.Min(288, steps - offset));
            var stopwatch = Stopwatch.StartNew();
            handoff.AdvanceTo(target);
            stopwatch.Stop();
            TestContext.WriteLine("Cold Aug 31 generation: " + stopwatch.Elapsed.TotalSeconds.ToString("0.000") + " seconds; digest=" + handoff.World.Digest);
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(60)));
            Assert.That(live.World.Clock.CurrentUtc, Is.EqualTo(target));
            Assert.That(live.World.Digest, Is.EqualTo(handoff.World.Digest));
        }

        [Test]
        public void TenThousandStableMembersAdvanceHeadlessly()
        {
            DeterministicSimulation simulation = Create();
            for (int index = simulation.World.Members.Count; index < 10000; index++)
            {
                simulation.World.Members.Add(new MemberRecord
                {
                    Id = "MEM-VALIDATION-" + index.ToString("00000"),
                    Name = "Validation Member " + index,
                    Role = "Community Member",
                    LabelId = "ARL1",
                    Skill = 40 + index % 41,
                    Energy = 100,
                    CareerMomentum = index % 30
                });
            }
            string[] identities = simulation.World.Members.Select(item => item.Id).ToArray();
            var stopwatch = Stopwatch.StartNew();
            simulation.AdvanceSteps(288L * 31L);
            stopwatch.Stop();
            TestContext.WriteLine("10,000 Member headless month: " + stopwatch.Elapsed.TotalSeconds.ToString("0.000") + " seconds");
            Assert.That(simulation.World.Members, Has.Count.EqualTo(10000));
            Assert.That(simulation.World.Members.Select(item => item.Id), Is.EqualTo(identities));
            Assert.That(simulation.World.Members.Select(item => item.Id).Distinct().Count(), Is.EqualTo(10000));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void SaveAdvanceLoadRestoresFullDigest()
        {
            DeterministicSimulation simulation = Create();
            simulation.AdvanceSteps(4320);
            string savedDigest = simulation.World.Digest;
            string json = SnapshotStore.Encode(simulation.World, "test");
            simulation.AdvanceSteps(1440);
            Assert.That(simulation.World.Digest, Is.Not.EqualTo(savedDigest));
            SnapshotEnvelope restored = SnapshotStore.Decode(json, 1);
            simulation.ReplaceWorld(restored.World);
            Assert.That(simulation.World.Digest, Is.EqualTo(savedDigest));
        }

        [Test]
        public void AtomicAutosavesRotateAndLeaveNoWritingResidue()
        {
            string directory = NewTemporaryDirectory();
            var store = new SnapshotStore(directory, 3);
            DeterministicSimulation simulation = Create();
            string first = simulation.World.Digest;
            store.SaveAutosave(simulation.World, "initialization");
            simulation.AdvanceSteps(288);
            store.SaveAutosave(simulation.World, "calendar checkpoint");
            Assert.That(File.Exists(Path.Combine(directory, "autosave.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "autosave-1.json")), Is.True);
            Assert.That(Directory.GetFiles(directory, "*.writing"), Is.Empty);
            SnapshotEnvelope recovery = store.Load("autosave-1.json", 1);
            Assert.That(recovery.World.Digest, Is.EqualTo(first));
        }

        [Test]
        public void CorruptAndIncompatibleSnapshotsFailWithoutChangingCurrentWorld()
        {
            DeterministicSimulation simulation = Create();
            string original = simulation.World.Digest;
            string json = SnapshotStore.Encode(simulation.World, "test");
            Assert.Throws<InvalidDataException>(() => SnapshotStore.Decode(json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2"), 1));
            Assert.Throws<InvalidDataException>(() => SnapshotStore.Decode(json.Replace(simulation.World.TuningVersion, "incompatible-tuning"), 1));
            Assert.Throws<InvalidDataException>(() => SnapshotStore.Decode(json.Replace("\"TrendIndex\":50", "\"TrendIndex\":51"), 1));
            Assert.That(simulation.World.Digest, Is.EqualTo(original));
        }

        [Test]
        public void LifecyclePreservesEachOutputAndDoesNotRequireAnAct()
        {
            DeterministicSimulation simulation = Create();
            simulation.World.Acts.Clear();
            simulation.RefreshDigest();
            simulation.AdvanceSteps(288L * 40L);
            CreativeWorkRecord track = simulation.World.Works.First(item => item.Stage == WorkStage.Track && item.Released);
            CreativeWorkRecord master = simulation.World.Works.Single(item => item.Id == track.SourceOutputId && item.Stage == WorkStage.Master);
            CreativeWorkRecord demo = simulation.World.Works.Single(item => item.Id == master.SourceOutputId && item.Stage == WorkStage.DemoRecording);
            CreativeWorkRecord sheet = simulation.World.Works.Single(item => item.Id == demo.SourceOutputId && item.Stage == WorkStage.SheetMusic);
            Assert.That(track.Id, Is.Not.EqualTo(master.Id));
            Assert.That(master.Id, Is.Not.EqualTo(demo.Id));
            Assert.That(demo.Id, Is.Not.EqualTo(sheet.Id));
            Assert.That(track.OptionalActId, Is.Empty);
            Assert.That(master.HasDownstreamOutput, Is.True);
            Assert.That(simulation.World.Events.Any(item => item.Headline.Contains("released as a Track") && item.RelatedId == master.Id), Is.True);
        }

        [Test]
        public void ConsequentialEventsAreBoundedUniqueAndCanonicallyOrdered()
        {
            DeterministicSimulation simulation = Create();
            simulation.AdvanceTo(ScenarioDefinition.GenericObserverSeed().EndDateUtc);
            Assert.That(simulation.World.Events.Count, Is.LessThanOrEqualTo(600));
            Assert.That(simulation.World.Events.Select(item => item.Id).Distinct().Count(), Is.EqualTo(simulation.World.Events.Count));
            WorldEventRecord[] canonical = simulation.World.Events.OrderBy(item => item.OccurredTicks).ThenBy(item => item.Priority).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
            Assert.That(simulation.World.Events, Is.EqualTo(canonical));
            Assert.That(simulation.World.Events.Where(item => !string.IsNullOrEmpty(item.ParentEventId)).All(item => item.ParentEventId.StartsWith("EVT-", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void ObserverQueriesReturnDetachedReadOnlyProjections()
        {
            DeterministicSimulation simulation = Create();
            string digest = simulation.World.Digest;
            LabelRecord projection = simulation.FindLabel("ARL1");
            projection.Cash = -1;
            Assert.That(simulation.FindLabel("ARL1").Cash, Is.Not.EqualTo(-1));
            Assert.That(simulation.World.Digest, Is.EqualTo(digest));
        }

        [Test]
        public void OptionalCloudAdapterIsDisabledAndIsolated()
        {
            DeterministicSimulation simulation = Create();
            string digest = simulation.World.Digest;
            OptionalCloudAdapter cloud = new DisabledCloudAdapter();
            string error;
            Assert.That(cloud.TryUpload(simulation.World, out error), Is.False);
            Assert.That(cloud.IsAvailable, Is.False);
            Assert.That(error, Does.Contain("works offline"));
            Assert.That(simulation.World.Digest, Is.EqualTo(digest));
        }

        [Test]
        public void WorldPresenterRefreshDoesNotMutateDigest()
        {
            DeterministicSimulation simulation = Create();
            simulation.AdvanceSteps(480);
            string digest = simulation.World.Digest;
            var presenterObject = new GameObject("Bloomville Presenter Test");
            try
            {
                BloomvilleWorldPresenter presenter = presenterObject.AddComponent<BloomvilleWorldPresenter>();
                presenter.SetMaterials(CreateTestMaterialSet());
                presenter.RefreshFromSnapshot(simulation.World);
                Assert.That(simulation.World.Digest, Is.EqualTo(digest));
                Assert.That(DeterminismDigest.Calculate(simulation.World), Is.EqualTo(digest));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void NeighborhoodShowsLocalStructuresAndKeepsOrganizationsInSimulation()
        {
            DeterministicSimulation simulation = Create();
            var presenterObject = new GameObject("Bloomville Presenter Test");
            try
            {
                BloomvilleWorldPresenter presenter = presenterObject.AddComponent<BloomvilleWorldPresenter>();
                presenter.SetMaterials(CreateTestMaterialSet());
                presenter.RefreshFromSnapshot(simulation.World);
                Assert.That(presenter.DisplayedProductionStructureCount, Is.EqualTo(3));
                Assert.That(simulation.World.Labels.Count, Is.EqualTo(8));
                Assert.That(presenter.TryGetLabelAnchor("ARL1", out _), Is.True);
                Assert.That(presenter.TryGetLabelAnchor("BRL1", out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void BrandObserverSceneAndFrozenPackageSourcesArePresent()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RLS/Brand/favicon-darkmode-512.png"), Is.Not.Null);
            Assert.That(File.Exists(Path.GetFullPath("Assets/RLS/Scenes/RLSObserver.unity")), Is.True);
            Assert.That(File.Exists(Path.GetFullPath("Packages/manifest.json")), Is.True);
            Assert.That(File.Exists(Path.GetFullPath("Packages/packages-lock.json")), Is.True);
        }

        private static void AssertDigestChanges(WorldSnapshot source, Action<WorldSnapshot> mutation)
        {
            WorldSnapshot copy = JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(source));
            string before = DeterminismDigest.Calculate(copy);
            mutation(copy);
            Assert.That(DeterminismDigest.Calculate(copy), Is.Not.EqualTo(before));
        }

        private string NewTemporaryDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rls-observer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            temporaryDirectories.Add(directory);
            return directory;
        }

        private static DeterministicSimulation Create()
        {
            return new DeterministicSimulation(ScenarioDefinition.GenericObserverSeed(), SimulationTuning.ObserverPreview());
        }

        private static BloomvilleMaterialSet CreateTestMaterialSet()
        {
            Shader shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            Material shared = new Material(shader);
            Assert.That(shared.HasProperty("_Color"), Is.True);
            Assert.That(shared.HasProperty("_EmissionColor"), Is.True);
            shared.SetColor("_Color", new Color(0.3f, 0.35f, 0.4f));
            return new BloomvilleMaterialSet
            {
                Ground = shared,
                Road = shared,
                CityHallBase = shared,
                CityHallDome = shared,
                PlazaGarden = shared,
                FloraCanopy = shared,
                FloraTrunk = shared,
                FloraBiolum = shared,
                StructureBase = shared,
                AnngloraDistrictPad = shared,
                ByteriaDistrictPad = shared,
                CrowniaDistrictPad = shared,
                AnngloraHeadquarters = shared,
                ByteriaHeadquarters = shared,
                CrowniaHeadquarters = shared,
                AnngloraWindow = shared,
                ByteriaNeonWindow = shared,
                CrowniaReflectiveWindow = shared,
                CanalReflect = shared
            };
        }
    }
}
