using System.Collections.Generic;
using ArcanaWars.Core.Board;

namespace ArcanaWars.Core.Session
{
    /// <summary>
    /// What a card would cover if it were played at some tile, and whether the board would accept it
    /// there. Produced by <see cref="MatchSession.PreviewPlacement"/> for pointer feedback — see
    /// there for why this deliberately answers less than a real play does.
    /// </summary>
    public readonly struct PlacementPreview
    {
        /// <summary>The tiles the card would occupy. Empty when the shape doesn't fit on the board at all (it ran off an edge).</summary>
        public readonly IReadOnlyList<HexCoord> Tiles;

        /// <summary>True if those tiles are free, on the player's own side, and unwalled.</summary>
        public readonly bool Fits;

        public PlacementPreview(IReadOnlyList<HexCoord> tiles, bool fits)
        {
            Tiles = tiles ?? System.Array.Empty<HexCoord>();
            Fits = fits;
        }

        public bool HasTiles => Tiles != null && Tiles.Count > 0;
    }
}
