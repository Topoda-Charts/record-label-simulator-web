using UnityEngine;
using UnityEngine.InputSystem;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController
    {
        private void HandleObserverKeyboard()
        {
            if (showSnapshotPicker || ToolkitHasFocus()) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (keyboard.digit1Key.wasPressedThisFrame) SetSpeed(1);
                if (keyboard.digit2Key.wasPressedThisFrame) SetSpeed(4);
                if (keyboard.digit3Key.wasPressedThisFrame) SetSpeed(16);
                if (keyboard.nKey.wasPressedThisFrame) { SafeAutosave("before single step"); simulation.SingleStep(); RefreshWorldPresenter(true); }
                if (keyboard.escapeKey.wasPressedThisFrame) ReturnToMenu();
                if (keyboard.homeKey.wasPressedThisFrame && cameraRig != null) cameraRig.ResetOverview(reducedMotion);
            }

            if (cameraRig == null || keyboard == null)
            {
                return;
            }

            float pan = 1f * Time.unscaledDeltaTime;
            Vector2 panAxis = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) panAxis.x -= pan;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) panAxis.x += pan;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) panAxis.y += pan;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) panAxis.y -= pan;
            cameraRig.HandleKeyboardPan(panAxis, Time.unscaledDeltaTime);

            float orbitDirection = 0f;
            if (keyboard.qKey.isPressed) orbitDirection -= 1f;
            if (keyboard.eKey.isPressed) orbitDirection += 1f;
            cameraRig.HandleOrbitKeys(orbitDirection, Time.unscaledDeltaTime);
        }

        private void HandleObserverCameraInput()
        {
            if (cameraRig == null)
            {
                return;
            }

            cameraRig.SetReducedMotion(reducedMotion);
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            Vector2 guiPointer = new Vector2(pointer.x, Screen.height - pointer.y);
            bool pointerOverHud = IsPointerOverObserverHud(guiPointer);
            if (!pointerOverHud && mouse.leftButton.wasPressedThisFrame)
                toolkitRoot?.panel?.focusController?.focusedElement?.Blur();
            if (!pointerOverHud) cameraRig.HandleZoom(mouse.scroll.ReadValue().y / 120f);
            bool orbitHeld = Keyboard.current != null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

            if (mouse.leftButton.wasPressedThisFrame && orbitHeld && !pointerOverHud)
            {
                cameraRig.BeginOrbitDrag(pointer);
            }
            else if ((mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame) && !pointerOverHud)
            {
                cameraRig.BeginPanDrag(pointer);
            }

            if (mouse.leftButton.isPressed && orbitHeld)
            {
                cameraRig.DragOrbit(pointer);
            }
            else if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            {
                cameraRig.DragPan(pointer);
            }

            if (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame || mouse.middleButton.wasReleasedThisFrame)
            {
                cameraRig.EndDrag();
            }
        }

        private void HandleObserverWorldSelection()
        {
            if (worldPresenter == null || cameraRig == null || cameraRig.ObserverCamera == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (Keyboard.current != null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed))
            {
                return;
            }

            EnsureObserverLayoutRects();
            Vector2 pointer = mouse.position.ReadValue();
            Vector2 guiPointer = new Vector2(pointer.x, Screen.height - pointer.y);
            if (!observerWorldGuiRect.Contains(guiPointer) || IsPointerOverObserverHud(guiPointer))
            {
                return;
            }

            Ray ray = cameraRig.ObserverCamera.ScreenPointToRay(pointer);
            if (worldPresenter.TryPick(ray, out string labelId))
            {
                selectedLabelId = labelId;
                worldPresenter.SetSelectedLabel(selectedLabelId);
                worldPresenter.RefreshFromSnapshot(simulation.World);
                cameraRig.Focus(worldPresenter.GetFocusPoint(selectedLabelId), reducedMotion);
            }
        }

        private void RefreshWorldPresenter(bool force)
        {
            if (worldPresenter == null || simulation == null || state != ApplicationState.Observer)
            {
                return;
            }

            bool stepChanged = simulation.World.Clock.StepIndex != lastPresentedStepIndex;
            presenterRefreshAccumulator += Time.unscaledDeltaTime;
            if (!force && !stepChanged && presenterRefreshAccumulator < 0.25f)
            {
                return;
            }

            presenterRefreshAccumulator = 0f;
            lastPresentedStepIndex = simulation.World.Clock.StepIndex;
            worldPresenter.SetSelectedLabel(selectedLabelId);
            worldPresenter.RefreshFromSnapshot(simulation.World);
        }

        private bool IsPointerOverObserverHud(Vector2 guiPointer)
        {
            if (toolkitRoot != null) return ToolkitContainsPointer(guiPointer);
            if (guiPointer.y <= 78f)
            {
                return true;
            }

            if (observerInspectorGuiRect.Contains(guiPointer) || observerEventsGuiRect.Contains(guiPointer))
            {
                return true;
            }

            if (showSnapshotPicker)
            {
                return true;
            }

            return false;
        }

        private void EnsureObserverLayoutRects()
        {
            if (toolkitRoot != null)
            {
                observerWorldGuiRect = new Rect(0, 0, Screen.width, Screen.height);
                return;
            }
            observerWorldGuiRect = new Rect(18f, 92f, Screen.width * 0.61f, Screen.height - 304f);
            observerInspectorGuiRect = new Rect(observerWorldGuiRect.xMax + 12f, 92f, Screen.width - observerWorldGuiRect.xMax - 30f, Screen.height - 110f);
            observerEventsGuiRect = new Rect(18f, observerWorldGuiRect.yMax + 12f, observerWorldGuiRect.width, Screen.height - observerWorldGuiRect.yMax - 30f);
        }
    }
}
