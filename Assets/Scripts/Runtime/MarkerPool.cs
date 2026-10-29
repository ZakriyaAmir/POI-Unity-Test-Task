using System.Collections.Generic;
using UnityEngine;

namespace Poi.Runtime
{
    // Simple stack pool of MarkerView instances. Prewarm up front so the game loop doesn't Instantiate.
    // If the budget is raised above what was prewarmed it grows (one time cost).
    public class MarkerPool : IMarkerPool
    {
        readonly MarkerView _prefab;
        readonly Transform _parent;
        readonly IIconLoader _loader;
        readonly Stack<MarkerView> _free;

        public int CreatedCount { get; private set; }
        public int FreeCount => _free.Count;

        public MarkerPool(MarkerView prefab, Transform parent, IIconLoader loader, int prewarm)
        {
            _prefab = prefab;
            _parent = parent;
            _loader = loader;
            _free = new Stack<MarkerView>(Mathf.Max(16, prewarm));
            Prewarm(prewarm);
        }

        public void Prewarm(int total)
        {
            while (CreatedCount < total) _free.Push(Create());
        }

        public IMarkerView Get() => _free.Count > 0 ? _free.Pop() : Create();

        public void Release(IMarkerView view)
        {
            var mv = (MarkerView)view;
            if (mv == null) return; // already destroyed (scene unloading)
            mv.Unbind();
            _free.Push(mv);
        }

        MarkerView Create()
        {
            var mv = Object.Instantiate(_prefab, _parent);
            mv.Init(_loader);
            mv.Unbind(); // starts hidden
            CreatedCount++;
            return mv;
        }
    }
}
