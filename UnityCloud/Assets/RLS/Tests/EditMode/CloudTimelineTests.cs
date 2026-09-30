using System;
using System.Linq;
using NUnit.Framework;
using Topoda.RLS.Observer;
using UnityEngine;

namespace Topoda.RLS.Tests.EditMode
{
    public sealed class CloudTimelineTests
    {
        [Test]
        public void SeptemberObservationContinuesFromAugustHandoffWithSameAuthoritativeState()
        {
            var direct = new DeterministicSimulation(ScenarioDefinition.GenericObserverSeed(), SimulationTuning.ObserverPreview());
            var handoff = new DeterministicSimulation(ScenarioDefinition.GenericObserverSeed(), SimulationTuning.ObserverPreview());
            DateTime end = new DateTime(2425, 9, 30, 0, 0, 0, DateTimeKind.Utc);
            direct.AdvanceTo(end);
            handoff.AdvanceTo(ScenarioDefinition.GenericObserverSeed().EndDateUtc);
            handoff.AdvanceTo(end);
            Assert.That(handoff.World.Digest, Is.EqualTo(direct.World.Digest));
            Assert.That(direct.World.Clock.CurrentUtc, Is.EqualTo(end));
            Assert.That(direct.World.Works.Any(w => w.Stage == WorkStage.Track), Is.True);
            string snapshot = SnapshotStore.Encode(direct.World, "September observation");
            var loaded = SnapshotStore.Decode(snapshot, direct.World.SchemaVersion);
            Assert.That(loaded.World.Digest, Is.EqualTo(direct.World.Digest));
            Debug.Log("RLS_SEPTEMBER_VALIDATED digest=" + direct.World.Digest + " snapshotBytes=" + System.Text.Encoding.UTF8.GetByteCount(snapshot));
        }
    }
}
