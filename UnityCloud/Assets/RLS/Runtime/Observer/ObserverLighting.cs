using UnityEngine;

namespace Topoda.RLS.Observer
{
    public static class ObserverLighting
    {
        // Twilight spans sunrise/sunset rather than switching the entire scene at one tick.
        public static float Daylight(float dayFraction)
        {
            float elevation = Mathf.Sin((Mathf.Repeat(dayFraction, 1f) - 0.25f) * Mathf.PI * 2f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.25f, elevation));
        }
    }
}
