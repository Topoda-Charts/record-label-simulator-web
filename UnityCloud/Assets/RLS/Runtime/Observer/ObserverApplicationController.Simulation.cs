using System;
using System.Collections;
using UnityEngine;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController
    {
        private void BeginObserver(ObserverMode selectedMode)
        {
            mode = selectedMode;
            state = ApplicationState.Loading;
            loadingProgress = 0f;
            generationStartedRealtime = Time.realtimeSinceStartupAsDouble;
            StartCoroutine(PrepareObserver());
        }

        private IEnumerator PrepareObserver()
        {
            simulation = new DeterministicSimulation(ScenarioDefinition.GenericObserverSeed(), SimulationTuning.ObserverPreview());
            loadingProgress = 0.08f;
            loadingMessage = "Creating stable Members, Acts, labels, and Structures…";
            yield return null;

            if (reviewStage == ObserverReviewStage.MenuAndShell)
            {
                loadingProgress = 1f;
                loadingMessage = "Observer shell ready";
                yield return null;
            }
            else if (mode == ObserverMode.OpenHandoff)
            {
                DateTime target = ScenarioDefinition.GenericObserverSeed().EndDateUtc;
                long totalSteps = (long)(target - simulation.World.Clock.CurrentUtc).TotalMinutes / simulation.World.Clock.FixedStepMinutes;
                const int chunk = 4800;
                while (simulation.World.Clock.CurrentUtc < target)
                {
                    long remaining = (long)(target - simulation.World.Clock.CurrentUtc).TotalMinutes / simulation.World.Clock.FixedStepMinutes;
                    simulation.AdvanceSteps(Math.Min(chunk, remaining));
                    loadingProgress = 0.12f + 0.83f * (1f - (float)remaining / totalSteps);
                    loadingMessage = "Simulating autonomous market events through " + simulation.World.Clock.CurrentUtc.ToString("MMM d, yyyy") + "…";
                    yield return null;
                }
            }

            simulation.World.Clock.Paused = true;
            simulation.RefreshDigest();
            loadingProgress = 1f;
            loadingMessage = "Observer ready";
            yield return null;
            SafeAutosave("observer initialized");
            coldGenerationElapsedSeconds = Time.realtimeSinceStartupAsDouble - generationStartedRealtime;
            statusMessage = (mode == ObserverMode.OpenHandoff ? "Cold Aug 31 generation" : "Observer initialization") + ": " + coldGenerationElapsedSeconds.ToString("0.000") + " s";
            Debug.Log("RLS_OBSERVER_READY mode=" + mode + " elapsedSeconds=" + coldGenerationElapsedSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " digest=" + simulation.World.Digest);
            state = ApplicationState.Observer;
            RefreshWorldPresenter(true);
            if (cameraRig != null)
            {
                cameraRig.ResetOverview(false);
            }
        }

        private void TogglePause()
        {
            SafeAutosave("before pause change");
            simulation.World.Clock.Paused = !simulation.World.Clock.Paused;
            simulation.RefreshDigest();
            statusMessage = simulation.World.Clock.Paused ? "Paused safely" : "Observation resumed";
        }

        private void SetSpeed(int speed)
        {
            SafeAutosave("before speed change");
            simulation.World.Clock.Speed = speed;
            simulation.RefreshDigest();
            statusMessage = "Speed set to ×" + speed;
        }

        private void Skip(TimeSpan duration)
        {
            try
            {
                SafeAutosave("before Skip Time");
                simulation.AdvanceTo(simulation.World.Clock.CurrentUtc + duration);
                simulation.World.Clock.Paused = true;
                simulation.RefreshDigest();
                statusMessage = "Skipped to " + simulation.World.Clock.CurrentUtc.ToString("MMM d, yyyy HH:mm");
            }
            catch (Exception exception)
            {
                plainLanguageError = exception.Message;
            }
        }

        private void ResetWorld()
        {
            SafeAutosave("before reset");
            simulation.Reset();
            if (mode == ObserverMode.OpenHandoff && reviewStage != ObserverReviewStage.MenuAndShell)
            {
                simulation.AdvanceTo(ScenarioDefinition.GenericObserverSeed().EndDateUtc);
            }
            simulation.World.Clock.Paused = true;
            simulation.RefreshDigest();
            statusMessage = "Reset reproduced the selected mode's starting state";
        }

        private void SaveNamedSnapshot()
        {
            bool wasPaused = simulation.World.Clock.Paused;
            try
            {
                simulation.World.Clock.Paused = true;
                simulation.RefreshDigest();
                SnapshotMetadata saved = snapshots.SaveNamed(simulation.World, snapshotName);
                statusMessage = "Saved “" + saved.DisplayName + "” locally";
                snapshotList = snapshots.List();
            }
            catch (Exception exception)
            {
                plainLanguageError = "The snapshot was not saved. Your current world is unchanged. " + exception.Message;
            }
            finally
            {
                simulation.World.Clock.Paused = wasPaused;
                simulation.RefreshDigest();
            }
        }

        private void LoadSnapshot(string fileName)
        {
            try
            {
                SafeAutosave("before loading snapshot");
                SnapshotEnvelope envelope = snapshots.Load(fileName, ScenarioDefinition.GenericObserverSeed().SchemaVersion);
                if (simulation == null)
                {
                    simulation = new DeterministicSimulation(ScenarioDefinition.GenericObserverSeed(), SimulationTuning.ObserverPreview());
                }
                simulation.ReplaceWorld(envelope.World);
                simulation.World.Clock.Paused = true;
                state = ApplicationState.Observer;
                showSnapshotPicker = false;
                statusMessage = "Loaded “" + envelope.Metadata.DisplayName + "” and verified its digest";
            }
            catch (Exception exception)
            {
                plainLanguageError = exception.Message;
            }
        }

        private void ReturnToMenu()
        {
            SafeAutosave("before return to menu");
            state = ApplicationState.MainMenu;
        }

        private void ExitCleanly()
        {
            state = ApplicationState.Exiting;
            SafeAutosave("clean exit");
            Application.Quit();
        }

        private void SafeAutosave(string reason)
        {
            if (simulation == null || snapshots == null || reviewStage == ObserverReviewStage.MenuAndShell)
            {
                return;
            }

            bool wasPaused = simulation.World.Clock.Paused;
            try
            {
                simulation.World.Clock.Paused = true;
                simulation.RefreshDigest();
                SnapshotMetadata saved = snapshots.SaveAutosave(simulation.World, reason);
                statusMessage = "Autosaved " + new DateTime(saved.SavedAtUtcTicks, DateTimeKind.Utc).ToString("HH:mm:ss") + " — " + reason;
            }
            catch (Exception exception)
            {
                plainLanguageError = "Autosave could not finish. Your previous valid recovery save is still available. " + exception.Message;
            }
            finally
            {
                simulation.World.Clock.Paused = wasPaused;
                simulation.RefreshDigest();
            }
        }
    }
}
