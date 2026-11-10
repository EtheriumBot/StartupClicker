namespace StartupClicker.Core
{
    /// <summary>Tracks which building (by uid) occupies each cell of the square build grid.</summary>
    public sealed class GridModel
    {
        readonly int[,] cells;
        public int Size { get; }

        public GridModel(int size)
        {
            Size = size;
            cells = new int[size, size];
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Size && y < Size;
        }

        /// <summary>Uid of the building on this cell, or 0 if empty / out of bounds.</summary>
        public int At(int x, int y)
        {
            return InBounds(x, y) ? cells[x, y] : 0;
        }

        /// <summary>True if a size x size footprint at (x, y) is on the map and free. Cells held by ignoreUid count as free (used when moving).</summary>
        public bool CanPlace(int x, int y, int size, int ignoreUid = 0)
        {
            if (x < 0 || y < 0 || x + size > Size || y + size > Size) return false;
            for (int i = x; i < x + size; i++)
                for (int j = y; j < y + size; j++)
                    if (cells[i, j] != 0 && cells[i, j] != ignoreUid) return false;
            return true;
        }

        public void Fill(int x, int y, int size, int uid)
        {
            for (int i = x; i < x + size; i++)
                for (int j = y; j < y + size; j++)
                    if (InBounds(i, j)) cells[i, j] = uid;
        }

        public void Clear(int uid)
        {
            for (int i = 0; i < Size; i++)
                for (int j = 0; j < Size; j++)
                    if (cells[i, j] == uid) cells[i, j] = 0;
        }
    }
}
