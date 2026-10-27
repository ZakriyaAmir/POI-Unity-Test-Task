namespace Poi.Core
{
    // Plain data for one point of interest. Positions are in world meters on the ground plane (x/z).
    public struct PoiData
    {
        public int Id;
        public string Name;
        public int Type;
        public float X;
        public float Z;
        public string IconKey;
        public int Priority; // higher = more important

        public PoiData(int id, string name, int type, float x, float z, string iconKey, int priority)
        {
            Id = id;
            Name = name;
            Type = type;
            X = x;
            Z = z;
            IconKey = iconKey;
            Priority = priority;
        }
    }
}
