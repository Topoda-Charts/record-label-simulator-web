using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

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
            }
            string json = JsonUtility.ToJson(receipt);
            Debug.Log("RLS_BROWSER_READBACK " + json);
#if UNITY_WEBGL && !UNITY_EDITOR
            RlsObserverReceipt(json);
#endif
        }
    }
}
