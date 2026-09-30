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
        private const float RoadSpacing = 16f;
        private const float ProxyPickRadius = 0.36f;
        private const float ProxyPickSegmentHalfHeight = 0.54f;
        private static readonly int StandardColorId = Shader.PropertyToID("_Color");

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
            proxies.Clear();
            labelDrawOrder.Clear();
            IReadOnlyList<string> ids = BloomvilleMemberProxyPlanning.SelectMemberIdsForPresentation(snapshot.Members, sampleCap);
            for (int index = 0; index < ids.Count; index++)
            {
                MemberRecord member = FindMember(snapshot, ids[index]);
                if (member == null)
                {
                    continue;
                }

                if (!presenter.TryGetLabelAnchor(member.LabelId, out Vector3 anchor))
                {
                    continue;
                }

                uint hash = HashMemberId(member.Id);
                Vector3 spawn = anchor + DeterministicOffset(hash, 6f, 10f);
                spawn = SnapToWalkable(spawn);
                var state = new MemberProxyState
                {
                    MemberId = member.Id,
                    LabelId = member.LabelId,
                    Position = spawn,
                    GoalIndex = (int)(hash % 4u),
                    GoalTimer = 0f
                };
                state.GoalPosition = ResolveGoalPosition(snapshot, state, hash);
                proxies.Add(state);

                if (!labelDrawOrder.Contains(member.LabelId))
                {
                    labelDrawOrder.Add(member.LabelId);
                }
            }
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

            for (int index = 0; index < proxies.Count; index++)
            {
                MemberProxyState proxy = proxies[index];
                Vector3 next = StepTowardRoadGoal(proxy.Position, proxy.GoalPosition, MoveSpeed * deltaSeconds);
                proxy.Position = next;
                if ((proxy.Position - proxy.GoalPosition).sqrMagnitude <= ArriveDistance * ArriveDistance)
                {
                    proxy.GoalIndex = (proxy.GoalIndex + 1) % 4;
                    uint hash = HashMemberId(proxy.MemberId) + (uint)proxy.GoalIndex;
                    proxy.GoalPosition = ResolveGoalPosition(snapshot, proxy, hash);
                }
            }
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
            float x = Mathf.Round(position.x / RoadSpacing) * RoadSpacing;
            float z = Mathf.Round(position.z / RoadSpacing) * RoadSpacing;
            x = Mathf.Clamp(x, -RoadSpacing * 4f, RoadSpacing * 4f);
            z = Mathf.Clamp(z, -RoadSpacing * 4f, RoadSpacing * 4f);
            return new Vector3(x, ProxyHeight * 0.5f, z);
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
