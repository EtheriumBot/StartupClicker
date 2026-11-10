using StartupClicker.Core;
using UnityEngine;

namespace StartupClicker.Game
{
    /// <summary>
    /// Converts between grid coordinates and world positions.
    ///
    /// The map is a square grid drawn as a diamond: +x runs down-right on screen, +y runs down-left.
    /// One tile is 1 world unit wide and 0.5 tall (the classic 2:1 isometric look), so moving one
    /// cell along x shifts a sprite half a unit right and a quarter unit down.
    /// </summary>
    public static class IsoGrid
    {
        public const float PixelsPerUnit = 256f;
        const float Centre = Catalog.GridSize / 2f;

        /// <summary>World position of a (possibly fractional) grid point. The grid centre maps to (0, 0).</summary>
        public static Vector3 ToWorld(float gx, float gy)
        {
            return new Vector3((gx - gy) * 0.5f, -(gx + gy - 2f * Centre) * 0.25f, 0f);
        }

        /// <summary>Inverse of ToWorld: which fractional grid point is under this world position.</summary>
        public static Vector2 ToGrid(Vector2 world)
        {
            float a = world.x / 0.5f;                       // = gx - gy
            float b = -world.y / 0.25f + 2f * Centre;       // = gx + gy
            return new Vector2((a + b) * 0.5f, (b - a) * 0.5f);
        }

        /// <summary>
        /// Draw order. Things further down the screen (larger gx + gy) must be drawn later so they
        /// overlap what is behind them - this is what makes flat sprites look 3D.
        /// </summary>
        public static int Order(float gx, float gy)
        {
            return Mathf.RoundToInt((gx + gy) * 10f);
        }

        /// <summary>Where a building's sprite pivot (the front corner of its footprint) goes.</summary>
        public static Vector3 FootprintFront(int x, int y, int size)
        {
            return ToWorld(x + size, y + size);
        }

        public static Vector3 FootprintCentre(int x, int y, int size)
        {
            return ToWorld(x + size * 0.5f, y + size * 0.5f);
        }
    }
}
