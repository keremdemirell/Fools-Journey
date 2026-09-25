using System.Collections.Generic;

namespace ArcanaWars.Core.Board
{
    /// <summary>
    /// The unit footprints from GDD section 4.2, plus ThreeByOne for The Lovers. The Lovers' two-unit
    /// placement itself is ability code (LoversAbility); only the shape of each half lives here.
    ///
    /// Unity serializes enums as their integer value, so the MajorArcanaCardDefinition assets store
    /// these as numbers. NEW VALUES MUST BE APPENDED AT THE END — inserting one in the middle would
    /// silently shift every card after it onto the wrong shape.
    /// </summary>
    public enum FootprintShapeType
    {
        Single,       // 1x1 — most Minor Arcana, Aces, Knights, Princes, Fool, Hermit
        OneByTwo,     // 1x2 — High Priestess
        TwoByOne,     // 2x1 — Death
        OneByThree,   // 1x3 — Empress, Emperor
        TwoByTwo,     // 2x2 — Hierophant, Strength, Justice
        TwoByThree,   // 2x3 — Devil
        SevenCluster, // 1 center + 6 surrounding — Queens, Kings, Hanged Man's Tree
        ThreeByThree, // 3x3 — Wheel of Fortune, Temperance, Moon
        FourByTwo,    // 4x2 — Chariot
        TwoByFour,    // 2x4 — Judgement
        FourByFour,   // 4x4 — Tower, Sun
        EntireBoard,  // The World — resolved dynamically by HexGrid, not here
        ThreeByOne    // 3x1 (3 rows x 1 col) — each of The Lovers' two units. Appended last; see the note above.
    }

    /// <summary>
    /// Resolves a footprint shape into absolute hex tiles given an anchor tile.
    ///
    /// Two different translation rules are used depending on the shape, and mixing them up
    /// silently produces wrong tiles on odd-parity rows:
    ///  - Rectangular shapes (4x4, 2x3, ...) are defined by column/row deltas and must be
    ///    translated in OFFSET space (anchor.Col + dCol, anchor.Row + dRow), then each
    ///    resulting tile converted to axial individually.
    ///  - The 7-tile cluster is a true hex ring and must be translated in AXIAL space
    ///    (anchor + neighbor directions), because offset coordinates are not translation-
    ///    invariant for hex adjacency — the same relative offset means a different hex
    ///    shape depending on whether the anchor's row is even or odd.
    /// </summary>
    public static class FootprintShapes
    {
        public static IReadOnlyList<HexCoord> Resolve(HexCoord anchor, FootprintShapeType type)
        {
            switch (type)
            {
                // Rectangle(anchor, cols, rows) — shape names are Rows x Cols (e.g. "2x3" = 2 rows of 3), so args are passed (cols, rows), not (rows, cols).
                case FootprintShapeType.Single: return Rectangle(anchor, 1, 1);
                case FootprintShapeType.OneByTwo: return Rectangle(anchor, 2, 1);   // 1 row x 2 cols
                case FootprintShapeType.TwoByOne: return Rectangle(anchor, 1, 2);   // 2 rows x 1 col
                case FootprintShapeType.OneByThree: return Rectangle(anchor, 3, 1); // 1 row x 3 cols
                case FootprintShapeType.TwoByTwo: return Rectangle(anchor, 2, 2);
                case FootprintShapeType.TwoByThree: return Rectangle(anchor, 3, 2); // 2 rows x 3 cols
                case FootprintShapeType.ThreeByThree: return Rectangle(anchor, 3, 3);
                case FootprintShapeType.FourByTwo: return Rectangle(anchor, 2, 4);  // 4 rows x 2 cols
                case FootprintShapeType.TwoByFour: return Rectangle(anchor, 4, 2);  // 2 rows x 4 cols
                case FootprintShapeType.FourByFour: return Rectangle(anchor, 4, 4);
                case FootprintShapeType.ThreeByOne: return Rectangle(anchor, 1, 3); // 3 rows x 1 col
                case FootprintShapeType.SevenCluster: return SevenTileCluster(anchor);
                case FootprintShapeType.EntireBoard:
                    // No fixed shape — HexGrid.GetFootprintCoords resolves this to every
                    // playable tile on the board at placement time.
                    return null;
                default:
                    return Rectangle(anchor, 1, 1);
            }
        }

        private static IReadOnlyList<HexCoord> Rectangle(HexCoord anchor, int cols, int rows)
        {
            var anchorOffset = anchor.ToOffset();
            var result = new List<HexCoord>(cols * rows);
            for (int dRow = 0; dRow < rows; dRow++)
                for (int dCol = 0; dCol < cols; dCol++)
                    result.Add(new OffsetCoord(anchorOffset.Col + dCol, anchorOffset.Row + dRow).ToAxial());
            return result;
        }

        private static IReadOnlyList<HexCoord> SevenTileCluster(HexCoord anchor)
        {
            var result = new List<HexCoord>(7) { anchor };
            result.AddRange(anchor.Ring(1));
            return result;
        }
    }
}
