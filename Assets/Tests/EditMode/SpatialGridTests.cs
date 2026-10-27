using System;
using System.Collections.Generic;
using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class SpatialGridTests
    {
        const float WorldMin = -8000f;
        const float WorldSize = 16000f;

        SpatialGrid NewGrid() => new SpatialGrid(WorldMin, WorldSize, 250f);

        [Test]
        public void Insert_ThenQueryRect_FindsIt()
        {
            var grid = NewGrid();
            grid.Insert(1, 100f, 100f);
            grid.Insert(2, 5000f, 5000f);

            var results = new List<int>();
            grid.QueryRect(0f, 0f, 200f, 200f, results);

            CollectionAssert.AreEquivalent(new[] { 1 }, results);
        }

        [Test]
        public void Move_AcrossCells_UpdatesQueries()
        {
            var grid = NewGrid();
            grid.Insert(1, 10f, 10f);
            grid.Move(1, 3000f, -2000f);

            var results = new List<int>();
            grid.QueryRect(0f, 0f, 100f, 100f, results);
            Assert.IsEmpty(results);

            grid.QueryRect(2900f, -2100f, 3100f, -1900f, results);
            CollectionAssert.AreEquivalent(new[] { 1 }, results);
        }

        [Test]
        public void Remove_TakesItOut_AndKeepsOthers()
        {
            var grid = NewGrid();
            grid.Insert(1, 10f, 10f);
            grid.Insert(2, 20f, 20f);
            grid.Insert(3, 30f, 30f);

            Assert.IsTrue(grid.Remove(1));
            Assert.IsFalse(grid.Remove(1));
            Assert.AreEqual(2, grid.Count);

            var results = new List<int>();
            grid.QueryRect(0f, 0f, 100f, 100f, results);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, results);

            // the swapped entries should still be removable
            grid.Remove(3);
            results.Clear();
            grid.QueryRect(0f, 0f, 100f, 100f, results);
            CollectionAssert.AreEquivalent(new[] { 2 }, results);
        }

        [Test]
        public void QueryCircle_UsesGroundDistance()
        {
            var grid = NewGrid();
            grid.Insert(1, 0f, 0f);
            grid.Insert(2, 70f, 70f);  // ~99m away
            grid.Insert(3, 80f, 80f);  // ~113m away

            var results = new List<int>();
            grid.QueryCircle(0f, 0f, 100f, results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void OutsideWorld_IsClampedNotLost()
        {
            var grid = NewGrid();
            grid.Insert(1, 9000f, -9000f);
            var results = new List<int>();
            grid.QueryRect(8500f, -9500f, 9500f, -8500f, results);
            CollectionAssert.AreEquivalent(new[] { 1 }, results);
        }

        [Test]
        public void LargeCount_RandomChurn_MatchesBruteForce()
        {
            var rng = new Random(1234);
            var grid = NewGrid();
            var truth = new Dictionary<int, (float x, float z)>();
            int nextId = 0;

            float Rand() => WorldMin + (float)rng.NextDouble() * WorldSize;

            for (int i = 0; i < 50000; i++)
            {
                float x = Rand(), z = Rand();
                grid.Insert(nextId, x, z);
                truth[nextId] = (x, z);
                nextId++;
            }

            var ids = new List<int>(truth.Keys);
            for (int step = 0; step < 20000; step++)
            {
                int id = ids[rng.Next(ids.Count)];
                int op = rng.Next(3);
                if (op == 0 && truth.ContainsKey(id))
                {
                    // small move, sometimes across a cell border
                    var p = truth[id];
                    float nx = p.x + (float)(rng.NextDouble() - 0.5) * 600f;
                    float nz = p.z + (float)(rng.NextDouble() - 0.5) * 600f;
                    grid.Move(id, nx, nz);
                    truth[id] = (nx, nz);
                }
                else if (op == 1 && truth.ContainsKey(id))
                {
                    grid.Remove(id);
                    truth.Remove(id);
                }
                else
                {
                    float x = Rand(), z = Rand();
                    grid.Insert(nextId, x, z);
                    truth[nextId] = (x, z);
                    ids.Add(nextId);
                    nextId++;
                }
            }

            Assert.AreEqual(truth.Count, grid.Count);

            var results = new List<int>();
            var expected = new List<int>();
            for (int q = 0; q < 50; q++)
            {
                float cx = Rand(), cz = Rand(), half = 200f + (float)rng.NextDouble() * 800f;

                results.Clear(); expected.Clear();
                grid.QueryRect(cx - half, cz - half, cx + half, cz + half, results);
                foreach (var kv in truth)
                {
                    var p = kv.Value;
                    if (p.x >= cx - half && p.x <= cx + half && p.z >= cz - half && p.z <= cz + half)
                        expected.Add(kv.Key);
                }
                CollectionAssert.AreEquivalent(expected, results, "rect query " + q);

                results.Clear(); expected.Clear();
                grid.QueryCircle(cx, cz, half, results);
                foreach (var kv in truth)
                {
                    var p = kv.Value;
                    if (SpatialGrid.GroundDistanceSq(p.x, p.z, cx, cz) <= half * half)
                        expected.Add(kv.Key);
                }
                CollectionAssert.AreEquivalent(expected, results, "circle query " + q);
            }
        }
    }
}
