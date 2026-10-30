using System;
using System.Collections.Generic;
using Poi.Core;
using UnityEngine;

namespace Poi.Runtime
{
    // Reads the seed POIs from a json text, format:
    // { "pois": [ { "id":1, "name":"..", "type":0, "position":{"x":0,"y":0,"z":0}, "iconKey":"icon_0", "priority":3 } ] }
    public static class JsonPoiSeedSource
    {
        [Serializable] struct Position { public float x, y, z; }

        [Serializable]
        struct Row
        {
            public int id;
            public string name;
            public int type;
            public Position position;
            public string iconKey;
            public int priority;
        }

        [Serializable] class SeedFile { public Row[] pois; }

        // only called once at startup, so the allocations here are fine
        public static List<PoiData> Load(string json)
        {
            var result = new List<PoiData>();
            if (string.IsNullOrEmpty(json)) return result;

            var file = JsonUtility.FromJson<SeedFile>(json);
            if (file?.pois == null) return result;

            foreach (var r in file.pois)
                result.Add(new PoiData(r.id, r.name, r.type, r.position.x, r.position.z, r.iconKey, r.priority));
            return result;
        }
    }
}
