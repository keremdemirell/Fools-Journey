using UnityEngine;
using ArcanaWars.Core.Board;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// Turns board coordinates into world positions. This is Presentation's job, not Core's — Core
    /// knows which tiles are adjacent, but has no opinion about how big a hex is on screen.
    ///
    /// Pointy-top hexes laid out in "odd-r" rows, matching HexCoord's own offset convention: odd
    /// rows are nudged half a hex to the right. Row increases upward (+Y), so row 0 — Player A's
    /// back rank — sits at the bottom of the screen, with Player B's Soul at the top.
    /// </summary>
    public static class HexLayout
    {
        private static readonly float Sqrt3 = Mathf.Sqrt(3f);

        /// <summary>Centre of the given tile in world space, for a hex whose corner-to-centre radius is `size`.</summary>
        public static Vector3 ToWorld(HexCoord coord, float size)
        {
            var offset = coord.ToOffset();
            float x = size * Sqrt3 * (offset.Col + 0.5f * (offset.Row & 1));
            float y = size * 1.5f * offset.Row;
            return new Vector3(x, y, 0f);
        }

        /// <summary>Horizontal distance between neighbouring tile centres in the same row.</summary>
        public static float ColumnSpacing(float size) => size * Sqrt3;

        /// <summary>Vertical distance between rows.</summary>
        public static float RowSpacing(float size) => size * 1.5f;
    }
}
