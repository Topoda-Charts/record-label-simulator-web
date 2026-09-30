using System.Linq;
using NUnit.Framework;
using Topoda.RLS.Observer;

namespace Topoda.RLS.Tests.EditMode
{
    public sealed class BloomvilleMemberProxiesTests
    {
        [Test]
        public void SampleSelectionRespectsCapAndStableOrdering()
        {
            DeterministicSimulation simulation = new DeterministicSimulation(ScenarioDefinition.GenericObserverSeed(), SimulationTuning.ObserverPreview());
            var members = simulation.World.Members;
            Assert.That(members.Count, Is.GreaterThan(0));

            var selected = BloomvilleMemberProxyPlanning.SelectMemberIdsForPresentation(members, BloomvilleMemberProxyPlanning.SampleCap);
            Assert.That(selected.Count, Is.LessThanOrEqualTo(BloomvilleMemberProxyPlanning.SampleCap));
            Assert.That(selected.Count, Is.EqualTo(members.Count));
            Assert.That(selected, Is.EqualTo(selected.OrderBy(id => id, System.StringComparer.Ordinal).ToArray()));

            string digestBefore = simulation.World.Digest;
            BloomvilleMemberProxyPlanning.SelectMemberIdsForPresentation(members, 40);
            simulation.RefreshDigest();
            Assert.That(simulation.World.Digest, Is.EqualTo(digestBefore));
        }

        [Test]
        public void ReleaseEventPulseMapsTrackSubjectToLabel()
        {
            var snapshot = new WorldSnapshot();
            snapshot.Labels.Add(new LabelRecord { Id = "ARL1", Name = "Annglora Record Label 1", Nation = "Annglora", MarketCode = "ARL1" });
            snapshot.Works.Add(new CreativeWorkRecord
            {
                Id = "OUT-0100",
                Title = "Fixture Track",
                LabelId = "ARL1",
                Stage = WorkStage.Track,
                Released = true
            });

            var release = new WorldEventRecord
            {
                Category = "Release",
                SubjectId = "OUT-0100",
                RelatedId = "OUT-0099"
            };

            bool resolved = BloomvilleMemberProxyPlanning.TryResolvePulseLabelId(release, snapshot, out string labelId);
            Assert.That(resolved, Is.True);
            Assert.That(labelId, Is.EqualTo("ARL1"));
        }
    }
}
