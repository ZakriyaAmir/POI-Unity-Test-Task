using System;

namespace Poi.Core
{
    // Source of live POI updates. Whoever owns the data (simulator, network, etc) implements this.
    public interface IPoiFeed
    {
        event Action<PoiData> Added;
        // id, new x, new z
        event Action<int, float, float> Moved;
        // something other than position changed (name, icon, priority, type)
        event Action<PoiData> Changed;
        event Action<int> Removed;
    }
}
