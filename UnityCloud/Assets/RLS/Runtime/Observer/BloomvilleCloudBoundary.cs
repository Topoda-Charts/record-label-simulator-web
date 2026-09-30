using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Topoda.RLS.Observer
{
    /// <summary>
    /// Lightweight decorative cloud puffs at the outer edge of the built neighborhood.
    /// This component owns only its transient mesh and material; it does not participate
    /// in simulation state, collision, or visibility rules.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    public sealed class BloomvilleCloudBoundary : MonoBehaviour
    {
        private const int MaxPuffs = 48;
        private const int LatitudeSegments = 6;
        private const int LongitudeSegments = 12;
        private const float MinBoundaryRadius = 90f;
        private const float MaxBoundaryRadius = 110f;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField, Range(24, MaxPuffs)] private int puffCount = MaxPuffs;
        [SerializeField, Range(MinBoundaryRadius, MaxBoundaryRadius)] private float innerRadius = 94f;
        [SerializeField, Range(MinBoundaryRadius, MaxBoundaryRadius)] private float outerRadius = 108f;
        [SerializeField, Range(20f, 40f)] private float altitude = 29f;

        private Matrix4x4[] puffMatrices;
        private MaterialPropertyBlock propertyBlock;
        private Mesh puffMesh;
        private Material puffMaterial;
        private ObserverApplicationController controller;
        private int activePuffCount;

        private void Awake()
        {
            activePuffCount = Mathf.Clamp(puffCount, 1, MaxPuffs);
            puffMatrices = new Matrix4x4[activePuffCount];
            propertyBlock = new MaterialPropertyBlock();
            controller = FindFirstObjectByType<ObserverApplicationController>();
            puffMesh = CreatePuffMesh();
            puffMaterial = CreatePuffMaterial();
            BuildPuffRing();
        }

        private void LateUpdate()
        {
            if (puffMesh == null || puffMaterial == null || puffMatrices == null || activePuffCount == 0)
            {
                return;
            }

            if (controller == null)
            {
                controller = FindFirstObjectByType<ObserverApplicationController>();
            }

            propertyBlock.SetColor(ColorPropertyId, GetDayNightTint());
            Graphics.DrawMeshInstanced(
                puffMesh,
                0,
                puffMaterial,
                puffMatrices,
                activePuffCount,
                propertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                null,
                LightProbeUsage.Off,
                null);
        }

        private void OnDestroy()
        {
            DestroyOwned(puffMesh);
            DestroyOwned(puffMaterial);
            puffMesh = null;
            puffMaterial = null;
        }

        private void BuildPuffRing()
        {
            float lowerRadius = Mathf.Clamp(innerRadius, MinBoundaryRadius, MaxBoundaryRadius);
            float upperRadius = Mathf.Clamp(outerRadius, lowerRadius, MaxBoundaryRadius);
            for (int index = 0; index < activePuffCount; index++)
            {
                float angle = index * 2.39996323f;
                float radiusWave = Mathf.Repeat(index * 0.61803399f, 1f);
                float radius = Mathf.Lerp(lowerRadius, upperRadius, radiusWave);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = altitude + Mathf.Sin(index * 1.73f) * 2.8f;
                float width = Mathf.Lerp(10f, 14f, Mathf.Repeat(index * 0.37f, 1f));
                float depth = Mathf.Lerp(8f, 11f, Mathf.Repeat(index * 0.53f, 1f));
                float height = Mathf.Lerp(4.8f, 6.5f, Mathf.Repeat(index * 0.71f, 1f));
                float yaw = angle * Mathf.Rad2Deg + 90f;

                Vector3 localPosition = new Vector3(x, y, z);
                Vector3 worldPosition = transform.TransformPoint(localPosition);
                Quaternion worldRotation = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
                Vector3 scale = new Vector3(width, height, depth);
                puffMatrices[index] = Matrix4x4.TRS(worldPosition, worldRotation, scale);
            }
        }

        private Color GetDayNightTint()
        {
            float daylight = 0.62f;
            WorldSnapshot snapshot = controller?.Simulation?.World;
            if (snapshot?.Clock != null)
            {
                DateTime time = snapshot.Clock.CurrentUtc;
                float dayFraction = (time.Hour + time.Minute / 60f) / 24f;
                daylight = ObserverLighting.Daylight(dayFraction);
            }

            Color night = new Color(0.62f, 0.74f, 0.94f, 0.4f);
            Color day = new Color(1f, 0.98f, 0.9f, 0.56f);
            return Color.Lerp(night, day, daylight);
        }

        private static Mesh CreatePuffMesh()
        {
            int columns = LongitudeSegments + 1;
            var vertices = new Vector3[(LatitudeSegments + 1) * columns];
            var triangles = new int[LatitudeSegments * LongitudeSegments * 6];
            int vertexIndex = 0;
            for (int latitude = 0; latitude <= LatitudeSegments; latitude++)
            {
                float phi = Mathf.PI * latitude / LatitudeSegments;
                float ring = Mathf.Sin(phi);
                float y = Mathf.Cos(phi);
                for (int longitude = 0; longitude <= LongitudeSegments; longitude++)
                {
                    float theta = Mathf.PI * 2f * longitude / LongitudeSegments;
                    vertices[vertexIndex++] = new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta));
                }
            }

            int triangleIndex = 0;
            for (int latitude = 0; latitude < LatitudeSegments; latitude++)
            {
                for (int longitude = 0; longitude < LongitudeSegments; longitude++)
                {
                    int first = latitude * columns + longitude;
                    int nextRow = first + columns;
                    triangles[triangleIndex++] = first;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = nextRow + 1;
                    triangles[triangleIndex++] = first;
                    triangles[triangleIndex++] = nextRow + 1;
                    triangles[triangleIndex++] = first + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "RLS Cloud Puff Mesh",
                hideFlags = HideFlags.DontSave,
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreatePuffMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "RLS Cloud Boundary Material",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0f);
            material.SetColor("_Color", Color.white);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.enableInstancing = true;
            return material;
        }

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

