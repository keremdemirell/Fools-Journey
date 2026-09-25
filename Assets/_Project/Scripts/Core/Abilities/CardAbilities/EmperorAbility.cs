using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// IV. The Emperor (GDD 11.IV) — "Erects an unbreakable vertical wall across the board. Enemies
    /// cannot be played inside it, and units inside cannot be moved." Attack 1. Condition: 2+ Kings
    /// played this match.
    ///
    /// THE WALL (user's design, revised 2026-09-15): two lines, one in the column just LEFT of the
    /// Emperor and one just RIGHT of it, each running from one end of the board to the other — both
    /// sides, including the neutral row. The Emperor's own columns stay open between them:
    ///
    ///     W . . . W
    ///     W . . . W
    ///     W E E E W
    ///     W . . . W
    ///
    /// Wall tiles are solid for everyone: nobody places there, and nothing moves onto or off them
    /// (Tile.IsWalled; movement is enforced in UnitRegistry.TryMoveUnits). A unit that was already
    /// standing in one of those columns when the wall went up stays where it is, pinned. Next to a
    /// board edge a line may fall off the board; then there's simply one line.
    ///
    /// The first version walled the Emperor's own three columns against the enemy only; the user
    /// replaced it with this after seeing it drawn out.
    ///
    /// DURATION (my call, confirmed by the user): it stands while the Emperor lives and comes down
    /// when it dies — roughly five rounds on its 5 HP. "Unbreakable" is read as "nothing can knock it
    /// down early", not "it outlasts its maker".
    ///
    /// A spawned Emperor (Death's replacement, Judgement's resurrection) raises no wall — spawns don't
    /// fire on-play effects — and tearing down a wall that was never raised is harmless (Tile counts
    /// never go below zero).
    /// </summary>
    public sealed class EmperorAbility : UnitAbility
    {
        public const int RequiredKingsPlayed = 2;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            int kings = query.GetHistory(side).CountOfKind(CardKind.King);
            if (kings < RequiredKingsPlayed)
            {
                reason = $"The Emperor needs {RequiredKingsPlayed} Kings played this match (you have played {kings}).";
                return false;
            }

            reason = null;
            return true;
        }

        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            var footprint = ctx.Board.GetFootprintCoords(self.AnchorCoord, self.Footprint);
            SetWall(ctx.Board, WallColumns(footprint), raise: true);
        }

        public override void OnOwnDeath(IAbilityContext ctx, UnitDeath death)
        {
            SetWall(ctx.Board, WallColumns(death.Coords), raise: false);
        }

        /// <summary>The column just outside each edge of the footprint.</summary>
        public static HashSet<int> WallColumns(IReadOnlyList<HexCoord> footprint)
        {
            var columns = new HashSet<int>();
            if (footprint == null || footprint.Count == 0) return columns;

            int left = int.MaxValue, right = int.MinValue;
            foreach (var coord in footprint)
            {
                int col = coord.ToOffset().Col;
                if (col < left) left = col;
                if (col > right) right = col;
            }

            columns.Add(left - 1);
            columns.Add(right + 1);
            return columns;
        }

        private static void SetWall(HexGrid board, HashSet<int> columns, bool raise)
        {
            foreach (var tile in board.AllTiles)
            {
                if (!columns.Contains(tile.Coord.ToOffset().Col)) continue;
                if (raise) tile.AddWall();
                else tile.RemoveWall();
            }
        }
    }
}
