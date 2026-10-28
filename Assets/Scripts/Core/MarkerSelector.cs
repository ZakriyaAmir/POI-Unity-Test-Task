using System;
using System.Collections.Generic;

namespace Poi.Core
{
    public enum MarkerLod { None = 0, Icon = 1, IconAndLabel = 2 }

    public struct SelectedMarker
    {
        public int Id;
        public int Priority;
        public float DistanceSq;
        public MarkerLod Lod;
    }

    // Picks which POIs get a marker this frame.
    // 1. ask the grid only for the visible rect clipped to the icon range around the player
    // 2. filter (type, min priority, range)
    // 3. keep the best N in a bounded heap (worst one on top, so it is easy to kick out)
    // Every buffer is allocated up front, so Select does not allocate after warm-up.
    public class MarkerSelector
    {
        readonly PoiStore _store;
        readonly List<int> _candidates;
        SelectedMarker[] _heap;
        SelectedMarker[] _results;
        int _budget;
        int _resultCount;

        // filters, can change any time, used on the next Select
        ulong _typeMask = ulong.MaxValue; // bit per type (types 0..63)
        public int MinPriority = int.MinValue;

        public float IconRange = 2000f;
        public float LabelRange = 500f;

        public int ResultCount => _resultCount;
        public SelectedMarker[] Results => _results; // only the first ResultCount are valid, best first
        public int LastCandidateCount { get; private set; }

        public int Budget
        {
            get => _budget;
            set
            {
                _budget = Math.Max(0, value);
                if (_heap == null || _heap.Length < _budget)
                {
                    _heap = new SelectedMarker[_budget];
                    _results = new SelectedMarker[_budget];
                }
                _resultCount = 0;
            }
        }

        public MarkerSelector(PoiStore store, int budget = 250, int candidateCapacity = 4096)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _candidates = new List<int>(candidateCapacity);
            Budget = budget;
        }

        public void SetTypeEnabled(int type, bool enabled)
        {
            if (type < 0 || type > 63) return;
            if (enabled) _typeMask |= 1UL << type;
            else _typeMask &= ~(1UL << type);
        }

        public bool IsTypeEnabled(int type) => type >= 0 && type <= 63 && (_typeMask & (1UL << type)) != 0;
        public void EnableAllTypes() => _typeMask = ulong.MaxValue;

        // visible rect is the camera footprint on the ground (x/z), computed by the caller.
        // returns how many markers were picked, read them from Results.
        public int Select(float playerX, float playerZ, float rectMinX, float rectMinZ, float rectMaxX, float rectMaxZ)
        {
            _resultCount = 0;
            LastCandidateCount = 0;
            if (_budget == 0) return 0;

            // nothing past icon range can show, so shrink the query to that box
            float minX = Math.Max(rectMinX, playerX - IconRange);
            float minZ = Math.Max(rectMinZ, playerZ - IconRange);
            float maxX = Math.Min(rectMaxX, playerX + IconRange);
            float maxZ = Math.Min(rectMaxZ, playerZ + IconRange);
            if (minX > maxX || minZ > maxZ) return 0;

            _candidates.Clear();
            _store.Grid.QueryRect(minX, minZ, maxX, maxZ, _candidates);
            LastCandidateCount = _candidates.Count;

            int heapCount = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (TryMakeMarker(_candidates[i], playerX, playerZ, out var marker))
                    heapCount = PushToHeap(marker, heapCount);
            }

            CopyHeapToResults(heapCount);
            return _resultCount;
        }

        // false if the POI is filtered out or too far away
        bool TryMakeMarker(int id, float playerX, float playerZ, out SelectedMarker marker)
        {
            marker = default;
            if (!_store.TryGet(id, out var poi)) return false;
            if (poi.Priority < MinPriority) return false;
            if (!IsTypeEnabled(poi.Type)) return false;

            float distSq = SpatialGrid.GroundDistanceSq(poi.X, poi.Z, playerX, playerZ);
            if (distSq > IconRange * IconRange) return false;

            marker.Id = id;
            marker.Priority = poi.Priority;
            marker.DistanceSq = distSq;
            marker.Lod = distSq <= LabelRange * LabelRange ? MarkerLod.IconAndLabel : MarkerLod.Icon;
            return true;
        }

        // add while there is room, after that only replace the worst kept one. returns new heap count
        int PushToHeap(in SelectedMarker marker, int heapCount)
        {
            if (heapCount < _budget)
            {
                _heap[heapCount] = marker;
                SiftUp(heapCount);
                return heapCount + 1;
            }
            if (IsBetter(marker, _heap[0]))
            {
                _heap[0] = marker;
                SiftDown(0, heapCount);
            }
            return heapCount;
        }

        // pop worst first and fill the result from the back, so results are best first
        void CopyHeapToResults(int heapCount)
        {
            _resultCount = heapCount;
            for (int i = heapCount - 1; i >= 0; i--)
            {
                _results[i] = _heap[0];
                _heap[0] = _heap[i];
                SiftDown(0, i);
            }
        }

        // higher priority wins, then closer, then lower id (so the order is stable)
        static bool IsBetter(in SelectedMarker a, in SelectedMarker b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            if (a.DistanceSq != b.DistanceSq) return a.DistanceSq < b.DistanceSq;
            return a.Id < b.Id;
        }

        // min-heap on "goodness": the worst kept marker is at index 0
        void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!IsBetter(_heap[p], _heap[i])) break;
                Swap(i, p);
                i = p;
            }
        }

        void SiftDown(int i, int count)
        {
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, worst = i;
                if (l < count && IsBetter(_heap[worst], _heap[l])) worst = l;
                if (r < count && IsBetter(_heap[worst], _heap[r])) worst = r;
                if (worst == i) return;
                Swap(i, worst);
                i = worst;
            }
        }

        void Swap(int a, int b)
        {
            var t = _heap[a]; _heap[a] = _heap[b]; _heap[b] = t;
        }
    }
}
