using UnityEngine;

namespace Topoda.RLS.Observer
{
    public sealed class ObserverCameraRig : MonoBehaviour
    {
        private const float MinDistance = 6f;
        private const float MaxDistance = 165f;
        private const float DefaultDistance = 118f;
        private const float MemberFollowWorldBound = 64f;
        private const float MemberFollowPitch = 32f;
        private const float MemberFollowDistance = 12f;
        private static readonly Vector3 OverviewFocus = new Vector3(0f, 0f, 0f);

        [SerializeField] private Camera observerCamera;

        private float yaw;
        private float pitch = 52f;
        private float distance = DefaultDistance;
        private float zoomTarget = DefaultDistance;
        private float zoomVelocity;
        private Vector3 zoomAnchor;
        private Vector2 zoomPointer;
        private bool hasZoomAnchor;
        private bool memberFollowActive;
        private Vector3 focusPoint = OverviewFocus;
        private Vector3 focusTarget = OverviewFocus;
        private Vector3 focusVelocity;
        private bool draggingPan;
        private bool draggingOrbit;
        private Vector2 lastPointer;
        private bool reducedMotion;

        public Camera ObserverCamera { get { return observerCamera; } }
        public float ViewDistance { get { return distance; } }
        public float ZoomTarget { get { return zoomTarget; } }

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
            zoomTarget = DefaultDistance;
            zoomVelocity = 0f;
            hasZoomAnchor = false;
            memberFollowActive = false;
            focusVelocity = Vector3.zero;
            if (instant || reducedMotion)
            {
                ApplyTransformImmediate();
            }
        }

        public void Focus(Vector3 worldPoint, bool instant)
        {
            hasZoomAnchor = false;
            memberFollowActive = false;
            focusTarget = worldPoint;
            focusTarget.y = Mathf.Max(focusTarget.y, 0f);
            if (instant || reducedMotion)
            {
                focusPoint = focusTarget;
                focusVelocity = Vector3.zero;
                ApplyTransformImmediate();
            }
        }

        public void FollowMember(Vector3 worldPoint)
        {
            memberFollowActive = true;
            focusTarget = new Vector3(
                Mathf.Clamp(worldPoint.x, -MemberFollowWorldBound, MemberFollowWorldBound),
                Mathf.Clamp(worldPoint.y, 0f, 4f) + 0.82f,
                Mathf.Clamp(worldPoint.z, -MemberFollowWorldBound, MemberFollowWorldBound));
            zoomTarget = MemberFollowDistance;
            hasZoomAnchor = false;
            pitch = MemberFollowPitch;
        }

        public void StopFollowing()
        {
            memberFollowActive = false;
        }

        public void HandleKeyboardPan(Vector2 axis, float deltaTime)
        {
            if (axis.sqrMagnitude <= 0.0001f)
            {
                return;
            }
            hasZoomAnchor = false;

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
            HandleZoom(scrollDelta, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        public void HandleZoom(float scrollDelta, Vector2 screenPosition)
        {
            if (Mathf.Abs(scrollDelta) <= 0.001f)
            {
                return;
            }

            zoomPointer = screenPosition;
            hasZoomAnchor = TryGroundPoint(screenPosition, out zoomAnchor);
            // A notch changes scale by about 10%; fractional trackpad input is
            // preserved and accumulated bursts cannot jump across the scene.
            zoomTarget = Mathf.Clamp(zoomTarget * Mathf.Exp(-Mathf.Clamp(scrollDelta, -3f, 3f) * 0.1f), MinDistance, MaxDistance);
        }

        public void BeginPanDrag(Vector2 screenPosition)
        {
            hasZoomAnchor = false;
            draggingPan = true;
            lastPointer = screenPosition;
        }

        public void BeginOrbitDrag(Vector2 screenPosition)
        {
            hasZoomAnchor = false;
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
                distance = zoomTarget;
            }
            else
            {
                focusPoint = Vector3.SmoothDamp(focusPoint, focusTarget, ref focusVelocity, 0.38f);
                distance = Mathf.SmoothDamp(distance, zoomTarget, ref zoomVelocity, 0.14f, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            ApplyTransformImmediate();
            if (hasZoomAnchor && TryGroundPoint(zoomPointer, out Vector3 currentAnchor))
            {
                Vector3 correction = zoomAnchor - currentAnchor;
                correction.y = 0f;
                focusPoint += correction;
                focusTarget += correction;
                ClampFocus();
                ApplyTransformImmediate();
                if (Mathf.Abs(distance - zoomTarget) < 0.002f) hasZoomAnchor = false;
            }
        }

        private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            point = Vector3.zero;
            if (observerCamera == null) return false;
            Ray ray = observerCamera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter) || enter > 1000f) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        private void ApplyTransformImmediate()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
            observerCamera.transform.position = focusPoint + offset;
            observerCamera.transform.rotation = rotation;
            if (memberFollowActive && Physics.SphereCast(focusPoint, 0.3f, offset.normalized, out RaycastHit obstruction, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // Keep the viewing path on the member's side of an intervening
                // wall instead of leaving the camera behind the building.
                observerCamera.transform.position = focusPoint + offset.normalized * Mathf.Max(1.1f, obstruction.distance - 0.45f);
            }
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
