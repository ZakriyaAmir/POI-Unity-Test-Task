using Poi.Core;

namespace Poi.Runtime
{
    // One on-screen marker. Bind = start showing a POI, Apply = update it, Unbind = back to the pool.
    public interface IMarkerView
    {
        int PoiId { get; }
        void Bind(in PoiData poi, MarkerLod lod);
        void Apply(in PoiData poi, MarkerLod lod);
        void Unbind();
    }

    public interface IMarkerPool
    {
        IMarkerView Get();
        void Release(IMarkerView view);
    }
}
