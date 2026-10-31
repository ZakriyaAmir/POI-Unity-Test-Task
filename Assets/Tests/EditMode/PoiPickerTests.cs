using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class PoiPickerTests
    {
        static PoiStore NewStore() => new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));

        [Test]
        public void PicksClosestInRadius()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 5, 0, "", 1));
            store.Add(new PoiData(2, "", 0, 2, 0, "", 1));
            Assert.AreEqual(2, new PoiPicker(store).FindPoiAt(0, 0, 8));
        }

        [Test]
        public void NothingThere_ReturnsMinusOne()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 50, 0, "", 1));
            Assert.AreEqual(-1, new PoiPicker(store).FindPoiAt(0, 0, 8));
        }

        [Test]
        public void CanPickFilterIsUsed()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "", 0, 1, 0, "", 1));
            store.Add(new PoiData(2, "", 0, 4, 0, "", 1));
            var picker = new PoiPicker(store) { CanPick = id => id != 1 };
            Assert.AreEqual(2, picker.FindPoiAt(0, 0, 8));
        }
    }
}
