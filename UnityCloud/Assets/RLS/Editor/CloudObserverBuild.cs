using System;
using System.IO;
using System.Linq;
using Topoda.RLS.Observer;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Topoda.RLS.Editor
{
    public static class CloudObserverBuild
    {
        public const string ScenePath = "Assets/RLS/Scenes/RLSObserver.unity";
        private const string MaterialFolder = "Assets/RLS/Materials/Bloomville";

        public static void GenerateScene()
        {
            Directory.CreateDirectory("Assets/RLS/Scenes");
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadowDistance = 160;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.vSyncCount = 0;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Observer Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.68f, .74f, .80f);
            camera.fieldOfView = 58;
            camera.farClipPlane = 500;
            cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<ObserverCameraRig>();
            rig.Configure(camera);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1, .96f, .88f);
            sunObject.transform.rotation = Quaternion.Euler(50, 30, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .62f, .72f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .46f, .49f);
            RenderSettings.ambientGroundColor = new Color(.25f, .28f, .27f);
            BloomvilleMaterialSet materials = CreateMaterials();
            var world = new GameObject("Central Bloomville").AddComponent<BloomvilleWorldPresenter>();
            world.SetMaterials(materials);
            world.Configure(sun, null, camera, materials);
            world.gameObject.AddComponent<BloomvilleCloudBoundary>();
            var application = new GameObject("RLS Observer Application").AddComponent<ObserverApplicationController>();
            application.Configure(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RLS/Brand/favicon-darkmode-512.png"), ObserverReviewStage.Complete);
            application.WireObserverScene(world, rig);
            application.gameObject.AddComponent<CloudObserverReadback>();
            CreatePanelSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("RLS_CLOUD_SCENE_GENERATED pipeline=BuiltIn scene=" + ScenePath);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Topoda Charts Studios";
            PlayerSettings.productName = "RLS Gaia Observer";
            PlayerSettings.bundleVersion = "0.1.0-cloud.1";
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
        }

        private static void CreatePanelSettings()
        {
            const string path = "Assets/RLS/Resources/ObserverPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ObserverTheme");
            EditorUtility.SetDirty(panel);
        }

        public static void BuildWebGLIteration()
        {
            BuildWebGLPlayer(true);
        }

        public static void BuildWebGL()
        {
            BuildWebGLPlayer(false);
        }

        private static void BuildWebGLPlayer(bool iteration)
        {
            GenerateScene();
            if (iteration)
            {
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Debug);
                PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            }
            else
            {
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSpeed);
            }
            string output = ReadArgument("-buildOutput") ?? Path.GetFullPath("Builds/WebGL");
            var options = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Unity WebGL build failed: " + report.summary.result);
            string sourceCommit = Environment.GetEnvironmentVariable("RLS_BUILD_COMMIT") ?? "unavailable";
            string receipt = "{\"unityVersion\":\"" + Application.unityVersion + "\",\"pipeline\":\"BuiltIn\",\"target\":\"WebGL\",\"sourceCommit\":\"" + sourceCommit + "\",\"profile\":\"" + (iteration ? "Iteration" : "Release") + "\",\"development\":false,\"buildSeconds\":" + report.summary.totalTime.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"builtAtUtc\":\"" + DateTime.UtcNow.ToString("O") + "\",\"totalBytes\":" + report.summary.totalSize + "}";
            File.WriteAllText(Path.Combine(output, "BUILD-INFO.json"), receipt);
            string htmlPath = Path.Combine(output, "index.html");
            string html = File.ReadAllText(htmlPath);
            html = html.Replace(".then((unityInstance) => {", ".then((unityInstance) => { window.rlsUnityInstance = unityInstance;");
            html = html.Replace("canvas.style.width = \"960px\";", "canvas.style.width = \"100vw\";");
            html = html.Replace("canvas.style.height = \"600px\";", "canvas.style.height = \"100vh\";");
            html = html.Replace("</head>", "<style>html,body{margin:0;background:#000;overflow:hidden}#unity-container.unity-desktop{position:fixed;left:0;top:0;transform:none;width:100vw;height:100vh}#unity-canvas{width:100vw!important;height:100vh!important}#unity-footer{display:none}</style></head>");
            File.WriteAllText(htmlPath, html);
            Debug.Log("RLS_CLOUD_BUILD_SUCCEEDED output=" + output + " bytes=" + report.summary.totalSize);
        }

        public static void BuildWindowsValidation()
        {
            GenerateScene();
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            string output = ReadArgument("-buildOutput") ?? Path.GetFullPath("Builds/Windows/RLS.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows validation build failed: " + report.summary.result);
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static Material Lit(string name, Color color, float metallic = 0, float smoothness = .35f, Color? emission = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
            if (emission.HasValue) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission.Value); }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static BloomvilleMaterialSet CreateMaterials()
        {
            return new BloomvilleMaterialSet {
                Ground = Lit("ObserverGround", new Color(.34f,.4f,.36f)),
                Road = Lit("ObserverRoad", new Color(.22f,.26f,.3f)),
                CityHallBase = Lit("ObserverCityHallBase", new Color(.42f,.28f,.58f), .1f,.42f),
                CityHallDome = Lit("ObserverCityHallDome", new Color(.78f,.66f,.38f), .85f,.82f),
                PlazaGarden = Lit("ObserverPlazaGarden", new Color(.28f,.46f,.32f)),
                FloraCanopy = Lit("ObserverFloraCanopy", new Color(.14f,.42f,.24f)),
                FloraTrunk = Lit("ObserverFloraTrunk", new Color(.22f,.16f,.12f)),
                FloraBiolum = Lit("ObserverFloraBiolum", new Color(.08f,.22f,.18f), emission:new Color(.12f,.95f,.78f)),
                StructureBase = Lit("ObserverStructure", new Color(.42f,.46f,.52f), .12f,.48f),
                AnngloraDistrictPad = Lit("ObserverAnngloraPad", new Color(.18f,.24f,.28f)),
                ByteriaDistrictPad = Lit("ObserverByteriaPad", new Color(.12f,.2f,.32f)),
                CrowniaDistrictPad = Lit("ObserverCrowniaPad", new Color(.28f,.18f,.16f), .72f,.78f),
                AnngloraHeadquarters = Lit("ObserverAnngloraHQ", new Color(.46f,.58f,.72f), .12f,.44f),
                ByteriaHeadquarters = Lit("ObserverByteriaHQ", new Color(.22f,.48f,.72f), .35f,.72f),
                CrowniaHeadquarters = Lit("ObserverCrowniaHQ", new Color(.72f,.48f,.28f), .92f,.86f),
                AnngloraWindow = Lit("ObserverAnngloraWindow", new Color(.06f,.1f,.12f), emission:new Color(.15f,.45f,.36f)),
                ByteriaNeonWindow = Lit("ObserverByteriaNeonWindow", new Color(.04f,.08f,.14f), emission:new Color(.2f,.65f,1)),
                CrowniaReflectiveWindow = Lit("ObserverCrowniaWindow", new Color(.62f,.48f,.32f), .88f,.9f),
                CanalReflect = Lit("ObserverCanalReflect", new Color(.08f,.18f,.28f), .15f,.82f)
            };
        }
    }
}
