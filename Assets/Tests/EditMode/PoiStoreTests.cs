using System;
using System.Collections.Generic;
using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class PoiStoreTests
    {
        class FakeFeed : IPoiFeed
        {
            public event Action<PoiData> Added;
            public event Action<int, float, float> Moved;
            public event Action<PoiData> Changed;
            public event Action<int> Removed;
            public void Add(PoiData p) => Added?.Invoke(p);
            public void Move(int id, float x, float z) => Moved?.Invoke(id, x, z);
            public void Change(PoiData p) => Changed?.Invoke(p);
            public void Remove(int id) => Removed?.Invoke(id);
        }

        static PoiStore NewStore() => new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));

        [Test]
        public void FeedEvents_KeepStoreAndGridInSync()
        {
            var feed = new FakeFeed();
            var store = NewStore();
            store.Attach(feed);

            feed.Add(new PoiData(1, "a", 0, 10, 10, "i", 1));
            Assert.AreEqual(1, store.Count);
            Assert.IsTrue(store.Grid.Contains(1));

            feed.Move(1, 3000, -3000);
            store.TryGet(1, out var p);
            Assert.AreEqual(3000f, p.X);
            store.Grid.TryGetPosition(1, out var gx, out var gz);
            Assert.AreEqual(3000f, gx); Assert.AreEqual(-3000f, gz);

            feed.Change(new PoiData(1, "a", 2, 3000, -3000, "i2", 5));
            store.TryGet(1, out p);
            Assert.AreEqual(5, p.Priority);
            Assert.AreEqual(2, p.Type);
            Assert.AreEqual("i2", p.IconKey);
        }

        [Test]
        public void FeedRemove_AndDetach()
        {
            var feed = new FakeFeed();
            var store = NewStore();
            store.Attach(feed);
            feed.Add(new PoiData(1, "a", 0, 0, 0, "i", 1));
            feed.Remove(1);
            Assert.AreEqual(0, store.Count);
            Assert.IsFalse(store.Grid.Contains(1));

            store.Detach();
            feed.Add(new PoiData(2, "b", 0, 0, 0, "i", 1));
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void DirectApi_Works()
        {
            var store = NewStore();
            store.Add(new PoiData(5, "x", 0, 1, 1, "i", 0));
            store.Move(5, 100, 100);
            store.Move(99, 1, 1); // unknown id is ignored
            var list = new List<int>();
            store.Grid.QueryRect(50, 50, 150, 150, list);
            CollectionAssert.AreEqual(new[] { 5 }, list);
            Assert.IsTrue(store.Remove(5));
            Assert.IsFalse(store.Remove(5));
        }
    }
}
