using NUnit.Framework;
using Topoda.RLS.Observer;

namespace Topoda.RLS.Tests
{
    public sealed class ObserverPresentationTests
    {
        [Test]
        public void LightingHasReadableDayNightEndpoints()
        {
            Assert.That(ObserverLighting.Daylight(0f), Is.EqualTo(0f));
            Assert.That(ObserverLighting.Daylight(0.5f), Is.EqualTo(1f));
            Assert.That(ObserverLighting.Daylight(1f), Is.EqualTo(ObserverLighting.Daylight(0f)));
        }

        [Test]
        public void LightingChangesSmoothlyAcrossFiveMinuteTicks()
        {
            float last = ObserverLighting.Daylight(0);
            for (int tick = 1; tick <= 288; tick++)
            {
                float next = ObserverLighting.Daylight(tick / 288f);
                Assert.That(System.Math.Abs(next - last), Is.LessThan(0.09f));
                Assert.That(next, Is.InRange(0f, 1f));
                last = next;
            }
        }
    }
}
