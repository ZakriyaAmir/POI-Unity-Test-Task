using System;
using System.Collections.Generic;
using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class PoiFeedSimulatorTests
    {
        const float Dt = 1f / 60f;

        [Test]
        public void GrowsAndShrinks_SpreadOverTicks()
        {
            var sim = new PoiFeedSimulator(-8000f, 16000f, new Random(1));
            var store = new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));
            store.Attach(sim);

            sim.TargetCount = 250000;
            sim.MaxCountChangePerTick = 10000;
            sim.Tick(Dt);
            Assert.AreEqual(10000, sim.Count); // not all at once
            for (int i = 0; i < 30; i++) sim.Tick(Dt);
            Assert.AreEqual(250000, sim.Count);
            Assert.AreEqual(250000, store.Count);

            sim.TargetCount = 1000;
            for (int i = 0; i < 30; i++) sim.Tick(Dt);
            Assert.AreEqual(1000, sim.Count);
            Assert.AreEqual(1000, store.Count);
            Assert.AreEqual(1000, store.Grid.Count);
        }

        [Test]
        public void ChurnRates_FollowFakeClock()
        {
            var sim = new PoiFeedSimulator(-8000f, 16000f, new Random(2));
            int added = 0, removed = 0;
            sim.TargetCount = 1000;
            sim.Tick(Dt);
            sim.Added += _ => added++;
            sim.Removed += _ => removed++;

            sim.TargetCount = int.MinValue; // turn off target so only churn counts
            sim.MaxCountChangePerTick = 0;
            sim.AddsPerSecond = 30;
            sim.RemovesPerSecond = 12;
            for (int i = 0; i < 100; i++) sim.Tick(0.1f); // 10 seconds
            Assert.That(added, Is.InRange(299, 300));
            Assert.That(removed, Is.InRange(119, 120));
        }

        [Test]
        public void MoveShare_MovesThatManyPerTick_AndRotates()
        {
            var sim = new PoiFeedSimulator(-8000f, 16000f, new Random(3));
            sim.TargetCount = 1000;
            sim.Tick(Dt);
            var movedIds = new HashSet<int>();
            int moves = 0;
            sim.Moved += (id, x, z) => { moves++; movedIds.Add(id); };
            sim.MoveShare = 0.1f;
            sim.Tick(Dt);
            Assert.AreEqual(100, moves);
            for (int i = 0; i < 9; i++) sim.Tick(Dt);
            Assert.AreEqual(1000, movedIds.Count); // window went over everyone
        }

        [Test]
        public void Moves_StayInsideWorld()
        {
            var sim = new PoiFeedSimulator(-100f, 200f, new Random(4));
            sim.TargetCount = 200;
            sim.MoveShare = 1f;
            sim.MoveSpeed = 500f;
            bool outside = false;
            sim.Moved += (id, x, z) => { if (x < -100 || x > 100 || z < -100 || z > 100) outside = true; };
            for (int i = 0; i < 200; i++) sim.Tick(Dt);
            Assert.IsFalse(outside);
        }

        [Test]
        public void SteadyMoves_DoNotAllocate()
        {
            var sim = new PoiFeedSimulator(-8000f, 16000f, new Random(5));
            var store = new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));
            store.Attach(sim);
            sim.TargetCount = 25000;
            sim.MaxCountChangePerTick = 25000;
            sim.MoveShare = 0.2f;
            for (int i = 0; i < 20; i++) sim.Tick(Dt); // warm-up
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) sim.Tick(Dt);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
