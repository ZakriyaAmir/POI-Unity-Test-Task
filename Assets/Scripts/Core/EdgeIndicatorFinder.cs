using System.Collections.Generic;

namespace Poi.Core
{
    public struct EdgeIndicator
    {
        public int Id;
        public int Priority;
        public float DistanceSq;
        // spot on the edge of the screen rect (ground meters) where the arrow goes
        public float EdgeX;
        public float EdgeZ;
    }

    // Finds important POIs that are near the player but off screen,
    // so the UI can draw a little arrow on the screen edge pointing at them.
    // Only looks inside SearchRange around the player, so the cost doesn't grow with the world size.
    public class EdgeIndicatorFinder
    {
        // only POIs with at least this priority get an arrow
        public int MinPriority = 4;
        // how far (meters) from the player we look for off screen POIs
        public float SearchRange = 1000f;
        // keep arrows a bit inside the screen edge (meters on the ground)
        public float EdgeInset = 10f;

        readonly PoiStore _store;
        readonly List<int> _nearbyIds;
        readonly EdgeIndicator[] _indicators;
        int _count;

        public int Count => _count;
        public EdgeIndicator[] Indicators => _indicators; // first Count are valid, best first
        public System.Func<int, bool> IsTypeEnabled; // optional type filter, set once

        // maxArrows = most arrows shown at once
        public EdgeIndicatorFinder(PoiStore store, int maxArrows = 8, int nearbyCapacity = 16384)
        {
            _store = store;
            _indicators = new EdgeIndicator[maxArrows];
            _nearbyIds = new List<int>(nearbyCapacity);
        }

        public int FindOffScreenPois(float playerX, float playerZ, in GroundRect screen)
        {
            _count = 0;
            _nearbyIds.Clear();
            _store.Grid.QueryCircle(playerX, playerZ, SearchRange, _nearbyIds);

            for (int i = 0; i < _nearbyIds.Count; i++)
            {
                if (!_store.TryGet(_nearbyIds[i], out var poi)) continue;
                if (poi.Priority < MinPriority) continue;
                if (IsTypeEnabled != null && !IsTypeEnabled(poi.Type)) continue;
                if (screen.Contains(poi.X, poi.Z)) continue; // on screen, it already has a marker

                var indicator = new EdgeIndicator
                {
                    Id = poi.Id,
                    Priority = poi.Priority,
                    DistanceSq = SpatialGrid.GroundDistanceSq(poi.X, poi.Z, playerX, playerZ)
                };
                AddIfGoodEnough(indicator);
            }

            for (int i = 0; i < _count; i++)
                PutOnScreenEdge(ref _indicators[i], playerX, playerZ, screen);
            return _count;
        }

        // keeps the list sorted best first. small list (8ish) so a simple insert is fine
        void AddIfGoodEnough(EdgeIndicator indicator)
        {
            int spot = _count;
            while (spot > 0 && IsBetter(indicator, _indicators[spot - 1])) spot--;
            if (spot >= _indicators.Length) return; // worse than everything we have

            int last = _count < _indicators.Length ? _count : _indicators.Length - 1;
            for (int i = last; i > spot; i--) _indicators[i] = _indicators[i - 1];
            _indicators[spot] = indicator;
            if (_count < _indicators.Length) _count++;
        }

        // higher priority first, then closer
        static bool IsBetter(in EdgeIndicator a, in EdgeIndicator b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            return a.DistanceSq < b.DistanceSq;
        }

        // walk from the screen center towards the POI and stop at the screen edge
        void PutOnScreenEdge(ref EdgeIndicator indicator, float playerX, float playerZ, in GroundRect screen)
        {
            _store.TryGet(indicator.Id, out var poi);
            float centerX = (screen.MinX + screen.MaxX) * 0.5f;
            float centerZ = (screen.MinZ + screen.MaxZ) * 0.5f;
            float halfW = (screen.MaxX - screen.MinX) * 0.5f - EdgeInset;
            float halfH = (screen.MaxZ - screen.MinZ) * 0.5f - EdgeInset;
            if (halfW < 0f) halfW = 0f;
            if (halfH < 0f) halfH = 0f;

            float dx = poi.X - centerX;
            float dz = poi.Z - centerZ;
            // how much we need to shrink the direction so it fits inside the rect
            float scaleX = dx != 0f ? halfW / System.Math.Abs(dx) : float.MaxValue;
            float scaleZ = dz != 0f ? halfH / System.Math.Abs(dz) : float.MaxValue;
            float scale = System.Math.Min(1f, System.Math.Min(scaleX, scaleZ));

            indicator.EdgeX = centerX + dx * scale;
            indicator.EdgeZ = centerZ + dz * scale;
        }
    }
}
