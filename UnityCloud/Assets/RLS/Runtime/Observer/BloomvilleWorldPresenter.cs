using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Topoda.RLS.Observer
{
    public sealed class BloomvilleWorldPresenter : MonoBehaviour
    {
        private static readonly Dictionary<string, Vector3> LabelAnchors = new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            { "ARL1", new Vector3(-36f, 0f, 36f) },
            { "ARL2", new Vector3(-36f, 0f, 0f) },
            { "ARL3", new Vector3(-36f, 0f, -36f) }
        };

        [SerializeField] private Light sunLight;
        [SerializeField] private BloomvilleMaterialSet materials;
        [SerializeField] private Transform worldRoot;
        [SerializeField] private string selectedLabelId;

        private Transform groundRoot;
        private Transform landmarkRoot;
        private Transform floraRoot;
        private Transform labelsRoot;
        private readonly Dictionary<string, LabelVisual> labelVisuals = new Dictionary<string, LabelVisual>(StringComparer.Ordinal);
        private readonly List<Material> biolumFloraMaterials = new List<Material>();
        private Font labelFont;
        private Camera billboardCamera;
        private Light moonFillLight;
        private readonly List<Transform> worldBillboards = new List<Transform>(12);
        private float selectedEmissionPulse;
        private float biolumNightStrength;
        private bool renderSettingsCaptured;
        private AmbientMode originalAmbientMode;
        private float originalAmbientIntensity;
        private Color originalAmbientSkyColor;
        private Color originalAmbientEquatorColor;
        private Color originalAmbientGroundColor;
        private bool originalFog;
        private FogMode originalFogMode;
        private Color originalFogColor;
        private float originalFogStartDistance;
        private float originalFogEndDistance;
        private float originalFogDensity;
        private Material originalSkybox;
        private Material runtimeSkybox;
        private Light originalRenderSettingsSun;
        private const float LabelLodNearDistance = 58f;
        private const float StylizedDaySunIntensity = 1.15f;
        private const float StylizedNightSunIntensity = 0.08f;
        private const float StylizedNightMoonIntensity = 0.18f;
        private const float GroundHalfExtent = 60f;

        public int LabelHeadquarterCount { get { return labelVisuals.Count; } }
        public int DisplayedProductionStructureCount { get { return labelVisuals.Count; } }

        public BloomvilleMaterialSet Materials { get { return materials; } }

        public bool TryGetLabelAnchor(string labelId, out Vector3 anchor)
        {
            if (string.IsNullOrEmpty(labelId))
            {
                anchor = default;
                return false;
            }

            return LabelAnchors.TryGetValue(labelId, out anchor);
        }

        public void SetMaterials(BloomvilleMaterialSet materialSet)
        {
            materials = materialSet;
        }

        public void Configure(Light directionalSun, UnityEngine.Object unusedAtmosphere, Camera cameraForBillboards, BloomvilleMaterialSet materialSet)
        {
            sunLight = directionalSun;
            materials = materialSet;
            billboardCamera = cameraForBillboards;
            CaptureRenderSettings();
            EnsureWorldRoot();
            EnsureBaseGeometry();
        }

        public void SetSelectedLabel(string labelId)
        {
            selectedLabelId = labelId;
        }

        public void RefreshFromSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot == null || materials == null)
            {
                return;
            }

            CaptureRenderSettings();
            EnsureWorldRoot();
            EnsureLabelFont();
            EnsureBaseGeometry();
            UpdateLighting(snapshot);
            RebuildLabels(snapshot);
            selectedEmissionPulse = (selectedEmissionPulse + Time.unscaledDeltaTime) % 1000f;
            ApplySelectionHighlight();
            ApplyBiolumFloraIntensity();
        }

        private void OnDestroy()
        {
            RestoreRenderSettings();
        }

        public bool TryPick(Ray ray, out string labelId)
        {
            labelId = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 512f))
            {
                BloomvillePickTarget target = hit.collider.GetComponentInParent<BloomvillePickTarget>();
                if (target != null && !string.IsNullOrEmpty(target.LabelId))
                {
                    labelId = target.LabelId;
                    return true;
                }
            }

            return false;
        }

        public Vector3 GetFocusPoint(string labelId)
        {
            if (TryGetLabelAnchor(labelId, out Vector3 anchor))
            {
                float height = 6f;
                if (labelVisuals.TryGetValue(labelId, out LabelVisual visual) && visual.Headquarters != null)
                {
                    height = visual.Headquarters.localScale.y * 0.65f;
                }

                return anchor + new Vector3(0f, height, 0f);
            }

            return new Vector3(0f, 8f, 0f);
        }

        private void LateUpdate()
        {
            if (billboardCamera == null)
            {
                billboardCamera = Camera.main;
            }

            if (billboardCamera == null)
            {
                return;
            }

            Transform cameraTransform = billboardCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            for (int index = 0; index < worldBillboards.Count; index++)
            {
                Transform marker = worldBillboards[index];
                if (marker != null)
                {
                    OrientBillboard(marker, cameraTransform);
                }
            }

            foreach (KeyValuePair<string, LabelVisual> pair in labelVisuals)
            {
                LabelVisual visual = pair.Value;
                Transform marker = visual.Marker;
                if (marker == null)
                {
                    continue;
                }

                if (visual.Headquarters != null)
                {
                    float lift = visual.Headquarters.lossyScale.y * 0.55f + 3.2f;
                    marker.position = visual.Headquarters.position + Vector3.up * lift;
                }

                OrientBillboard(marker, cameraTransform);
                TextMesh text = marker.GetComponent<TextMesh>();
                if (text == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(cameraPosition, marker.position);
                text.characterSize = Mathf.Clamp(distance * 0.0048f, 0.11f, 0.26f);
                bool showFullName = pair.Key == selectedLabelId || distance < LabelLodNearDistance;
                text.text = showFullName ? visual.MarketCode + "\n" + visual.DisplayName : visual.MarketCode;
            }
        }

        private static void OrientBillboard(Transform marker, Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return;
            }

            marker.rotation = cameraTransform.rotation;
        }

        private void EnsureWorldRoot()
        {
            if (worldRoot == null)
            {
                worldRoot = transform;
            }

            if (groundRoot == null)
            {
                groundRoot = new GameObject("Ground").transform;
                groundRoot.SetParent(worldRoot, false);
            }

            if (landmarkRoot == null)
            {
                landmarkRoot = new GameObject("Landmarks").transform;
                landmarkRoot.SetParent(worldRoot, false);
            }

            if (floraRoot == null)
            {
                floraRoot = new GameObject("Flora").transform;
                floraRoot.SetParent(worldRoot, false);
            }

            if (labelsRoot == null)
            {
                labelsRoot = new GameObject("Labels").transform;
                labelsRoot.SetParent(worldRoot, false);
            }
        }

        private void EnsureLabelFont()
        {
            if (labelFont != null)
            {
                return;
            }

            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (labelFont == null)
            {
                labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        private void EnsureBaseGeometry()
        {
            if (groundRoot.childCount > 0)
            {
                return;
            }

            const float lotWidth = 24f;
            const float streetWidth = 12f;
            const float roadSpan = 120f;
            CreatePrimitive(PrimitiveType.Plane, groundRoot, "Central neighborhood ground", Vector3.zero, new Vector3(12f, 1f, 12f), materials.Ground, false);
            // Nine lots in the currently visible Central slice. These are lots,
            // not nine Areas; unopened Areas have no ground plane here.
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    CreatePrimitive(PrimitiveType.Cube, groundRoot, "Lot " + x + "/" + z,
                        new Vector3(x * 36f, 0.012f, z * 36f), new Vector3(lotWidth, 0.024f, lotWidth), materials.PlazaGarden, false);
                }
            }
            foreach (float street in new[] { -54f, -18f, 18f, 54f })
            {
                CreatePrimitive(PrimitiveType.Cube, groundRoot, "North-south street", new Vector3(street, 0.04f, 0f), new Vector3(streetWidth, 0.04f, roadSpan), materials.Road, false);
                CreatePrimitive(PrimitiveType.Cube, groundRoot, street == -18f ? "Main Street" : "East-west street", new Vector3(0f, 0.04f, street), new Vector3(roadSpan, 0.04f, streetWidth), materials.Road, false);
                foreach (float side in new[] { -4.8f, 4.8f })
                {
                    CreatePrimitive(PrimitiveType.Cube, groundRoot, "Sidewalk", new Vector3(street + side, 0.09f, 0f), new Vector3(2.4f, 0.10f, roadSpan), materials.CityHallBase, false);
                    CreatePrimitive(PrimitiveType.Cube, groundRoot, "Sidewalk", new Vector3(0f, 0.09f, street + side), new Vector3(roadSpan, 0.10f, 2.4f), materials.CityHallBase, false);
                }
            }

            CreatePrimitive(PrimitiveType.Cylinder, landmarkRoot, "Plaza Ring", new Vector3(0f, 0.05f, 0f), new Vector3(14f, 0.05f, 14f), materials.PlazaGarden, false);
            CreatePrimitive(PrimitiveType.Cube, landmarkRoot, "Annglora City Hall", new Vector3(0f, 4f, 0f), new Vector3(10f, 8f, 10f), materials.CityHallBase, true);
            BuildCityHallFacade();
            CreatePrimitive(PrimitiveType.Sphere, landmarkRoot, "City Hall Dome", new Vector3(0f, 8f, 0f), new Vector3(6.2f, 3.8f, 6.2f), materials.CityHallDome, false);
            GameObject cityHallLabel = CreateWorldLabel(landmarkRoot, "City Hall", new Vector3(0f, 13f, 0f), 0.22f, new Color(0.92f, 0.96f, 0.93f));
            worldBillboards.Add(cityHallLabel.transform);

            BuildPlazaTerraces();
            BuildCentralGarden();
            BuildFloraRing();

            foreach (KeyValuePair<string, Vector3> anchor in LabelAnchors)
            {
                CreateDistrictPad(anchor.Value, anchor.Key.Substring(0, 3));
            }
        }

        private void BuildCityHallFacade()
        {
            if (materials.CityHallDome != null)
            {
                CreatePrimitive(PrimitiveType.Cube, landmarkRoot, "City Hall Roof Cornice", new Vector3(0f, 8f, 0f), new Vector3(10.45f, 0.2f, 10.45f), materials.CityHallDome, false);
            }

            if (materials.AnngloraWindow != null)
            {
                for (int row = 0; row < 2; row++)
                {
                    float y = row == 0 ? 4.5f : 6.45f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        CreatePrimitive(PrimitiveType.Cube, landmarkRoot, "City Hall Front Window", new Vector3(side * 2.55f, y, 5.06f), new Vector3(1.05f, 1.3f, 0.14f), materials.AnngloraWindow, false);
                    }
                }

                CreatePrimitive(PrimitiveType.Cube, landmarkRoot, "City Hall West Window", new Vector3(-5.06f, 5.35f, 0f), new Vector3(0.14f, 1.3f, 1.05f), materials.AnngloraWindow, false);
                CreatePrimitive(PrimitiveType.Cube, landmarkRoot, "City Hall East Window", new Vector3(5.06f, 5.35f, 0f), new Vector3(0.14f, 1.3f, 1.05f), materials.AnngloraWindow, false);
            }

            if (materials.Road != null)
            {
                CreatePrimitive(PrimitiveType.Cube, landmarkRoot, "City Hall Entry", new Vector3(0f, 1.55f, 5.07f), new Vector3(1.8f, 3.1f, 0.16f), materials.Road, false);
            }
        }

        private void BuildPlazaTerraces()
        {
            Vector3[] terraceOffsets = { new Vector3(-11f, 0.35f, 11f), new Vector3(11f, 0.35f, 11f), new Vector3(-11f, 0.35f, -11f), new Vector3(11f, 0.35f, -11f) };
            for (int index = 0; index < terraceOffsets.Length; index++)
            {
                CreatePrimitive(PrimitiveType.Cube, landmarkRoot, "Plaza Terrace", terraceOffsets[index], new Vector3(5f, 0.35f, 5f), materials.PlazaGarden, false);
            }
        }

        private void BuildCentralGarden()
        {
            for (int index = 0; index < 6; index++)
            {
                float angle = index * Mathf.PI * 2f / 6f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 6.5f, 0.12f, Mathf.Sin(angle) * 6.5f);
                CreatePrimitive(PrimitiveType.Cube, floraRoot, "Garden Bed", position, new Vector3(2.2f, 0.12f, 1.4f), materials.PlazaGarden, false);
            }
        }

        private void BuildCanalAccent()
        {
            CreatePrimitive(PrimitiveType.Cube, groundRoot, "Canal Reflect", new Vector3(0f, 0.03f, 22f), new Vector3(28f, 0.05f, 3.5f), materials.CanalReflect, false);
        }

        private void BuildFloraRing()
        {
            biolumFloraMaterials.Clear();
            for (int index = 0; index < 14; index++)
            {
                float angle = index * Mathf.PI * 2f / 14f;
                float radius = 19f + (index % 3) * 2.5f;
                Vector3 basePosition = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                CreatePrimitive(PrimitiveType.Cylinder, floraRoot, "Tree Trunk", basePosition + new Vector3(0f, 1.2f, 0f), new Vector3(0.35f, 2.4f, 0.35f), materials.FloraTrunk, false);
                GameObject canopy = CreatePrimitive(PrimitiveType.Sphere, floraRoot, "Tree Canopy", basePosition + new Vector3(0f, 3.2f, 0f), Vector3.one * (2.2f + (index % 2)), materials.FloraCanopy, false);
                if (index % 3 == 0)
                {
                    Material biolumMaterial = Instantiate(materials.FloraBiolum);
                    GameObject glow = CreatePrimitive(PrimitiveType.Sphere, floraRoot, "Biolum Flora", basePosition + new Vector3(0f, 0.45f, 0f), Vector3.one * 0.55f, biolumMaterial, false);
                    biolumFloraMaterials.Add(biolumMaterial);
                }
            }
        }

        private void CreateDistrictPad(Vector3 center, string districtCode)
        {
            Material padMaterial = materials.AnngloraDistrictPad;
            if (districtCode == "BRL")
            {
                padMaterial = materials.ByteriaDistrictPad;
            }
            else if (districtCode == "CRL")
            {
                padMaterial = materials.CrowniaDistrictPad;
            }

            CreatePrimitive(PrimitiveType.Cube, groundRoot, "Production lot", center + new Vector3(0f, 0.035f, 0f), new Vector3(24f, 0.025f, 24f), materials.PlazaGarden, false);
        }

        private void RebuildLabels(WorldSnapshot snapshot)
        {
            for (int index = 0; index < snapshot.Labels.Count; index++)
            {
                LabelRecord label = snapshot.Labels[index];
                StructureRecord structure = snapshot.Structures.Find(item => item.LabelId == label.Id);
                if (structure == null) continue;
                if (!LabelAnchors.TryGetValue(label.Id, out Vector3 anchor))
                {
                    continue;
                }

                if (!labelVisuals.TryGetValue(label.Id, out LabelVisual visual))
                {
                    visual = CreateLabelVisual(label.Id, anchor, label.Nation);
                    labelVisuals[label.Id] = visual;
                }

                UpdateLabelVisual(visual, label, snapshot);
            }
        }

        private LabelVisual CreateLabelVisual(string labelId, Vector3 anchor, string nation)
        {
            var root = new GameObject("Production lot " + labelId).transform;
            root.SetParent(labelsRoot, false);
            root.position = anchor;

            Material hqTemplate = materials.StructureBase;
            Material hqMaterial = Instantiate(hqTemplate);
            GameObject headquarters = CreatePrimitive(PrimitiveType.Cube, root, "Production Structure", new Vector3(0f, 2.5f, 0f), new Vector3(12f, 5f, 10f), hqMaterial, true);
            headquarters.AddComponent<BloomvillePickTarget>().LabelId = labelId;
            BuildHeadquartersFacade(headquarters.transform, nation);

            Transform marker = CreateWorldLabel(root, "Production", new Vector3(0f, 7.5f, 0f), 0.2f, Color.white).transform;
            worldBillboards.Add(marker);

            var structuresRoot = new GameObject("Structures").transform;
            structuresRoot.SetParent(root, false);

            return new LabelVisual
            {
                Root = root,
                Headquarters = headquarters.transform,
                HeadquartersRenderer = headquarters.GetComponent<Renderer>(),
                HeadquartersMaterial = hqMaterial,
                Nation = nation,
                Marker = marker,
                MarketCode = labelId,
                DisplayName = labelId,
                StructuresRoot = structuresRoot,
                StructureBlocks = new List<Renderer>(),
                LastStructureSignature = int.MinValue
            };
        }

        private void BuildHeadquartersFacade(Transform building, string nation)
        {
            Material window = GetFacadeWindowTemplate(nation);
            if (window != null)
            {
                CreatePrimitive(PrimitiveType.Cube, building, "HQ Front Window", new Vector3(-0.24f, 0.12f, 0.508f), new Vector3(0.17f, 0.16f, 0.02f), window, false);
                CreatePrimitive(PrimitiveType.Cube, building, "HQ Front Window", new Vector3(0.24f, 0.12f, 0.508f), new Vector3(0.17f, 0.16f, 0.02f), window, false);
                CreatePrimitive(PrimitiveType.Cube, building, "HQ Side Window", new Vector3(-0.508f, 0.12f, 0f), new Vector3(0.02f, 0.16f, 0.17f), window, false);
                CreatePrimitive(PrimitiveType.Cube, building, "HQ Side Window", new Vector3(0.508f, 0.12f, 0f), new Vector3(0.02f, 0.16f, 0.17f), window, false);
            }

            if (materials.Road != null)
            {
                CreatePrimitive(PrimitiveType.Cube, building, "HQ Entry", new Vector3(0f, -0.31f, 0.508f), new Vector3(0.18f, 0.34f, 0.024f), materials.Road, false);
            }

            if (materials.CityHallBase != null)
            {
                CreatePrimitive(PrimitiveType.Cube, building, "Production flat roof", new Vector3(0f, 0.51f, 0f), new Vector3(1.05f, 0.07f, 1.05f), materials.CityHallBase, false);
                // The existing fixture combines three production occupations.
                // Roof pods express that work capacity without assigning it a
                // new canonical Structure type or putting identity on walls.
                for (int index = -1; index <= 1; index++)
                    CreatePrimitive(PrimitiveType.Cube, building, "Production roof pod", new Vector3(index * 0.28f, 0.60f, 0f), new Vector3(0.19f, 0.16f, 0.56f), materials.CityHallBase, false);
            }
        }

        private Material GetFacadeWindowTemplate(string nation)
        {
            if (string.Equals(nation, "Byteria", StringComparison.Ordinal))
            {
                return materials.ByteriaNeonWindow;
            }

            if (string.Equals(nation, "Crownia", StringComparison.Ordinal))
            {
                return materials.CrowniaReflectiveWindow;
            }

            return materials.AnngloraWindow;
        }

        private void UpdateLabelVisual(LabelVisual visual, LabelRecord label, WorldSnapshot snapshot)
        {
            Color baseColor = ParseHexColor(label.ColorHex);
            const float height = 5f;
            visual.Headquarters.localScale = new Vector3(12f, height, 10f);
            visual.Headquarters.localPosition = new Vector3(0f, height * 0.5f, 0f);

            if (visual.HeadquartersMaterial == null)
            {
                visual.HeadquartersMaterial = Instantiate(GetHeadquartersTemplate(label.Nation));
                visual.HeadquartersRenderer.sharedMaterial = visual.HeadquartersMaterial;
            }

            SetStandardColor(visual.HeadquartersMaterial, GetStandardColor(materials.StructureBase));

            visual.Marker.localPosition = new Vector3(0f, height + 3.6f, 0f);
            visual.MarketCode = label.MarketCode;
            visual.DisplayName = "Production Structure";
            TextMesh text = visual.Marker.GetComponent<TextMesh>();
            if (text != null)
            {
                text.text = label.MarketCode;
                text.color = Color.Lerp(Color.white, baseColor, 0.35f);
            }

            int structureSignature = ComputeStructureSignature(label.Id, snapshot);
            if (structureSignature != visual.LastStructureSignature)
            {
                visual.LastStructureSignature = structureSignature;
                RebuildStructures(visual, label, snapshot, baseColor);
            }
        }

        private static int ComputeStructureSignature(string labelId, WorldSnapshot snapshot)
        {
            int hash = 17;
            for (int index = 0; index < snapshot.Structures.Count; index++)
            {
                StructureRecord structure = snapshot.Structures[index];
                if (structure.LabelId != labelId)
                {
                    continue;
                }

                hash = (hash * 31) + structure.Id.GetHashCode();
                hash = (hash * 31) + structure.OccupiedSlots;
                hash = (hash * 31) + structure.SlotCount;
            }

            return hash;
        }

        private void RebuildStructures(LabelVisual visual, LabelRecord label, WorldSnapshot snapshot, Color baseColor)
        {
            for (int index = visual.StructuresRoot.childCount - 1; index >= 0; index--)
            {
                DisposeGameObject(visual.StructuresRoot.GetChild(index).gameObject);
            }

            visual.StructureBlocks.Clear();
            List<StructureRecord> structures = new List<StructureRecord>();
            for (int index = 0; index < snapshot.Structures.Count; index++)
            {
                StructureRecord structure = snapshot.Structures[index];
                if (structure.LabelId == label.Id)
                {
                    structures.Add(structure);
                }
            }

            // The first record is the visible main building, not a separate
            // label HQ. Only additional real Structure records create annexes.
            for (int index = 1; index < structures.Count; index++)
            {
                float angle = (Mathf.PI * 2f * index) / Mathf.Max(1, structures.Count);
                float radius = visual.Headquarters.localScale.x * 0.85f + 3.5f;
                Vector3 localPosition = new Vector3(Mathf.Cos(angle) * radius, 1.2f, Mathf.Sin(angle) * radius);
                float occupancy = structures[index].SlotCount <= 0 ? 0f : (float)structures[index].OccupiedSlots / structures[index].SlotCount;
                Vector3 scale = new Vector3(2.8f, 2.4f + occupancy * 2f, 2.8f);
                Material structureMaterial = Instantiate(materials.StructureBase);
                SetStandardColor(structureMaterial, Color.Lerp(GetStandardColor(materials.StructureBase), baseColor, 0.22f));
                GameObject block = CreatePrimitive(PrimitiveType.Cube, visual.StructuresRoot, structures[index].Kind ?? "Structure", localPosition, scale, structureMaterial, true);
                visual.StructureBlocks.Add(block.GetComponent<Renderer>());

                int windowRows = 2 + Mathf.RoundToInt(occupancy * 3f);
                for (int row = 0; row < windowRows; row++)
                {
                    Material windowMaterial = GetWindowMaterial(label.Nation, occupancy);
                    GameObject window = CreatePrimitive(PrimitiveType.Cube, block.transform, "Window", new Vector3(0f, -0.2f + row * 0.35f, 0.52f), new Vector3(0.7f, 0.18f, 0.08f), windowMaterial, false);
                    visual.StructureBlocks.Add(window.GetComponent<Renderer>());
                }
            }
        }

        private Material GetWindowMaterial(string nation, float occupancy)
        {
            Material template = materials.AnngloraWindow;
            if (string.Equals(nation, "Byteria", StringComparison.Ordinal))
            {
                template = materials.ByteriaNeonWindow;
            }
            else if (string.Equals(nation, "Crownia", StringComparison.Ordinal))
            {
                template = materials.CrowniaReflectiveWindow;
            }

            Material instance = Instantiate(template);
            if (string.Equals(nation, "Byteria", StringComparison.Ordinal))
            {
                SetEmissive(instance, new Color(0.2f, 0.85f, 1f), 180f + occupancy * 520f);
            }
            else if (string.Equals(nation, "Annglora", StringComparison.Ordinal))
            {
                SetEmissive(instance, new Color(0.15f, 0.92f, 0.72f), 40f + occupancy * 120f);
            }

            return instance;
        }

        private void ApplySelectionHighlight()
        {
            foreach (KeyValuePair<string, LabelVisual> pair in labelVisuals)
            {
                if (pair.Value.HeadquartersMaterial == null)
                {
                    continue;
                }

                bool selected = pair.Key == selectedLabelId;
                if (selected)
                {
                    SetEmissive(pair.Value.HeadquartersMaterial, Color.black, 0f);
                    TextMesh marker = pair.Value.Marker.GetComponent<TextMesh>();
                    if (marker != null) marker.color = new Color(0.75f, 0.9f, 1f);
                }
                else
                {
                    SetEmissive(pair.Value.HeadquartersMaterial, Color.black, 0f);
                }

            }
        }

        private void ApplyNationHeadquartersFinish(Material material, string nation)
        {
            if (string.Equals(nation, "Byteria", StringComparison.Ordinal))
            {
                SetEmissive(material, new Color(0.15f, 0.75f, 1f), 85f);
                SetStandardFloat(material, "_Metallic", 0.35f);
                SetStandardFloat(material, "_Glossiness", 0.72f);
            }
            else if (string.Equals(nation, "Crownia", StringComparison.Ordinal))
            {
                SetEmissive(material, Color.black, 0f);
                SetStandardFloat(material, "_Metallic", 0.92f);
                SetStandardFloat(material, "_Glossiness", 0.88f);
            }
            else
            {
                SetEmissive(material, new Color(0.35f, 0.18f, 0.55f), 18f);
                SetStandardFloat(material, "_Metallic", 0.12f);
                SetStandardFloat(material, "_Glossiness", 0.46f);
            }
        }

        private void EnsureMoonFillLight()
        {
            if (moonFillLight != null)
            {
                return;
            }

            var moonObject = new GameObject("Moon Fill");
            moonObject.transform.SetParent(transform, false);
            moonFillLight = moonObject.AddComponent<Light>();
            moonFillLight.type = LightType.Directional;
            moonFillLight.shadows = LightShadows.None;
        }

        private void CaptureRenderSettings()
        {
            if (renderSettingsCaptured)
            {
                return;
            }

            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalAmbientSkyColor = RenderSettings.ambientSkyColor;
            originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            originalAmbientGroundColor = RenderSettings.ambientGroundColor;
            originalFog = RenderSettings.fog;
            originalFogMode = RenderSettings.fogMode;
            originalFogColor = RenderSettings.fogColor;
            originalFogStartDistance = RenderSettings.fogStartDistance;
            originalFogEndDistance = RenderSettings.fogEndDistance;
            originalFogDensity = RenderSettings.fogDensity;
            originalSkybox = RenderSettings.skybox;
            originalRenderSettingsSun = RenderSettings.sun;
            renderSettingsCaptured = true;
        }

        private void RestoreRenderSettings()
        {
            if (!renderSettingsCaptured)
            {
                return;
            }

            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.ambientSkyColor = originalAmbientSkyColor;
            RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
            RenderSettings.ambientGroundColor = originalAmbientGroundColor;
            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogStartDistance = originalFogStartDistance;
            RenderSettings.fogEndDistance = originalFogEndDistance;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.sun = originalRenderSettingsSun;
            if (RenderSettings.skybox == runtimeSkybox)
            {
                RenderSettings.skybox = originalSkybox;
            }

            if (runtimeSkybox != null)
            {
                DisposeGameObject(runtimeSkybox);
                runtimeSkybox = null;
            }

            renderSettingsCaptured = false;
        }

        private void EnsureRuntimeSkybox()
        {
            if (runtimeSkybox != null)
            {
                return;
            }

            Shader proceduralSky = Shader.Find("Skybox/Procedural");
            if (proceduralSky == null)
            {
                return;
            }

            Material currentSkybox = RenderSettings.skybox;
            runtimeSkybox = currentSkybox != null && currentSkybox.shader == proceduralSky
                ? Instantiate(currentSkybox)
                : new Material(proceduralSky);
            runtimeSkybox.name = "RLS Observer Procedural Sky";
            runtimeSkybox.hideFlags = HideFlags.DontSave;
            RenderSettings.skybox = runtimeSkybox;
        }

        private void ApplyBuiltInAtmosphere(float daylight, float dayFraction)
        {
            CaptureRenderSettings();
            EnsureRuntimeSkybox();

            float noonBlend = 1f - Mathf.Abs(dayFraction - 0.5f) * 2f;
            Color dayTop = Color.Lerp(new Color(0.42f, 0.62f, 0.92f), new Color(0.28f, 0.48f, 0.82f), noonBlend);
            Color nightTop = new Color(0.06f, 0.1f, 0.22f);
            Color dayGround = new Color(0.72f, 0.78f, 0.82f);
            Color nightGround = new Color(0.16f, 0.2f, 0.3f);

            if (runtimeSkybox != null)
            {
                SetMaterialColor(runtimeSkybox, "_SkyTint", Color.Lerp(nightTop, dayTop, daylight));
                SetMaterialColor(runtimeSkybox, "_GroundColor", Color.Lerp(nightGround, dayGround, daylight));
                SetMaterialFloat(runtimeSkybox, "_AtmosphereThickness", Mathf.Lerp(0.75f, 1.15f, daylight));
                SetMaterialFloat(runtimeSkybox, "_Exposure", Mathf.Lerp(0.55f, 1f, daylight));
                SetMaterialFloat(runtimeSkybox, "_SunDisk", 1f);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = Mathf.Lerp(0.62f, 0.95f, daylight);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.1f, 0.14f, 0.26f), dayTop, daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.16f, 0.19f, 0.28f), new Color(0.64f, 0.72f, 0.8f), daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.06f, 0.07f, 0.11f), new Color(0.34f, 0.33f, 0.29f), daylight);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(new Color(0.1f, 0.14f, 0.23f), new Color(0.62f, 0.72f, 0.8f), daylight);
            RenderSettings.fogStartDistance = Mathf.Lerp(130f, 180f, daylight);
            RenderSettings.fogEndDistance = Mathf.Lerp(780f, 640f, daylight);
        }

        private void UpdateLighting(WorldSnapshot snapshot)
        {
            if (snapshot?.Clock == null)
            {
                return;
            }

            EnsureMoonFillLight();
            DateTime time = snapshot.Clock.CurrentUtc;
            float dayFraction = (time.Hour + time.Minute / 60f) / 24f;
            float sunAngle = (dayFraction * 360f) - 90f;
            float daylight = ObserverLighting.Daylight(dayFraction);
            biolumNightStrength = 1f - daylight;
            ApplyBuiltInAtmosphere(daylight, dayFraction);

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(sunAngle, 30f, 0f);
                sunLight.color = Color.Lerp(new Color(0.62f, 0.78f, 1f), new Color(1f, 0.96f, 0.88f), daylight);
                sunLight.intensity = Mathf.Lerp(StylizedNightSunIntensity, StylizedDaySunIntensity, daylight);
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = Mathf.Lerp(0.22f, 0.68f, daylight);
                RenderSettings.sun = sunLight;
            }

            if (moonFillLight != null)
            {
                moonFillLight.transform.rotation = Quaternion.Euler(sunAngle + 165f, 210f, 0f);
                moonFillLight.color = new Color(0.55f, 0.68f, 0.92f);
                moonFillLight.intensity = StylizedNightMoonIntensity * (1f - daylight);
            }
        }

        private void ApplyBiolumFloraIntensity()
        {
            for (int index = 0; index < biolumFloraMaterials.Count; index++)
            {
                Material instance = biolumFloraMaterials[index];
                if (instance == null)
                {
                    continue;
                }

                SetEmissive(instance, new Color(0.15f, 0.98f, 0.82f), 420f + 680f * biolumNightStrength);
            }
        }

        private Material GetHeadquartersTemplate(string nation)
        {
            if (string.Equals(nation, "Byteria", StringComparison.Ordinal))
            {
                return materials.ByteriaHeadquarters;
            }

            if (string.Equals(nation, "Crownia", StringComparison.Ordinal))
            {
                return materials.CrowniaHeadquarters;
            }

            return materials.AnngloraHeadquarters;
        }

        private static void SetEmissive(Material material, Color color, float strength)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            float intensity = Mathf.Clamp(strength / 220f, 0f, 4f);
            material.SetColor("_EmissionColor", color * intensity);
        }

        private static void SetStandardColor(Material material, Color color)
        {
            SetMaterialColor(material, "_Color", color);
        }

        private static Color GetStandardColor(Material material)
        {
            return material != null && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        }

        private static void SetMaterialColor(Material material, string property, Color color)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        private static void SetMaterialFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetStandardFloat(Material material, string property, float value)
        {
            SetMaterialFloat(material, property, value);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collider)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            if (material != null)
            {
                primitive.GetComponent<Renderer>().sharedMaterial = material;
            }

            if (!collider)
            {
                Collider primitiveCollider = primitive.GetComponent<Collider>();
                if (primitiveCollider != null)
                {
                    DisposeGameObject(primitiveCollider);
                }
            }

            return primitive;
        }

        private static void DisposeGameObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private GameObject CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float characterSize, Color color)
        {
            EnsureLabelFont();
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.font = labelFont;
            textMesh.characterSize = characterSize;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = color;
            return labelObject;
        }

        private static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return Color.white;
            }

            if (!hex.StartsWith("#", StringComparison.Ordinal))
            {
                hex = "#" + hex;
            }

            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }

        private sealed class LabelVisual
        {
            public Transform Root;
            public Transform Headquarters;
            public Renderer HeadquartersRenderer;
            public Material HeadquartersMaterial;
            public string Nation;
            public Transform Marker;
            public string MarketCode;
            public string DisplayName;
            public Transform StructuresRoot;
            public List<Renderer> StructureBlocks;
            public int LastStructureSignature;
        }
    }
}
