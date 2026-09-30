using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Topoda.RLS.Observer
{
    // Browser QA uses the same observer and simulation APIs as the UI.
    public sealed class CloudObserverReadback : MonoBehaviour
    {
        [Serializable] private sealed class Receipt
        {
            public string state;
            public string date;
            public string digest;
            public int members;
            public int labels;
            public int tracks;
            public int events;
            public long cash;
            public bool snapshotRoundTrip;
            public int visibleRenderers;
            public bool paused;
            public string selectedMember;
            public string followingMember;
            public bool followingOccluded;
            public float cameraDistance;
            public float zoomTarget;
            public Vector3 cameraPosition;
            public ControlPoint[] controls;
            public MemberPoint[] memberPoints;
            public string[] hudText;
        }
        [Serializable] private sealed class ControlPoint
        {
            public string text;
            public float x, y, width, height;
        }
        [Serializable] private sealed class MemberPoint
        {
            public string id;
            public float x, y;
        }
        private bool snapshotRoundTrip;
        private ObserverApplicationController Host => GetComponent<ObserverApplicationController>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RlsObserverReceipt(string json);
#endif
        public void Watch(string unused) { Host.BeginObserverSession(ObserverMode.WatchPreSimulation); }
        public void Handoff(string unused) { Host.BeginObserverSession(ObserverMode.OpenHandoff); }
        public void September(string unused)
        {
            Host.Simulation.AdvanceTo(new DateTime(2425, 9, 30, 12, 0, 0, DateTimeKind.Utc));
            Summary("");
        }
        public void RoundTrip(string unused)
        {
            var simulation = Host.Simulation;
            var store = new SnapshotStore(ObserverSnapshotPaths.Current, SimulationTuning.ObserverPreview().RecoverySaveCount);
            string digest = simulation.World.Digest;
            var saved = store.SaveNamed(simulation.World, "Browser verification");
            var loaded = store.Load(saved.FileName, simulation.World.SchemaVersion);
            snapshotRoundTrip = loaded.World.Digest == digest;
            Summary("");
        }
        public void LoadLatest(string unused)
        {
            var store = new SnapshotStore(ObserverSnapshotPaths.Current, SimulationTuning.ObserverPreview().RecoverySaveCount);
            var saved = store.List().First(s => s.DisplayName == "Browser verification");
            Host.Simulation.ReplaceWorld(store.Load(saved.FileName, Host.Simulation.World.SchemaVersion).World);
            Summary("");
        }
        public void Summary(string unused)
        {
            var receipt = new Receipt { state = Host.CurrentState.ToString(), snapshotRoundTrip = snapshotRoundTrip, visibleRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length };
            var world = Host.Simulation?.World;
            if (world != null) {
                receipt.date = world.Clock.CurrentUtc.ToString("O"); receipt.digest = world.Digest;
                receipt.members = world.Members.Count; receipt.labels = world.Labels.Count;
                receipt.tracks = world.Works.Count(w=>w.Stage==WorkStage.Track);
                receipt.events = world.Events.Count; receipt.cash = world.Labels.Sum(l=>(long)l.Cash);
                receipt.paused = world.Clock.Paused;
            }
            receipt.selectedMember = Host.SelectedMemberId;
            receipt.followingMember = Host.FollowingMemberId;
            var rig = FindFirstObjectByType<ObserverCameraRig>();
            if (rig != null && rig.ObserverCamera != null)
            {
                receipt.cameraDistance = rig.ViewDistance;
                receipt.zoomTarget = rig.ZoomTarget;
                receipt.cameraPosition = rig.ObserverCamera.transform.position;
                var proxies = FindFirstObjectByType<BloomvilleMemberProxies>();
                var points = new List<MemberPoint>();
                if (proxies != null && world != null)
                {
                    foreach (var member in world.Members)
                    {
                        if (!proxies.TryGetMemberPosition(member.Id, out Vector3 position)) continue;
                        Vector3 point = rig.ObserverCamera.WorldToScreenPoint(position + Vector3.up * 0.82f);
                        if (point.z > 0f) points.Add(new MemberPoint { id = member.Id, x = point.x, y = Screen.height - point.y });
                    }
                    if (!string.IsNullOrEmpty(receipt.followingMember) && proxies.TryGetMemberPosition(receipt.followingMember, out Vector3 followed))
                        receipt.followingOccluded = Physics.Linecast(rig.ObserverCamera.transform.position, followed + Vector3.up * 0.82f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                }
                receipt.memberPoints = points.ToArray();
            }
            var document = Host.GetComponent<UIDocument>();
            if (document != null && document.rootVisualElement.worldBound.width > 0f)
            {
                var root = document.rootVisualElement;
                float scale = Screen.width / root.worldBound.width;
                receipt.controls = root.Query<Button>().ToList()
                    .Where(button => button.enabledInHierarchy && !string.IsNullOrEmpty(button.text) && button.worldBound.width > 0f && button.worldBound.height > 0f)
                    .Select(button => new ControlPoint { text = button.text, x = button.worldBound.center.x * scale, y = button.worldBound.center.y * scale, width = button.worldBound.width * scale, height = button.worldBound.height * scale }).ToArray();
                receipt.hudText = root.Query<Label>().ToList().Select(label => label.text).Where(text => !string.IsNullOrEmpty(text)).Take(120).ToArray();
            }
            string json = JsonUtility.ToJson(receipt);
            Debug.Log("RLS_BROWSER_READBACK " + json);
#if UNITY_WEBGL && !UNITY_EDITOR
            RlsObserverReceipt(json);
#endif
        }
    }
}
