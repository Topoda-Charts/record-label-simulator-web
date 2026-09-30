using UnityEngine;

namespace Topoda.RLS.Observer
{
    public sealed class ObserverCameraRig : MonoBehaviour
    {
        private const float MinDistance = 20f;
        private const float MaxDistance = 165f;
        private const float DefaultDistance = 118f;
        private static readonly Vector3 OverviewFocus = new Vector3(0f, 0f, 0f);

        [SerializeField] private Camera observerCamera;

        private float yaw;
        private float pitch = 52f;
        private float distance = DefaultDistance;
        private Vector3 focusPoint = OverviewFocus;
        private Vector3 focusTarget = OverviewFocus;
        private Vector3 focusVelocity;
        private bool draggingPan;
        private bool draggingOrbit;
        private Vector2 lastPointer;
        private bool reducedMotion;

        public Camera ObserverCamera { get { return observerCamera; } }

        public void Configure(Camera camera)
        {
            observerCamera = camera;
            ResetOverview(true);
        }

        public void SetReducedMotion(bool enabled)
        {
            reducedMotion = enabled;
        }

        public void ResetOverview(bool instant)
        {
            focusTarget = OverviewFocus;
            focusPoint = OverviewFocus;
            yaw = 32f;
            pitch = 52f;
            distance = DefaultDistance;
            focusVelocity = Vector3.zero;
            if (instant || reducedMotion)
            {
                ApplyTransformImmediate();
            }
        }

        public void Focus(Vector3 worldPoint, bool instant)
        {
            focusTarget = worldPoint;
            focusTarget.y = Mathf.Max(focusTarget.y, 0f);
            if (instant || reducedMotion)
            {
                focusPoint = focusTarget;
                focusVelocity = Vector3.zero;
                ApplyTransformImmediate();
            }
        }

        public void HandleKeyboardPan(Vector2 axis, float deltaTime)
        {
            if (axis.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 forward = FlatForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 delta = (right * axis.x + forward * axis.y) * (distance * 0.55f * deltaTime);
            focusTarget += delta;
            focusPoint += delta;
            ClampFocus();
        }

        public void HandleOrbitKeys(float direction, float deltaTime)
        {
            if (Mathf.Abs(direction) <= 0.001f)
            {
                return;
            }

            yaw += direction * 48f * deltaTime;
        }

        public void HandleZoom(float scrollDelta)
        {
            if (Mathf.Abs(scrollDelta) <= 0.001f)
            {
                return;
            }

            distance = Mathf.Clamp(distance - scrollDelta * distance * 0.12f, MinDistance, MaxDistance);
        }

        public void BeginPanDrag(Vector2 screenPosition)
        {
            draggingPan = true;
            lastPointer = screenPosition;
        }

        public void BeginOrbitDrag(Vector2 screenPosition)
        {
            draggingOrbit = true;
            lastPointer = screenPosition;
        }

        public void EndDrag()
        {
            draggingPan = false;
            draggingOrbit = false;
        }

        public void DragPan(Vector2 screenPosition)
        {
            if (!draggingPan)
            {
                return;
            }

            Vector2 delta = screenPosition - lastPointer;
            lastPointer = screenPosition;
            Vector3 forward = FlatForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float scale = distance * 0.0022f;
            focusTarget -= right * delta.x * scale;
            focusTarget -= forward * delta.y * scale;
            focusPoint = focusTarget;
            ClampFocus();
        }

        public void DragOrbit(Vector2 screenPosition)
        {
            if (!draggingOrbit)
            {
                return;
            }

            Vector2 delta = screenPosition - lastPointer;
            lastPointer = screenPosition;
            yaw += delta.x * 0.22f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.18f, 12f, 78f);
        }

        private void LateUpdate()
        {
            if (observerCamera == null)
            {
                return;
            }

            if (reducedMotion)
            {
                focusPoint = focusTarget;
            }
            else
            {
                focusPoint = Vector3.SmoothDamp(focusPoint, focusTarget, ref focusVelocity, 0.38f);
            }

            ApplyTransformImmediate();
        }

        private void ApplyTransformImmediate()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
            observerCamera.transform.position = focusPoint + offset;
            observerCamera.transform.rotation = rotation;
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            forward.y = 0f;
            return forward.normalized;
        }

        private void ClampFocus()
        {
            focusTarget.x = Mathf.Clamp(focusTarget.x, -95f, 95f);
            focusTarget.z = Mathf.Clamp(focusTarget.z, -95f, 95f);
            focusTarget.y = 0f;
            focusPoint.x = Mathf.Clamp(focusPoint.x, -95f, 95f);
            focusPoint.z = Mathf.Clamp(focusPoint.z, -95f, 95f);
            focusPoint.y = 0f;
        }
    }
}
