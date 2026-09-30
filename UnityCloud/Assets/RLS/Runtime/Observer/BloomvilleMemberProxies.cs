using System;
using System.Collections.Generic;
using UnityEngine;

namespace Topoda.RLS.Observer
{
    /// <summary>
    /// Presentation-only Member silhouettes for Central Bloomville (TH-03).
    /// Does not read or write simulation digest inputs.
    /// </summary>
    public static class BloomvilleMemberProxyPlanning
    {
        public const int SampleCap = 120;
        public const float StreetPitch = 36f;
        public const float StreetCenterOffset = 18f;
        public const float StreetLaneMargin = 4f;
        public const float WorldBounds = 64f;

        public static IReadOnlyList<string> SelectMemberIdsForPresentation(IList<MemberRecord> members, int cap = SampleCap)
        {
            if (members == null || members.Count == 0 || cap <= 0)
            {
                return Array.Empty<string>();
            }

            int take = Math.Min(cap, members.Count);
            var sorted = new List<MemberRecord>(members);
            sorted.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
            var ids = new string[take];
            for (int index = 0; index < take; index++)
            {
                ids[index] = sorted[index].Id;
            }

            return ids;
        }

        public static bool TrySeparatePair(
            Vector3 left,
            Vector3 right,
            float minimumSpacing,
            Vector3 stableTieDirection,
            out Vector3 separatedLeft,
            out Vector3 separatedRight)
        {
            separatedLeft = left;
            separatedRight = right;
            if (minimumSpacing <= 0f)
            {
                return false;
            }

            Vector3 delta = left - right;
            delta.y = 0f;
            float distanceSquared = delta.sqrMagnitude;
            if (distanceSquared >= minimumSpacing * minimumSpacing)
            {
                return false;
            }

            Vector3 direction;
            float distance;
            if (distanceSquared <= 0.000001f)
            {
                stableTieDirection.y = 0f;
                direction = stableTieDirection.sqrMagnitude > 0.000001f
                    ? stableTieDirection.normalized
                    : Vector3.right;
                distance = 0f;
            }
            else
            {
                distance = Mathf.Sqrt(distanceSquared);
                direction = delta / distance;
            }

            float correction = (minimumSpacing - distance) * 0.5f + 0.005f;
            separatedLeft = left + direction * correction;
            separatedRight = right - direction * correction;
            return true;
        }

        public static bool TryFindSafeGridFallback(
            Vector3 current,
            float gridSpacing,
            float gridOffset,
            float laneMargin,
            int maximumRing,
            uint tieBreakSeed,
            Func<Vector3, bool> isSafe,
            out Vector3 fallback,
            out int candidatesChecked)
        {
            fallback = current;
            candidatesChecked = 0;
            if (gridSpacing <= 0f || maximumRing < 0 || isSafe == null)
            {
                return false;
            }

            float originX = Mathf.Round((current.x - gridOffset) / gridSpacing) * gridSpacing + gridOffset;
            float originZ = Mathf.Round((current.z - gridOffset) / gridSpacing) * gridSpacing + gridOffset;
            for (int ring = 0; ring <= maximumRing; ring++)
            {
                int ringPointCount = ring == 0 ? 1 : ring * 8;
                int laneCount = laneMargin > 0f ? 4 : 1;
                int candidateCount = ringPointCount * laneCount;
                int start = candidateCount == 1
                    ? 0
                    : (int)((tieBreakSeed ^ ((uint)ring * 2654435761u)) % (uint)candidateCount);
                for (int offset = 0; offset < candidateCount; offset++)
                {
                    int candidateIndex = (start + offset) % candidateCount;
                    GetRingOffset(ring, candidateIndex / laneCount, out int dx, out int dz);
                    int laneIndex = candidateIndex % laneCount;
                    float laneX = laneCount == 1 || laneIndex % 2 == 0 ? -laneMargin : laneMargin;
                    float laneZ = laneCount == 1 || laneIndex < 2 ? -laneMargin : laneMargin;
                    if (laneCount == 1)
                    {
                        laneX = 0f;
                        laneZ = 0f;
                    }

                    Vector3 candidate = new Vector3(
                        originX + dx * gridSpacing + laneX,
                        current.y,
                        originZ + dz * gridSpacing + laneZ);
                    candidatesChecked++;
                    if (isSafe(candidate))
                    {
                        fallback = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void GetRingOffset(int ring, int index, out int x, out int z)
        {
            if (ring == 0)
            {
                x = 0;
                z = 0;
                return;
            }

            int edgeLength = ring * 2 + 1;
            if (index < edgeLength)
            {
                x = -ring + index;
                z = -ring;
                return;
            }

            index -= edgeLength;
            edgeLength = ring * 2;
            if (index < edgeLength)
            {
                x = ring;
                z = -ring + 1 + index;
                return;
            }

            index -= edgeLength;
            if (index < edgeLength)
            {
                x = ring - 1 - index;
                z = ring;
                return;
            }

            index -= edgeLength;
            x = -ring;
            z = ring - 1 - index;
        }

        public static bool TryResolvePulseLabelId(WorldEventRecord worldEvent, WorldSnapshot snapshot, out string labelId)
        {
            labelId = null;
            if (worldEvent == null || snapshot == null)
            {
                return false;
            }

            string category = worldEvent.Category ?? string.Empty;
            if (string.Equals(category, "Promotion", StringComparison.Ordinal))
            {
                labelId = worldEvent.SubjectId;
                return !string.IsNullOrEmpty(labelId);
            }

            if (string.Equals(category, "Chart", StringComparison.Ordinal))
            {
                if (TryLabelFromWork(snapshot, worldEvent.SubjectId, out labelId))
                {
                    return true;
                }

                labelId = worldEvent.RelatedId;
                return IsKnownLabel(snapshot, labelId);
            }

            if (string.Equals(category, "Release", StringComparison.Ordinal)
                || string.Equals(category, "Production", StringComparison.Ordinal)
                || string.Equals(category, "Creation", StringComparison.Ordinal))
            {
                if (TryLabelFromWork(snapshot, worldEvent.SubjectId, out labelId))
                {
                    return true;
                }

                if (TryLabelFromWork(snapshot, worldEvent.RelatedId, out labelId))
                {
                    return true;
                }
            }

            if (IsKnownLabel(snapshot, worldEvent.RelatedId))
            {
                labelId = worldEvent.RelatedId;
                return true;
            }

            return false;
        }

        private static bool TryLabelFromWork(WorldSnapshot snapshot, string workId, out string labelId)
        {
            labelId = null;
            if (string.IsNullOrEmpty(workId))
            {
                return false;
            }

            for (int index = 0; index < snapshot.Works.Count; index++)
            {
                CreativeWorkRecord work = snapshot.Works[index];
                if (work.Id == workId)
                {
                    labelId = work.LabelId;
                    return !string.IsNullOrEmpty(labelId);
                }
            }

            return false;
        }

        private static bool IsKnownLabel(WorldSnapshot snapshot, string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (int index = 0; index < snapshot.Labels.Count; index++)
            {
                if (snapshot.Labels[index].Id == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
    public sealed class BloomvilleMemberProxies : MonoBehaviour
    {
        private const float ProxyHeight = 1.65f;
        private const float MoveSpeed = 2.8f;
        private const float ArriveDistance = 0.35f;
        private const float LogicInterval = 0.12f;
        private const float PulseDurationSeconds = 2.4f;
        private const float RoadSpacing = BloomvilleMemberProxyPlanning.StreetPitch;
        private const float ProxyPickRadius = 0.36f;
        private const float ProxyPickSegmentHalfHeight = 0.54f;
        private const float ProxyColliderRadius = 0.3f;
        private const float MinimumMemberSpacing = 0.64f;
        private const float CollisionQuerySkin = 0.01f;
        private const float NoProgressRecoverySeconds = 1.2f;
        // Built-in layer 2 keeps proxy colliders out of DefaultRaycastLayers camera and picking casts.
        private const int IgnoreRaycastLayer = 2;
        private const int MaximumSeparationPasses = 4;
        private const int MaximumFallbackRing = 4;
        private static readonly int StandardColorId = Shader.PropertyToID("_Color");
        private static readonly float[] AvoidanceAngles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f, 180f };
        private static readonly int StaticCollisionMask = ~(1 << IgnoreRaycastLayer);

        [SerializeField] private int sampleCap = BloomvilleMemberProxyPlanning.SampleCap;
        [SerializeField] private bool drawInstancedFigures = true;

        private BloomvilleWorldPresenter presenter;
        private ObserverApplicationController controller;
        private Transform pulseRoot;
        private Mesh proxyMesh;
        private Material proxyMaterial;
        private Material pulseMaterial;
        private readonly List<MemberProxyState> proxies = new List<MemberProxyState>(BloomvilleMemberProxyPlanning.SampleCap);
        private readonly Dictionary<string, LabelPulseState> labelPulses = new Dictionary<string, LabelPulseState>(StringComparer.Ordinal);
        private readonly Dictionary<string, Matrix4x4[]> labelMatrices = new Dictionary<string, Matrix4x4[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, MaterialPropertyBlock> labelPropertyBlocks = new Dictionary<string, MaterialPropertyBlock>(StringComparer.Ordinal);
        private readonly List<string> labelDrawOrder = new List<string>(8);
        private long lastSeenEventSequence;
        private long lastPresentedStepIndex = -1;
        private bool eventCursorInitialized;
        private float logicAccumulator;
        private bool reducedMotion;

        public int PresentedMemberCount => proxies.Count;
        public int ActiveMemberColliderCount { get; private set; }
        public float MinimumObservedMemberSpacing { get; private set; }
        public int LastBlockedMemberCount { get; private set; }
        public int TotalBlockedMovementCount { get; private set; }
        public int RecoveryAttemptCount { get; private set; }
        public int RecoveryFailureCount { get; private set; }
        public int SafeFallbackCount { get; private set; }
        public int UnresolvedOverlapCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SelfAttachAfterSceneLoad()
        {
            BloomvilleWorldPresenter foundPresenter = FindFirstObjectByType<BloomvilleWorldPresenter>();
            if (foundPresenter == null)
            {
                return;
            }

            if (foundPresenter.GetComponent<BloomvilleMemberProxies>() != null)
            {
                return;
            }

            foundPresenter.gameObject.AddComponent<BloomvilleMemberProxies>();
        }

        private void Awake()
        {
            presenter = GetComponent<BloomvilleWorldPresenter>();
            if (presenter == null)
            {
                presenter = FindFirstObjectByType<BloomvilleWorldPresenter>();
            }

            controller = FindFirstObjectByType<ObserverApplicationController>();
            EnsureProxyMesh();
            EnsurePulseRoot();
        }

        private void OnEnable()
        {
            for (int index = 0; index < proxies.Count; index++)
            {
                if (proxies[index].Collider != null)
                {
                    proxies[index].Collider.enabled = true;
                }
            }

            UpdatePresentationCounters();
        }

        private void OnDisable()
        {
            for (int index = 0; index < proxies.Count; index++)
            {
                if (proxies[index].Collider != null)
                {
                    proxies[index].Collider.enabled = false;
                }
            }

            UpdatePresentationCounters();
        }

        private void OnDestroy()
        {
            for (int index = 0; index < proxies.Count; index++)
            {
                DestroyProxyCollider(proxies[index]);
            }
        }

        private void Update()
        {
            if (presenter == null)
            {
                return;
            }

            RefreshReducedMotion();
            TryResolveMaterials();
            WorldSnapshot snapshot = controller?.Simulation?.World;
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.Clock.StepIndex != lastPresentedStepIndex)
            {
                lastPresentedStepIndex = snapshot.Clock.StepIndex;
                RebuildProxyRoster(snapshot);
                DetectNewEvents(snapshot);
            }

            logicAccumulator += Time.unscaledDeltaTime;
            if (logicAccumulator >= LogicInterval)
            {
                logicAccumulator = 0f;
                TickProxyGoals(snapshot);
                TickProxyMovement(LogicInterval);
                TickLabelPulses(LogicInterval);
            }

            if (drawInstancedFigures && proxyMesh != null && proxyMaterial != null)
            {
                DrawInstancedProxies();
            }
        }

        private void RefreshReducedMotion()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<ObserverApplicationController>();
            }

            reducedMotion = controller != null && controller.ReducedMotionEnabled;
        }

        private void EnsureProxyMesh()
        {
            if (proxyMesh != null)
            {
                return;
            }

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            // Keep the built-in collider component reachable when WebGL strips unused engine types.
            _ = temp.GetComponent<CapsuleCollider>();
            MeshFilter filter = temp.GetComponent<MeshFilter>();
            proxyMesh = filter.sharedMesh;
            Destroy(temp);
        }

        public bool TryPickMember(Ray ray, out string memberId)
        {
            memberId = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState proxy = proxies[index];
                if (TryIntersectProxy(ray, proxy.Position, out float distance) && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    memberId = proxy.MemberId;
                }
            }

            return !string.IsNullOrEmpty(memberId);
        }

        public bool TryGetMemberPosition(string memberId, out Vector3 position)
        {
            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState proxy = proxies[index];
                if (string.Equals(proxy.MemberId, memberId, StringComparison.Ordinal))
                {
                    position = proxy.Position;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        private static bool TryIntersectProxy(Ray ray, Vector3 center, out float nearestDistance)
        {
            nearestDistance = float.MaxValue;
            Vector3 origin = ray.origin - center;
            Vector3 direction = ray.direction;
            float a = direction.x * direction.x + direction.z * direction.z;
            if (a > 0.000001f)
            {
                float b = 2f * (origin.x * direction.x + origin.z * direction.z);
                float c = origin.x * origin.x + origin.z * origin.z - ProxyPickRadius * ProxyPickRadius;
                float discriminant = b * b - 4f * a * c;
                if (discriminant >= 0f)
                {
                    float root = Mathf.Sqrt(discriminant);
                    ConsiderCylinderHit(ray, origin, (-b - root) / (2f * a), ref nearestDistance);
                    ConsiderCylinderHit(ray, origin, (-b + root) / (2f * a), ref nearestDistance);
                }
            }

            ConsiderSphereHit(ray, center + Vector3.up * ProxyPickSegmentHalfHeight, ref nearestDistance);
            ConsiderSphereHit(ray, center - Vector3.up * ProxyPickSegmentHalfHeight, ref nearestDistance);
            return nearestDistance < float.MaxValue;
        }

        private static void ConsiderCylinderHit(Ray ray, Vector3 originOffset, float distance, ref float nearestDistance)
        {
            if (distance < 0f || distance >= nearestDistance)
            {
                return;
            }

            float hitY = originOffset.y + ray.direction.y * distance;
            if (hitY >= -ProxyPickSegmentHalfHeight && hitY <= ProxyPickSegmentHalfHeight)
            {
                nearestDistance = distance;
            }
        }

        private static void ConsiderSphereHit(Ray ray, Vector3 sphereCenter, ref float nearestDistance)
        {
            Vector3 offset = ray.origin - sphereCenter;
            float directionLengthSquared = Vector3.Dot(ray.direction, ray.direction);
            float halfB = Vector3.Dot(offset, ray.direction);
            float c = Vector3.Dot(offset, offset) - ProxyPickRadius * ProxyPickRadius;
            float discriminant = halfB * halfB - directionLengthSquared * c;
            if (discriminant < 0f || directionLengthSquared <= 0.000001f)
            {
                return;
            }

            float root = Mathf.Sqrt(discriminant);
            float distance = (-halfB - root) / directionLengthSquared;
            if (distance < 0f)
            {
                distance = (-halfB + root) / directionLengthSquared;
            }

            if (distance >= 0f && distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        private void EnsurePulseRoot()
        {
            if (pulseRoot != null)
            {
                return;
            }

            pulseRoot = new GameObject("Member Event Pulses").transform;
            pulseRoot.SetParent(transform, false);
        }

        private void TryResolveMaterials()
        {
            if (proxyMaterial != null && pulseMaterial != null)
            {
                return;
            }

            BloomvilleMaterialSet materialSet = presenter.Materials;
            Material structure = materialSet?.StructureBase;
            Material window = materialSet?.AnngloraWindow ?? structure;
            if (structure == null)
            {
                return;
            }

            proxyMaterial = Instantiate(structure);
            proxyMaterial.enableInstancing = true;

            pulseMaterial = Instantiate(window);
        }

        private void RebuildProxyRoster(WorldSnapshot snapshot)
        {
            Physics.SyncTransforms();
            var previousById = new Dictionary<string, MemberProxyState>(StringComparer.Ordinal);
            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState previous = proxies[index];
                if (!string.IsNullOrEmpty(previous.MemberId))
                {
                    previousById[previous.MemberId] = previous;
                }
            }

            proxies.Clear();
            labelDrawOrder.Clear();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var retainedIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<string> ids = BloomvilleMemberProxyPlanning.SelectMemberIdsForPresentation(snapshot.Members, sampleCap);
            for (int index = 0; index < ids.Count; index++)
            {
                if (!seenIds.Add(ids[index]))
                {
                    continue;
                }

                MemberRecord member = FindMember(snapshot, ids[index]);
                if (member == null)
                {
                    continue;
                }

                Vector3 anchor = Vector3.zero;
                if (presenter == null || !presenter.TryGetLabelAnchor(member.LabelId, out anchor))
                {
                    // Other Nations remain in the economic simulation, not
                    // physically packed into the Central Annglora slice.
                    continue;
                }

                uint hash = HashMemberId(member.Id);
                MemberProxyState state;
                if (previousById.TryGetValue(member.Id, out MemberProxyState existing))
                {
                    state = existing;
                    if (!string.Equals(state.LabelId, member.LabelId, StringComparison.Ordinal))
                    {
                        state.LabelId = member.LabelId;
                        state.GoalIndex = (int)(hash % 4u);
                        state.GoalTimer = 0f;
                        state.GoalPosition = ResolveGoalPosition(snapshot, state, hash);
                    }

                    if (!IsSafeGridFallbackCandidate(state.Position, state.Position, state, false)
                        && TryFindSafeGridFallback(state.Position, state.MemberId, state, out Vector3 safePosition, out _))
                    {
                        state.Position = safePosition;
                        state.NoProgressSeconds = 0f;
                    }
                }
                else
                {
                    Vector3 spawn = SnapToWalkable(anchor + DeterministicOffset(hash, 6f, 10f));
                    state = new MemberProxyState
                    {
                        MemberId = member.Id,
                        LabelId = member.LabelId,
                        Position = spawn,
                        GoalIndex = (int)(hash % 4u),
                        GoalTimer = 0f
                    };
                    if (TryFindSafeGridFallback(state.Position, state.MemberId, state, out Vector3 safeSpawn, out _))
                    {
                        state.Position = safeSpawn;
                    }

                    state.GoalPosition = ResolveGoalPosition(snapshot, state, hash);
                }

                EnsureProxyCollider(state);
                proxies.Add(state);
                retainedIds.Add(member.Id);

                if (!labelDrawOrder.Contains(member.LabelId))
                {
                    labelDrawOrder.Add(member.LabelId);
                }
            }

            foreach (KeyValuePair<string, MemberProxyState> pair in previousById)
            {
                if (!retainedIds.Contains(pair.Key))
                {
                    DestroyProxyCollider(pair.Value);
                }
            }

            Physics.SyncTransforms();
            ResolveMemberOverlaps();
            UpdatePresentationCounters();
        }

        private void EnsureProxyCollider(MemberProxyState proxy)
        {
            if (proxy.ColliderRoot != null && proxy.Collider != null)
            {
                proxy.ColliderRoot.transform.position = proxy.Position;
                proxy.Collider.enabled = true;
                return;
            }

            if (proxy.ColliderRoot != null)
            {
                DestroyProxyCollider(proxy);
            }

            var colliderObject = new GameObject("Member Collider " + proxy.MemberId);
            colliderObject.layer = IgnoreRaycastLayer;
            colliderObject.transform.position = proxy.Position;
            CapsuleCollider capsule = colliderObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = ProxyColliderRadius;
            capsule.height = ProxyHeight;
            capsule.center = Vector3.zero;
            capsule.isTrigger = false;
            proxy.ColliderRoot = colliderObject;
            proxy.Collider = capsule;
        }

        private void DestroyProxyCollider(MemberProxyState proxy)
        {
            if (proxy == null || proxy.ColliderRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(proxy.ColliderRoot);
            }
            else
            {
                DestroyImmediate(proxy.ColliderRoot);
            }

            proxy.ColliderRoot = null;
            proxy.Collider = null;
        }

        private static MemberRecord FindMember(WorldSnapshot snapshot, string memberId)
        {
            for (int index = 0; index < snapshot.Members.Count; index++)
            {
                MemberRecord member = snapshot.Members[index];
                if (member.Id == memberId)
                {
                    return member;
                }
            }

            return null;
        }

        private void DetectNewEvents(WorldSnapshot snapshot)
        {
            long maxSequence = lastSeenEventSequence;
            for (int index = 0; index < snapshot.Events.Count; index++)
            {
                WorldEventRecord worldEvent = snapshot.Events[index];
                if (worldEvent.Sequence > maxSequence)
                {
                    maxSequence = worldEvent.Sequence;
                }
            }

            if (!eventCursorInitialized)
            {
                lastSeenEventSequence = maxSequence;
                eventCursorInitialized = true;
                return;
            }

            for (int index = 0; index < snapshot.Events.Count; index++)
            {
                WorldEventRecord worldEvent = snapshot.Events[index];
                if (worldEvent.Sequence > lastSeenEventSequence
                    && BloomvilleMemberProxyPlanning.TryResolvePulseLabelId(worldEvent, snapshot, out string labelId))
                {
                    TriggerLabelPulse(labelId);
                }
            }

            lastSeenEventSequence = maxSequence;
        }

        private void TriggerLabelPulse(string labelId)
        {
            if (!presenter.TryGetLabelAnchor(labelId, out Vector3 anchor))
            {
                return;
            }

            if (!labelPulses.TryGetValue(labelId, out LabelPulseState pulse))
            {
                pulse = CreatePulseVisual(labelId, anchor);
                labelPulses[labelId] = pulse;
            }

            pulse.Remaining = PulseDurationSeconds;
            pulse.PeakIntensity = reducedMotion ? 80f : 220f;
            pulse.Root.position = anchor + new Vector3(0f, 6f, 0f);
            pulse.Root.gameObject.SetActive(true);
        }

        private LabelPulseState CreatePulseVisual(string labelId, Vector3 anchor)
        {
            var root = new GameObject("Pulse " + labelId).transform;
            root.SetParent(pulseRoot, false);
            root.position = anchor + new Vector3(0f, 6f, 0f);
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Beacon";
            sphere.transform.SetParent(root, false);
            sphere.transform.localScale = Vector3.one * 1.4f;
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Material instance = Instantiate(pulseMaterial);
            sphere.GetComponent<Renderer>().sharedMaterial = instance;
            return new LabelPulseState { Root = root, Renderer = sphere.GetComponent<Renderer>(), Material = instance };
        }

        private void TickLabelPulses(float deltaSeconds)
        {
            foreach (KeyValuePair<string, LabelPulseState> pair in labelPulses)
            {
                LabelPulseState pulse = pair.Value;
                if (pulse.Remaining <= 0f)
                {
                    pulse.Root.gameObject.SetActive(false);
                    continue;
                }

                pulse.Remaining -= deltaSeconds;
                float normalized = Mathf.Clamp01(pulse.Remaining / PulseDurationSeconds);
                float wave = reducedMotion ? normalized : normalized * (0.65f + Mathf.Sin((1f - normalized) * Mathf.PI) * 0.35f);
                SetStandardEmission(pulse.Material, new Color(0.35f, 0.82f, 1f), pulse.PeakIntensity * wave);
                float scale = reducedMotion ? 1.2f : 1.2f + (1f - normalized) * 0.8f;
                pulse.Root.localScale = Vector3.one * scale;
            }
        }

        private void TickProxyGoals(WorldSnapshot snapshot)
        {
            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState proxy = proxies[index];
                proxy.GoalTimer += LogicInterval;
                if (proxy.GoalTimer < 8f)
                {
                    continue;
                }

                proxy.GoalTimer = 0f;
                proxy.GoalIndex = (proxy.GoalIndex + 1) % 4;
                uint hash = HashMemberId(proxy.MemberId) + (uint)proxy.GoalIndex;
                proxy.GoalPosition = ResolveGoalPosition(snapshot, proxy, hash);
            }
        }

        private void TickProxyMovement(float deltaSeconds)
        {
            WorldSnapshot snapshot = controller?.Simulation?.World;
            if (snapshot == null)
            {
                return;
            }

            LastBlockedMemberCount = 0;
            ResolveMemberOverlaps();
            Physics.SyncTransforms();
            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState proxy = proxies[index];
                Vector3 current = proxy.Position;
                Vector3 intended = StepTowardRoadGoal(current, proxy.GoalPosition, MoveSpeed * deltaSeconds);
                bool attemptedMove = (intended - current).sqrMagnitude > 0.000001f;
                Vector3 next = current;
                bool moved = attemptedMove && TryFindCollisionAvoidingStep(proxy, intended, MoveSpeed * deltaSeconds, out next);
                if (moved)
                {
                    float distanceBefore = HorizontalDistance(current, proxy.GoalPosition);
                    proxy.Position = next;
                    if (proxy.ColliderRoot != null)
                    {
                        proxy.ColliderRoot.transform.position = next;
                    }

                    float distanceAfter = HorizontalDistance(proxy.Position, proxy.GoalPosition);
                    if (distanceBefore - distanceAfter > 0.025f)
                    {
                        proxy.NoProgressSeconds = 0f;
                    }
                    else
                    {
                        proxy.NoProgressSeconds += deltaSeconds;
                    }
                }
                else if (attemptedMove)
                {
                    LastBlockedMemberCount++;
                    TotalBlockedMovementCount++;
                    proxy.NoProgressSeconds += deltaSeconds;
                }
                else if (!IsStaticCapsulePathClear(current, current)
                    || !IsMemberPositionClear(current, proxy, MinimumMemberSpacing))
                {
                    LastBlockedMemberCount++;
                    TotalBlockedMovementCount++;
                    proxy.NoProgressSeconds += deltaSeconds;
                }

                if ((proxy.Position - proxy.GoalPosition).sqrMagnitude <= ArriveDistance * ArriveDistance)
                {
                    proxy.GoalIndex = (proxy.GoalIndex + 1) % 4;
                    uint hash = HashMemberId(proxy.MemberId) + (uint)proxy.GoalIndex;
                    proxy.GoalPosition = ResolveGoalPosition(snapshot, proxy, hash);
                }

                if (proxy.NoProgressSeconds >= NoProgressRecoverySeconds)
                {
                    RecoverProxy(snapshot, proxy);
                }
            }

            ResolveMemberOverlaps();
            Physics.SyncTransforms();
            UpdatePresentationCounters();
        }

        private bool TryFindCollisionAvoidingStep(MemberProxyState proxy, Vector3 intended, float maxStep, out Vector3 accepted)
        {
            accepted = proxy.Position;
            Vector3 route = intended - proxy.Position;
            route.y = 0f;
            if (route.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            Vector3 forward = route.normalized;
            float distance = Mathf.Min(maxStep, route.magnitude);
            float tieSign = (HashMemberId(proxy.MemberId) & 1u) == 0u ? 1f : -1f;
            for (int index = 0; index < AvoidanceAngles.Length; index++)
            {
                float angle = index == 0 ? 0f : AvoidanceAngles[index] * tieSign;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 candidate = proxy.Position + direction * distance;
                candidate.y = proxy.Position.y;
                if (!IsStaticCapsulePathClear(proxy.Position, candidate)
                    || !IsMemberPathClear(proxy, proxy.Position, candidate))
                {
                    continue;
                }

                accepted = candidate;
                return true;
            }

            return false;
        }

        private bool IsStaticCapsulePathClear(Vector3 from, Vector3 to)
        {
            Vector3 displacement = to - from;
            displacement.y = 0f;
            float distance = displacement.magnitude;
            float radius = ProxyColliderRadius - CollisionQuerySkin;
            float segmentHalfHeight = ProxyHeight * 0.5f - ProxyColliderRadius;
            Vector3 lower = from - Vector3.up * segmentHalfHeight;
            Vector3 upper = from + Vector3.up * segmentHalfHeight;
            Vector3 targetLower = to - Vector3.up * segmentHalfHeight;
            Vector3 targetUpper = to + Vector3.up * segmentHalfHeight;

            if (distance > 0.0001f
                && Physics.CapsuleCast(
                    lower,
                    upper,
                    radius,
                    displacement / distance,
                    out _,
                    distance,
                    StaticCollisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return !Physics.CheckCapsule(
                targetLower,
                targetUpper,
                radius,
                StaticCollisionMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsMemberPathClear(MemberProxyState moving, Vector3 from, Vector3 to)
        {
            Vector3 segment = to - from;
            segment.y = 0f;
            float lengthSquared = segment.sqrMagnitude;
            float minimumSquared = MinimumMemberSpacing * MinimumMemberSpacing;
            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState other = proxies[index];
                if (other == moving)
                {
                    continue;
                }

                Vector3 otherPosition = other.Position;
                otherPosition.y = from.y;
                Vector3 fromDelta = from - otherPosition;
                float startDistanceSquared = fromDelta.sqrMagnitude;
                Vector3 toDelta = to - otherPosition;
                float endDistanceSquared = toDelta.sqrMagnitude;
                float t = lengthSquared <= 0.000001f
                    ? 1f
                    : Mathf.Clamp01(Vector3.Dot(otherPosition - from, segment) / lengthSquared);
                Vector3 closest = from + segment * t;
                closest.y = from.y;
                float closestDistanceSquared = (closest - otherPosition).sqrMagnitude;
                if (t <= 0.001f
                    && startDistanceSquared < minimumSquared
                    && endDistanceSquared > startDistanceSquared + 0.0001f)
                {
                    continue;
                }

                if (closestDistanceSquared < minimumSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private void RecoverProxy(WorldSnapshot snapshot, MemberProxyState proxy)
        {
            RecoveryAttemptCount++;
            proxy.NoProgressSeconds = 0f;
            proxy.GoalIndex = (proxy.GoalIndex + 1) % 4;
            uint hash = HashMemberId(proxy.MemberId) + (uint)proxy.GoalIndex;
            proxy.GoalPosition = ResolveGoalPosition(snapshot, proxy, hash);

            if (!TryFindSafeGridFallback(proxy.Position, proxy.MemberId, proxy, true, out Vector3 fallback, out _))
            {
                RecoveryFailureCount++;
                return;
            }

            proxy.Position = fallback;
            if (proxy.ColliderRoot != null)
            {
                proxy.ColliderRoot.transform.position = fallback;
            }

            SafeFallbackCount++;
        }

        private bool TryFindSafeGridFallback(
            Vector3 current,
            string memberId,
            MemberProxyState movingProxy,
            out Vector3 fallback,
            out int candidatesChecked)
        {
            return TryFindSafeGridFallback(current, memberId, movingProxy, false, out fallback, out candidatesChecked);
        }

        private bool TryFindSafeGridFallback(
            Vector3 current,
            string memberId,
            MemberProxyState movingProxy,
            bool requireRelocation,
            out Vector3 fallback,
            out int candidatesChecked)
        {
            if (!requireRelocation && IsSafeGridFallbackCandidate(current, current, movingProxy, false))
            {
                fallback = current;
                candidatesChecked = 1;
                return true;
            }

            return BloomvilleMemberProxyPlanning.TryFindSafeGridFallback(
                current,
                RoadSpacing,
                BloomvilleMemberProxyPlanning.StreetCenterOffset,
                BloomvilleMemberProxyPlanning.StreetLaneMargin,
                MaximumFallbackRing,
                HashMemberId(memberId),
                candidate => IsSafeGridFallbackCandidate(candidate, current, movingProxy, requireRelocation),
                out fallback,
                out candidatesChecked);
        }

        private bool IsSafeGridFallbackCandidate(Vector3 candidate, Vector3 current, MemberProxyState movingProxy, bool requireRelocation)
        {
            if (Mathf.Abs(candidate.x) > BloomvilleMemberProxyPlanning.WorldBounds
                || Mathf.Abs(candidate.z) > BloomvilleMemberProxyPlanning.WorldBounds)
            {
                return false;
            }

            if (requireRelocation && HorizontalDistance(candidate, current) < MinimumMemberSpacing)
            {
                return false;
            }

            return IsStaticCapsulePathClear(candidate, candidate)
                && IsMemberPositionClear(candidate, movingProxy, MinimumMemberSpacing);
        }

        private bool IsMemberPositionClear(Vector3 position, MemberProxyState moving, float minimumSpacing)
        {
            float minimumSquared = minimumSpacing * minimumSpacing;
            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState other = proxies[index];
                if (other == moving)
                {
                    continue;
                }

                Vector3 delta = position - other.Position;
                delta.y = 0f;
                if (delta.sqrMagnitude < minimumSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private void ResolveMemberOverlaps()
        {
            UnresolvedOverlapCount = 0;
            for (int pass = 0; pass < MaximumSeparationPasses; pass++)
            {
                bool movedAny = false;
                for (int leftIndex = 0; leftIndex < proxies.Count; leftIndex++)
                {
                    MemberProxyState left = proxies[leftIndex];
                    for (int rightIndex = leftIndex + 1; rightIndex < proxies.Count; rightIndex++)
                    {
                        MemberProxyState right = proxies[rightIndex];
                        if (!BloomvilleMemberProxyPlanning.TrySeparatePair(
                                left.Position,
                                right.Position,
                                MinimumMemberSpacing,
                                StablePairDirection(left, right),
                                out Vector3 separatedLeft,
                                out Vector3 separatedRight))
                        {
                            continue;
                        }

                        bool leftClear = IsStaticCapsulePathClear(separatedLeft, separatedLeft);
                        bool rightClear = IsStaticCapsulePathClear(separatedRight, separatedRight);
                        if (leftClear && rightClear)
                        {
                            left.Position = separatedLeft;
                            right.Position = separatedRight;
                            movedAny = true;
                            continue;
                        }

                        Vector3 direction = StablePairDirection(left, right);
                        Vector3 delta = left.Position - right.Position;
                        delta.y = 0f;
                        float correction = Mathf.Max(0f, MinimumMemberSpacing - delta.magnitude) + 0.01f;
                        if (leftClear)
                        {
                            Vector3 oneSided = left.Position + direction * correction;
                            if (IsStaticCapsulePathClear(oneSided, oneSided))
                            {
                                left.Position = oneSided;
                                movedAny = true;
                            }
                        }
                        else if (rightClear)
                        {
                            Vector3 oneSided = right.Position - direction * correction;
                            if (IsStaticCapsulePathClear(oneSided, oneSided))
                            {
                                right.Position = oneSided;
                                movedAny = true;
                            }
                        }
                    }
                }

                if (!movedAny)
                {
                    break;
                }
            }

            for (int leftIndex = 0; leftIndex < proxies.Count; leftIndex++)
            {
                MemberProxyState left = proxies[leftIndex];
                for (int rightIndex = leftIndex + 1; rightIndex < proxies.Count; rightIndex++)
                {
                    Vector3 delta = left.Position - proxies[rightIndex].Position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude + 0.0001f < MinimumMemberSpacing * MinimumMemberSpacing)
                    {
                        UnresolvedOverlapCount++;
                    }
                }
            }

            for (int index = 0; index < proxies.Count; index++)
            {
                if (proxies[index].ColliderRoot != null)
                {
                    proxies[index].ColliderRoot.transform.position = proxies[index].Position;
                }
            }
        }

        private static Vector3 StablePairDirection(MemberProxyState left, MemberProxyState right)
        {
            uint hash = HashMemberId(left.MemberId) ^ (HashMemberId(right.MemberId) * 16777619u);
            float angle = (hash & 0xFFFFu) * (Mathf.PI * 2f / 65536f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (string.Compare(left.MemberId, right.MemberId, StringComparison.Ordinal) > 0)
            {
                direction = -direction;
            }

            return direction;
        }

        private void UpdatePresentationCounters()
        {
            ActiveMemberColliderCount = 0;
            for (int index = 0; index < proxies.Count; index++)
            {
                if (proxies[index].Collider != null && proxies[index].Collider.enabled)
                {
                    ActiveMemberColliderCount++;
                }
            }

            if (proxies.Count < 2)
            {
                MinimumObservedMemberSpacing = 0f;
                return;
            }

            float minimumSquared = float.MaxValue;
            for (int leftIndex = 0; leftIndex < proxies.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < proxies.Count; rightIndex++)
                {
                    Vector3 delta = proxies[leftIndex].Position - proxies[rightIndex].Position;
                    delta.y = 0f;
                    minimumSquared = Mathf.Min(minimumSquared, delta.sqrMagnitude);
                }
            }

            MinimumObservedMemberSpacing = Mathf.Sqrt(minimumSquared);
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private Vector3 ResolveGoalPosition(WorldSnapshot snapshot, MemberProxyState proxy, uint hash)
        {
            if (presenter == null || !presenter.TryGetLabelAnchor(proxy.LabelId, out Vector3 anchor))
            {
                return proxy.Position;
            }

            switch (proxy.GoalIndex)
            {
                case 0:
                    return SnapToWalkable(anchor + new Vector3(0f, 0f, 2f));
                case 1:
                    return SnapToWalkable(ResolveWorkStructureWorld(snapshot, proxy.LabelId));
                case 2:
                    return SnapToWalkable(anchor + DeterministicOffset(hash + 17u, 7f, 12f));
                default:
                    return SnapToWalkable(anchor + DeterministicOffset(hash + 41u, 9f, 14f));
            }
        }

        private Vector3 ResolveWorkStructureWorld(WorldSnapshot snapshot, string labelId)
        {
            if (presenter == null || !presenter.TryGetLabelAnchor(labelId, out Vector3 anchor))
            {
                return Vector3.zero;
            }

            int structureCount = 0;
            for (int index = 0; index < snapshot.Structures.Count; index++)
            {
                StructureRecord structure = snapshot.Structures[index];
                if (structure.LabelId != labelId)
                {
                    continue;
                }

                structureCount++;
            }

            if (structureCount == 0)
            {
                return anchor + new Vector3(4f, 0f, 4f);
            }

            float angle = (Mathf.PI * 2f * 0) / Mathf.Max(1, structureCount);
            float radius = 8f;
            Vector3 local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return anchor + local;
        }

        private static Vector3 StepTowardRoadGoal(Vector3 current, Vector3 goal, float maxStep)
        {
            Vector3 routedGoal = SnapToWalkable(goal);
            Vector3 corner = new Vector3(routedGoal.x, current.y, current.z);
            if ((current - corner).sqrMagnitude > 0.04f)
            {
                return MoveTowards(current, corner, maxStep);
            }

            corner = new Vector3(routedGoal.x, current.y, routedGoal.z);
            return MoveTowards(current, corner, maxStep);
        }

        private static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxStep)
        {
            Vector3 delta = target - current;
            float distance = delta.magnitude;
            if (distance <= maxStep || distance <= 0.0001f)
            {
                return target;
            }

            return current + delta / distance * maxStep;
        }

        private static Vector3 SnapToWalkable(Vector3 position)
        {
            float x = SnapToStreetLane(position.x);
            float z = SnapToStreetLane(position.z);
            return new Vector3(x, ProxyHeight * 0.5f, z);
        }

        private static float SnapToStreetLane(float coordinate)
        {
            float nearest = 0f;
            float nearestDistance = float.MaxValue;
            for (int roadIndex = -2; roadIndex <= 1; roadIndex++)
            {
                float roadCenter = BloomvilleMemberProxyPlanning.StreetCenterOffset + roadIndex * RoadSpacing;
                for (int side = -1; side <= 1; side += 2)
                {
                    float lane = roadCenter + side * BloomvilleMemberProxyPlanning.StreetLaneMargin;
                    if (Mathf.Abs(lane) > BloomvilleMemberProxyPlanning.WorldBounds)
                    {
                        continue;
                    }

                    float distance = Mathf.Abs(coordinate - lane);
                    if (distance < nearestDistance - 0.0001f
                        || (Mathf.Abs(distance - nearestDistance) <= 0.0001f && lane < nearest))
                    {
                        nearest = lane;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        private static Vector3 DeterministicOffset(uint hash, float minRadius, float maxRadius)
        {
            float t = (hash & 0xFFFF) / 65535f;
            float angle = (hash >> 16) * (Mathf.PI * 2f / 65535f);
            float radius = Mathf.Lerp(minRadius, maxRadius, t);
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static uint HashMemberId(string memberId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < memberId.Length; index++)
                {
                    hash ^= memberId[index];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private void DrawInstancedProxies()
        {
            foreach (string labelId in labelDrawOrder)
            {
                int count = 0;
                for (int index = 0; index < proxies.Count; index++)
                {
                    if (proxies[index].LabelId == labelId)
                    {
                        count++;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                if (!labelMatrices.TryGetValue(labelId, out Matrix4x4[] matrices) || matrices.Length < count)
                {
                    matrices = new Matrix4x4[Mathf.NextPowerOfTwo(count)];
                    labelMatrices[labelId] = matrices;
                }

                int matrixIndex = 0;
                Color tint = ResolveLabelTint(labelId);
                for (int index = 0; index < proxies.Count; index++)
                {
                    MemberProxyState proxy = proxies[index];
                    if (proxy.LabelId != labelId)
                    {
                        continue;
                    }

                    matrices[matrixIndex++] = Matrix4x4.TRS(
                        proxy.Position,
                        Quaternion.Euler(0f, (HashMemberId(proxy.MemberId) % 360u), 0f),
                        new Vector3(0.55f, 0.9f, 0.55f));
                }

                if (!labelPropertyBlocks.TryGetValue(labelId, out MaterialPropertyBlock block))
                {
                    block = new MaterialPropertyBlock();
                    labelPropertyBlocks[labelId] = block;
                }

                block.SetColor(StandardColorId, tint);
                Graphics.DrawMeshInstanced(proxyMesh, 0, proxyMaterial, matrices, matrixIndex, block);
            }
        }

        private static void SetStandardEmission(Material material, Color color, float strength)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            float intensity = Mathf.Clamp(strength / 220f, 0f, 4f);
            material.SetColor("_EmissionColor", color * intensity);
        }

        private Color ResolveLabelTint(string labelId)
        {
            WorldSnapshot snapshot = controller?.Simulation?.World;
            if (snapshot == null)
            {
                return new Color(0.82f, 0.84f, 0.88f);
            }

            for (int index = 0; index < snapshot.Labels.Count; index++)
            {
                LabelRecord label = snapshot.Labels[index];
                if (label.Id != labelId)
                {
                    continue;
                }

                if (ColorUtility.TryParseHtmlString("#" + label.ColorHex, out Color parsed))
                {
                    return Color.Lerp(parsed, Color.white, 0.25f);
                }
            }

            return new Color(0.82f, 0.84f, 0.88f);
        }

        private sealed class MemberProxyState
        {
            public string MemberId;
            public string LabelId;
            public Vector3 Position;
            public Vector3 GoalPosition;
            public int GoalIndex;
            public float GoalTimer;
            public float NoProgressSeconds;
            public GameObject ColliderRoot;
            public CapsuleCollider Collider;
        }

        private sealed class LabelPulseState
        {
            public Transform Root;
            public Renderer Renderer;
            public Material Material;
            public float Remaining;
            public float PeakIntensity;
        }
    }
}
