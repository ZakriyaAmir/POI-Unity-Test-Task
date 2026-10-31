using System;

namespace Poi.Core
{
    // something that sends live POI changes. the simulator or a server feed both do this.
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
