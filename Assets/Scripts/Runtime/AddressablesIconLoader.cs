using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Poi.Runtime
{
    // Loads icon sprites through Addressables, one load per icon key.
    // There are only a few icon keys, so loaded sprites are kept until Dispose, which releases the handles.
    // Markers waiting for a load are kept in a list and get the sprite (with their token) when it is done.
    public class AddressablesIconLoader : IIconLoader, IDisposable
    {
        struct Waiter
        {
            public IIconReceiver Receiver;
            public int Token;
        }

        class Entry
        {
            public AsyncOperationHandle<Sprite> Handle;
            public Sprite Sprite;
            public bool Done;
            public readonly List<Waiter> Waiting = new List<Waiter>(16);
        }

        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        bool _disposed;

        public void Request(string iconKey, IIconReceiver receiver, int token)
        {
            if (_disposed || string.IsNullOrEmpty(iconKey) || receiver == null) return;

            if (_entries.TryGetValue(iconKey, out var e))
            {
                if (e.Done) receiver.OnIconLoaded(token, e.Sprite);
                else e.Waiting.Add(new Waiter { Receiver = receiver, Token = token });
                return;
            }

            // first time we see this key: start the load (waiter added first in case it completes right away)
            e = new Entry();
            _entries.Add(iconKey, e);
            e.Waiting.Add(new Waiter { Receiver = receiver, Token = token });
            e.Handle = Addressables.LoadAssetAsync<Sprite>(iconKey);
            e.Handle.Completed += h => OnLoaded(e, h);
        }

        void OnLoaded(Entry e, AsyncOperationHandle<Sprite> h)
        {
            if (_disposed) return;
            e.Done = true;
            e.Sprite = h.Status == AsyncOperationStatus.Succeeded ? h.Result : null;
            if (e.Sprite == null) Debug.LogWarning("POI icon failed to load");

            for (int i = 0; i < e.Waiting.Count; i++)
            {
                var w = e.Waiting[i];
                // marker could be destroyed by now (unity null check)
                if (w.Receiver is UnityEngine.Object o && o == null) continue;
                w.Receiver.OnIconLoaded(w.Token, e.Sprite);
            }
            e.Waiting.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var kv in _entries)
                if (kv.Value.Handle.IsValid()) Addressables.Release(kv.Value.Handle);
            _entries.Clear();
        }
    }
}
