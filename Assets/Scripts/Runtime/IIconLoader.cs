using UnityEngine;

namespace Poi.Runtime
{
    // Whoever asked for an icon. The token is handed back so the receiver can tell
    // if the answer is still for what it is showing now (it may have been recycled meanwhile).
    public interface IIconReceiver
    {
        void OnIconLoaded(int token, Sprite sprite);
    }

    // Async icon loading. The answer can come right away (cached) or some frames later.
    public interface IIconLoader
    {
        void Request(string iconKey, IIconReceiver receiver, int token);
    }
}
