using System;

namespace Poi.Core
{
    // Fake live feed. Call Tick(dt) once per frame.
    // All settings can be changed while running.
    // POIs live in plain arrays (swap-remove), so no allocations once the arrays are big enough.
    // moving goes through the array a chunk at a time, so we only touch the POIs that move this frame.
    public class PoiFeedSimulator : IPoiFeed
    {
        public event Action<PoiData> Added;
        public event Action<int, float, float> Moved;
        public event Action<PoiData> Changed;
        public event Action<int> Removed;

        // settings
        public int TargetCount;
        public int MaxCountChangePerTick = 5000; // grow/shrink is spread over frames
        public float AddsPerSecond;              // churn
        public float RemovesPerSecond;
        public float MoveShare;                  // 0..1, share of POIs moving each tick
        public float MoveSpeed = 5f;             // meters per second
        public int TypeCount = 8;
        public int MaxPriority = 5;

        readonly Random _rng;
        readonly float _worldMin, _worldMax;

        int[] _ids;
        float[] _x, _z, _dirX, _dirZ;
        int _count;
        int _nextId = 1;
        int _moveCursor;
        float _addAcc, _removeAcc;

        static readonly string[] Names = BuildNames(256);
        static readonly string[] IconKeys = { "icon_0", "icon_1", "icon_2", "icon_3", "icon_4", "icon_5", "icon_6", "icon_7" };

        public int Count => _count;

        public PoiFeedSimulator(float worldMin, float worldSize, Random rng = null, int capacity = 1024)
        {
            _worldMin = worldMin;
            _worldMax = worldMin + worldSize;
            _rng = rng ?? new Random();
            Resize(Math.Max(16, capacity));
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            GoTowardsTargetCount();
            DoChurn(dt);
            MoveSomePois(dt);
        }

        // grow or shrink to TargetCount, but at most MaxCountChangePerTick per frame
        void GoTowardsTargetCount()
        {
            int diff = TargetCount - _count;
            int steps = Math.Min(Math.Abs(diff), MaxCountChangePerTick);
            for (int i = 0; i < steps; i++)
            {
                if (diff > 0) Spawn();
                else RemoveAt(_rng.Next(_count));
            }
        }

        // random adds/removes. fractions carry over to the next tick
        void DoChurn(float dt)
        {
            _addAcc += AddsPerSecond * dt;
            _removeAcc += RemovesPerSecond * dt;
            while (_addAcc >= 1f)
            {
                _addAcc -= 1f;
                Spawn();
            }
            while (_removeAcc >= 1f)
            {
                _removeAcc -= 1f;
                if (_count > 0) RemoveAt(_rng.Next(_count));
            }
        }

        // moves the next MoveShare of the POIs, continuing where the last tick stopped
        void MoveSomePois(float dt)
        {
            int moving = (int)(_count * Clamp01(MoveShare));
            float step = MoveSpeed * dt;
            for (int i = 0; i < moving; i++)
            {
                if (_moveCursor >= _count) _moveCursor = 0;
                MoveAt(_moveCursor, step);
                _moveCursor++;
            }
        }

        // runtime control: change one POI, e.g. bump priority
        public bool SetPriority(int id, int priority)
        {
            int i = IndexOf(id);
            if (i < 0) return false;
            var p = Make(i);
            p.Priority = priority;
            Changed?.Invoke(p);
            return true;
        }

        // linear on purpose, only for debug/tests, never called per frame
        int IndexOf(int id)
        {
            for (int i = 0; i < _count; i++) if (_ids[i] == id) return i;
            return -1;
        }

        void Spawn()
        {
            if (_count == _ids.Length) Resize(_ids.Length * 2);
            int i = _count++;
            _ids[i] = _nextId++;
            _x[i] = Range(_worldMin, _worldMax);
            _z[i] = Range(_worldMin, _worldMax);
            double a = _rng.NextDouble() * Math.PI * 2.0;
            _dirX[i] = (float)Math.Cos(a);
            _dirZ[i] = (float)Math.Sin(a);
            Added?.Invoke(Make(i));
        }

        PoiData Make(int i)
        {
            int id = _ids[i];
            int type = id % Math.Max(1, TypeCount);
            int prio = (id * 7) % (MaxPriority + 1);
            return new PoiData(id, Names[id % Names.Length], type, _x[i], _z[i], IconKeys[type % IconKeys.Length], prio);
        }

        void RemoveAt(int i)
        {
            int id = _ids[i];
            int last = --_count;
            _ids[i] = _ids[last]; _x[i] = _x[last]; _z[i] = _z[last];
            _dirX[i] = _dirX[last]; _dirZ[i] = _dirZ[last];
            Removed?.Invoke(id);
        }

        void MoveAt(int i, float step)
        {
            float x = _x[i] + _dirX[i] * step;
            float z = _z[i] + _dirZ[i] * step;
            // bounce off the world edge
            if (x < _worldMin || x > _worldMax) { _dirX[i] = -_dirX[i]; x = Math.Max(_worldMin, Math.Min(_worldMax, x)); }
            if (z < _worldMin || z > _worldMax) { _dirZ[i] = -_dirZ[i]; z = Math.Max(_worldMin, Math.Min(_worldMax, z)); }
            _x[i] = x; _z[i] = z;
            Moved?.Invoke(_ids[i], x, z);
        }

        void Resize(int size)
        {
            Array.Resize(ref _ids, size);
            Array.Resize(ref _x, size);
            Array.Resize(ref _z, size);
            Array.Resize(ref _dirX, size);
            Array.Resize(ref _dirZ, size);
        }

        float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        static string[] BuildNames(int n)
        {
            var arr = new string[n];
            for (int i = 0; i < n; i++) arr[i] = "POI " + i;
            return arr;
        }
    }
}
