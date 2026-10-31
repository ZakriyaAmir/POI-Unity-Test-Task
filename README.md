# POI Unity Test Task

Small Unity 2022.3.62f2 prototype of a Points of Interest marker system for a 16 km x 16 km world,
with a live dataset (POIs get added, moved and removed all the time) and a top-down camera.

## How to run

1. Clone, open the folder with Unity **2022.3.62f2** (first open imports packages, takes a bit).
2. Open `Assets/Scenes/PoiDemo.unity` and press **Play**. Nothing else to set up.

Addressables is set to "Use Asset Database" play mode, so it works in the editor without building content.
(For a player build you would need to build the Addressables content first, I did not test a build.)

Controls: WASD / arrows move, Shift = 4x faster, Q/E or mouse wheel = camera height (50 - 400 m), H = hide the panel.
The panel on the left lets you change at runtime: target POI count (1k / 25k / 250k buttons or slider), adds/s,
removes/s, moving share, move speed, budget N, type filters, min priority, camera height, auto run, and has
Add / Move / Remove buttons that go through the public API (`PoiDemo.AddPoi / MovePoi / RemovePoi`).
Stats at the top: frame ms, POI count, active markers, candidates, lookup ms, apply ms, feed ms and GC bytes
allocated by the POI update.

Note: IMGUI itself allocates every frame while the panel is visible. The "GC alloc in POI update" number only
counts our Update, hide the panel (H) if you look at GC alloc in the Profiler.

**Tests:** Window > General > Test Runner > EditMode > Run All. Or headless:

```
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

**Benchmark:** tick `Run On Start` on the `BenchmarkRunner` component in the scene and press Play, or headless:

```
Unity -batchmode -nographics -projectPath . -executeMethod Poi.EditorTools.BenchmarkLauncher.Run -poiBenchmark -logFile bench.log
```

It writes `Benchmarks/results.md` and a profiler capture `Benchmarks/profile_250k.raw`
(open it in Window > Analysis > Profiler > Load).

### Config asset

All main settings live in `Assets/Data/PoiConfig.asset` (make more with Create > POI > Poi Config). PoiDemo copies
them into itself, the camera, the edge indicators and the click panel at startup in `ApplyConfig()`. Leave the Config
slot empty and the scripts just use their own Inspector values. Changes while playing (panel, API) still go through
PoiDemo / the simulator like before.

## Design

Three layers, each its own assembly:

- **Poi.Core** (`Assets/Scripts/Core`, no UnityEngine reference at all) - data and logic.
  - `PoiData` plain struct (id, name, type, x/z, iconKey, priority).
  - `IPoiFeed` with Added / Moved / Changed / Removed events, `PoiFeedSimulator` implements it.
  - `SpatialGrid` uniform grid (200 m cells -> 80 x 80 cells), updated incrementally.
  - `PoiStore` keeps a dictionary of POIs and the grid in sync, listens to a feed, also has Add/Move/Update/Remove for direct use.
  - `MarkerSelector` answers "which POIs get a marker right now" + LOD + filters + budget.
  - `GroundRect` camera footprint math.
- **Poi.Runtime** (`Assets/Scripts/Runtime`) - Unity side.
  - `JsonPoiSeedSource` reads `Assets/Data/poi_seed.json` (400 POIs, 8 types, priority 0-5).
  - `MarkerView` (prefab script) icon `SpriteRenderer` + `TextMesh` label. `MarkerPool` prewarmed stack pool.
  - `IIconLoader` / `AddressablesIconLoader` async icon loads through Addressables.
  - `MarkerPresenter` diffs the selector result against active markers by POI id.
  - `TopDownCamera`, `PlayerController`, `DemoUi`, `BenchmarkRunner`.
  - `PoiDemo` is the composition root: it creates everything and passes dependencies in through constructors.
- **Poi.Editor** - `PoiProjectSetup` (menu POI > Rebuild demo assets) that generated the placeholder icons,
  marker prefab, addressable entries and the scene. Output is committed, you don't need to run it.

### Per frame

1. `PoiFeedSimulator.Tick` -> events -> `PoiStore` updates dictionary + grid (O(1) each).
2. `TopDownCamera` gives the visible ground rectangle.
3. `MarkerSelector.Select`: intersect visible rect with the box around the player of size icon range,
   query only the grid cells under that box, drop the ones filtered out by type / min priority / range,
   keep the best N in a bounded heap (priority first, then ground distance, then id so it's stable).
4. `MarkerPresenter`: already shown -> update position/LOD, new -> take from pool and bind, not selected anymore -> back to pool.

### Markers and LOD

Distance is measured on the ground from the player (x/z), not from the camera.
- ground distance > icon range (350 m) -> no marker
- between label range (80 m) and icon range -> icon
- < 80 m -> icon + label
- outside the camera rect -> no marker at any range

With the camera at 400 m the visible area is about 820 x 460 m (60 deg FOV, 16:9), so at max height all three
cases are on screen at once. Camera pitch is exactly **90 deg** (straight down), perspective camera.
The ranges are fields on `PoiDemo`.

Only POIs that have a marker exist as GameObjects (at most N, from the pool). Selection uses only the grid, no physics.

### Stale icons

Every `MarkerView` has a version number which goes up on Bind, Unbind and when the icon key changes. The icon
request carries the version, and when the load completes the marker ignores it if the version doesn't match.
So a late load after the marker was recycled to another POI, or the POI was removed, does nothing.
`AddressablesIconLoader` loads every icon key once, keeps the sprite and releases all handles in `Dispose`
(there are only 8 icon keys so caching them is cheaper than load/release per marker).
The marker prefab is also loaded via Addressables and released in `PoiDemo.OnDestroy`.

## Cost of the lookup

- Insert / remove / move: O(1) (dictionary + swap-remove in the cell list). Moving only touches cells when the POI crosses a cell border.
- Lookup: **O(C + K + K log N)**, C = cells overlapping the query box (bounded by camera height and icon range, at most ~4 x 4 = 16 cells here),
  K = POIs inside those cells, N = budget. **It does not depend on the total number of POIs in the world**, only on how many are near the camera.

Honest caveat: with POIs spread uniformly, more POIs in the world also means more POIs in the visible area,
so K grows with density. That's why lookup is 0.009 ms at 25k and 0.108 ms at 250k (about 50 vs 313 candidates).
The work per frame follows K (what is in view), not the world total - you can see that in the "avg candidates" column.
If the world got 10x bigger with the same density, the lookup would stay the same.

## Measurements

Measured on this machine: Apple M1 x8, RAM 8192 MB
**In the editor, in play mode, in batch mode with `-nographics`** (graphics device Null). That means there is no
rendering and no GPU cost in these numbers, it's basically the CPU cost of the scripts plus Unity's player loop.
In a normal editor with rendering the frame times will be higher; the lookup and GC columns should be similar.
No build was measured.

Setup: player auto-running at 80 m/s, camera at 400 m (widest), budget 250, 5% of POIs moving every frame,
200 adds/s + 200 removes/s, 300 warm-up frames then 1500 measured frames. The seed file adds 400 POIs on top of the simulator count.

| POIs in world | Avg frame time | 95th pct frame time | Lookup cost per frame (ms) | GC alloc per frame after warm-up | Active markers |
|---|---|---|---|---|---|
| 1,000 (+seed, actual 1,400) | 1.08 ms | 1.30 ms | 0.005 | 0.0 B | 24 |
| 25,000 (+seed, actual 25,400) | 1.45 ms | 1.75 ms | 0.014 | 0.0 B | 46 |
| 250,000 (+seed, actual 250,400) | 4.57 ms | 5.17 ms | 0.163 | 0.0 B | 250 |

Extra numbers from the same run (full output in `Benchmarks/results.md`). The machine was noisier on this run than the first one, the old code measured the same back to back, so the small rise is the machine not the cleanup.

| POIs | max frame (ms) | avg feed tick (ms) | avg marker apply (ms) | avg candidates | GC alloc in POI code per frame (B) | total GC bytes over measured frames |
|---|---|---|---|---|---|---|
| 1,000 | 7.55 | 0.010 | 0.013 | 25 | 0.0 | 0 |
| 25,000 | 8.49 | 0.153 | 0.030 | 48 | 0.0 | 0 |
| 250,000 | 11.50 | 1.485 | 0.245 | 332 | 0.0 | 0 |

- GC alloc is `GC.GetAllocatedBytesForCurrentThread()` over each whole frame (main thread), with the IMGUI panel hidden.
- At 1k and 25k there are fewer POIs in view than the budget, so active markers are below 250. At 250k the budget is full.
- Most of the 250k frame is the simulator itself: moving 5% = 12,500 POIs every frame (0.79 ms). That part is O(moving POIs)
  by definition, it's the fake service, not the lookup.
- Profiler capture for the 250k case: `Benchmarks/profile_250k.raw`, recorded in a separate pass after the measurement
  (also in batch mode without graphics, so no rendering markers in it).

## Extras (the optional "nice to have" bits)

- **Label declutter** (`LabelDeclutter`): markers come best first, so we go down the list and keep a label only if it
  doesn't overlap one we already kept. The rest show icon only. Toggle `DeclutterLabels` on PoiDemo.
- **Edge indicators** (`EdgeIndicatorFinder` + `EdgeIndicatorUi`): important POIs (priority >= 4) near the player but
  off screen get a small orange square on the screen edge pointing at them. Only looks within 1 km, max 8 at once.
- **Click for details** (`PoiPicker` + `MarkerClickDetails`): left click a marker and a panel top right shows its name,
  id, type, priority, position and icon. No colliders, the click becomes a ground spot and we ask the grid for the
  closest POI within a few meters. The panel updates live and closes if the POI gets removed.
- **Mock server feed** (`FakePoiServer` + `MockSocketFeed`): a fake server sends a starting list (like a REST call) and
  then text messages `add/move/change/remove` (like a WebSocket). Messages can come from any thread, they wait in a
  queue and get handled on the main thread. Tick `UseMockServerFeed` on PoiDemo to use it instead of the simulator
  (off by default).

## Tests

49 EditMode tests, all passing headless:
- `SpatialGridTests` insert, move across cells, remove, out of world, 50k random churn checked against brute force.
- `PoiStoreTests`, `PoiFeedSimulatorTests` feed events, grow/shrink of the count, no allocs.
- `MarkerSelectorTests` budget, priority-then-distance order, filters, LOD, churn vs brute force, no allocs at 250k.
- `GroundRectTests` camera footprint.
- `MarkerViewTests` pool reuse, stale icon after recycle / after removal / after icon change ignored (fake loader with manual completion), LOD.
- `MarkerPresenterTests` diff (no rebind), release on leaving view / removal, filters, budget under churn, no allocs.
- `LabelDeclutterTests`, `EdgeIndicatorFinderTests`, `PoiPickerTests`, `MockSocketFeedTests` for the extras.

No PlayMode tests.

## Trade-offs / limitations

- Uniform grid instead of quadtree: simpler, O(1) updates, good for a bounded 16 km world. Very uneven density
  (everything in one city) would make some cells big; a quadtree or smaller cells would help there.
- The selector runs every frame. Could be throttled, wasn't needed by the numbers.
- Labels are `TextMesh` (legacy) to avoid extra packages. Text is small at 400 m, but labels only show within 80 m anyway.
- Icons are cached per key for the session. Fine with 8 keys, with thousands of keys you'd want ref counting and release.
- POI types are limited to 0..63 (bit mask filter).
- `PoiFeedSimulator.SetPriority` is linear (debug only, not used per frame).
- Not done (optional): Jobs/Burst. The other optional extras are done, see Extras above.
- No player build tested, numbers are editor + no graphics only.
