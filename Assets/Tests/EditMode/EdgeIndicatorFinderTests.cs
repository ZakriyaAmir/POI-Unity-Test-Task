using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class EdgeIndicatorFinderTests
    {
        static PoiStore NewStore() => new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));
        static readonly GroundRect Screen = new GroundRect(-100, -100, 100, 100);

        [Test]
        public void OnlyOffScreenImportantPois()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 0, 0, "", 5));    // on screen
            store.Add(new PoiData(2, "", 0, 500, 0, "", 5));  // off screen, important
            store.Add(new PoiData(3, "", 0, -500, 0, "", 1)); // off screen, not important
            store.Add(new PoiData(4, "", 0, 5000, 0, "", 5)); // too far
            var f = new EdgeIndicatorFinder(store) { MinPriority = 4, SearchRange = 1000 };

            Assert.AreEqual(1, f.FindOffScreenPois(0, 0, Screen));
            Assert.AreEqual(2, f.Indicators[0].Id);
        }

        [Test]
        public void ArrowSitsOnScreenEdge()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 500, 0, "", 5));
            var f = new EdgeIndicatorFinder(store) { EdgeInset = 10 };

            f.FindOffScreenPois(0, 0, Screen);

            Assert.AreEqual(90f, f.Indicators[0].EdgeX, 0.01f);
            Assert.AreEqual(0f, f.Indicators[0].EdgeZ, 0.01f);
        }

        [Test]
        public void KeepsBestOnes_WhenTooMany()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 300, 0, "", 4));
            store.Add(new PoiData(2, "", 0, 600, 0, "", 5));
            store.Add(new PoiData(3, "", 0, 200, 0, "", 5));
            var f = new EdgeIndicatorFinder(store, 2);

            Assert.AreEqual(2, f.FindOffScreenPois(0, 0, Screen));
            Assert.AreEqual(3, f.Indicators[0].Id); // prio 5, closer
            Assert.AreEqual(2, f.Indicators[1].Id);
        }

        [Test]
        public void TypeFilterIsUsed()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 3, 500, 0, "", 5));
            var f = new EdgeIndicatorFinder(store) { IsTypeEnabled = t => t != 3 };

            Assert.AreEqual(0, f.FindOffScreenPois(0, 0, Screen));
        }
    }
}
