using System.IO;
using Poi.Runtime;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Poi.EditorTools
{
    // Builds the placeholder assets once: icon sprites, marker prefab, addressable entries and the demo scene.
    // The results are committed, so you only need this if you want to regenerate them (menu POI/Rebuild demo assets).
    public static class PoiProjectSetup
    {
        const string ArtDir = "Assets/Art";
        const string IconDir = "Assets/Art/Icons";
        const string PrefabPath = "Assets/Prefabs/Marker.prefab";
        public const string ScenePath = "Assets/Scenes/PoiDemo.unity";
        const string SeedPath = "Assets/Data/poi_seed.json";

        static readonly Color[] Colors =
        {
            new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.7f, 0.2f), new Color(0.2f, 0.4f, 0.95f), new Color(0.95f, 0.8f, 0.1f),
            new Color(0.8f, 0.3f, 0.9f), new Color(0.1f, 0.85f, 0.85f), new Color(1f, 0.55f, 0.1f), new Color(0.95f, 0.95f, 0.95f)
        };

        [MenuItem("POI/Rebuild demo assets")]
        public static void Rebuild()
        {
            MakeIcons();
            MakePrefab();
            SetupAddressables();
            MakeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("POI demo assets rebuilt");
        }

        static void MakeIcons()
        {
            Directory.CreateDirectory(IconDir);
            for (int i = 0; i < Colors.Length; i++)
            {
                string path = $"{IconDir}/icon_{i}.png";
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f, dy = y - 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // filled circle with a dark ring, and i+1 dots in the middle so types differ by more than color
                    Color c = d > 31f ? Color.clear : d > 26f ? Color.black : Colors[i];
                    tex.SetPixel(x, y, c);
                }
                for (int k = 0; k <= i; k++)
                {
                    int cx = 12 + k * 5, cy = 30;
                    for (int y = cy; y < cy + 4; y++)
                    for (int x = cx; x < cx + 3; x++) tex.SetPixel(x, y, Color.black);
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Sprite;
                imp.spritePixelsPerUnit = 64;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
        }

        static void MakePrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root = new GameObject("Marker");
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, face the top-down camera
            root.transform.localScale = Vector3.one * 12f;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(root.transform, false);
            var sr = iconGo.AddComponent<SpriteRenderer>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var mr = labelGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;

            var view = root.AddComponent<MarkerView>();
            view.Setup(sr, tm, mr);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        static void SetupAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("POI") ?? settings.CreateGroup("POI", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            void Add(string path, string address)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                var e = settings.CreateOrMoveEntry(guid, group);
                e.address = address;
            }

            Add(PrefabPath, "MarkerPrefab");
            for (int i = 0; i < Colors.Length; i++) Add($"{IconDir}/icon_{i}.png", $"icon_{i}");

            settings.ActivePlayModeDataBuilderIndex = 0; // "Use Asset Database" so play mode works without a content build
            EditorUtility.SetDirty(settings);
        }

        static void MakeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(60f, 30f, 0f);

            // 16km ground, just so movement is visible (Plane is 10x10 units)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground 16km";
            ground.transform.localScale = new Vector3(1600f, 1f, 1600f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            var groundMat = new Material(Shader.Find("Unlit/Texture"));
            groundMat.mainTexture = MakeGroundTexture();
            groundMat.mainTextureScale = new Vector2(160f, 160f); // one tile = 100m
            AssetDatabase.CreateAsset(groundMat, $"{ArtDir}/Ground.mat");
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.transform.localScale = Vector3.one * 8f;
            var pc = player.AddComponent<PlayerController>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            var tdc = camGo.AddComponent<TopDownCamera>();
            tdc.Target = player.transform;
            tdc.Snap();

            var markers = new GameObject("Markers");

            var sys = new GameObject("POI System");
            var demo = sys.AddComponent<PoiDemo>();
            demo.SeedJson = AssetDatabase.LoadAssetAtPath<TextAsset>(SeedPath);
            demo.Player = player.transform;
            demo.Camera = tdc;
            demo.MarkerRoot = markers.transform;

            var ui = sys.AddComponent<DemoUi>();
            ui.Demo = demo; ui.Player = pc; ui.Camera = tdc;

            var bench = sys.AddComponent<BenchmarkRunner>();
            bench.Demo = demo; bench.Player = pc; bench.Camera = tdc; bench.Ui = ui;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static Texture2D MakeGroundTexture()
        {
            string path = $"{ArtDir}/ground_tile.png";
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                tex.SetPixel(x, y, x == 0 || y == 0 ? new Color(0.35f, 0.4f, 0.35f) : new Color(0.22f, 0.27f, 0.22f));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
