using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// Positional lookups that more than one ability needs. These live here rather than on HexGrid
    /// because they're about UNITS and facing, not about tiles — HexGrid deliberately knows nothing
    /// about who occupies what beyond an occupant id.
    ///
    /// "In front of" is defined the same way AttackResolver defines a lane: columns are lanes, and
    /// forward means toward the enemy's back rank (increasing rows for Player A, decreasing for B).
    /// Keeping that one definition shared is the point — a Hierophant that buffed a different set of
    /// tiles than the ones its own units attack down would be nearly impossible to reason about.
    /// </summary>
    public static class BoardQueries
    {
        /// <summary>Every distinct column the unit's footprint occupies.</summary>
        public static HashSet<int> FootprintColumns(HexGrid grid, UnitInstance unit)
        {
            var cols = new HashSet<int>();
            var coords = grid.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
            if (coords == null) return cols;

            foreach (var coord in coords)
                cols.Add(coord.ToOffset().Col);
            return cols;
        }

        /// <summary>
        /// Friendly units standing in front of this one (GDD 11.V, The Hierophant): any allied unit
        /// occupying a tile in one of this unit's own columns, on a row past this unit's front edge
        /// in that column, heading toward the enemy.
        ///
        /// Deduplicated by unit id, because a wide ally spanning several of the Hierophant's columns
        /// is still one unit and must be buffed once, not once per column.
        /// </summary>
        public static List<UnitInstance> FriendlyUnitsInFront(IMatchQuery query, UnitRegistry units, UnitInstance unit)
        {
            var grid = query.Board;
            var results = new List<UnitInstance>();
            var seen = new HashSet<int> { unit.Id };
            int direction = unit.Owner == PlayerSide.A ? 1 : -1;

            var ownCoords = grid.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
            if (ownCoords == null) return results;

            // Front row per column, so a footprint that isn't a flat rectangle still starts scanning
            // from its own leading edge rather than from behind itself.
            var frontRowByCol = new Dictionary<int, int>();
            foreach (var coord in ownCoords)
            {
                var offset = coord.ToOffset();
                if (!frontRowByCol.TryGetValue(offset.Col, out var front) ||
                    (direction > 0 ? offset.Row > front : offset.Row < front))
                {
                    frontRowByCol[offset.Col] = offset.Row;
                }
            }

            foreach (var pair in frontRowByCol)
            {
                for (int row = pair.Value + direction; row >= 0 && row < grid.Rows; row += direction)
                {
                    var coord = new OffsetCoord(pair.Key, row).ToAxial();
                    if (!grid.Contains(coord)) continue; // chamfered corner

                    var occupant = units.GetUnitAt(coord);
                    if (occupant == null || occupant.IsDead) continue;
                    if (occupant.Owner != unit.Owner) continue;
                    if (!seen.Add(occupant.Id)) continue;

                    results.Add(occupant);
                }
            }

            return results;
        }

        /// <summary>
        /// Live units with at least one tile within `radius` of any tile in `origin`. Used by The
        /// Tower's explosion. Excludes `excludeUnitId` (the exploding unit itself, which is already
        /// off the board by then, but the guard keeps this reusable).
        /// </summary>
        public static List<UnitInstance> UnitsWithinRadius(
            HexGrid grid, UnitRegistry units, IReadOnlyList<HexCoord> origin, int radius, int excludeUnitId = -1)
        {
            var results = new List<UnitInstance>();
            var seen = new HashSet<int>();
            if (origin == null) return results;

            foreach (var unit in units.AllUnits)
            {
                if (unit.Id == excludeUnitId || unit.IsDead) continue;
                if (seen.Contains(unit.Id)) continue;

                var unitCoords = grid.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
                if (unitCoords == null) continue;

                foreach (var uc in unitCoords)
                {
                    bool near = false;
                    foreach (var oc in origin)
                    {
                        if (uc.DistanceTo(oc) <= radius) { near = true; break; }
                    }

                    if (near)
                    {
                        seen.Add(unit.Id);
                        results.Add(unit);
                        break;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Every live enemy in one column of a footprint, nearest first — THE definition of an attack
        /// lane (GDD 5.1/5.2). The search starts one step past the footprint's own front edge in that
        /// column (so a unit several rows tall doesn't scan its own tiles) and runs toward the enemy's
        /// back rank. Missing tiles (chamfered corners) are skipped, not treated as blocking.
        ///
        /// It lives here rather than inside AttackResolver because The Chariot has to ask the same
        /// question about a spot it isn't standing on yet ("would I be blocked over there?"), and two
        /// copies of the lane rule would eventually disagree.
        ///
        /// Each enemy appears ONCE even if it fills several rows of the lane. The version that used to
        /// live in AttackResolver added a unit once per tile, so a 2-row enemy was pierced twice by a
        /// single Over-Attack — fixed here when the rule moved.
        ///
        /// `column` must be one of the footprint's columns (placement and movement both guarantee it);
        /// if it isn't, there's no front edge to search from and the lane is reported empty.
        /// </summary>
        public static List<UnitInstance> EnemiesInLane(
            HexGrid grid, UnitRegistry units, PlayerSide owner, IReadOnlyList<HexCoord> footprint, int column)
        {
            var results = new List<UnitInstance>();
            if (footprint == null) return results;

            int direction = owner == PlayerSide.A ? 1 : -1; // A's back rank is row 0, B's is row Rows-1

            int? frontRow = null;
            foreach (var coord in footprint)
            {
                var offset = coord.ToOffset();
                if (offset.Col != column) continue;
                if (frontRow == null || (direction > 0 ? offset.Row > frontRow : offset.Row < frontRow))
                    frontRow = offset.Row;
            }

            if (frontRow == null) return results;

            for (int row = frontRow.Value + direction; row >= 0 && row < grid.Rows; row += direction)
            {
                var coord = new OffsetCoord(column, row).ToAxial();
                if (!grid.TryGetTile(coord, out _)) continue;

                var occupant = units.GetUnitAt(coord);
                if (occupant != null && !occupant.IsDead && occupant.Owner != owner && !results.Contains(occupant))
                    results.Add(occupant);
            }

            return results;
        }

        /// <summary>
        /// Would `shape` fit at `anchor` for `owner` once the unit `replacedUnitId` is gone? Every tile
        /// must exist, be on the owner's own side (GDD 4.3), not be walled, and be either empty or
        /// held by the unit being replaced — and must not be one of the `reserved` tiles, which is how
        /// a card that puts TWO things on the board (the Hanged Man and its Tree, Death and its
        /// replacement) stops them from both claiming the same ground.
        ///
        /// Used by choice validation, before anything is spent: a replacement that turns out not to
        /// fit is refused up front rather than discovered after the unit is already dead.
        /// </summary>
        public static bool FitsReplacing(
            IMatchQuery query, PlayerSide owner, HexCoord anchor, FootprintShapeType shape,
            int replacedUnitId, IReadOnlyList<HexCoord> reserved)
        {
            var coords = query.Board.GetFootprintCoords(anchor, shape);
            if (coords == null || coords.Count == 0) return false;

            foreach (var coord in coords)
            {
                if (!query.Board.TryGetTile(coord, out var tile)) return false;
                if (tile.Side != owner || tile.IsWalled) return false;
                if (!tile.IsEmpty && tile.OccupantId != replacedUnitId) return false;

                if (reserved != null)
                    foreach (var taken in reserved)
                        if (taken == coord) return false;
            }

            return true;
        }
    }
}
