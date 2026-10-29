using NUnit.Framework;
using Poi.Core;
using UnityEngine;

namespace Poi.Runtime.Tests
{
    public class MarkerViewTests
    {
        MarkerView _prefab;
        FakeIconLoader _loader;
        Transform _root;

        [SetUp]
        public void SetUp()
        {
            _prefab = TestUtil.MakeMarkerPrefab();
            _root = new GameObject("root").transform;
            _loader = new FakeIconLoader();
            _loader.Sprites["icon_a"] = TestUtil.MakeSprite("a");
            _loader.Sprites["icon_b"] = TestUtil.MakeSprite("b");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_prefab.gameObject);
            Object.DestroyImmediate(_root.gameObject);
        }

        static PoiData Poi(int id, string icon, string name = "n") => new PoiData(id, name, 0, 10, 20, icon, 1);

        [Test]
        public void Late_icon_load_after_recycle_is_ignored()
        {
            var pool = new MarkerPool(_prefab, _root, _loader, 1);
            var m = (MarkerView)pool.Get();
            m.Bind(Poi(1, "icon_a", "first"), MarkerLod.IconAndLabel);
            Assert.AreEqual(1, _loader.Requests.Count);

            // recycle before the icon arrives, reuse for another POI with another icon
            pool.Release(m);
            var again = (MarkerView)pool.Get();
            Assert.AreSame(m, again, "pool should hand back the same instance");
            again.Bind(Poi(2, "icon_b", "second"), MarkerLod.IconAndLabel);

            _loader.Complete(0); // old load for POI 1 finishes late
            Assert.IsNull(again.CurrentIcon, "stale icon must not show");

            _loader.Complete(1);
            Assert.AreEqual("b", again.CurrentIcon.name);
            Assert.AreEqual(2, again.PoiId);
            Assert.AreEqual("second", again.CurrentLabel);
        }

        [Test]
        public void Late_icon_load_after_poi_removed_is_ignored()
        {
            var pool = new MarkerPool(_prefab, _root, _loader, 1);
            var m = (MarkerView)pool.Get();
            m.Bind(Poi(1, "icon_a"), MarkerLod.Icon);
            pool.Release(m); // POI removed -> marker back in the pool

            _loader.Complete(0);
            Assert.IsNull(m.CurrentIcon);
            Assert.IsFalse(m.gameObject.activeSelf);
            Assert.AreEqual(-1, m.PoiId);
        }

        [Test]
        public void Icon_key_change_ignores_older_load()
        {
            var pool = new MarkerPool(_prefab, _root, _loader, 1);
            var m = (MarkerView)pool.Get();
            m.Bind(Poi(1, "icon_a"), MarkerLod.Icon);
            m.Apply(Poi(1, "icon_b"), MarkerLod.Icon); // same POI changed icon while loading

            _loader.Complete(1);
            _loader.Complete(0); // older request arrives last
            Assert.AreEqual("b", m.CurrentIcon.name);
        }

        [Test]
        public void Lod_switches_icon_and_label()
        {
            var pool = new MarkerPool(_prefab, _root, _loader, 1);
            var m = (MarkerView)pool.Get();
            m.Bind(Poi(1, "icon_a"), MarkerLod.IconAndLabel);
            Assert.IsTrue(m.IconVisible);
            Assert.IsTrue(m.LabelVisible);

            m.Apply(Poi(1, "icon_a"), MarkerLod.Icon);
            Assert.IsTrue(m.IconVisible);
            Assert.IsFalse(m.LabelVisible);
            Assert.AreEqual(1, _loader.Requests.Count, "same icon key should not load again");
        }

        [Test]
        public void Pool_prewarms_and_reuses()
        {
            var pool = new MarkerPool(_prefab, _root, _loader, 5);
            Assert.AreEqual(5, pool.CreatedCount);
            Assert.AreEqual(5, pool.FreeCount);

            var a = pool.Get();
            pool.Release(a);
            for (int i = 0; i < 20; i++) pool.Release(pool.Get());
            Assert.AreEqual(5, pool.CreatedCount, "get/release cycles must not create new markers");
        }
    }
}
