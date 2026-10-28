using System;
using System.Collections.Generic;

namespace Poi.Core
{
    // Keeps the POI data and the spatial grid in sync.
    // Listens to a feed, but you can also call Add/Move/Update/Remove directly (runtime control).
    public class PoiStore : IDisposable
    {
        readonly Dictionary<int, PoiData> _data;
        readonly SpatialGrid _grid;
        IPoiFeed _feed;

        public SpatialGrid Grid => _grid;
        public int Count => _data.Count;

        public PoiStore(SpatialGrid grid, int expectedCount = 1024)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _data = new Dictionary<int, PoiData>(expectedCount);
        }

        public void Attach(IPoiFeed feed)
        {
            Detach();
            _feed = feed;
            if (_feed == null) return;
            _feed.Added += Add;
            _feed.Moved += Move;
            _feed.Changed += Update;
            _feed.Removed += RemoveById;
        }

        public void Detach()
        {
            if (_feed == null) return;
            _feed.Added -= Add;
            _feed.Moved -= Move;
            _feed.Changed -= Update;
            _feed.Removed -= RemoveById;
            _feed = null;
        }

        public void Dispose() => Detach();

        public bool TryGet(int id, out PoiData poi) => _data.TryGetValue(id, out poi);

        // add, or replace if the id is already there
        public void Add(PoiData poi)
        {
            _data[poi.Id] = poi;
            _grid.Insert(poi.Id, poi.X, poi.Z);
        }

        public void Move(int id, float x, float z)
        {
            if (!_data.TryGetValue(id, out var poi)) return;
            poi.X = x;
            poi.Z = z;
            _data[id] = poi;
            _grid.Move(id, x, z);
        }

        // non position change (priority, icon, ...). position is applied too in case it differs
        public void Update(PoiData poi)
        {
            if (!_data.ContainsKey(poi.Id)) return;
            _data[poi.Id] = poi;
            _grid.Move(poi.Id, poi.X, poi.Z);
        }

        public bool Remove(int id)
        {
            if (!_data.Remove(id)) return false;
            _grid.Remove(id);
            return true;
        }

        void RemoveById(int id) => Remove(id);

        public void AddRange(IReadOnlyList<PoiData> pois)
        {
            for (int i = 0; i < pois.Count; i++) Add(pois[i]);
        }
    }
}
