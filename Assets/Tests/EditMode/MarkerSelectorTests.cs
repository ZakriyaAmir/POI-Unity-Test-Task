using System;
using System.Collections.Generic;
using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class MarkerSelectorTests
    {
        static PoiStore NewStore() => new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));
        const float Big = 8000f;

        static int Sel(MarkerSelector s, float px = 0, float pz = 0) => s.Select(px, pz, -Big, -Big, Big, Big);

        [Test]
        public void OrdersByPriority_ThenDistance()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 100, 0, "", 1));
            store.Add(new PoiData(2, "", 0, 50, 0, "", 1));
            store.Add(new PoiData(3, "", 0, 900, 0, "", 5));
            store.Add(new PoiData(4, "", 0, 10, 0, "", 0));
            var s = new MarkerSelector(store, 3);
            Assert.AreEqual(3, Sel(s));
            Assert.AreEqual(3, s.Results[0].Id);
            Assert.AreEqual(2, s.Results[1].Id);
            Assert.AreEqual(1, s.Results[2].Id); // id 4 dropped, lowest priority
        }

        [Test]
        public void LodRanges()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 100, 0, "", 0));  // label
            store.Add(new PoiData(2, "", 0, 1000, 0, "", 0)); // icon
            store.Add(new PoiData(3, "", 0, 3000, 0, "", 0)); // nothing
            var s = new MarkerSelector(store) { IconRange = 2000, LabelRange = 500 };
            Assert.AreEqual(2, Sel(s));
            Assert.AreEqual(MarkerLod.IconAndLabel, s.Results[0].Lod);
            Assert.AreEqual(MarkerLod.Icon, s.Results[1].Lod);
        }

        [Test]
        public void VisibleRect_Culls()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 100, 100, "", 0));
            store.Add(new PoiData(2, "", 0, -100, 100, "", 0));
            var s = new MarkerSelector(store);
            Assert.AreEqual(1, s.Select(0, 0, 0, 0, 500, 500));
            Assert.AreEqual(1, s.Results[0].Id);
        }

        [Test]
        public void Filters_ApplyOnNextSelect()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 10, 0, "", 1));
            store.Add(new PoiData(2, "", 1, 20, 0, "", 3));
            store.Add(new PoiData(3, "", 2, 30, 0, "", 5));
            var s = new MarkerSelector(store);
            Assert.AreEqual(3, Sel(s));
            s.SetTypeEnabled(1, false);
            Assert.AreEqual(2, Sel(s));
            s.MinPriority = 4;
            Assert.AreEqual(1, Sel(s));
            Assert.AreEqual(3, s.Results[0].Id);
            s.EnableAllTypes(); s.MinPriority = int.MinValue;
            Assert.AreEqual(3, Sel(s));
        }

        [Test]
        public void Budget_NeverExceeded_UnderChurn_AndMatchesBruteForce()
        {
            var sim = new PoiFeedSimulator(-8000f, 16000f, new Random(7));
            var store = NewStore();
            store.Attach(sim);
            sim.TargetCount = 50000; sim.MaxCountChangePerTick = 50000;
            sim.AddsPerSecond = 2000; sim.RemovesPerSecond = 2000;
            sim.MoveShare = 0.3f; sim.MoveSpeed = 50f;
            var s = new MarkerSelector(store, 250) { IconRange = 3000, LabelRange = 600 };
            var rng = new Random(8);
            for (int f = 0; f < 60; f++)
            {
                sim.Tick(1f / 30f);
                if (f == 30) s.Budget = 100; // change budget at runtime
                float px = (float)(rng.NextDouble() * 8000 - 4000), pz = (float)(rng.NextDouble() * 8000 - 4000);
                int n = s.Select(px, pz, px - 2500, pz - 1500, px + 2500, pz + 1500);
                Assert.LessOrEqual(n, s.Budget);
                if (f % 10 == 0) CheckBrute(store, s, px, pz, n);
            }
        }

        // compare with a full scan + sort
        static void CheckBrute(PoiStore store, MarkerSelector s, float px, float pz, int n)
        {
            var all = new List<int>();
            store.Grid.QueryRect(-9000, -9000, 9000, 9000, all);
            var ok = new List<SelectedMarker>();
            foreach (var id in all)
            {
                store.TryGet(id, out var p);
                if (p.X < px - 2500 || p.X > px + 2500 || p.Z < pz - 1500 || p.Z > pz + 1500) continue;
                float d = SpatialGrid.GroundDistanceSq(p.X, p.Z, px, pz);
                if (d > s.IconRange * s.IconRange) continue;
                ok.Add(new SelectedMarker { Id = id, Priority = p.Priority, DistanceSq = d });
            }
            ok.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority)
                : a.DistanceSq != b.DistanceSq ? a.DistanceSq.CompareTo(b.DistanceSq) : a.Id.CompareTo(b.Id));
            Assert.AreEqual(Math.Min(ok.Count, s.Budget), n);
            for (int i = 0; i < n; i++) Assert.AreEqual(ok[i].Id, s.Results[i].Id);
        }

        [Test]
        public void Select_DoesNotAllocate_AfterWarmup()
        {
            var sim = new PoiFeedSimulator(-8000f, 16000f, new Random(9));
            var store = NewStore();
            store.Attach(sim);
            sim.TargetCount = 250000; sim.MaxCountChangePerTick = 250000;
            sim.Tick(0.016f);
            var s = new MarkerSelector(store, 250, 16384);
            for (int i = 0; i < 5; i++) s.Select(i * 10, 0, -3000, -2000, 3000, 2000);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) s.Select(i * 10, 0, -3000 + i * 10, -2000, 3000 + i * 10, 2000);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.AreEqual(250, s.ResultCount);
        }
    }
}
