using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// VI. The Lovers (GDD 11.VI) — "Places 2 units with a gap between them. Left unit: Heal 1. Right
    /// unit: Attack 1. Any unit placed in the gap gains +1 Attack and +1 Heal." Two 3-tile units, 3 HP
    /// each, for one card.
    ///
    /// GAP = 4 tiles (user decision, 2026-09-15; the GDD says 5, which the user judged too much).
    ///
    /// The card's own unit is the LEFT Lover (Heal 1 in its stat block); on play it brings the RIGHT
    /// Lover (Attack 1, SpawnTemplates.LoversRight) five columns over, in the same rows, leaving exactly
    /// four columns between them. "Left" means the lower column number — the same convention the Wands'
    /// "unit to my left" buff already uses for both players.
    ///
    /// SHAPE: each Lover is an UPRIGHT three-tile column (ThreeByOne: 3 rows x 1 col), which the user
    /// liked. It was originally forced — two flat 1x3 strips plus the gap didn't fit the old 9-wide board
    /// — but the board is now 18 wide, so flat strips (10 columns in all) would fit too. Switching is a
    /// matter of the footprint and the constants below, if that's ever wanted. Upright, the formation is
    /// 6 columns wide and the gap is the 4x3 block of tiles between them.
    ///
    /// Room for the right Lover is checked in ValidatePlay, before any mana is spent — including that it
    /// doesn't land on the tiles the left Lover itself is taking.
    ///
    /// THE GAP: a friendly unit that ENTERS the board (played or spawned) with any tile in the gap,
    /// while both Lovers are alive, permanently gains +1 Attack and +1 Heal. "Placed" is read as the
    /// moment of arrival — units already standing there when the Lovers arrive aren't "placed in the
    /// gap", and a unit keeps what it gained if a Lover later dies.
    /// </summary>
    public sealed class LoversAbility : UnitAbility
    {
        public const int GapWidth = 4;
        public const int PartnerColumnOffset = GapWidth + 1;
        public const int PillarHeight = 3;
        public const int GapAttackBonus = 1;
        public const int GapHealBonus = 1;

        public override bool ValidatePlay(IMatchQuery query, PlayRequest request, out string reason)
        {
            var partnerAnchor = PartnerAnchor(request.Anchor);
            if (!BoardQueries.FitsReplacing(query, request.Side, partnerAnchor, FootprintShapeType.ThreeByOne, -1, request.Footprint))
            {
                reason = $"The right-hand Lover needs a free 3-tile column {PartnerColumnOffset} columns to the right, on your side.";
                return false;
            }

            reason = null;
            return true;
        }

        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            var partnerAnchor = PartnerAnchor(self.AnchorCoord);
            ctx.TrySpawnUnit(self.Owner, partnerAnchor, partnerAnchor.ToOffset().Col, SpawnTemplates.LoversRight(self.PrintedCost), out _);
        }

        public override void OnUnitPlaced(IAbilityContext ctx, UnitInstance self, UnitInstance placed)
        {
            if (placed.Owner != self.Owner) return;

            var partner = FindPartner(ctx, self);
            if (partner == null || placed.Id == partner.Id) return;

            var gap = GapCoords(self);
            var placedCoords = ctx.Board.GetFootprintCoords(placed.AnchorCoord, placed.Footprint);
            if (placedCoords == null) return;

            foreach (var coord in placedCoords)
            {
                if (!gap.Contains(coord)) continue;

                placed.AttackPower += GapAttackBonus;
                placed.HealPower += GapHealBonus;
                return;
            }
        }

        private static HexCoord PartnerAnchor(HexCoord leftAnchor)
        {
            var offset = leftAnchor.ToOffset();
            return new OffsetCoord(offset.Col + PartnerColumnOffset, offset.Row).ToAxial();
        }

        /// <summary>The right Lover belonging to this left Lover: same owner, standing exactly where it was placed. Null once it's gone — and with it, the gap.</summary>
        private static UnitInstance FindPartner(IMatchQuery query, UnitInstance self)
        {
            var expected = PartnerAnchor(self.AnchorCoord);
            foreach (var unit in query.AllUnits)
            {
                if (unit.IsDead || unit.Owner != self.Owner) continue;
                if (unit.DefinitionId == SpawnTemplates.LoversRightId && unit.AnchorCoord == expected)
                    return unit;
            }
            return null;
        }

        /// <summary>The columns strictly between the two Lovers, over the 3 rows they share.</summary>
        private static HashSet<HexCoord> GapCoords(UnitInstance leftLover)
        {
            var anchor = leftLover.AnchorCoord.ToOffset();
            var gap = new HashSet<HexCoord>();
            for (int dCol = 1; dCol <= GapWidth; dCol++)
                for (int dRow = 0; dRow < PillarHeight; dRow++)
                    gap.Add(new OffsetCoord(anchor.Col + dCol, anchor.Row + dRow).ToAxial());
            return gap;
        }
    }
}
