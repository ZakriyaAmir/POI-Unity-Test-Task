using System.Collections.Generic;
using Poi.Core;
using UnityEngine;

namespace Poi.Runtime.Tests
{
    // Icon loader where the test decides when (and in which order) loads complete.
    public class FakeIconLoader : IIconLoader
    {
        public struct Pending { public string Key; public IIconReceiver Receiver; public int Token; }
        public readonly List<Pending> Requests = new List<Pending>();
        public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public void Request(string iconKey, IIconReceiver receiver, int token) =>
            Requests.Add(new Pending { Key = iconKey, Receiver = receiver, Token = token });

        public void Complete(int index)
        {
            var p = Requests[index];
            p.Receiver.OnIconLoaded(p.Token, Sprites[p.Key]);
        }
    }

    public class FakeView : IMarkerView
    {
        public int PoiId { get; private set; } = -1;
        public MarkerLod Lod;
        public int Binds, Applies, Unbinds;
        public void Bind(in PoiData poi, MarkerLod lod) { PoiId = poi.Id; Lod = lod; Binds++; }
        public void Apply(in PoiData poi, MarkerLod lod) { Lod = lod; Applies++; }
        public void Unbind() { PoiId = -1; Unbinds++; }
    }

    public class FakePool : IMarkerPool
    {
        public readonly Stack<FakeView> Free = new Stack<FakeView>();
        public readonly List<FakeView> Handed = new List<FakeView>();
        public int Created, Gets, Releases;
        public IMarkerView Get()
        {
            Gets++;
            if (Free.Count > 0) return Free.Pop();
            Created++;
            var v = new FakeView();
            Handed.Add(v);
            return v;
        }
        public void Release(IMarkerView v) { Releases++; v.Unbind(); Free.Push((FakeView)v); }
    }

    public static class TestUtil
    {
        public static Sprite MakeSprite(string name)
        {
            var t = new Texture2D(2, 2);
            var s = Sprite.Create(t, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            s.name = name;
            return s;
        }

        public static MarkerView MakeMarkerPrefab()
        {
            var go = new GameObject("marker test");
            var icon = new GameObject("icon").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(go.transform);
            var labelGo = new GameObject("label");
            labelGo.transform.SetParent(go.transform);
            var tm = labelGo.AddComponent<TextMesh>();
            var mv = go.AddComponent<MarkerView>();
            mv.Setup(icon, tm, labelGo.GetComponent<MeshRenderer>());
            return mv;
        }
    }
}
