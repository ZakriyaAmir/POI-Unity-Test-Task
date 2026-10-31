using System;
using System.Collections.Generic;
using System.Globalization;

namespace Poi.Core
{
    // Fake server for the mock feed. It first sends a starting list (like a REST "get all"),
    // then keeps pushing small live changes (like WebSocket messages).
    // Everything goes out as text messages into a MockSocketFeed.
    public class FakePoiServer
    {
        // how many POIs the starting list has
        public int StartCount = 1000;
        // live messages per second
        public float AddsPerSecond = 5f;
        public float MovesPerSecond = 50f;
        public float RemovesPerSecond = 5f;
        // world area the POIs are made in
        public float WorldMin = -8000f;
        public float WorldSize = 16000f;

        readonly MockSocketFeed _feed;
        readonly Random _random;
        readonly List<int> _liveIds = new List<int>(4096);
        int _nextId = 5_000_000; // far away from simulator and API ids
        float _addTimer, _moveTimer, _removeTimer;

        public FakePoiServer(MockSocketFeed feed, int seed = 42)
        {
            _feed = feed;
            _random = new Random(seed);
        }

        // like a REST call that returns every POI at once
        public void SendStartingList()
        {
            for (int i = 0; i < StartCount; i++) SendAdd();
        }

        // call once per frame with the frame time
        public void Tick(float deltaTime)
        {
            _addTimer += AddsPerSecond * deltaTime;
            _moveTimer += MovesPerSecond * deltaTime;
            _removeTimer += RemovesPerSecond * deltaTime;

            while (_addTimer >= 1f) { _addTimer -= 1f; SendAdd(); }
            while (_moveTimer >= 1f) { _moveTimer -= 1f; SendMove(); }
            while (_removeTimer >= 1f) { _removeTimer -= 1f; SendRemove(); }
        }

        void SendAdd()
        {
            int id = _nextId++;
            _liveIds.Add(id);
            int type = _random.Next(8);
            string message = "add;" + id + ";Server POI " + id + ";" + type + ";" +
                             Num(RandomCoord()) + ";" + Num(RandomCoord()) + ";icon_" + type + ";" + _random.Next(6);
            _feed.ReceiveMessage(message);
        }

        void SendMove()
        {
            if (_liveIds.Count == 0) return;
            int id = _liveIds[_random.Next(_liveIds.Count)];
            _feed.ReceiveMessage("move;" + id + ";" + Num(RandomCoord()) + ";" + Num(RandomCoord()));
        }

        void SendRemove()
        {
            if (_liveIds.Count == 0) return;
            int index = _random.Next(_liveIds.Count);
            int id = _liveIds[index];
            _liveIds[index] = _liveIds[_liveIds.Count - 1]; // swap with last, then drop last
            _liveIds.RemoveAt(_liveIds.Count - 1);
            _feed.ReceiveMessage("remove;" + id);
        }

        float RandomCoord() => WorldMin + (float)_random.NextDouble() * WorldSize;

        static string Num(float f) => f.ToString("F1", CultureInfo.InvariantCulture);
    }
}
