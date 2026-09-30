using NUnit.Framework;
using Topoda.RLS.Observer;
using UnityEngine;

namespace Topoda.RLS.Tests.EditMode
{
    public sealed class BloomvilleMemberProxyAvoidanceTests
    {
        [Test]
        public void ForcedOverlapSeparatesWithStableHorizontalTieBreak()
        {
            Vector3 overlap = new Vector3(4f, 0.825f, -2f);
            bool separated = BloomvilleMemberProxyPlanning.TrySeparatePair(
                overlap,
                overlap,
                0.64f,
                Vector3.right,
                out Vector3 left,
                out Vector3 right);

            Assert.That(separated, Is.True);
            Assert.That(left.y, Is.EqualTo(overlap.y));
            Assert.That(right.y, Is.EqualTo(overlap.y));
            Assert.That(Vector2.Distance(new Vector2(left.x, left.z), new Vector2(right.x, right.z)), Is.GreaterThanOrEqualTo(0.64f));

            BloomvilleMemberProxyPlanning.TrySeparatePair(
                overlap,
                overlap,
                0.64f,
                Vector3.right,
                out Vector3 repeatedLeft,
                out Vector3 repeatedRight);
            Assert.That(repeatedLeft, Is.EqualTo(left));
            Assert.That(repeatedRight, Is.EqualTo(right));
        }

        [Test]
        public void BlockedFallbackSearchFindsOnlySafeStreetPointAndHasFiniteBound()
        {
            Vector3 stuck = new Vector3(18f, 0.825f, 18f);
            const int maximumRing = 2;
            int callbackCount = 0;
            bool found = BloomvilleMemberProxyPlanning.TryFindSafeGridFallback(
                stuck,
                BloomvilleMemberProxyPlanning.StreetPitch,
                BloomvilleMemberProxyPlanning.StreetCenterOffset,
                BloomvilleMemberProxyPlanning.StreetLaneMargin,
                maximumRing,
                0x1234u,
                candidate =>
                {
                    callbackCount++;
                    return Mathf.Approximately(candidate.x, 22f) && Mathf.Approximately(candidate.z, 14f);
                },
                out Vector3 fallback,
                out int checkedCount);

            int candidateBound = (1 + 8 + 16) * 4;
            Assert.That(found, Is.True);
            Assert.That(fallback, Is.EqualTo(new Vector3(22f, stuck.y, 14f)));
            Assert.That(callbackCount, Is.EqualTo(checkedCount));
            Assert.That(checkedCount, Is.LessThanOrEqualTo(candidateBound));

            bool foundWhenFullyBlocked = BloomvilleMemberProxyPlanning.TryFindSafeGridFallback(
                stuck,
                BloomvilleMemberProxyPlanning.StreetPitch,
                BloomvilleMemberProxyPlanning.StreetCenterOffset,
                BloomvilleMemberProxyPlanning.StreetLaneMargin,
                maximumRing,
                0x1234u,
                _ => false,
                out _,
                out int blockedCount);
            Assert.That(foundWhenFullyBlocked, Is.False);
            Assert.That(blockedCount, Is.EqualTo(candidateBound));
        }
    }
}
