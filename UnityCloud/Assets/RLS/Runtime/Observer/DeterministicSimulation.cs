using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Topoda.RLS.Observer
{
    /// <summary>
    /// Authoritative observer kernel. Presentation state never enters this type.
    /// Stable ordering is time, priority, then persistent event ID.
    /// </summary>
    public sealed class DeterministicSimulation : ObserverQuery
    {
        private readonly ScenarioDefinition scenario;
        private readonly SimulationTuning tuning;
        private WorldSnapshot world;

        public DeterministicSimulation(ScenarioDefinition definition, SimulationTuning configuration)
        {
            scenario = definition ?? throw new ArgumentNullException("definition");
            tuning = configuration ?? throw new ArgumentNullException("configuration");
            world = CreateInitialWorld();
        }

        public WorldSnapshot World { get { return world; } }
        public ScenarioDefinition Scenario { get { return scenario; } }
        public SimulationTuning Tuning { get { return tuning; } }

        public void ReplaceWorld(WorldSnapshot replacement)
        {
            if (replacement == null) throw new ArgumentNullException("replacement");
            if (replacement.SchemaVersion != scenario.SchemaVersion)
                throw new InvalidOperationException("This snapshot was made by an incompatible version of the observer build.");
            if (!string.Equals(replacement.TuningVersion, tuning.Version, StringComparison.Ordinal))
                throw new InvalidOperationException("This snapshot uses different simulation tuning and cannot be loaded silently.");
            string calculated = DeterminismDigest.Calculate(replacement);
            if (!string.Equals(replacement.Digest, calculated, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This snapshot did not pass its integrity check, so it was not loaded.");
            world = replacement;
        }

        public void Reset() { world = CreateInitialWorld(); }
        public void SingleStep() { AdvanceSteps(1); }

        public void AdvanceSteps(long stepCount)
        {
            if (stepCount < 0) throw new ArgumentOutOfRangeException("stepCount");
            for (long index = 0; index < stepCount; index++)
            {
                DateTime before = world.Clock.CurrentUtc;
                world.Clock.SingleStep();
                DateTime after = world.Clock.CurrentUtc;
                if (before.Date != after.Date) ProcessNewDay(after.Date);
                if (before.Hour != after.Hour && after.Minute == 0 && after.Hour % 6 == 0) ProcessAudienceBatch(after);
            }
            RefreshDigest();
        }

        public void AdvanceTo(DateTime targetUtc)
        {
            DateTime target = DateTime.SpecifyKind(targetUtc, DateTimeKind.Utc);
            if (target < world.Clock.CurrentUtc) throw new InvalidOperationException("The observer clock cannot move backward. Load a snapshot or reset instead.");
            long minutes = (long)(target - world.Clock.CurrentUtc).TotalMinutes;
            if (minutes % world.Clock.FixedStepMinutes != 0) throw new InvalidOperationException("Skip Time must land on a five-minute boundary.");
            AdvanceSteps(minutes / world.Clock.FixedStepMinutes);
        }

        public string RefreshDigest()
        {
            world.Digest = DeterminismDigest.Calculate(world);
            return world.Digest;
        }

        // ObserverQuery returns detached projections so presentation cannot mutate authority.
        public LabelRecord FindLabel(string id) { return Copy(LabelById(id)); }
        public ActRecord FindAct(string id) { return Copy(world.Acts.FirstOrDefault(item => item.Id == id)); }
        public CreativeWorkRecord FindWork(string id) { return Copy(world.Works.FirstOrDefault(item => item.Id == id)); }
        public IList<WorldEventRecord> RecentEvents(int count, string category)
        {
            IEnumerable<WorldEventRecord> query = world.Events;
            if (!string.IsNullOrWhiteSpace(category) && category != "All") query = query.Where(item => item.Category == category);
            return query.OrderByDescending(item => item.OccurredTicks).ThenByDescending(item => item.Priority).ThenByDescending(item => item.Id, StringComparer.Ordinal).Take(Math.Max(0, count)).Select(Copy).ToList();
        }
        public IList<ChartEntryRecord> CurrentChart() { return world.Chart.OrderBy(item => item.Rank).Select(Copy).ToList(); }
        public long TotalMarketCash() { return world.Labels.Sum(item => item.Cash); }

        private WorldSnapshot CreateInitialWorld()
        {
            var initial = new WorldSnapshot
            {
                SchemaVersion = scenario.SchemaVersion,
                TuningVersion = tuning.Version,
                Seed = scenario.Seed,
                Clock = new SimulationClock { CurrentTicks = scenario.StartTicks, FixedStepMinutes = tuning.FixedStepMinutes, Paused = true, Speed = 1, StepIndex = 0 },
                TrendIndex = 50,
                NextEventSequence = 1
            };
            world = initial;

            string[] firstNames = { "Ari", "Mika", "Nia", "Sol", "Tavi", "Ivo", "Ena", "Lux" };
            string[] lastNames = { "Vale", "Rowan", "Miro", "Hale", "Sato", "Koa", "Ren", "Bell" };
            string[] actNouns = { "Signals", "Gardens", "Transit", "Mirrors", "Weather", "Embers", "Windows", "Tides" };
            for (int index = 0; index < scenario.LabelFixtures.Count; index++)
            {
                LabelFixture fixture = scenario.LabelFixtures[index];
                world.Labels.Add(new LabelRecord
                {
                    Id = fixture.Id, Name = fixture.Name, Nation = fixture.Nation, MarketCode = fixture.MarketCode, Archetype = fixture.Archetype,
                    ReleaseCadencePreference = fixture.ReleaseCadencePreference, QualityPreference = fixture.QualityPreference,
                    PromotionPreference = fixture.PromotionPreference, CashReservePreference = fixture.CashReservePreference,
                    AudienceGrowthPreference = fixture.AudienceGrowthPreference, TrendResponsePreference = fixture.TrendResponsePreference,
                    ColorHex = fixture.ColorHex, Cash = tuning.InitialLabelCash, Reputation = 24 + index * 3,
                    CatalogValue = 0, PromotionPressure = 10 + index * 2, ChartScore = 0
                });

                string actId = "ACT-" + (index + 1).ToString("00", CultureInfo.InvariantCulture);
                var act = new ActRecord { Id = actId, Name = firstNames[index] + " " + actNouns[index], LabelId = fixture.Id, Audience = 1800 + index * 375, Momentum = 20 + index * 3 };
                string[] roles = { "Songwriter", "Recording Artist", "Producer" };
                for (int creatorIndex = 0; creatorIndex < roles.Length; creatorIndex++)
                {
                    string creatorId = "CRT-" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + "-" + (creatorIndex + 1);
                    world.Members.Add(new MemberRecord
                    {
                        Id = creatorId,
                        Name = firstNames[(index + creatorIndex) % firstNames.Length] + " " + lastNames[(index * 2 + creatorIndex) % lastNames.Length],
                        Role = roles[creatorIndex], LabelId = fixture.Id, Skill = 42 + index * 3 + creatorIndex * 4, Energy = 100, CareerMomentum = 15 + index
                    });
                    act.MemberIds.Add(creatorId);
                }
                world.Acts.Add(act);

                string structureId = "STR-" + (index + 1).ToString("00", CultureInfo.InvariantCulture);
                world.Structures.Add(new StructureRecord { Id = structureId, LabelId = fixture.Id, Name = fixture.Name + " Production Structure", Kind = "Production Structure", SlotCount = 3, OccupiedSlots = 0, Condition = 82 + index });
                world.Slots.Add(new SlotRecord { Id = structureId + "-SNG", StructureId = structureId, SlotType = "Songwriting", ActiveOutputId = string.Empty, AvailableAtTicks = scenario.StartTicks });
                world.Slots.Add(new SlotRecord { Id = structureId + "-REC", StructureId = structureId, SlotType = "Recording", ActiveOutputId = string.Empty, AvailableAtTicks = scenario.StartTicks });
                world.Slots.Add(new SlotRecord { Id = structureId + "-PRO", StructureId = structureId, SlotType = "Production", ActiveOutputId = string.Empty, AvailableAtTicks = scenario.StartTicks });

                EnsureStream("creation/" + fixture.Id);
                EnsureStream("production/" + fixture.Id);
                EnsureStream("promotion/" + fixture.Id);
                EnsureStream("audience/" + fixture.Id);
            }
            EnsureStream("trend/global");

            world.Nations.Add(new NationAggregateRecord { Id = "NAT-ANN", Name = "Annglora", Population = 142000000, ActiveAudience = 28700000, MarketHeat = 53 });
            world.Nations.Add(new NationAggregateRecord { Id = "NAT-BYT", Name = "Byteria", Population = 89000000, ActiveAudience = 18100000, MarketHeat = 47 });
            world.Nations.Add(new NationAggregateRecord { Id = "NAT-CRO", Name = "Crownia", Population = 64000000, ActiveAudience = 12200000, MarketHeat = 44 });
            for (int index = 0; index < 18; index++)
            {
                NationAggregateRecord nation = world.Nations[index % world.Nations.Count];
                world.AudienceChunks.Add(new AudienceChunkRecord
                {
                    Id = "AUD-" + (index + 1).ToString("00", CultureInfo.InvariantCulture), Nation = nation.Name,
                    MemberCount = 300000 + index * 17000, MusicInterest = 45 + index % 9,
                    TrendSensitivity = 35 + (index * 7) % 40, SpendingPower = 30 + (index * 5) % 45,
                    FavoriteLabelId = scenario.LabelFixtures[index % scenario.LabelFixtures.Count].Id
                });
            }
            AddEvent(10, "World", "Central Bloomville", string.Empty, string.Empty, "The observer sandbox opened", "Eight autonomous validation labels entered the market. No player-management route exists.", "labels=8");
            AddEvent(20, "Clock", "2425-01-01", string.Empty, string.Empty, "The five-minute world clock began", "Every authoritative change advances on the deterministic five-minute spine.", "stepMinutes=5");
            RefreshDigest();
            return world;
        }

        private void ProcessNewDay(DateTime date)
        {
            int dayNumber = (int)(date - scenario.StartDateUtc.Date).TotalDays;
            world.TrendIndex = 35 + NextInt("trend/global", 46);
            foreach (SlotRecord slot in world.Slots.Where(item => item.AvailableAtTicks <= world.Clock.CurrentTicks)) slot.ActiveOutputId = string.Empty;

            for (int index = 0; index < world.Labels.Count; index++)
            {
                LabelRecord label = world.Labels[index];
                label.Cash -= tuning.DailyOperatingCost + index * 23;
                label.PromotionPressure = 10 + NextInt("promotion/" + label.Id, 71);
                int interval = Math.Max(4, tuning.SheetMusicIntervalDays + (50 - label.ReleaseCadencePreference) / 20);
                if ((dayNumber + index) % interval == 0) CreateSheetMusic(label, date, dayNumber);
                AdvanceProductionOutputs(label, date);
                RunPromotion(label);
            }
            UpdateChart(date);
            UpdateStructures();
            RecoverCreators();
        }

        private void ProcessAudienceBatch(DateTime time)
        {
            foreach (CreativeWorkRecord track in world.Works.Where(item => item.Stage == WorkStage.Track).OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                LabelRecord label = LabelById(track.LabelId);
                long newStreams = 8 + track.Quality / 4 + label.PromotionPressure / 5 + NextInt("audience/" + label.Id, 18);
                track.LifetimeStreams += newStreams;
                label.Cash += Math.Max(1, newStreams / 4);
                label.CatalogValue += (int)Math.Max(1, newStreams / 20);
            }
        }

        private void CreateSheetMusic(LabelRecord label, DateTime date, int dayNumber)
        {
            MemberRecord songwriter = Creator(label.Id, "Songwriter");
            SlotRecord slot = AvailableSlot(label.Id, "Songwriting");
            if (songwriter == null || slot == null) return;
            string[] titles = { "Neon Weather", "Slow Satellite", "Borrowed Morning", "Glass Flowers", "City of Echoes", "Afterimage", "Soft Current", "Open Window" };
            int ordinal = world.Works.Count + 1;
            var output = new CreativeWorkRecord
            {
                Id = "OUT-" + ordinal.ToString("0000", CultureInfo.InvariantCulture),
                Title = titles[(dayNumber + ParseFinalDigit(label.MarketCode)) & 7] + " " + ordinal.ToString("00", CultureInfo.InvariantCulture),
                LabelId = label.Id, Stage = WorkStage.SheetMusic, SourceOutputId = string.Empty,
                SongwriterCreatorId = songwriter.Id, RecordingArtistCreatorId = string.Empty, ProducerCreatorId = string.Empty,
                StructureId = slot.StructureId, SlotId = slot.Id, OptionalActId = string.Empty,
                Theme = "Theme-" + (1 + NextInt("creation/" + label.Id, 6)), Mood = string.Empty, ContentGenre = string.Empty,
                Quality = 38 + label.QualityPreference / 8 + NextInt("creation/" + label.Id, 34), CreatedTicks = date.Ticks,
                ReadyForNextStageTicks = date.AddDays(tuning.DemoLeadDays).Ticks, HasDownstreamOutput = false, Released = false, LifetimeStreams = 0
            };
            world.Works.Add(output);
            Occupy(slot, output);
            songwriter.Energy = Math.Max(0, songwriter.Energy - 12);
            AddEvent(40, "Creation", output.Id, songwriter.Id, string.Empty, output.Title + " became Sheet Music", "A Songwriter Creator ID used a compatible Structure Slot to create a separate Sheet Music output.", "outputType=SheetMusic");
        }

        private void AdvanceProductionOutputs(LabelRecord label, DateTime date)
        {
            CreativeWorkRecord sheet = ReadyOutput(label.Id, WorkStage.SheetMusic);
            if (sheet != null)
            {
                MemberRecord artist = Creator(label.Id, "Recording Artist");
                SlotRecord slot = AvailableSlot(label.Id, "Recording");
                if (artist != null && slot != null) CreateDemo(sheet, artist, slot, date);
            }

            CreativeWorkRecord demo = ReadyOutput(label.Id, WorkStage.DemoRecording);
            if (demo != null)
            {
                MemberRecord producer = Creator(label.Id, "Producer");
                SlotRecord slot = AvailableSlot(label.Id, "Production");
                if (producer != null && slot != null) CreateMaster(demo, producer, slot, date);
            }

            CreativeWorkRecord master = ReadyOutput(label.Id, WorkStage.Master);
            if (master != null) ReleaseTrack(master, label, date);
        }

        private void CreateDemo(CreativeWorkRecord sheet, MemberRecord artist, SlotRecord slot, DateTime date)
        {
            CreativeWorkRecord demo = NewLinkedOutput(sheet, WorkStage.DemoRecording, date);
            demo.RecordingArtistCreatorId = artist.Id;
            demo.Mood = "Mood-" + (1 + NextInt("production/" + sheet.LabelId, 9));
            demo.Quality = Math.Min(100, sheet.Quality + NextInt("production/" + sheet.LabelId, 7));
            demo.StructureId = slot.StructureId; demo.SlotId = slot.Id;
            demo.ReadyForNextStageTicks = date.AddDays(tuning.MasterLeadDays).Ticks;
            sheet.HasDownstreamOutput = true;
            world.Works.Add(demo); Occupy(slot, demo); artist.Energy = Math.Max(0, artist.Energy - 12);
            string parent = EventForSubject(sheet.Id);
            AddEvent(40, "Production", demo.Id, sheet.Id, parent, demo.Title + " became a Demo Recording", "A Recording Artist Creator ID used the preserved Sheet Music in a compatible Structure Slot.", "outputType=DemoRecording;source=" + sheet.Id);
        }

        private void CreateMaster(CreativeWorkRecord demo, MemberRecord producer, SlotRecord slot, DateTime date)
        {
            CreativeWorkRecord master = NewLinkedOutput(demo, WorkStage.Master, date);
            master.ProducerCreatorId = producer.Id;
            master.ContentGenre = "Genre-" + (1 + NextInt("production/" + demo.LabelId, 6));
            master.Quality = Math.Min(100, demo.Quality + 3 + NextInt("production/" + demo.LabelId, 8));
            master.StructureId = slot.StructureId; master.SlotId = slot.Id;
            master.ReadyForNextStageTicks = date.AddDays(tuning.ReleaseLeadDays).Ticks;
            demo.HasDownstreamOutput = true;
            world.Works.Add(master); Occupy(slot, master); producer.Energy = Math.Max(0, producer.Energy - 12);
            AddEvent(40, "Production", master.Id, demo.Id, EventForSubject(demo.Id), master.Title + " became a Master", "A Producer Creator ID used the preserved Demo Recording to create a separate Master.", "outputType=Master;source=" + demo.Id);
        }

        private void ReleaseTrack(CreativeWorkRecord master, LabelRecord label, DateTime date)
        {
            CreativeWorkRecord track = NewLinkedOutput(master, WorkStage.Track, date);
            track.Quality = master.Quality;
            track.ReadyForNextStageTicks = date.Ticks;
            track.Released = true;
            track.OptionalActId = string.Empty;
            master.HasDownstreamOutput = true;
            world.Works.Add(track);
            label.CatalogValue += track.Quality * 9;
            label.Reputation += Math.Max(1, track.Quality / 20);
            AddEvent(50, "Release", track.Id, master.Id, EventForSubject(master.Id), track.Title + " was released as a Track", "Release preserved the Master and created a separate Track referencing it. No Act was required.", "outputType=Track;sourceMaster=" + master.Id);
        }

        private CreativeWorkRecord NewLinkedOutput(CreativeWorkRecord source, WorkStage stage, DateTime date)
        {
            return new CreativeWorkRecord
            {
                Id = "OUT-" + (world.Works.Count + 1).ToString("0000", CultureInfo.InvariantCulture), Title = source.Title,
                LabelId = source.LabelId, Stage = stage, SourceOutputId = source.Id,
                SongwriterCreatorId = source.SongwriterCreatorId, RecordingArtistCreatorId = source.RecordingArtistCreatorId,
                ProducerCreatorId = source.ProducerCreatorId, StructureId = string.Empty, SlotId = string.Empty,
                OptionalActId = string.Empty, Theme = source.Theme, Mood = source.Mood, ContentGenre = source.ContentGenre,
                Quality = source.Quality, CreatedTicks = date.Ticks, ReadyForNextStageTicks = date.Ticks,
                HasDownstreamOutput = false, Released = false, LifetimeStreams = 0
            };
        }

        private void RunPromotion(LabelRecord label)
        {
            CreativeWorkRecord track = world.Works.Where(item => item.LabelId == label.Id && item.Stage == WorkStage.Track).OrderByDescending(item => item.CreatedTicks).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
            long protectedCash = tuning.InitialLabelCash * label.CashReservePreference / 250;
            int threshold = Math.Max(30, 78 - label.PromotionPreference / 2);
            if (track == null || label.Cash - tuning.PromotionCost < protectedCash || label.PromotionPressure < threshold) return;
            label.Cash -= tuning.PromotionCost;
            int trendFit = 100 - Math.Abs(world.TrendIndex - label.TrendResponsePreference);
            int response = tuning.BaseAudienceResponse + track.Quality / 5 + label.AudienceGrowthPreference / 10 + trendFit / 20 + NextInt("promotion/" + label.Id, 13);
            ActRecord act = world.Acts.FirstOrDefault(item => item.LabelId == label.Id);
            if (act != null) { act.Audience += response * 11; act.Momentum = Math.Min(100, act.Momentum + response / 4); }
            track.LifetimeStreams += response * 7;
            AddEvent(30, "Promotion", label.Id, track.Id, EventForSubject(track.Id), label.Name + " promoted " + track.Title, "The autonomous label spent from its Wallet; audience response changed streams and finances.", "cost=" + tuning.PromotionCost + ";response=" + response);
        }

        private void UpdateChart(DateTime date)
        {
            world.Chart.Clear();
            List<CreativeWorkRecord> ranked = world.Works.Where(item => item.Stage == WorkStage.Track)
                .OrderByDescending(item => item.LifetimeStreams + item.Quality * 20L).ThenBy(item => item.Id, StringComparer.Ordinal).Take(10).ToList();
            for (int index = 0; index < ranked.Count; index++)
            {
                CreativeWorkRecord track = ranked[index];
                world.Chart.Add(new ChartEntryRecord { Rank = index + 1, WorkId = track.Id, Title = track.Title, LabelId = track.LabelId, Score = (int)Math.Min(int.MaxValue, track.LifetimeStreams + track.Quality * 20L) });
            }
            foreach (LabelRecord label in world.Labels) label.ChartScore = world.Chart.Where(item => item.LabelId == label.Id).Sum(item => Math.Max(0, 11 - item.Rank) * item.Score / 100);
            if (date.DayOfWeek == DayOfWeek.Monday && world.Chart.Count > 0)
            {
                ChartEntryRecord leader = world.Chart[0];
                AddEvent(60, "Chart", leader.WorkId, leader.LabelId, EventForSubject(leader.WorkId), leader.Title + " leads the Central Bloomville chart", "The weekly ordering uses released Tracks, deterministic audience activity, quality, promotion, and stable Track ID tie-breaking.", "rank=1;score=" + leader.Score);
            }
        }

        private void UpdateStructures()
        {
            foreach (StructureRecord structure in world.Structures)
            {
                structure.OccupiedSlots = world.Slots.Count(item => item.StructureId == structure.Id && item.AvailableAtTicks > world.Clock.CurrentTicks);
                structure.Condition = Math.Max(55, structure.Condition - (structure.OccupiedSlots == structure.SlotCount ? 1 : 0));
            }
        }

        private void RecoverCreators()
        {
            foreach (MemberRecord creator in world.Members.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                creator.Energy = Math.Min(100, creator.Energy + 3);
                if (NextInt("creation/" + creator.LabelId, 5) == 0) creator.CareerMomentum = Math.Min(100, creator.CareerMomentum + 1);
            }
        }

        private CreativeWorkRecord ReadyOutput(string labelId, WorkStage stage)
        {
            return world.Works.Where(item => item.LabelId == labelId && item.Stage == stage && !item.HasDownstreamOutput && item.ReadyForNextStageTicks <= world.Clock.CurrentTicks)
                .OrderBy(item => item.ReadyForNextStageTicks).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        private MemberRecord Creator(string labelId, string role) { return world.Members.FirstOrDefault(item => item.LabelId == labelId && item.Role == role); }
        private LabelRecord LabelById(string id) { return world.Labels.FirstOrDefault(item => item.Id == id); }
        private SlotRecord AvailableSlot(string labelId, string slotType)
        {
            string structureId = world.Structures.First(item => item.LabelId == labelId).Id;
            return world.Slots.Where(item => item.StructureId == structureId && item.SlotType == slotType && item.AvailableAtTicks <= world.Clock.CurrentTicks).OrderBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        }
        private void Occupy(SlotRecord slot, CreativeWorkRecord output) { slot.ActiveOutputId = output.Id; slot.AvailableAtTicks = output.ReadyForNextStageTicks; }
        private string EventForSubject(string subjectId) { return world.Events.Where(item => item.SubjectId == subjectId).OrderByDescending(item => item.Sequence).Select(item => item.Id).FirstOrDefault() ?? string.Empty; }

        private void EnsureStream(string streamId)
        {
            if (world.RandomStreams.Any(item => item.StreamId == streamId)) return;
            ulong seed = StableHash64(streamId) ^ ((ulong)(uint)scenario.Seed << 32) ^ 0x9E3779B97F4A7C15UL;
            world.RandomStreams.Add(new RandomStreamState { StreamId = streamId, State = seed == 0 ? 1UL : seed });
            world.RandomStreams.Sort((left, right) => string.CompareOrdinal(left.StreamId, right.StreamId));
        }

        private int NextInt(string streamId, int maximumExclusive)
        {
            EnsureStream(streamId);
            RandomStreamState stream = world.RandomStreams.First(item => item.StreamId == streamId);
            ulong x = stream.State;
            x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
            stream.State = x;
            return (int)((x * 2685821657736338717UL) % (ulong)maximumExclusive);
        }

        private void AddEvent(int priority, string category, string subjectId, string relatedId, string parentId, string headline, string explanation, string payload)
        {
            long sequence = world.NextEventSequence++;
            world.Events.Add(new WorldEventRecord
            {
                Id = "EVT-" + sequence.ToString("00000000", CultureInfo.InvariantCulture), Sequence = sequence,
                OccurredTicks = world.Clock.CurrentTicks, Priority = priority, Category = category,
                SubjectId = subjectId, RelatedId = relatedId ?? string.Empty, ParentEventId = parentId ?? string.Empty,
                Headline = headline, Explanation = explanation, AuthoritativePayload = payload ?? string.Empty
            });
            List<WorldEventRecord> ordered = world.Events.OrderBy(item => item.OccurredTicks).ThenBy(item => item.Priority).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
            world.Events = ordered.Skip(Math.Max(0, ordered.Count - 600)).ToList();
        }

        private static int ParseFinalDigit(string value) { char last = value[value.Length - 1]; return last >= '0' && last <= '9' ? last - '0' : 0; }
        private static ulong StableHash64(string value)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte valueByte in Encoding.UTF8.GetBytes(value)) { hash ^= valueByte; hash *= 1099511628211UL; }
            return hash;
        }

        private static LabelRecord Copy(LabelRecord item) { if (item == null) return null; return new LabelRecord { Id=item.Id,Name=item.Name,Nation=item.Nation,MarketCode=item.MarketCode,Archetype=item.Archetype,ReleaseCadencePreference=item.ReleaseCadencePreference,QualityPreference=item.QualityPreference,PromotionPreference=item.PromotionPreference,CashReservePreference=item.CashReservePreference,AudienceGrowthPreference=item.AudienceGrowthPreference,TrendResponsePreference=item.TrendResponsePreference,ColorHex=item.ColorHex,Cash=item.Cash,Reputation=item.Reputation,CatalogValue=item.CatalogValue,PromotionPressure=item.PromotionPressure,ChartScore=item.ChartScore }; }
        private static ActRecord Copy(ActRecord item) { if (item == null) return null; return new ActRecord { Id=item.Id,Name=item.Name,LabelId=item.LabelId,MemberIds=new List<string>(item.MemberIds),Audience=item.Audience,Momentum=item.Momentum }; }
        private static CreativeWorkRecord Copy(CreativeWorkRecord item) { if (item == null) return null; return new CreativeWorkRecord { Id=item.Id,Title=item.Title,LabelId=item.LabelId,Stage=item.Stage,SourceOutputId=item.SourceOutputId,SongwriterCreatorId=item.SongwriterCreatorId,RecordingArtistCreatorId=item.RecordingArtistCreatorId,ProducerCreatorId=item.ProducerCreatorId,StructureId=item.StructureId,SlotId=item.SlotId,OptionalActId=item.OptionalActId,Theme=item.Theme,Mood=item.Mood,ContentGenre=item.ContentGenre,Quality=item.Quality,CreatedTicks=item.CreatedTicks,ReadyForNextStageTicks=item.ReadyForNextStageTicks,HasDownstreamOutput=item.HasDownstreamOutput,Released=item.Released,LifetimeStreams=item.LifetimeStreams }; }
        private static WorldEventRecord Copy(WorldEventRecord item) { return new WorldEventRecord { Id=item.Id,Sequence=item.Sequence,OccurredTicks=item.OccurredTicks,Priority=item.Priority,Category=item.Category,SubjectId=item.SubjectId,RelatedId=item.RelatedId,ParentEventId=item.ParentEventId,Headline=item.Headline,Explanation=item.Explanation,AuthoritativePayload=item.AuthoritativePayload }; }
        private static ChartEntryRecord Copy(ChartEntryRecord item) { return new ChartEntryRecord { Rank=item.Rank,WorkId=item.WorkId,Title=item.Title,LabelId=item.LabelId,Score=item.Score }; }
    }

    public static class DeterminismDigest
    {
        public const string ContractVersion = "digest-v2-authoritative-only";

        public static string Calculate(WorldSnapshot world)
        {
            if (world == null) throw new ArgumentNullException("world");
            var text = new StringBuilder(65536);
            Add(text, ContractVersion); Add(text, world.SchemaVersion); Add(text, world.TuningVersion); Add(text, world.Seed);
            Add(text, world.Clock.CurrentTicks); Add(text, world.Clock.FixedStepMinutes); Add(text, world.Clock.Paused); Add(text, world.Clock.Speed); Add(text, world.Clock.StepIndex);
            Add(text, world.NextEventSequence); Add(text, world.TrendIndex);
            foreach (RandomStreamState item in world.RandomStreams.OrderBy(value => value.StreamId, StringComparer.Ordinal)) { Add(text,item.StreamId);Add(text,item.State); }
            foreach (MemberRecord item in world.Members.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.Name);Add(text,item.Role);Add(text,item.LabelId);Add(text,item.Skill);Add(text,item.Energy);Add(text,item.CareerMomentum); }
            foreach (ActRecord item in world.Acts.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.Name);Add(text,item.LabelId);foreach(string id in item.MemberIds.OrderBy(value=>value,StringComparer.Ordinal))Add(text,id);Add(text,item.Audience);Add(text,item.Momentum); }
            foreach (LabelRecord item in world.Labels.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.Name);Add(text,item.Nation);Add(text,item.MarketCode);Add(text,item.Archetype);Add(text,item.ReleaseCadencePreference);Add(text,item.QualityPreference);Add(text,item.PromotionPreference);Add(text,item.CashReservePreference);Add(text,item.AudienceGrowthPreference);Add(text,item.TrendResponsePreference);Add(text,item.ColorHex);Add(text,item.Cash);Add(text,item.Reputation);Add(text,item.CatalogValue);Add(text,item.PromotionPressure);Add(text,item.ChartScore); }
            foreach (StructureRecord item in world.Structures.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.LabelId);Add(text,item.Name);Add(text,item.Kind);Add(text,item.SlotCount);Add(text,item.OccupiedSlots);Add(text,item.Condition); }
            foreach (SlotRecord item in world.Slots.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.StructureId);Add(text,item.SlotType);Add(text,item.ActiveOutputId);Add(text,item.AvailableAtTicks); }
            foreach (CreativeWorkRecord item in world.Works.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.Title);Add(text,item.LabelId);Add(text,(int)item.Stage);Add(text,item.SourceOutputId);Add(text,item.SongwriterCreatorId);Add(text,item.RecordingArtistCreatorId);Add(text,item.ProducerCreatorId);Add(text,item.StructureId);Add(text,item.SlotId);Add(text,item.OptionalActId);Add(text,item.Theme);Add(text,item.Mood);Add(text,item.ContentGenre);Add(text,item.Quality);Add(text,item.CreatedTicks);Add(text,item.ReadyForNextStageTicks);Add(text,item.HasDownstreamOutput);Add(text,item.Released);Add(text,item.LifetimeStreams); }
            foreach (AudienceChunkRecord item in world.AudienceChunks.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.Nation);Add(text,item.MemberCount);Add(text,item.MusicInterest);Add(text,item.TrendSensitivity);Add(text,item.SpendingPower);Add(text,item.FavoriteLabelId); }
            foreach (NationAggregateRecord item in world.Nations.OrderBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.Name);Add(text,item.Population);Add(text,item.ActiveAudience);Add(text,item.MarketHeat); }
            foreach (ChartEntryRecord item in world.Chart.OrderBy(value => value.Rank).ThenBy(value => value.WorkId, StringComparer.Ordinal)) { Add(text,item.Rank);Add(text,item.WorkId);Add(text,item.Title);Add(text,item.LabelId);Add(text,item.Score); }
            foreach (WorldEventRecord item in world.Events.OrderBy(value => value.OccurredTicks).ThenBy(value => value.Priority).ThenBy(value => value.Id, StringComparer.Ordinal)) { Add(text,item.Id);Add(text,item.Sequence);Add(text,item.OccurredTicks);Add(text,item.Priority);Add(text,item.Category);Add(text,item.SubjectId);Add(text,item.RelatedId);Add(text,item.ParentEventId);Add(text,item.Headline);Add(text,item.Explanation);Add(text,item.AuthoritativePayload); }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static void Add(StringBuilder text, object value)
        {
            string stringValue = value == null ? "<null>" : Convert.ToString(value, CultureInfo.InvariantCulture);
            text.Append(stringValue.Length).Append(':').Append(stringValue).Append('|');
        }
    }
}
