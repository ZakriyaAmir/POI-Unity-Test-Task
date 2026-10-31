using Poi.Core;
using UnityEngine;

namespace Poi.Runtime
{
    // Quick IMGUI control panel + stats. IMGUI allocates every frame by itself,
    // so hide it (H) when checking GC alloc. The benchmark turns it off.
    public class DemoUi : MonoBehaviour
    {
        public PoiDemo Demo;
        public PlayerController Player;
        public TopDownCamera Camera;
        public bool Visible = true;

        const int TypeCount = 8;
        readonly bool[] _types = { true, true, true, true, true, true, true, true };
        int _minPriority;
        int _lastApiId = -1;
        float _frameMsSmooth;
        Vector2 _scroll;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) Visible = !Visible;
            _frameMsSmooth = Mathf.Lerp(_frameMsSmooth, Time.unscaledDeltaTime * 1000f, 0.05f);
        }

        void OnGUI()
        {
            if (!Visible || Demo == null || !Demo.Ready) return;
            var sim = Demo.Simulator;

            GUILayout.BeginArea(new Rect(10, 10, 330, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawStats();
            DrawFeedControls(sim);
            DrawMarkerControls();
            DrawApiButtons();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawStats()
        {
            GUILayout.Label($"frame {_frameMsSmooth:F2} ms | POIs {Demo.Store.Count:N0}");
            GUILayout.Label($"active markers {Demo.Presenter.ActiveCount} / {Demo.Selector.Budget}  candidates {Demo.Selector.LastCandidateCount}");
            GUILayout.Label($"lookup {Demo.LastSelectMs:F3} ms  apply {Demo.LastApplyMs:F3} ms  feed {Demo.LastFeedMs:F3} ms");
            GUILayout.Label($"GC alloc in POI update: {Demo.LastUpdateAllocBytes} B");
            GUILayout.Label("WASD move, Shift fast, Q/E or wheel height, H hide");
        }

        void DrawFeedControls(PoiFeedSimulator sim)
        {
            GUILayout.Space(6);
            GUILayout.Label($"Target POI count: {sim.TargetCount:N0}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1k")) sim.TargetCount = 1000;
            if (GUILayout.Button("25k")) sim.TargetCount = 25000;
            if (GUILayout.Button("250k")) sim.TargetCount = 250000;
            if (GUILayout.Button("0")) sim.TargetCount = 0;
            GUILayout.EndHorizontal();
            sim.TargetCount = (int)GUILayout.HorizontalSlider(sim.TargetCount, 0, 250000);

            GUILayout.Label($"Adds/s {sim.AddsPerSecond:F0}");
            sim.AddsPerSecond = GUILayout.HorizontalSlider(sim.AddsPerSecond, 0, 2000);
            GUILayout.Label($"Removes/s {sim.RemovesPerSecond:F0}");
            sim.RemovesPerSecond = GUILayout.HorizontalSlider(sim.RemovesPerSecond, 0, 2000);
            GUILayout.Label($"Moving share {sim.MoveShare * 100f:F1}%");
            sim.MoveShare = GUILayout.HorizontalSlider(sim.MoveShare, 0, 0.2f);
            GUILayout.Label($"Move speed {sim.MoveSpeed:F0} m/s");
            sim.MoveSpeed = GUILayout.HorizontalSlider(sim.MoveSpeed, 0, 100);
        }

        void DrawMarkerControls()
        {
            GUILayout.Space(6);
            GUILayout.Label($"Marker budget N = {Demo.Selector.Budget}");
            int budget = (int)GUILayout.HorizontalSlider(Demo.Selector.Budget, 0, 600);
            if (budget != Demo.Selector.Budget) Demo.SetBudget(budget);

            GUILayout.Label("Types");
            GUILayout.BeginHorizontal();
            for (int t = 0; t < TypeCount; t++)
            {
                bool on = GUILayout.Toggle(_types[t], t.ToString());
                if (on != _types[t]) { _types[t] = on; Demo.SetTypeEnabled(t, on); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Min priority {_minPriority}");
            int mp = (int)GUILayout.HorizontalSlider(_minPriority, 0, 5);
            if (mp != _minPriority) { _minPriority = mp; Demo.SetMinPriority(mp); }

            GUILayout.Label($"Camera height {Camera.Height:F0} m");
            Camera.Height = GUILayout.HorizontalSlider(Camera.Height, Camera.MinHeight, Camera.MaxHeight);
            Player.AutoRun = GUILayout.Toggle(Player.AutoRun, "Auto run");
            bool declutter = GUILayout.Toggle(Demo.DeclutterLabels, "Hide overlapping labels");
            if (declutter != Demo.DeclutterLabels) Demo.SetDeclutterOn(declutter);
        }

        // add / move / remove one POI through the public API
        void DrawApiButtons()
        {
            GUILayout.Space(6);
            GUILayout.Label("Public API test");
            var pp = Player.transform.position;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add here"))
                _lastApiId = Demo.AddPoi("Added by hand", 0, pp.x + 20f, pp.z, "icon_0", 5);
            GUI.enabled = _lastApiId >= 0;
            if (GUILayout.Button("Move here")) Demo.MovePoi(_lastApiId, pp.x + 20f, pp.z + 20f);
            if (GUILayout.Button("Remove")) { Demo.RemovePoi(_lastApiId); _lastApiId = -1; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}
