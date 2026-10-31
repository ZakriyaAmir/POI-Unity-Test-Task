using System;
using Poi.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Poi.Runtime
{
    // Composition root of the demo scene. Builds the data/logic objects, loads the marker prefab
    // through Addressables, and runs feed -> store -> selector -> presenter every frame.
    // Also the public runtime API (add / move / remove a POI, change filters).
    // runs after the player moved, so markers match this frame's camera
    [DefaultExecutionOrder(100)]
    public class PoiDemo : MonoBehaviour
    {
        [Header("Scene refs")]
        public TextAsset SeedJson;
        public PoiConfig Config; // optional, if set its values win over the ones below
        public Transform Player;
        public TopDownCamera Camera;
        public Transform MarkerRoot;
        public string MarkerPrefabKey = "MarkerPrefab";

        [Header("World")]
        public float WorldMin = -8000f;
        public float WorldSize = 16000f;
        public float CellSize = 200f;

        [Header("Feed simulator (start values, change at runtime through Simulator)")]
        public int InitialCount = 1000;
        public float AddsPerSecond = 50f;
        public float RemovesPerSecond = 50f;
        [Range(0f, 1f)] public float MoveShare = 0.05f;
        public float MoveSpeed = 10f;

        [Header("Mock server feed (instead of the simulator)")]
        public bool UseMockServerFeed = false;
        public int MockStartCount = 1000;

        [Header("Markers")]
        public int Budget = 250;
        public float IconRange = 350f;   // ground distance from player
        public float LabelRange = 80f;
        public bool DeclutterLabels = true; // hide labels that sit on top of each other

        public PoiStore Store { get; private set; }
        public PoiFeedSimulator Simulator { get; private set; }
        public MockSocketFeed MockFeed { get; private set; }
        public FakePoiServer MockServer { get; private set; }
        public MarkerSelector Selector { get; private set; }
        public MarkerPresenter Presenter { get; private set; }
        public LabelDeclutter Declutter { get; private set; }
        public bool Ready => Presenter != null;

        // stats for the overlay / benchmark
        public double LastSelectMs => Presenter?.LastSelectMs ?? 0;
        public double LastApplyMs => Presenter?.LastApplyMs ?? 0;
        public double LastFeedMs { get; private set; }
        public long LastUpdateAllocBytes { get; private set; } // managed bytes allocated by our Update

        AddressablesIconLoader _icons;
        MarkerPool _pool;
        AsyncOperationHandle<GameObject> _prefabHandle;
        readonly System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();
        int _nextApiId = 2_000_000;

        void Awake()
        {
            ApplyConfig();
            CreateStoreAndFeed();
            Selector = new MarkerSelector(Store, Budget, 8192) { IconRange = IconRange, LabelRange = LabelRange };
            _icons = new AddressablesIconLoader();
            LoadMarkerPrefab();
        }

        // copy the numbers from the config asset into this script and the other demo scripts.
        // no config = keep whatever is set in the Inspector
        void ApplyConfig()
        {
            if (Config == null) return;

            Budget = Config.Budget;
            IconRange = Config.IconRange;
            LabelRange = Config.LabelRange;
            DeclutterLabels = Config.DeclutterLabels;

            InitialCount = Config.InitialCount;
            AddsPerSecond = Config.AddsPerSecond;
            RemovesPerSecond = Config.RemovesPerSecond;
            MoveShare = Config.MoveShare;
            MoveSpeed = Config.MoveSpeed;

            if (Camera != null)
            {
                Camera.MinHeight = Config.MinHeight;
                Camera.MaxHeight = Config.MaxHeight;
                Camera.Height = Camera.Height; // re-clamp to the new limits
            }

            var edges = GetComponent<EdgeIndicatorUi>();
            if (edges != null)
            {
                edges.MinPriority = Config.EdgeMinPriority;
                edges.SearchRange = Config.EdgeSearchRange;
                edges.MaxArrows = Config.EdgeMaxArrows;
            }

            var click = GetComponent<MarkerClickDetails>();
            if (click != null) click.ClickRadius = Config.ClickRadius;
        }

        void CreateStoreAndFeed()
        {
            var grid = new SpatialGrid(WorldMin, WorldSize, CellSize, 1 << 16);
            Store = new PoiStore(grid, 1 << 16);

            if (SeedJson != null)
                Store.AddRange(JsonPoiSeedSource.Load(SeedJson.text));

            Simulator = new PoiFeedSimulator(WorldMin, WorldSize, new System.Random(1234), 1 << 16)
            {
                TargetCount = InitialCount,
                AddsPerSecond = AddsPerSecond,
                RemovesPerSecond = RemovesPerSecond,
                MoveShare = MoveShare,
                MoveSpeed = MoveSpeed
            };

            if (UseMockServerFeed) ConnectMockServer();
            else Store.Attach(Simulator);
        }

        // the store listens to the fake server instead of the simulator
        void ConnectMockServer()
        {
            MockFeed = new MockSocketFeed();
            MockServer = new FakePoiServer(MockFeed) { StartCount = MockStartCount, WorldMin = WorldMin, WorldSize = WorldSize };
            Store.Attach(MockFeed);
            MockServer.SendStartingList();
        }

        // the pool and presenter are made once the prefab is loaded, until then Ready is false
        void LoadMarkerPrefab()
        {
            _prefabHandle = Addressables.LoadAssetAsync<GameObject>(MarkerPrefabKey);
            _prefabHandle.Completed += OnPrefabLoaded;
        }

        void OnPrefabLoaded(AsyncOperationHandle<GameObject> h)
        {
            if (this == null) return;
            if (h.Status != AsyncOperationStatus.Succeeded || h.Result.GetComponent<MarkerView>() == null)
            {
                Debug.LogError("Could not load marker prefab '" + MarkerPrefabKey + "' from Addressables");
                return;
            }
            _pool = new MarkerPool(h.Result.GetComponent<MarkerView>(), MarkerRoot, _icons, Budget);
            Presenter = new MarkerPresenter(Store, Selector, _pool);
            Declutter = new LabelDeclutter(Store);
            if (DeclutterLabels) Presenter.SetDeclutter(Declutter);
        }

        void Update()
        {
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();

            _sw.Restart();
            if (UseMockServerFeed)
            {
                MockServer.Tick(Time.deltaTime);
                MockFeed.ProcessMessages();
            }
            else
            {
                Simulator.Tick(Time.deltaTime);
            }
            LastFeedMs = _sw.Elapsed.TotalMilliseconds;

            if (Presenter != null)
            {
                Camera.Snap();
                var p = Player.position;
                Presenter.Refresh(p.x, p.z, Camera.GetVisibleGroundRect());
            }

            LastUpdateAllocBytes = GC.GetAllocatedBytesForCurrentThread() - alloc0;
        }

        void OnDestroy()
        {
            Presenter?.Clear();
            Store?.Detach();
            _icons?.Dispose();
            if (_prefabHandle.IsValid()) Addressables.Release(_prefabHandle);
        }

        // ---------- runtime API ----------

        public void SetBudget(int n)
        {
            Selector.Budget = n;
            _pool?.Prewarm(n);
        }

        public void SetDeclutterOn(bool on)
        {
            DeclutterLabels = on;
            Presenter?.SetDeclutter(on ? Declutter : null);
        }

        public void SetTypeEnabled(int type, bool on) => Selector.SetTypeEnabled(type, on);
        public void SetMinPriority(int p) => Selector.MinPriority = p;

        public int AddPoi(string name, int type, float x, float z, string iconKey, int priority)
        {
            int id = _nextApiId++;
            Store.Add(new PoiData(id, name, type, x, z, iconKey, priority));
            return id;
        }

        public void MovePoi(int id, float x, float z) => Store.Move(id, x, z);
        public bool RemovePoi(int id) => Store.Remove(id);

        public bool SetPoiPriority(int id, int priority)
        {
            if (!Store.TryGet(id, out var poi)) return false;
            poi.Priority = priority;
            Store.Update(poi);
            return true;
        }
    }
}
