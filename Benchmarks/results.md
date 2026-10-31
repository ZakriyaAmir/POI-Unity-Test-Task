# POI benchmark results

- Date: 2026-10-31 20:28
- Unity 2022.3.62f2, editor: True, batch mode: False, graphics: Metal
- CPU: Apple M1 x8, RAM 8192 MB
- Player auto-running at 80 m/s, camera height 400 m (pitch 90), budget 250, icon range 350 m, label range 80 m
- Feed: 5% of POIs move every frame, 200 adds/s + 200 removes/s
- 300 warm-up frames, 1500 measured frames per row

| POIs in world | Avg frame time | 95th pct frame time | Lookup cost per frame (ms) | GC alloc per frame after warm-up | Active markers |
|---|---|---|---|---|---|
| 1,000 (+seed, actual 1,400) | 1.08 ms | 1.30 ms | 0.005 | 0.0 B | 24 |
| 25,000 (+seed, actual 25,400) | 1.45 ms | 1.75 ms | 0.014 | 0.0 B | 46 |
| 250,000 (+seed, actual 250,400) | 4.57 ms | 5.17 ms | 0.163 | 0.0 B | 250 |

Extra detail:

| POIs | max frame (ms) | avg feed tick (ms) | avg marker apply (ms) | avg candidates | GC alloc in POI code per frame (B) | total GC bytes over measured frames |
|---|---|---|---|---|---|---|
| 1,000 | 7.55 | 0.010 | 0.013 | 25 | 0.0 | 0 |
| 25,000 | 8.49 | 0.153 | 0.030 | 48 | 0.0 | 0 |
| 250,000 | 11.50 | 1.485 | 0.245 | 332 | 0.0 | 0 |

Profiler capture: Benchmarks/profile_250k.raw
