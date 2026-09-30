using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Topoda.RLS.Observer;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Topoda.RLS.Tests.EditMode
{
    public sealed class ObserverToolkitRuntimeTests
    {
        [Test]
        public void ToolkitResourcesAreIncluded()
        {
            Assert.That(Resources.Load<StyleSheet>("ObserverShell"), Is.Not.Null);
            Assert.That(Resources.Load<ThemeStyleSheet>("ObserverTheme"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SceneEntersObserverWithSingleToolkitAndStablePausedState()
        {
            // Test invocation must provide isolated storage before the scene's Awake runs.
            Assert.That(ObserverSnapshotPaths.Current, Does.Contain("TestResults"),
                "Run with -rlsSnapshotRoot <absolute TestResults snapshot directory>.");
            EditorSceneManager.OpenScene("Assets/RLS/Scenes/RLSObserver.unity");
            yield return new EnterPlayMode();
            var controller = Object.FindAnyObjectByType<ObserverApplicationController>();
            Assert.That(controller, Is.Not.Null);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (controller.CurrentState != ApplicationState.MainMenu && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(ApplicationState.MainMenu));
            yield return null;
            var document = controller.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(document.rootVisualElement.Query<Button>().ToList().Select(x => x.text),
                Does.Contain("Watch the World Grow"));
            controller.BeginObserverSession(ObserverMode.WatchPreSimulation);
            deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (controller.CurrentState != ApplicationState.Observer && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(ApplicationState.Observer));
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(document.rootVisualElement.Q(className: "topbar"), Is.Not.Null);
            Assert.That(document.rootVisualElement.Q(className: "inspector"), Is.Null,
                "The observer begins with the world visible and contextual inspection closed.");
            var inspect = document.rootVisualElement.Query<Button>().ToList().First(button => button.text == "Inspect");
            Assert.That(inspect.enabledInHierarchy, Is.True);
            // Opening/scrolling inspection is exercised through real browser
            // mouse input; this batch test checks the initial panel structure.
            Assert.That(document.rootVisualElement.Q(className: "event-feed"), Is.Not.Null);
            Assert.That(controller.Simulation.World.Clock.Paused, Is.True);
            long step = controller.Simulation.World.Clock.StepIndex;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(controller.Simulation.World.Clock.StepIndex, Is.EqualTo(step));
            var presenter = Object.FindAnyObjectByType<BloomvilleWorldPresenter>();
            Assert.That(presenter.LabelHeadquarterCount, Is.EqualTo(8));
            string capture = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/modernization-playmode.png"));
            ScreenCapture.CaptureScreenshot(capture);
            for (int i = 0; i < 20; i++) yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (UnityEditor.EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
