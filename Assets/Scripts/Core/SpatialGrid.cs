using System;
using System.Collections.Generic;

namespace Poi.Core
{
    // a simple grid over the whole square world. we update it as things change, never rebuild it.
    // each cell has a list of ids. every id also remembers which cell it's in and where in that list,
    // so removing is quick (swap with last) and moving only touches the old and new cell.
    // a query only looks at cells that touch the area and puts ids in the list you pass in,
    // so it only costs as much as the POIs near that area, not all of them.
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

        // adds ids inside the rect (edges count) to results. doesn't clear results first.
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

        // adds ids within radius (ground distance on x/z) of the center. doesn't clear results first.
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

        // spots outside the world just go into the nearest edge cell.
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
