using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Poi.Runtime
{
    // runs the perf test by itself. starts when unity is launched with -poiBenchmark
    // (or RunOnStart is ticked). For each POI count: player auto-running, feed churning,
    // camera at max height, wait a bit, then measure N frames. writes Benchmarks/results.md.
    [DefaultExecutionOrder(-1000)]
    public class BenchmarkRunner : MonoBehaviour
    {
        public PoiDemo Demo;
        public PlayerController Player;
        public TopDownCamera Camera;
        public DemoUi Ui;
        public bool RunOnStart;
        public int[] Counts = { 1000, 25000, 250000 };
        public int WarmupFrames = 300;
        public int MeasureFrames = 1500;
        public int ProfilerFrames = 300;

        public static bool CommandLineRequested => Array.IndexOf(Environment.GetCommandLineArgs(), "-poiBenchmark") >= 0;

        void Start()
        {
            if (!RunOnStart && !CommandLineRequested) { enabled = false; return; }
            StartCoroutine(Run());
        }

        readonly StringBuilder _table = new StringBuilder();
        readonly StringBuilder _extra = new StringBuilder();
        float[] _frames;

        IEnumerator Run()
        {
            PrepareScene();
            while (!Demo.Ready) yield return null;

            var sim = Demo.Simulator;
            sim.MoveShare = 0.05f;
            sim.AddsPerSecond = 200f;
            sim.RemovesPerSecond = 200f;

            _frames = new float[MeasureFrames];
            WriteTableHeaders();
            foreach (int count in Counts)
                yield return StartCoroutine(MeasureOneCount(count));

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Benchmarks");
            Directory.CreateDirectory(dir);
            string capture = Path.Combine(dir, "profile_250k.raw");
            yield return StartCoroutine(CaptureProfiler(capture));

            WriteResultsFile(dir, capture);
            Quit();
        }

        // no UI, no frame cap, player running, camera at max height (most POIs in view)
        void PrepareScene()
        {
            if (Ui != null) Ui.Visible = false;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Player.AutoRun = true;
            Camera.Height = Camera.MaxHeight;
        }

        void WriteTableHeaders()
        {
            _table.AppendLine("| POIs in world | Avg frame time | 95th pct frame time | Lookup cost per frame (ms) | GC alloc per frame after warm-up | Active markers |");
            _table.AppendLine("|---|---|---|---|---|---|");
            _extra.AppendLine("| POIs | max frame (ms) | avg feed tick (ms) | avg marker apply (ms) | avg candidates | GC alloc in POI code per frame (B) | total GC bytes over measured frames |");
            _extra.AppendLine("|---|---|---|---|---|---|---|");
        }

        // wait for the count, warm up, then measure MeasureFrames frames and add one row to each table
        IEnumerator MeasureOneCount(int count)
        {
            Demo.Simulator.TargetCount = count;
            while (Math.Abs(Demo.Store.Count - count) > 500) yield return null; // seed POIs are on top
            for (int i = 0; i < WarmupFrames; i++) yield return null;

            double lookup = 0, apply = 0, feed = 0, candidates = 0, active = 0;
            long allocTotal = 0, codeAlloc = 0;
            for (int i = 0; i < MeasureFrames; i++)
            {
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                yield return null;
                allocTotal += GC.GetAllocatedBytesForCurrentThread() - a0;
                _frames[i] = Time.unscaledDeltaTime * 1000f;
                lookup += Demo.LastSelectMs;
                apply += Demo.LastApplyMs;
                feed += Demo.LastFeedMs;
                codeAlloc += Demo.LastUpdateAllocBytes;
                candidates += Demo.Selector.LastCandidateCount;
                active += Demo.Presenter.ActiveCount;
            }

            Array.Sort(_frames);
            double avg = 0;
            for (int i = 0; i < _frames.Length; i++) avg += _frames[i];
            avg /= _frames.Length;
            float p95 = _frames[(int)(_frames.Length * 0.95f)];
            float max = _frames[_frames.Length - 1];
            int n = MeasureFrames;

            _table.AppendLine($"| {count:N0} (+seed, actual {Demo.Store.Count:N0}) | {avg:F2} ms | {p95:F2} ms | {lookup / n:F3} | {(double)allocTotal / n:F1} B | {active / n:F0} |");
            _extra.AppendLine($"| {count:N0} | {max:F2} | {feed / n:F3} | {apply / n:F3} | {candidates / n:F0} | {(double)codeAlloc / n:F1} | {allocTotal} |");
            Debug.Log($"[POI bench] {count}: avg {avg:F2} ms p95 {p95:F2} lookup {lookup / n:F3} alloc/frame {(double)allocTotal / n:F1}");
        }

        // profiler capture of the last (largest) case, separate pass so the profiler doesn't skew the table
        IEnumerator CaptureProfiler(string file)
        {
            Profiler.logFile = file;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            for (int i = 0; i < ProfilerFrames; i++) yield return null;
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = "";
        }

        void WriteResultsFile(string dir, string capture)
        {
            var sim = Demo.Simulator;
            var header = new StringBuilder();
            header.AppendLine("# POI benchmark results");
            header.AppendLine();
            header.AppendLine($"- Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
            header.AppendLine($"- Unity {Application.unityVersion}, editor: {Application.isEditor}, batch mode: {Application.isBatchMode}, graphics: {SystemInfo.graphicsDeviceType}");
            header.AppendLine($"- CPU: {SystemInfo.processorType} x{SystemInfo.processorCount}, RAM {SystemInfo.systemMemorySize} MB");
            header.AppendLine($"- Player auto-running at {Player.Speed} m/s, camera height {Camera.Height} m (pitch 90), budget {Demo.Selector.Budget}, icon range {Demo.Selector.IconRange} m, label range {Demo.Selector.LabelRange} m");
            header.AppendLine($"- Feed: 5% of POIs move every frame, {sim.AddsPerSecond} adds/s + {sim.RemovesPerSecond} removes/s");
            header.AppendLine($"- {WarmupFrames} warm-up frames, {MeasureFrames} measured frames per row");
            header.AppendLine();

            string captureText = File.Exists(capture) ? "Benchmarks/profile_250k.raw" : "not written";
            File.WriteAllText(Path.Combine(dir, "results.md"),
                header + _table.ToString() + "\nExtra detail:\n\n" + _extra + "\nProfiler capture: " + captureText + "\n");
            Debug.Log("[POI bench] done, written to " + dir);
        }

        void Quit()
        {
#if UNITY_EDITOR
            if (CommandLineRequested) UnityEditor.EditorApplication.Exit(0);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
