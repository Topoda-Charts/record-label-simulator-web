using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace Topoda.RLS.Observer
{
    /// <summary>
    /// TH-04 standalone capture harness. Active only when launched with
    /// <c>-rlsAutoCapture &lt;absoluteDir&gt;</c>; otherwise this type is never loaded at runtime.
    /// </summary>
    public static class ObserverAutoCaptureBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void TryStart()
        {
            string captureDirectory = TryReadCaptureDirectory();
            if (string.IsNullOrEmpty(captureDirectory))
            {
                return;
            }

            var hostObject = new GameObject("RLS Observer Auto Capture");
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            hostObject.AddComponent<ObserverAutoCaptureRunner>().Begin(captureDirectory);
        }

        internal static string TryReadCaptureDirectory()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], "-rlsAutoCapture", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = args[index + 1].Trim().Trim('"');
                    if (Path.IsPathRooted(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }

                    Debug.LogError("RLS auto capture requires an absolute directory path.");
                    return null;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Elena: implement on <see cref="ObserverApplicationController"/> so the harness can enter observer modes.
    /// </summary>
    public interface IObserverAutoCaptureHost
    {
        void BeginObserverSession(ObserverMode mode);
    }

    [Serializable]
    internal sealed class ObserverCaptureStepFps
    {
        public string stepId;
        public string fileName;
        public float fpsAtCapture;
        public float fpsAvgDuringSettle;
        public float fpsMinDuringSettle;
    }

    [Serializable]
    internal sealed class ObserverCaptureReport
    {
        public string startedUtc;
        public string finishedUtc;
        public string capturePhase;
        public string unityVersion;
        public string applicationVersion;
        public string observerTuningVersion;
        public string graphicsDeviceName;
        public string graphicsDeviceType;
        public int graphicsMemorySizeMb;
        public int systemMemorySizeMb;
        public int screenWidthRequested;
        public int screenHeightRequested;
        public int screenWidth;
        public int screenHeight;
        public string menuCaptureApplicationState;
        public float fpsAverage;
        public float fpsMin;
        public float fpsMinGlobal;
        public int fpsMinGlobalFrameIndex;
        public float fpsMinGlobalUnscaledTime;
        public ObserverCaptureStepFps[] captureStepFps;
        public string profilerAllocatedBytes;
        public string gcHeapBytes;
        public string digestBeforeSave;
        public string digestAfterAdvance;
        public string digestAfterLoad;
        public bool saveLoadDigestMatch;
        public bool hostImplemented;
        public string statusMessage;
        public string[] artifacts;
    }

    internal sealed class ObserverAutoCaptureRunner : MonoBehaviour
    {
        private const string DefaultFocusLabelId = "ARL1";
        private const float ObserverFpsWindowSeconds = 10f;
        private const float AdvanceRunSeconds = 4f;
        private const int AdvanceSpeed = 16;
        private const int DefaultSettleFrames = 6;
        private const int CloseupSettleFrames = 10;
        private const float MemberMotionSeconds = 2.5f;
        private const int TargetScreenWidth = 1920;
        private const int TargetScreenHeight = 1080;
        private const int MainMenuStableFrames = 12;
        private const int MenuSettleFrames = 2;

        private string captureDirectory;
        private ObserverApplicationController controller;
        private ObserverCaptureReport report;
        private readonly List<ObserverCaptureStepFps> stepFpsRecords = new List<ObserverCaptureStepFps>();

        private float fpsSum;
        private int fpsSamples;
        private float fpsMin = float.PositiveInfinity;
        private float fpsMinGlobal = float.PositiveInfinity;
        private int fpsMinGlobalFrameIndex;
        private float fpsMinGlobalUnscaledTime;
        private float lastInstantFps;
        private bool fpsSampling;
        private bool trackGlobalMin;

        private float stepSettleFpsSum;
        private int stepSettleFpsSamples;
        private float stepSettleFpsMin = float.PositiveInfinity;
        private bool stepFpsTracking;

        public void Begin(string directory)
        {
            captureDirectory = directory;
            Directory.CreateDirectory(captureDirectory);
            TryApplyCaptureResolution();
            report = new ObserverCaptureReport
            {
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                capturePhase = "PRE",
                unityVersion = Application.unityVersion,
                applicationVersion = Application.version,
                observerTuningVersion = SimulationTuning.ObserverPreview().Version,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsMemorySizeMb = SystemInfo.graphicsMemorySize,
                systemMemorySizeMb = SystemInfo.systemMemorySize,
                screenWidthRequested = TargetScreenWidth,
                screenHeightRequested = TargetScreenHeight,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                hostImplemented = false,
                statusMessage = "pending",
                artifacts = Array.Empty<string>(),
                captureStepFps = Array.Empty<ObserverCaptureStepFps>()
            };
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            // 📚 PRE — shell/menu captures before observer session (UNITY_DOCS_INDEX: absolute paths, screen res).
            yield return WaitForResolutionSettle();
            controller = FindFirstObjectByType<ObserverApplicationController>();
            if (controller == null)
            {
                report.statusMessage = "ObserverApplicationController was not found in the loaded scene.";
                yield return Finish(false);
                yield break;
            }

            RefreshScreenDimensionsInReport();

            bool sawSplash = false;
            yield return WaitUntilState(ApplicationState.Splash, 600, result => sawSplash = result);
            if (sawSplash)
            {
                yield return SettleAndTrackFps(DefaultSettleFrames);
                yield return CaptureStep("PRE-01", "01-splash.png", ApplicationState.Splash);
            }

            bool reachedMenu = false;
            yield return WaitUntilState(ApplicationState.MainMenu, 900, result => reachedMenu = result);
            if (!reachedMenu || controller.CurrentState != ApplicationState.MainMenu)
            {
                report.statusMessage = "PRE-02 aborted: main menu was not reached (state=" + controller.CurrentState + ").";
                yield return Finish(false);
                yield break;
            }

            yield return WaitForStableState(ApplicationState.MainMenu, MainMenuStableFrames, 300);
            if (controller.CurrentState != ApplicationState.MainMenu)
            {
                report.statusMessage = "PRE-02 aborted: left main menu before capture (state=" + controller.CurrentState + ").";
                yield return Finish(false);
                yield break;
            }

            yield return SettleAndTrackFps(MenuSettleFrames);
            report.menuCaptureApplicationState = controller.CurrentState.ToString();
            bool menuCaptured = false;
            yield return CaptureStep("PRE-02", "02-menu.png", ApplicationState.MainMenu, result => menuCaptured = result);
            if (!menuCaptured || controller.CurrentState != ApplicationState.MainMenu)
            {
                report.statusMessage = "PRE-02 failed: capture requires MainMenu (state=" + controller.CurrentState + ").";
                yield return Finish(false);
                yield break;
            }

            IObserverAutoCaptureHost host = controller as IObserverAutoCaptureHost;
            report.hostImplemented = host != null;
            if (host == null)
            {
                report.statusMessage =
                    "IObserverAutoCaptureHost is not implemented on ObserverApplicationController; observer steps were skipped.";
                yield return Finish(false);
                yield break;
            }

            // 🧠 EXECUTE — Aug 31 handoff observer captures, FPS per step, digest round trip inputs.
            report.capturePhase = "EXECUTE";
            BeginGlobalFpsTracking();

            if (controller.CurrentState != ApplicationState.MainMenu)
            {
                report.statusMessage = "Observer start blocked: expected MainMenu before handoff (state=" + controller.CurrentState + ").";
                yield return Finish(false);
                yield break;
            }

            host.BeginObserverSession(ObserverMode.OpenHandoff);
            yield return WaitForStableState(ApplicationState.Loading, 2, 60);
            yield return WaitForObserverReady(3600);
            if (controller.CurrentState != ApplicationState.Observer || controller.Simulation == null)
            {
                report.statusMessage = "Observer session did not reach a ready Observer state.";
                yield return Finish(false);
                yield break;
            }

            yield return SettleAndTrackFps(8);
            yield return CaptureStep("EXEC-03", "03-observer-overview.png");

            DeterministicSimulation simulation = controller.Simulation;
            if (TryAdvanceToNoon(simulation))
            {
                RefreshPresenter();
                ResetCameraOverview();
                yield return SettleAndTrackFps(8);
                yield return CaptureStep("EXEC-03b", "03b-overview-noon.png");
            }

            fpsSampling = true;
            float sampleStarted = Time.unscaledTime;
            while (Time.unscaledTime - sampleStarted < ObserverFpsWindowSeconds)
            {
                yield return null;
            }

            fpsSampling = false;
            report.fpsAverage = fpsSamples > 0 ? fpsSum / fpsSamples : 0f;
            report.fpsMin = float.IsPositiveInfinity(fpsMin) ? 0f : fpsMin;

            FocusDistrictCloseupForMembers(DefaultFocusLabelId);
            yield return RunMemberMotionSample(MemberMotionSeconds);
            yield return SettleAndTrackFps(CloseupSettleFrames);
            yield return CaptureStep("EXEC-07", "07-member-closeup.png");

            FocusLabelForCapture(DefaultFocusLabelId);
            yield return SettleAndTrackFps(CloseupSettleFrames);
            yield return CaptureStep("EXEC-04", "04-label-focus.png");

            simulation.World.Clock.Paused = false;
            simulation.World.Clock.Speed = AdvanceSpeed;
            float advanceStarted = Time.unscaledTime;
            while (Time.unscaledTime - advanceStarted < AdvanceRunSeconds)
            {
                yield return null;
            }

            simulation.World.Clock.Paused = true;
            simulation.RefreshDigest();
            RefreshPresenter();
            yield return SettleAndTrackFps(CloseupSettleFrames);
            yield return CaptureStep("EXEC-05", "05-after-advance.png");

            if (TryAdvanceToNight(simulation))
            {
                RefreshPresenter();
                yield return SettleAndTrackFps(CloseupSettleFrames);
                yield return CaptureStep("EXEC-06", "06-night.png");
            }

            // ❓ POST — snapshot integrity verification (SnapshotStore / DeterminismDigest).
            report.capturePhase = "POST";
            yield return RunSaveLoadRoundTrip(simulation);
            report.profilerAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong().ToString(CultureInfo.InvariantCulture);
            report.gcHeapBytes = GC.GetTotalMemory(false).ToString(CultureInfo.InvariantCulture);
            report.statusMessage = "Capture sequence completed.";
            yield return Finish(true);
        }

        private IEnumerator RunSaveLoadRoundTrip(DeterministicSimulation simulation)
        {
            int schemaVersion = ScenarioDefinition.GenericObserverSeed().SchemaVersion;
            var store = new SnapshotStore(ObserverSnapshotPaths.Current, SimulationTuning.ObserverPreview().RecoverySaveCount);
            simulation.World.Clock.Paused = true;
            report.digestBeforeSave = simulation.RefreshDigest();

            SnapshotMetadata saved;
            try
            {
                saved = store.SaveNamed(simulation.World, "TH-04 AutoCapture RoundTrip");
            }
            catch (Exception exception)
            {
                report.statusMessage = "Save/load round trip failed during save: " + exception.Message;
                report.saveLoadDigestMatch = false;
                yield break;
            }

            simulation.AdvanceSteps(96);
            report.digestAfterAdvance = simulation.RefreshDigest();

            try
            {
                SnapshotEnvelope envelope = store.Load(saved.FileName, schemaVersion);
                simulation.ReplaceWorld(envelope.World);
                simulation.World.Clock.Paused = true;
                report.digestAfterLoad = simulation.RefreshDigest();
                report.saveLoadDigestMatch = string.Equals(report.digestBeforeSave, report.digestAfterLoad, StringComparison.OrdinalIgnoreCase);
                RefreshPresenter();
            }
            catch (Exception exception)
            {
                report.statusMessage = "Save/load round trip failed during load: " + exception.Message;
                report.saveLoadDigestMatch = false;
            }

            yield return null;
        }

        private void BeginGlobalFpsTracking()
        {
            fpsSum = 0f;
            fpsSamples = 0;
            fpsMin = float.PositiveInfinity;
            fpsMinGlobal = float.PositiveInfinity;
            fpsMinGlobalFrameIndex = 0;
            fpsMinGlobalUnscaledTime = 0f;
            trackGlobalMin = true;
            fpsSampling = true;
        }

        private IEnumerator RunMemberMotionSample(float seconds)
        {
            DeterministicSimulation simulation = controller == null ? null : controller.Simulation;
            if (simulation == null)
            {
                yield break;
            }

            int priorSpeed = simulation.World.Clock.Speed;
            simulation.World.Clock.Paused = false;
            simulation.World.Clock.Speed = 1;
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < seconds)
            {
                yield return null;
            }

            simulation.World.Clock.Paused = true;
            simulation.World.Clock.Speed = priorSpeed;
        }

        private void ResetCameraOverview()
        {
            ObserverCameraRig rig = FindFirstObjectByType<ObserverCameraRig>();
            if (rig == null)
            {
                return;
            }

            rig.SetReducedMotion(true);
            rig.ResetOverview(true);
        }

        private void FocusLabelForCapture(string labelId)
        {
            BloomvilleWorldPresenter presenter = FindFirstObjectByType<BloomvilleWorldPresenter>();
            ObserverCameraRig rig = FindFirstObjectByType<ObserverCameraRig>();
            DeterministicSimulation simulation = controller == null ? null : controller.Simulation;
            if (presenter == null || rig == null || simulation == null)
            {
                return;
            }

            presenter.SetSelectedLabel(labelId);
            presenter.RefreshFromSnapshot(simulation.World);
            rig.SetReducedMotion(true);
            rig.Focus(presenter.GetFocusPoint(labelId), true);
        }

        private void FocusDistrictCloseupForMembers(string labelId)
        {
            BloomvilleWorldPresenter presenter = FindFirstObjectByType<BloomvilleWorldPresenter>();
            ObserverCameraRig rig = FindFirstObjectByType<ObserverCameraRig>();
            DeterministicSimulation simulation = controller == null ? null : controller.Simulation;
            if (presenter == null || rig == null || simulation == null)
            {
                return;
            }

            presenter.SetSelectedLabel(labelId);
            presenter.RefreshFromSnapshot(simulation.World);
            rig.SetReducedMotion(true);
            Vector3 focus = presenter.GetFocusPoint(labelId);
            focus += new Vector3(8f, -Mathf.Min(4f, focus.y - 1.5f), -6f);
            focus.y = Mathf.Max(1.5f, focus.y);
            rig.Focus(focus, true);
            for (int zoom = 0; zoom < 14; zoom++)
            {
                rig.HandleZoom(2.5f);
            }
        }

        private void RefreshPresenter()
        {
            BloomvilleWorldPresenter presenter = FindFirstObjectByType<BloomvilleWorldPresenter>();
            DeterministicSimulation simulation = controller == null ? null : controller.Simulation;
            if (presenter == null || simulation == null)
            {
                return;
            }

            presenter.RefreshFromSnapshot(simulation.World);
        }

        private static bool TryAdvanceToNoon(DeterministicSimulation simulation)
        {
            if (simulation == null)
            {
                return false;
            }

            try
            {
                DateTime current = simulation.World.Clock.CurrentUtc;
                DateTime noon = AlignToStep(new DateTime(current.Year, current.Month, current.Day, 12, 0, 0, DateTimeKind.Utc));
                if (noon <= current)
                {
                    return false;
                }

                simulation.AdvanceTo(noon);
                simulation.World.Clock.Paused = true;
                simulation.RefreshDigest();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RLS auto capture noon step skipped: " + exception.Message);
                return false;
            }
        }

        private static bool TryAdvanceToNight(DeterministicSimulation simulation)
        {
            if (simulation == null)
            {
                return false;
            }

            try
            {
                DateTime current = simulation.World.Clock.CurrentUtc;
                if (IsNight(current))
                {
                    return true;
                }

                DateTime night = AlignToStep(new DateTime(current.Year, current.Month, current.Day, 22, 0, 0, DateTimeKind.Utc));
                if (night <= current)
                {
                    night = night.AddDays(1);
                }

                simulation.AdvanceTo(night);
                simulation.World.Clock.Paused = true;
                simulation.RefreshDigest();
                return IsNight(simulation.World.Clock.CurrentUtc);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RLS auto capture night step skipped: " + exception.Message);
                return false;
            }
        }

        private static bool IsNight(DateTime timeUtc)
        {
            float dayFraction = (timeUtc.Hour + timeUtc.Minute / 60f) / 24f;
            return dayFraction < 0.22f || dayFraction > 0.78f;
        }

        private static DateTime AlignToStep(DateTime targetUtc)
        {
            int stepMinutes = SimulationTuning.ObserverPreview().FixedStepMinutes;
            int minute = targetUtc.Minute - (targetUtc.Minute % stepMinutes);
            return new DateTime(targetUtc.Year, targetUtc.Month, targetUtc.Day, targetUtc.Hour, minute, 0, DateTimeKind.Utc);
        }

        private static void TryApplyCaptureResolution()
        {
            Screen.SetResolution(TargetScreenWidth, TargetScreenHeight, FullScreenMode.Windowed);
        }

        private void RefreshScreenDimensionsInReport()
        {
            report.screenWidth = Screen.width;
            report.screenHeight = Screen.height;
        }

        private static IEnumerator WaitForResolutionSettle()
        {
            TryApplyCaptureResolution();
            for (int index = 0; index < 4; index++)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        private IEnumerator WaitUntilState(ApplicationState desired, int maxFrames, Action<bool> onComplete)
        {
            int frames = 0;
            while (controller != null && controller.CurrentState != desired && frames < maxFrames)
            {
                frames++;
                yield return null;
            }

            bool reached = controller != null && controller.CurrentState == desired;
            onComplete?.Invoke(reached);
        }

        private IEnumerator WaitForStableState(ApplicationState desired, int consecutiveFrames, int maxFrames)
        {
            int stable = 0;
            int frames = 0;
            while (controller != null && frames < maxFrames)
            {
                frames++;
                if (controller.CurrentState == desired)
                {
                    stable++;
                    if (stable >= consecutiveFrames)
                    {
                        yield break;
                    }
                }
                else
                {
                    stable = 0;
                }

                yield return null;
            }
        }

        private IEnumerator WaitForObserverReady(int maxFrames)
        {
            int frames = 0;
            while (frames < maxFrames)
            {
                if (controller != null &&
                    controller.CurrentState == ApplicationState.Observer &&
                    controller.Simulation != null &&
                    controller.Simulation.World != null)
                {
                    yield break;
                }

                frames++;
                yield return null;
            }
        }

        private IEnumerator SettleAndTrackFps(int count)
        {
            BeginStepFpsTracking();
            for (int index = 0; index < count; index++)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        private void BeginStepFpsTracking()
        {
            stepFpsTracking = true;
            stepSettleFpsSum = 0f;
            stepSettleFpsSamples = 0;
            stepSettleFpsMin = float.PositiveInfinity;
        }

        private void EndStepFpsTracking(string stepId, string fileName)
        {
            stepFpsTracking = false;
            var record = new ObserverCaptureStepFps
            {
                stepId = stepId,
                fileName = fileName,
                fpsAtCapture = lastInstantFps,
                fpsAvgDuringSettle = stepSettleFpsSamples > 0 ? stepSettleFpsSum / stepSettleFpsSamples : 0f,
                fpsMinDuringSettle = float.IsPositiveInfinity(stepSettleFpsMin) ? 0f : stepSettleFpsMin
            };
            stepFpsRecords.Add(record);
        }

        private IEnumerator CaptureStep(string stepId, string fileName, ApplicationState? requiredState = null, Action<bool> onComplete = null)
        {
            if (requiredState.HasValue)
            {
                int guardFrames = 0;
                while (controller != null &&
                       controller.CurrentState != requiredState.Value &&
                       guardFrames < 180)
                {
                    guardFrames++;
                    yield return null;
                }

                if (controller == null || controller.CurrentState != requiredState.Value)
                {
                    onComplete?.Invoke(false);
                    yield break;
                }
            }

            string path = Path.Combine(captureDirectory, fileName);
            if (requiredState.HasValue && controller.CurrentState != requiredState.Value)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForEndOfFrame();
            yield return null;

            bool stateOk = !requiredState.HasValue || (controller != null && controller.CurrentState == requiredState.Value);
            if (stateOk)
            {
                EndStepFpsTracking(stepId, fileName);
                Array.Resize(ref report.artifacts, report.artifacts.Length + 1);
                report.artifacts[report.artifacts.Length - 1] = path;
            }

            onComplete?.Invoke(stateOk);
        }

        private IEnumerator Finish(bool success)
        {
            report.finishedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            RefreshScreenDimensionsInReport();
            report.captureStepFps = stepFpsRecords.ToArray();
            report.fpsMinGlobal = float.IsPositiveInfinity(fpsMinGlobal) ? 0f : fpsMinGlobal;
            report.fpsMinGlobalFrameIndex = fpsMinGlobalFrameIndex;
            report.fpsMinGlobalUnscaledTime = fpsMinGlobalUnscaledTime;
            report.capturePhase = success ? "POST" : report.capturePhase;
            string reportPath = Path.Combine(captureDirectory, "capture-report.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Array.Resize(ref report.artifacts, report.artifacts.Length + 1);
            report.artifacts[report.artifacts.Length - 1] = reportPath;
            Debug.Log("RLS auto capture finished success=" + success + " report=" + reportPath);
            yield return null;
            Application.Quit();
        }

        private void RecordFpsSample(float fps)
        {
            lastInstantFps = fps;
            if (fpsSampling)
            {
                fpsSum += fps;
                fpsSamples++;
                if (fps < fpsMin)
                {
                    fpsMin = fps;
                }
            }

            if (trackGlobalMin && fps < fpsMinGlobal)
            {
                fpsMinGlobal = fps;
                fpsMinGlobalFrameIndex = Time.frameCount;
                fpsMinGlobalUnscaledTime = Time.unscaledTime;
            }

            if (stepFpsTracking)
            {
                stepSettleFpsSum += fps;
                stepSettleFpsSamples++;
                if (fps < stepSettleFpsMin)
                {
                    stepSettleFpsMin = fps;
                }
            }
        }

        private void Update()
        {
            if (!stepFpsTracking && !fpsSampling && !trackGlobalMin)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f)
            {
                return;
            }

            RecordFpsSample(1f / delta);
        }
    }
}
