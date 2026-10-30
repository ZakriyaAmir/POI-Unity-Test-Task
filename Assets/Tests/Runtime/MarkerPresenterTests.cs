using System;
using NUnit.Framework;
using Poi.Core;

namespace Poi.Runtime.Tests
{
    public class MarkerPresenterTests
    {
        PoiStore _store;
        MarkerSelector _selector;
        FakePool _pool;
        MarkerPresenter _presenter;
        static readonly GroundRect View = new GroundRect(-500, -500, 500, 500);

        [SetUp]
        public void SetUp()
        {
            _store = new PoiStore(new SpatialGrid(-8000, 16000, 200));
            _selector = new MarkerSelector(_store, 10) { IconRange = 400, LabelRange = 50 };
            _pool = new FakePool();
            _presenter = new MarkerPresenter(_store, _selector, _pool);
        }

        void Add(int id, float x, float z, int prio = 1, int type = 0) =>
            _store.Add(new PoiData(id, "p" + id, type, x, z, "icon_0", prio));

        [Test]
        public void Same_frame_twice_does_not_rebind()
        {
            for (int i = 0; i < 5; i++) Add(i, i * 10, 0);
            _presenter.Refresh(0, 0, View);
            _presenter.Refresh(0, 0, View);
            Assert.AreEqual(5, _presenter.ActiveCount);
            Assert.AreEqual(5, _pool.Gets);
            Assert.AreEqual(0, _pool.Releases);
        }

        [Test]
        public void Leaving_view_or_removed_releases_marker()
        {
            Add(1, 10, 0);
            Add(2, 300, 0);
            _presenter.Refresh(0, 0, View);
            Assert.AreEqual(2, _presenter.ActiveCount);

            _store.Remove(1);
            _presenter.Refresh(0, 0, new GroundRect(-500, -500, 100, 500)); // 2 is now outside the view
            Assert.AreEqual(0, _presenter.ActiveCount);
            Assert.AreEqual(2, _pool.Releases);
        }

        [Test]
        public void Lod_follows_ground_distance_from_player()
        {
            Add(1, 20, 0);
            _presenter.Refresh(0, 0, View);
            Assert.IsTrue(_presenter.IsShowing(1));

            // move the player away: label range 50 -> icon only, icon range 400 -> nothing
            var view = FindView(1);
            Assert.AreEqual(MarkerLod.IconAndLabel, view.Lod);
            _presenter.Refresh(200, 0, new GroundRect(-1000, -1000, 1000, 1000));
            Assert.AreEqual(MarkerLod.Icon, view.Lod);
            _presenter.Refresh(600, 0, new GroundRect(-1000, -1000, 1000, 1000));
            Assert.IsFalse(_presenter.IsShowing(1));
        }

        [Test]
        public void Filters_apply_on_next_refresh()
        {
            Add(1, 10, 0, prio: 1, type: 0);
            Add(2, 20, 0, prio: 4, type: 1);
            _presenter.Refresh(0, 0, View);
            Assert.AreEqual(2, _presenter.ActiveCount);

            _selector.SetTypeEnabled(1, false);
            _presenter.Refresh(0, 0, View);
            Assert.IsFalse(_presenter.IsShowing(2));

            _selector.EnableAllTypes();
            _selector.MinPriority = 3;
            _presenter.Refresh(0, 0, View);
            Assert.IsFalse(_presenter.IsShowing(1));
            Assert.IsTrue(_presenter.IsShowing(2));
        }

        [Test]
        public void Budget_holds_while_moving_and_churning_and_pool_stays_bounded()
        {
            var sim = new PoiFeedSimulator(-8000, 16000, new Random(3), 4096)
            {
                TargetCount = 20000, MaxCountChangePerTick = 20000,
                AddsPerSecond = 3000, RemovesPerSecond = 3000, MoveShare = 0.05f, MoveSpeed = 50f
            };
            _store.Attach(sim);
            _selector.Budget = 50;
            _selector.IconRange = 2000;

            float px = 0;
            for (int frame = 0; frame < 300; frame++)
            {
                sim.Tick(1f / 60f);
                px += 20f;
                if (px > 7000) px = -7000;
                _presenter.Refresh(px, 0, new GroundRect(px - 1500, -1500, px + 1500, 1500));
                Assert.LessOrEqual(_presenter.ActiveCount, 50);
                Assert.AreEqual(_pool.Gets - _pool.Releases, _presenter.ActiveCount);
            }
            Assert.LessOrEqual(_pool.Created, 50 * 2, "pool should recycle, not keep creating");
        }

        [Test]
        public void Refresh_does_not_allocate_after_warmup()
        {
            var rng = new Random(5);
            for (int i = 0; i < 5000; i++) Add(i, (float)rng.NextDouble() * 4000 - 2000, (float)rng.NextDouble() * 4000 - 2000, rng.Next(6));
            _selector.Budget = 200;
            var rect = new GroundRect(-1000, -1000, 1000, 1000);
            for (int i = 0; i < 50; i++) _presenter.Refresh(i * 5, 0, rect);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) _presenter.Refresh((i % 50) * 5, 0, rect);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before);
        }

        FakeView FindView(int id)
        {
            foreach (var v in _pool.Handed) if (v.PoiId == id) return v;
            return null;
        }
    }
}
