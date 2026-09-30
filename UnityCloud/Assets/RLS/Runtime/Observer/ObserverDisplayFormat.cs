using System;

namespace Topoda.RLS.Observer
{
    internal static class ObserverDisplayFormat
    {
        public static string SnapshotDate(SnapshotMetadata item)
        {
            return item.SimulationTicks <= 0
                ? "Unknown date"
                : new DateTime(item.SimulationTicks, DateTimeKind.Utc).ToString("MMM d, yyyy HH:mm");
        }

        public static string StageLabel(CreativeWorkRecord work)
        {
            return work.Stage == WorkStage.Track
                ? "Released Track • " + work.LifetimeStreams.ToString("N0") + " streams"
                : work.Stage.ToString().Replace("Recording", " Recording").Replace("Music", " Music");
        }

        public static string Preference(int value)
        {
            return value >= 80 ? "Very high"
                : value >= 65 ? "High"
                : value >= 45 ? "Balanced"
                : value >= 30 ? "Low"
                : "Very low";
        }
    }
}
