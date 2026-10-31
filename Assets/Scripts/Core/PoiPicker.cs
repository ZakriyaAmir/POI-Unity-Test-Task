using System.Collections.Generic;

namespace Poi.Core
{
    // Finds which POI is under a clicked ground spot.
    // Asks the grid only for a tiny circle around the click, so it's cheap at any POI count.
    public class PoiPicker
    {
        readonly PoiStore _store;
        readonly List<int> _nearbyIds = new List<int>(256);

        // optional: only pick POIs that pass this (e.g. "has a marker on screen"). set once
        public System.Func<int, bool> CanPick;

        public PoiPicker(PoiStore store)
        {
            _store = store;
        }

        // returns the closest POI id within radius meters of (x, z), or -1 if nothing is there
        public int FindPoiAt(float x, float z, float radius)
        {
            _nearbyIds.Clear();
            _store.Grid.QueryCircle(x, z, radius, _nearbyIds);

            int bestId = -1;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < _nearbyIds.Count; i++)
            {
                int id = _nearbyIds[i];
                if (CanPick != null && !CanPick(id)) continue;
                if (!_store.TryGet(id, out var poi)) continue;

                float distSq = SpatialGrid.GroundDistanceSq(poi.X, poi.Z, x, z);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestId = id;
                }
            }
            return bestId;
        }
    }
}
