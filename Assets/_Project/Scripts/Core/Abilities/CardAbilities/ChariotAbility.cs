using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// VII. The Chariot (GDD 11.VII) — "Before attacking, checks for an enemy in front. If blocked, it
    /// randomly repositions to an unblocked tile (pushing friendly units if necessary) and then attacks.
    /// If it cannot reposition, it attacks from its current spot." Attack 1, 4x2.
    ///
    /// Its Attack 1 is still an ordinary lane attack, resolved by AttackResolver like any other. What
    /// this adds is a step at the START of the Combat phase: if an enemy stands in its lane, it moves
    /// first, so the normal attack that follows lands from the new spot.
    ///
    /// The user left the movement details to me. The rules chosen:
    ///  - A destination is any spot on your side where its whole 4x2 fits and at least one of its
    ///    columns has a clear lane; it then attacks down the lowest such column.
    ///  - Free spots are preferred, picked at random ("randomly repositions").
    ///  - Only if there are none does it PUSH: it may take a spot occupied solely by friendly units that
    ///    lie entirely inside it, and those units slide into the space the Chariot just left, keeping
    ///    their arrangement — a swap. Nothing is ever shoved off the board or into another unit.
    ///  - Destinations never overlap its current spot. That's what makes the swap exact: the vacated
    ///    4x2 is the same shape as the destination, so everything pushed out of one fits the other.
    ///    Only rectangular units can be pushed (a 7-tile cluster doesn't translate cleanly on a hex
    ///    grid — see FootprintShapes), which in practice means anything that isn't a Queen, King or Tree.
    ///  - Units standing on an Emperor's wall — the Chariot itself, or anything it would push — can't
    ///    be moved, and it never moves onto a wall (anyone's). UnitRegistry.TryMoveUnits enforces
    ///    both; the checks here just avoid offering moves that would be refused.
    ///  - No legal spot means it stays and attacks from where it stands, exactly as the card says.
    /// </summary>
    public sealed class ChariotAbility : UnitAbility
    {
        public override void OnBeforeCombat(IAbilityContext ctx, UnitInstance self)
        {
            var grid = ctx.Board;
            var current = grid.GetFootprintCoords(self.AnchorCoord, self.Footprint);
            if (current == null || current.Count == 0) return;

            if (StandsInWall(grid, current)) return;
            if (BoardQueries.EnemiesInLane(grid, ctx.Units, self.Owner, current, self.AttackColumn).Count == 0) return;

            var free = new List<List<UnitMove>>();
            var pushing = new List<List<UnitMove>>();

            foreach (var tile in grid.AllTiles)
            {
                if (tile.Side != self.Owner) continue;

                var destination = grid.GetFootprintCoords(tile.Coord, self.Footprint);
                if (destination == null || destination.Count == 0) continue;

                if (!TryPlanMove(ctx, self, current, tile.Coord, destination, out var moves, out int pushed)) continue;
                (pushed == 0 ? free : pushing).Add(moves);
            }

            var pool = free.Count > 0 ? free : pushing;
            if (pool.Count == 0) return;

            ctx.Units.TryMoveUnits(pool[ctx.Rng.Next(pool.Count)]);
        }

        private static bool TryPlanMove(
            IAbilityContext ctx, UnitInstance self, IReadOnlyList<HexCoord> current, HexCoord destinationAnchor,
            IReadOnlyList<HexCoord> destination, out List<UnitMove> moves, out int pushedCount)
        {
            moves = null;
            pushedCount = 0;

            var grid = ctx.Board;
            var currentSet = new HashSet<HexCoord>(current);
            var destinationSet = new HashSet<HexCoord>(destination);
            var occupants = new Dictionary<int, UnitInstance>();

            foreach (var coord in destination)
            {
                if (currentSet.Contains(coord)) return false; // no overlap — see the class note on why
                if (!grid.TryGetTile(coord, out var tile)) return false;
                if (tile.Side != self.Owner || tile.IsWalled) return false;
                if (tile.IsEmpty) continue;

                var occupant = ctx.Units.GetUnitAt(coord);
                if (occupant == null || occupant.Owner != self.Owner) return false;
                occupants[occupant.Id] = occupant;
            }

            int attackColumn = FirstClearColumn(ctx, self.Owner, destination);
            if (attackColumn < 0) return false;

            var from = self.AnchorCoord.ToOffset();
            var to = destinationAnchor.ToOffset();
            int dCol = to.Col - from.Col;
            int dRow = to.Row - from.Row;

            moves = new List<UnitMove> { new UnitMove(self.Id, destinationAnchor, attackColumn) };

            foreach (var occupant in occupants.Values)
            {
                if (occupant.Footprint == FootprintShapeType.SevenCluster || occupant.Footprint == FootprintShapeType.EntireBoard)
                    return false;

                var occupantCoords = grid.GetFootprintCoords(occupant.AnchorCoord, occupant.Footprint);
                foreach (var coord in occupantCoords)
                {
                    if (!destinationSet.Contains(coord)) return false; // sticks out: can't be swapped cleanly
                    if (grid.TryGetTile(coord, out var tile) && tile.IsWalled) return false; // inside a wall: can't be moved
                }

                var anchor = occupant.AnchorCoord.ToOffset();
                moves.Add(new UnitMove(
                    occupant.Id,
                    new OffsetCoord(anchor.Col - dCol, anchor.Row - dRow).ToAxial(),
                    occupant.AttackColumn - dCol));
            }

            pushedCount = occupants.Count;
            return true;
        }

        /// <summary>The lowest column of the footprint whose lane holds no enemy, or -1.</summary>
        private static int FirstClearColumn(IAbilityContext ctx, PlayerSide owner, IReadOnlyList<HexCoord> footprint)
        {
            var columns = new SortedSet<int>();
            foreach (var coord in footprint) columns.Add(coord.ToOffset().Col);

            foreach (int column in columns)
                if (BoardQueries.EnemiesInLane(ctx.Board, ctx.Units, owner, footprint, column).Count == 0)
                    return column;

            return -1;
        }

        private static bool StandsInWall(HexGrid grid, IReadOnlyList<HexCoord> coords)
        {
            foreach (var coord in coords)
                if (grid.TryGetTile(coord, out var tile) && tile.IsWalled) return true;
            return false;
        }
    }
}
