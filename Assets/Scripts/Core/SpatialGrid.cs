using System;
using System.Collections.Generic;

namespace Poi.Core
{
    // Uniform grid over a square world, updated incrementally (no rebuilds).
    // Each cell keeps a list of ids. Every id remembers its cell and its index in that list,
    // so remove is a swap-remove and move only touches the two cells involved.
    // Queries only visit the cells overlapping the query area and write into a caller list,
    // so cost depends on how many POIs are near the area, not on the total count.
    public class SpatialGrid
    {
        struct Entry
        {
            public float X;
            public float Z;
            public int Cell;  // -1 = not in grid
            public int Slot;  // index inside the cell list
        }

        readonly float _worldMin;
        readonly float _cellSize;
        readonly int _cellsPerSide;
        readonly List<int>[] _cells;
        readonly Dictionary<int, Entry> _entries;

        public int Count => _entries.Count;
        public float CellSize => _cellSize;

        // worldMin/worldSize: e.g. -8000 and 16000 for a 16km world centered on origin.
        public SpatialGrid(float worldMin, float worldSize, float cellSize, int expectedCount = 1024)
        {
            if (worldSize <= 0f || cellSize <= 0f) throw new ArgumentException("size must be > 0");
            _worldMin = worldMin;
            _cellSize = cellSize;
            _cellsPerSide = Math.Max(1, (int)Math.Ceiling(worldSize / cellSize));
            _cells = new List<int>[_cellsPerSide * _cellsPerSide];
            _entries = new Dictionary<int, Entry>(expectedCount);
        }

        public bool Contains(int id) => _entries.ContainsKey(id);

        public bool TryGetPosition(int id, out float x, out float z)
        {
            if (_entries.TryGetValue(id, out var e)) { x = e.X; z = e.Z; return true; }
            x = z = 0f;
            return false;
        }

        public void Insert(int id, float x, float z)
        {
            if (_entries.ContainsKey(id)) { Move(id, x, z); return; }
            var e = new Entry { X = x, Z = z };
            AddToCell(ref e, CellIndex(x, z), id);
            _entries[id] = e;
        }

        public void Move(int id, float x, float z)
        {
            if (!_entries.TryGetValue(id, out var e)) return;
            int newCell = CellIndex(x, z);
            if (newCell != e.Cell)
            {
                RemoveFromCell(e);
                AddToCell(ref e, newCell, id);
            }
            e.X = x;
            e.Z = z;
            _entries[id] = e;
        }

        public bool Remove(int id)
        {
            if (!_entries.TryGetValue(id, out var e)) return false;
            RemoveFromCell(e);
            _entries.Remove(id);
            return true;
        }

        // Adds ids inside the rectangle (inclusive) to results. Does not clear results.
        public void QueryRect(float minX, float minZ, float maxX, float maxZ, List<int> results)
        {
            CellRange(minX, minZ, maxX, maxZ, out int cx0, out int cz0, out int cx1, out int cz1);
            for (int cz = cz0; cz <= cz1; cz++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    var cell = _cells[cz * _cellsPerSide + cx];
                    if (cell == null) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        int id = cell[i];
                        var e = _entries[id];
                        if (e.X >= minX && e.X <= maxX && e.Z >= minZ && e.Z <= maxZ)
                            results.Add(id);
                    }
                }
            }
        }

        // Adds ids within radius (ground distance on x/z) of the center. Does not clear results.
        public void QueryCircle(float cx, float cz, float radius, List<int> results)
        {
            float r2 = radius * radius;
            CellRange(cx - radius, cz - radius, cx + radius, cz + radius, out int x0, out int z0, out int x1, out int z1);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var cell = _cells[z * _cellsPerSide + x];
                    if (cell == null) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        int id = cell[i];
                        var e = _entries[id];
                        float dx = e.X - cx, dz = e.Z - cz;
                        if (dx * dx + dz * dz <= r2) results.Add(id);
                    }
                }
            }
        }

        public static float GroundDistanceSq(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return dx * dx + dz * dz;
        }

        // Positions outside the world get clamped to the edge cells.
        int CellIndex(float x, float z)
        {
            return ClampCell(z) * _cellsPerSide + ClampCell(x);
        }

        int ClampCell(float v)
        {
            int c = (int)Math.Floor((v - _worldMin) / _cellSize);
            if (c < 0) return 0;
            if (c >= _cellsPerSide) return _cellsPerSide - 1;
            return c;
        }

        void CellRange(float minX, float minZ, float maxX, float maxZ, out int x0, out int z0, out int x1, out int z1)
        {
            x0 = ClampCell(minX); x1 = ClampCell(maxX);
            z0 = ClampCell(minZ); z1 = ClampCell(maxZ);
        }

        void AddToCell(ref Entry e, int cellIndex, int id)
        {
            var cell = _cells[cellIndex];
            if (cell == null)
            {
                cell = new List<int>(8);
                _cells[cellIndex] = cell;
            }
            e.Cell = cellIndex;
            e.Slot = cell.Count;
            cell.Add(id);
        }

        // swap-remove: move the last id in the cell into the freed slot and fix its slot index
        void RemoveFromCell(Entry e)
        {
            var cell = _cells[e.Cell];
            int last = cell.Count - 1;
            if (e.Slot != last)
            {
                int movedId = cell[last];
                cell[e.Slot] = movedId;
                var moved = _entries[movedId];
                moved.Slot = e.Slot;
                _entries[movedId] = moved;
            }
            cell.RemoveAt(last);
        }
    }
}
