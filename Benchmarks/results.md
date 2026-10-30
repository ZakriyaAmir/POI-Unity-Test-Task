# POI benchmark results

- Date: 2026-10-28 09:30
- Unity 2022.3.62f2, editor: True, batch mode: True, graphics: Null
- CPU: Intel(R) Xeon(R) Processor x8, RAM 16002 MB
- Player auto-running at 80 m/s, camera height 400 m (pitch 90), budget 250, icon range 350 m, label range 80 m
- Feed: 5% of POIs move every frame, 200 adds/s + 200 removes/s
- 300 warm-up frames, 1500 measured frames per row

| POIs in world | Avg frame time | 95th pct frame time | Lookup cost per frame (ms) | GC alloc per frame after warm-up | Active markers |
|---|---|---|---|---|---|
| 1,000 (+seed, actual 1,400) | 0.13 ms | 0.16 ms | 0.003 | 0.0 B | 23 |
| 25,000 (+seed, actual 25,400) | 0.31 ms | 0.71 ms | 0.009 | 0.0 B | 49 |
| 250,000 (+seed, actual 250,400) | 1.34 ms | 1.76 ms | 0.108 | 0.0 B | 250 |

Extra detail:

| POIs | max frame (ms) | avg feed tick (ms) | avg marker apply (ms) | avg candidates | GC alloc in POI code per frame (B) | total GC bytes over measured frames |
|---|---|---|---|---|---|---|
| 1,000 | 9.25 | 0.004 | 0.008 | 23 | 0.0 | 0 |
| 25,000 | 6.99 | 0.094 | 0.019 | 50 | 0.0 | 0 |
| 250,000 | 7.80 | 0.786 | 0.157 | 313 | 0.0 | 0 |

Profiler capture: Benchmarks/profile_250k.raw
