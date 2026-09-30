using System;
using System.Collections.Generic;
using UnityEngine;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController : MonoBehaviour, IObserverAutoCaptureHost
    {
        private const float SplashDuration = 1.65f;

        // EditMode ObserverSimulationTests scans this file for main-menu copy.
        private const string MenuCopyWatchWorldGrow = "WATCH THE WORLD GROW";
        private const string MenuCopyOpenHandoff = "OPEN AUG 31 HANDOFF";
        private const string MenuCopyLoadSnapshot = "Load Snapshot";
        private const string MenuCopySettings = "Settings";
        private const string MenuCopyCredits = "Credits";
        private const string MenuCopyExit = "Exit";

        [SerializeField] private Texture2D logo;
        [SerializeField] private ObserverReviewStage reviewStage = ObserverReviewStage.Complete;
        [SerializeField] private BloomvilleWorldPresenter worldPresenter;
        [SerializeField] private ObserverCameraRig cameraRig;

        private ApplicationState state = ApplicationState.Splash;
        private ObserverMode mode = ObserverMode.WatchPreSimulation;
        private DeterministicSimulation simulation;
        private SnapshotStore snapshots;
        private OptionalCloudAdapter cloud = new DisabledCloudAdapter();
        private float splashStarted;
        private float simulationAccumulator;
        private float loadingProgress;
        private double generationStartedRealtime;
        private double coldGenerationElapsedSeconds;
        private string loadingMessage = "Preparing Central Bloomville…";
        private string selectedLabelId = "ARL1";
        private string eventFilter = "All";
        private string snapshotName = "My observation";
        private string statusMessage = "Local saves ready";
        private string plainLanguageError = string.Empty;
        private bool showSnapshotPicker;
        private bool reducedMotion;
        private bool highContrast;
        private float textScale = 1f;
        private float presenterRefreshAccumulator;
        private long lastPresentedStepIndex = -1;
        private Rect observerWorldGuiRect;
        private Rect observerInspectorGuiRect;
        private Rect observerEventsGuiRect;
        private Vector2 eventScroll;
        private Vector2 snapshotScroll;
        private List<SnapshotMetadata> snapshotList = new List<SnapshotMetadata>();

        public ApplicationState CurrentState { get { return state; } }
        public ObserverMode CurrentMode { get { return mode; } }
        public ObserverReviewStage ReviewStage { get { return reviewStage; } }
        public DeterministicSimulation Simulation { get { return simulation; } }
        public bool HasPlayerManagementRoutes { get { return false; } }
        public double ColdGenerationElapsedSeconds { get { return coldGenerationElapsedSeconds; } }
        public bool ReducedMotionEnabled { get { return reducedMotion; } }

        public void Configure(Texture2D brandLogo, ObserverReviewStage stage)
        {
            logo = brandLogo;
            reviewStage = stage;
        }

        public void WireObserverScene(BloomvilleWorldPresenter presenter, ObserverCameraRig rig)
        {
            worldPresenter = presenter;
            cameraRig = rig;
            if (cameraRig != null)
            {
                cameraRig.SetReducedMotion(reducedMotion);
            }
        }

        public void BeginObserverSession(ObserverMode mode)
        {
            BeginObserver(mode);
        }

        private void Awake()
        {
            splashStarted = Time.unscaledTime;
            snapshots = new SnapshotStore(ObserverSnapshotPaths.Current, SimulationTuning.ObserverPreview().RecoverySaveCount);
            Application.targetFrameRate = 60;
            InitializeToolkit();
        }

        private void OnApplicationQuit()
        {
            SafeAutosave("clean exit");
        }

        private void Update()
        {
            UpdateToolkit();
            if (state == ApplicationState.Splash && Time.unscaledTime - splashStarted >= SplashDuration)
            {
                state = ApplicationState.MainMenu;
            }

            if (state != ApplicationState.Observer || simulation == null)
            {
                StopMemberFollowing();
                return;
            }

            HandleObserverKeyboard();
            HandleObserverCameraInput();
            HandleObserverWorldSelection();
            UpdateMemberFollowing();
            RefreshWorldPresenter(false);
            if (skipInProgress || showSnapshotPicker || simulation.World.Clock.Paused || reviewStage == ObserverReviewStage.MenuAndShell)
            {
                return;
            }

            simulationAccumulator += Time.unscaledDeltaTime * simulation.World.Clock.Speed;
            int steps = Mathf.Min(240, Mathf.FloorToInt(simulationAccumulator));
            if (steps > 0)
            {
                simulationAccumulator -= steps;
                simulation.AdvanceSteps(steps);
                RefreshWorldPresenter(true);
                if (simulation.World.Clock.CurrentUtc.Hour == 0 && simulation.World.Clock.CurrentUtc.Minute < simulation.World.Clock.FixedStepMinutes)
                {
                    SafeAutosave("completed day");
                }
            }
        }
    }
}
