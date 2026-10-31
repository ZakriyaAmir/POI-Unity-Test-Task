using System;
using System.Collections.Generic;
using System.Globalization;

namespace Poi.Core
{
    // Pretend network feed, like a WebSocket that sends one text message per change.
    // Messages can arrive from any thread (ReceiveMessage), they wait in a queue,
    // and ProcessMessages() turns them into the normal feed events on the main thread.
    //
    // Message format, fields split by ';'
    //   add;id;name;type;x;z;iconKey;priority
    //   move;id;x;z
    //   change;id;name;type;x;z;iconKey;priority
    //   remove;id
    public class MockSocketFeed : IPoiFeed
    {
        public event Action<PoiData> Added;
        public event Action<int, float, float> Moved;
        public event Action<PoiData> Changed;
        public event Action<int> Removed;

        // most messages handled per frame, the rest wait for the next frame
        public int MaxMessagesPerFrame = 2000;

        readonly Queue<string> _inbox = new Queue<string>(4096);
        readonly object _lock = new object();

        public int WaitingCount { get { lock (_lock) return _inbox.Count; } }
        public int BadMessageCount { get; private set; }

        // the "socket" calls this when a message comes in
        public void ReceiveMessage(string message)
        {
            lock (_lock) _inbox.Enqueue(message);
        }

        // call once per frame from the main thread
        public int ProcessMessages()
        {
            int handled = 0;
            while (handled < MaxMessagesPerFrame)
            {
                string message;
                lock (_lock)
                {
                    if (_inbox.Count == 0) break;
                    message = _inbox.Dequeue();
                }
                HandleMessage(message);
                handled++;
            }
            return handled;
        }

        void HandleMessage(string message)
        {
            string[] parts = message.Split(';');
            string kind = parts[0];

            if (kind == "add" && parts.Length == 8) Added?.Invoke(ReadPoi(parts));
            else if (kind == "change" && parts.Length == 8) Changed?.Invoke(ReadPoi(parts));
            else if (kind == "move" && parts.Length == 4) Moved?.Invoke(ReadInt(parts[1]), ReadFloat(parts[2]), ReadFloat(parts[3]));
            else if (kind == "remove" && parts.Length == 2) Removed?.Invoke(ReadInt(parts[1]));
            else BadMessageCount++; // unknown or broken message, just skip it
        }

        static PoiData ReadPoi(string[] p)
        {
            return new PoiData(ReadInt(p[1]), p[2], ReadInt(p[3]), ReadFloat(p[4]), ReadFloat(p[5]), p[6], ReadInt(p[7]));
        }

        static int ReadInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);
        static float ReadFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}
