using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcanaWars.Core.Board
{
    /// <summary>
    /// The shared battlefield. Built as a rectangular block of hex tiles addressed by
    /// offset (col, row), with the four corners optionally chamfered off (via cornerCut)
    /// so the silhouette reads as hexagon-ish. Souls sit at the top and bottom (row 0 =
    /// Player A's back rank, row Rows-1 = Player B's back rank), so ROWS split the board
    /// into Player A's side, a possible neutral middle row (if Rows is odd), and Player
    /// B's side — and COLUMNS are the attack lanes a unit advances/attacks along toward
    /// the opposing Soul. Chamfering only shortens the outermost `cornerCut` columns near
    /// the top/bottom edges; every other column still spans the full board height, so the
    /// column-as-lane rule keeps working everywhere except right at the corners.
    /// </summary>
    public class HexGrid
    {
        public int Columns { get; }
        public int Rows { get; }

        private readonly Dictionary<HexCoord, Tile> _tiles;

        public HexGrid(int columns, int rows, int cornerCut = 1)
        {
            if (cornerCut < 0) throw new ArgumentOutOfRangeException(nameof(cornerCut));
            if (columns - 2 * cornerCut < 1) throw new ArgumentException("cornerCut leaves no tiles on the narrowest row.", nameof(cornerCut));

            Columns = columns;
            Rows = rows;
            _tiles = new Dictionary<HexCoord, Tile>(columns * rows);

            for (int row = 0; row < rows; row++)
            {
                int trim = CornerTrimForRow(row, rows, cornerCut);
                for (int col = trim; col < columns - trim; col++)
                {
                    var coord = new OffsetCoord(col, row).ToAxial();
                    _tiles[coord] = new Tile(coord, SideForRow(row, rows));
                }
            }
        }

        /// <summary>How many columns to shave off each side of this row. Tapers from cornerCut at row 0/last row down to 0 once cornerCut rows from either edge.</summary>
        private static int CornerTrimForRow(int row, int rows, int cornerCut)
        {
            if (cornerCut <= 0) return 0;
            int distanceFromNearestEdge = Math.Min(row, rows - 1 - row);
            return distanceFromNearestEdge < cornerCut ? cornerCut - distanceFromNearestEdge : 0;
        }

        private static PlayerSide? SideForRow(int row, int rows)
        {
            int half = rows / 2;
            if (rows % 2 == 1 && row == half) return null; // odd row count -> neutral middle rank
            return row < half ? PlayerSide.A : PlayerSide.B;
        }

        public bool Contains(HexCoord coord) => _tiles.ContainsKey(coord);

        public bool TryGetTile(HexCoord coord, out Tile tile) => _tiles.TryGetValue(coord, out tile);

        public IEnumerable<Tile> AllTiles => _tiles.Values;

        /// <summary>
        /// Resolves a shape anchored at the given tile into absolute board coordinates.
        /// EntireBoard (GDD 21, The World) means every tile on the anchor's OWN side, not the
        /// literal whole shared board — the World occupies your whole territory, not the
        /// opponent's too, which would make it unplaceable under the "your own side" placement
        /// rule below. Returns null if the anchor isn't on a real side (e.g. a neutral tile).
        /// </summary>
        public IReadOnlyList<HexCoord> GetFootprintCoords(HexCoord anchor, FootprintShapeType shape)
        {
            if (shape == FootprintShapeType.EntireBoard)
            {
                if (!TryGetTile(anchor, out var anchorTile) || anchorTile.Side == null)
                    return null;

                return AllTiles.Where(t => t.Side == anchorTile.Side).Select(t => t.Coord).ToList();
            }

            return FootprintShapes.Resolve(anchor, shape);
        }

        /// <summary>True if every tile the shape would occupy exists, belongs to `owner`'s own side (GDD 4.3), is empty, and isn't part of an Emperor's wall — anyone's, your own included.</summary>
        public bool CanPlaceFootprint(PlayerSide owner, HexCoord anchor, FootprintShapeType shape)
        {
            var coords = GetFootprintCoords(anchor, shape);
            if (coords == null || coords.Count == 0) return false;

            foreach (var coord in coords)
            {
                if (!TryGetTile(coord, out var tile)) return false;
                if (tile.Side != owner) return false;
                if (!tile.IsEmpty) return false;
                if (tile.IsWalled) return false;
            }

            return true;
        }
    }
}
