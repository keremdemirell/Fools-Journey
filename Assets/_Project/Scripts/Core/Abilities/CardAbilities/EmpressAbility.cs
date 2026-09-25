using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// III. The Empress (GDD 11.III) — greens the tiles she occupies; friendly cards later played on
    /// green tiles gain +3 HP. Condition: at least 2 Queens played previously this match.
    ///
    /// Two readings the GDD leaves open, decided here and flagged rather than silently assumed:
    ///  - The bonus is +3 per UNIT, not +3 per green tile it covers. "Any friendly cards played on
    ///    these green tiles gain +3 HP" reads as a property of the card played, and per-tile scaling
    ///    would hand a 4x4 Tower +48 HP for standing on one Empress's worth of ground.
    ///  - Greening OUTLIVES the Empress. She "greens the tiles", i.e. changes the terrain; nothing
    ///    in the card says the effect ends with her, and the tiles only become usable by anything
    ///    else once she's gone (she's standing on them).
    ///
    /// The +3 itself is applied at placement time in CardPlayResolver, not here — it has to be part
    /// of the new unit's StartingHp before the unit exists, and this ability only ever sees units
    /// that already do.
    /// </summary>
    public sealed class EmpressAbility : UnitAbility
    {
        public const int RequiredQueensPlayed = 2;
        public const int GreenTileHpBonus = 3;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            int queens = query.GetHistory(side).CountOfKind(CardKind.Queen);
            if (queens < RequiredQueensPlayed)
            {
                reason = $"The Empress needs {RequiredQueensPlayed} Queens played this match (you have played {queens}).";
                return false;
            }

            reason = null;
            return true;
        }

        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            var coords = ctx.Board.GetFootprintCoords(self.AnchorCoord, self.Footprint);
            if (coords == null) return;

            foreach (var coord in coords)
                if (ctx.Board.TryGetTile(coord, out var tile))
                    tile.IsGreened = true;
        }
    }
}
