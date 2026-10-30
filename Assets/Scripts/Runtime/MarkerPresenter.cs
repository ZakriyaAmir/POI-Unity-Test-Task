using System.Collections.Generic;
using System.Diagnostics;
using Poi.Core;

namespace Poi.Runtime
{
    // Glue between the selector (logic) and the pooled views (presentation).
    // Each Refresh: run the selector, then diff the result against the active markers by POI id:
    //  - already shown -> Apply (position / lod / label update)
    //  - new           -> take a view from the pool and Bind
    //  - not selected anymore (out of view, out of range, filtered, removed) -> Unbind and back to pool
    // Uses only preallocated containers, so no allocations after warm-up.
    public class MarkerPresenter
    {
        class Slot
        {
            public IMarkerView View;
            public int Stamp;
        }

        readonly PoiStore _store;
        readonly MarkerSelector _selector;
        readonly IMarkerPool _pool;
        readonly Dictionary<int, Slot> _active;
        readonly Stack<Slot> _freeSlots;
        readonly List<int> _toRemove;
        readonly Stopwatch _sw = new Stopwatch();
        int _frame;

        public int ActiveCount => _active.Count;
        public double LastSelectMs { get; private set; }
        public double LastApplyMs { get; private set; }

        public MarkerPresenter(PoiStore store, MarkerSelector selector, IMarkerPool pool)
        {
            _store = store;
            _selector = selector;
            _pool = pool;
            int cap = System.Math.Max(64, selector.Budget * 2);
            _active = new Dictionary<int, Slot>(cap);
            _freeSlots = new Stack<Slot>(cap);
            _toRemove = new List<int>(cap);
        }

        public bool IsShowing(int poiId) => _active.ContainsKey(poiId);

        public void Refresh(float playerX, float playerZ, in GroundRect visible)
        {
            _frame++;

            _sw.Restart();
            int n = _selector.Select(playerX, playerZ, visible.MinX, visible.MinZ, visible.MaxX, visible.MaxZ);
            LastSelectMs = _sw.Elapsed.TotalMilliseconds;

            _sw.Restart();
            ShowSelected(n);
            HideNotSelected();
            LastApplyMs = _sw.Elapsed.TotalMilliseconds;
        }

        // update markers already on screen, take new ones from the pool for the rest
        void ShowSelected(int n)
        {
            var results = _selector.Results;
            for (int i = 0; i < n; i++)
            {
                var r = results[i];
                if (!_store.TryGet(r.Id, out var poi)) continue;

                if (_active.TryGetValue(r.Id, out var slot))
                {
                    slot.View.Apply(poi, r.Lod);
                }
                else
                {
                    slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : new Slot();
                    slot.View = _pool.Get();
                    slot.View.Bind(poi, r.Lod);
                    _active.Add(r.Id, slot);
                }
                slot.Stamp = _frame;
            }
        }

        // anything not touched this frame goes back to the pool.
        // ids are collected first because we can't remove from a dictionary while looping it
        void HideNotSelected()
        {
            _toRemove.Clear();
            foreach (var kv in _active)
                if (kv.Value.Stamp != _frame) _toRemove.Add(kv.Key);

            for (int i = 0; i < _toRemove.Count; i++)
                ReleaseSlot(_toRemove[i]);
        }

        public void Clear()
        {
            _toRemove.Clear();
            foreach (var kv in _active) _toRemove.Add(kv.Key);
            for (int i = 0; i < _toRemove.Count; i++) ReleaseSlot(_toRemove[i]);
        }

        void ReleaseSlot(int id)
        {
            var slot = _active[id];
            _active.Remove(id);
            _pool.Release(slot.View);
            slot.View = null;
            _freeSlots.Push(slot);
        }
    }
}
