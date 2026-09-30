using System;
using System.Collections.Generic;

namespace Topoda.RLS.Observer
{
    public enum ApplicationState
    {
        Splash,
        MainMenu,
        Loading,
        Observer,
        Settings,
        Credits,
        Exiting
    }

    public enum ObserverMode
    {
        WatchPreSimulation,
        OpenHandoff
    }

    public enum ObserverReviewStage
    {
        MenuAndShell = 1,
        AutonomousSandbox = 2,
        Complete = 3
    }

    public enum WorkStage
    {
        SheetMusic,
        DemoRecording,
        Master,
        Track
    }

    [Serializable]
    public sealed class SimulationTuning
    {
        public string Version = "observer-0.1.0-preview.1";
        public bool IsCanon = false;
        public int FixedStepMinutes = 5;
        public int InitialLabelCash = 125000;
        public int DailyOperatingCost = 780;
        public int SheetMusicIntervalDays = 7;
        public int DemoLeadDays = 3;
        public int MasterLeadDays = 5;
        public int ReleaseLeadDays = 7;
        public int PromotionCost = 950;
        public int BaseAudienceResponse = 18;
        public int RecoverySaveCount = 5;

        public static SimulationTuning ObserverPreview()
        {
            return new SimulationTuning();
        }
    }

    [Serializable]
    public sealed class LabelFixture
    {
        public string Id;
        public string Name;
        public string Nation;
        public string MarketCode;
        public string Archetype;
        public int ReleaseCadencePreference;
        public int QualityPreference;
        public int PromotionPreference;
        public int CashReservePreference;
        public int AudienceGrowthPreference;
        public int TrendResponsePreference;
        public string ColorHex;

        public LabelFixture(
            string id,
            string name,
            string nation,
            string marketCode,
            string archetype,
            int releaseCadence,
            int quality,
            int promotion,
            int cashReserve,
            int audienceGrowth,
            int trendResponse,
            string colorHex)
        {
            Id = id;
            Name = name;
            Nation = nation;
            MarketCode = marketCode;
            Archetype = archetype;
            ReleaseCadencePreference = releaseCadence;
            QualityPreference = quality;
            PromotionPreference = promotion;
            CashReservePreference = cashReserve;
            AudienceGrowthPreference = audienceGrowth;
            TrendResponsePreference = trendResponse;
            ColorHex = colorHex;
        }
    }

    [Serializable]
    public sealed class ScenarioDefinition
    {
        public int Seed;
        public long StartTicks;
        public long EndTicks;
        public string TuningVersion;
        public int SchemaVersion;
        public string BaselineReference;
        public List<LabelFixture> LabelFixtures = new List<LabelFixture>();

        public DateTime StartDateUtc { get { return new DateTime(StartTicks, DateTimeKind.Utc); } }
        public DateTime EndDateUtc { get { return new DateTime(EndTicks, DateTimeKind.Utc); } }

        public static ScenarioDefinition GenericObserverSeed()
        {
            var scenario = new ScenarioDefinition
            {
                Seed = 24250101,
                StartTicks = new DateTime(2425, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks,
                EndTicks = new DateTime(2425, 8, 31, 0, 0, 0, DateTimeKind.Utc).Ticks,
                TuningVersion = SimulationTuning.ObserverPreview().Version,
                SchemaVersion = 1,
                BaselineReference = "generic-eight-label-validation-seed"
            };

            // The observer seed deliberately mirrors the campaign roster's nation/code formula
            // without instantiating its named Hann/ARL3 identities.
            scenario.LabelFixtures.Add(new LabelFixture("ARL1", "Annglora Record Label 1", "Annglora", "ARL1", "High quality preference", 35, 85, 45, 55, 55, 40, "74D9FF"));
            scenario.LabelFixtures.Add(new LabelFixture("ARL2", "Annglora Record Label 2", "Annglora", "ARL2", "High audience-growth preference", 50, 55, 55, 45, 90, 60, "B7A5FF"));
            scenario.LabelFixtures.Add(new LabelFixture("ARL3", "Annglora Record Label 3", "Annglora", "ARL3", "Evenly weighted preferences", 60, 60, 60, 60, 60, 60, "FFB86B"));
            scenario.LabelFixtures.Add(new LabelFixture("BRL1", "Byteria Record Label 1", "Byteria", "BRL1", "High trend-response preference", 55, 50, 60, 45, 65, 90, "87E6A1"));
            scenario.LabelFixtures.Add(new LabelFixture("BRL2", "Byteria Record Label 2", "Byteria", "BRL2", "High cash-reserve preference", 35, 65, 30, 95, 40, 35, "FF85B8"));
            scenario.LabelFixtures.Add(new LabelFixture("BRL3", "Byteria Record Label 3", "Byteria", "BRL3", "High release-cadence preference", 95, 45, 55, 35, 60, 55, "F7E07A"));
            scenario.LabelFixtures.Add(new LabelFixture("CRL1", "Crownia Record Label 1", "Crownia", "CRL1", "High promotion preference", 55, 60, 95, 35, 70, 70, "6C8FFF"));
            scenario.LabelFixtures.Add(new LabelFixture("CRL2", "Crownia Record Label 2", "Crownia", "CRL2", "High audience-growth preference", 50, 60, 45, 55, 95, 45, "FF8C72"));
            return scenario;
        }
    }

    [Serializable]
    public sealed class SimulationClock
    {
        public long CurrentTicks;
        public int FixedStepMinutes = 5;
        public bool Paused = true;
        public int Speed = 1;
        public long StepIndex;

        public DateTime CurrentUtc { get { return new DateTime(CurrentTicks, DateTimeKind.Utc); } }

        public void SingleStep()
        {
            CurrentTicks += TimeSpan.FromMinutes(FixedStepMinutes).Ticks;
            StepIndex++;
        }
    }

    [Serializable]
    public sealed class RandomStreamState
    {
        public string StreamId;
        public ulong State;
    }

    [Serializable]
    public sealed class MemberRecord
    {
        public string Id;
        public string Name;
        public string Role;
        public string LabelId;
        public int Skill;
        public int Energy;
        public int CareerMomentum;
    }

    [Serializable]
    public sealed class ActRecord
    {
        public string Id;
        public string Name;
        public string LabelId;
        public List<string> MemberIds = new List<string>();
        public int Audience;
        public int Momentum;
    }

    [Serializable]
    public sealed class LabelRecord
    {
        public string Id;
        public string Name;
        public string Nation;
        public string MarketCode;
        public string Archetype;
        public int ReleaseCadencePreference;
        public int QualityPreference;
        public int PromotionPreference;
        public int CashReservePreference;
        public int AudienceGrowthPreference;
        public int TrendResponsePreference;
        public string ColorHex;
        public long Cash;
        public int Reputation;
        public int CatalogValue;
        public int PromotionPressure;
        public int ChartScore;
    }

    [Serializable]
    public sealed class StructureRecord
    {
        public string Id;
        public string LabelId;
        public string Name;
        public string Kind;
        public int SlotCount;
        public int OccupiedSlots;
        public int Condition;
    }

    [Serializable]
    public sealed class SlotRecord
    {
        public string Id;
        public string StructureId;
        public string SlotType;
        public string ActiveOutputId;
        public long AvailableAtTicks;
    }

    [Serializable]
    public sealed class CreativeWorkRecord
    {
        public string Id;
        public string Title;
        public string LabelId;
        public WorkStage Stage;
        public string SourceOutputId;
        public string SongwriterCreatorId;
        public string RecordingArtistCreatorId;
        public string ProducerCreatorId;
        public string StructureId;
        public string SlotId;
        public string OptionalActId;
        public string Theme;
        public string Mood;
        public string ContentGenre;
        public int Quality;
        public long CreatedTicks;
        public long ReadyForNextStageTicks;
        public bool HasDownstreamOutput;
        public bool Released;
        public long LifetimeStreams;
    }

    [Serializable]
    public sealed class AudienceChunkRecord
    {
        public string Id;
        public string Nation;
        public int MemberCount;
        public int MusicInterest;
        public int TrendSensitivity;
        public int SpendingPower;
        public string FavoriteLabelId;
    }

    [Serializable]
    public sealed class NationAggregateRecord
    {
        public string Id;
        public string Name;
        public long Population;
        public long ActiveAudience;
        public int MarketHeat;
    }

    [Serializable]
    public sealed class ChartEntryRecord
    {
        public int Rank;
        public string WorkId;
        public string Title;
        public string LabelId;
        public int Score;
    }

    [Serializable]
    public sealed class WorldEventRecord
    {
        public string Id;
        public long Sequence;
        public long OccurredTicks;
        public int Priority;
        public string Category;
        public string SubjectId;
        public string RelatedId;
        public string ParentEventId;
        public string Headline;
        public string Explanation;
        public string AuthoritativePayload;
    }

    [Serializable]
    public sealed class WorldSnapshot
    {
        public int SchemaVersion;
        public string TuningVersion;
        public int Seed;
        public SimulationClock Clock = new SimulationClock();
        public List<RandomStreamState> RandomStreams = new List<RandomStreamState>();
        public List<MemberRecord> Members = new List<MemberRecord>();
        public List<ActRecord> Acts = new List<ActRecord>();
        public List<LabelRecord> Labels = new List<LabelRecord>();
        public List<StructureRecord> Structures = new List<StructureRecord>();
        public List<SlotRecord> Slots = new List<SlotRecord>();
        public List<CreativeWorkRecord> Works = new List<CreativeWorkRecord>();
        public List<AudienceChunkRecord> AudienceChunks = new List<AudienceChunkRecord>();
        public List<NationAggregateRecord> Nations = new List<NationAggregateRecord>();
        public List<ChartEntryRecord> Chart = new List<ChartEntryRecord>();
        public List<WorldEventRecord> Events = new List<WorldEventRecord>();
        public long NextEventSequence;
        public int TrendIndex;
        public string Digest;
    }

    [Serializable]
    public sealed class SnapshotMetadata
    {
        public string Id;
        public string DisplayName;
        public string FileName;
        public long SavedAtUtcTicks;
        public long SimulationTicks;
        public int Seed;
        public int SchemaVersion;
        public string TuningVersion;
        public string Digest;
        public bool IsAutosave;
        public bool IntegrityValid;
        public string Compatibility;
    }

    [Serializable]
    public sealed class SnapshotEnvelope
    {
        public SnapshotMetadata Metadata = new SnapshotMetadata();
        public WorldSnapshot World = new WorldSnapshot();
    }

    public interface ObserverQuery
    {
        LabelRecord FindLabel(string id);
        ActRecord FindAct(string id);
        CreativeWorkRecord FindWork(string id);
        IList<WorldEventRecord> RecentEvents(int count, string category);
        IList<ChartEntryRecord> CurrentChart();
        long TotalMarketCash();
    }

    public interface OptionalCloudAdapter
    {
        bool IsAvailable { get; }
        string Status { get; }
        bool TryUpload(WorldSnapshot snapshot, out string plainLanguageError);
    }

    public sealed class DisabledCloudAdapter : OptionalCloudAdapter
    {
        public bool IsAvailable { get { return false; } }
        public string Status { get { return "Cloud backup is off. This observer build saves locally and works offline."; } }

        public bool TryUpload(WorldSnapshot snapshot, out string plainLanguageError)
        {
            plainLanguageError = Status;
            return false;
        }
    }
}
