using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class LabelDeclutterTests
    {
        static PoiStore NewStore() => new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));

        static SelectedMarker Labeled(int id) => new SelectedMarker { Id = id, Lod = MarkerLod.IconAndLabel };

        [Test]
        public void OverlappingLabel_LowerOneLosesLabel()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "Cafe Central", 0, 0, 0, "", 5));
            store.Add(new PoiData(2, "Book Shop", 0, 2, 1, "", 1)); // almost same spot
            var markers = new[] { Labeled(1), Labeled(2) };

            var d = new LabelDeclutter(store);
            d.HideOverlappingLabels(markers, 2);

            Assert.AreEqual(MarkerLod.IconAndLabel, markers[0].Lod); // first (best) keeps it
            Assert.AreEqual(MarkerLod.Icon, markers[1].Lod);
            Assert.AreEqual(1, d.LastHiddenCount);
        }

        [Test]
        public void FarApartLabels_BothStay()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "Cafe", 0, 0, 0, "", 5));
            store.Add(new PoiData(2, "Shop", 0, 100, 100, "", 1));
            var markers = new[] { Labeled(1), Labeled(2) };

            new LabelDeclutter(store).HideOverlappingLabels(markers, 2);

            Assert.AreEqual(MarkerLod.IconAndLabel, markers[1].Lod);
        }

        [Test]
        public void IconOnlyMarkers_AreIgnored()
        {
            var store = NewStore();
            store.Add(new PoiData(1, "Cafe", 0, 0, 0, "", 5));
            store.Add(new PoiData(2, "Shop", 0, 0, 0, "", 1));
            var markers = new[] { new SelectedMarker { Id = 1, Lod = MarkerLod.Icon }, Labeled(2) };

            new LabelDeclutter(store).HideOverlappingLabels(markers, 2);

            Assert.AreEqual(MarkerLod.IconAndLabel, markers[1].Lod);
        }

        [Test]
        public void MoreLabelsThanMax_ExtraOnesHidden()
        {
            var store = NewStore();
            var markers = new SelectedMarker[3];
            for (int i = 0; i < 3; i++)
            {
                store.Add(new PoiData(i, "X", 0, i * 500, 0, "", 1));
                markers[i] = Labeled(i);
            }

            new LabelDeclutter(store, 2).HideOverlappingLabels(markers, 3);

            Assert.AreEqual(MarkerLod.Icon, markers[2].Lod);
        }
    }
}
