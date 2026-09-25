using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// IX. The Hermit (GDD 11.IX), as reworked by the user — every card you DRAW while the Hermit is
    /// isolated gets +1 HP, +1 Attack and +1 Heal.
    ///
    /// The GDD's original buffed "all units in your deck every turn". That can't be stored: a deck is a
    /// list of shared, immutable card definitions (the same asset can sit in both players' decks), so
    /// there is nowhere per-match to keep a deck-wide modifier. Buffing each copy as it's drawn keeps
    /// the spirit — the Hermit makes your FUTURE cards stronger — and lives on the copy in hand
    /// (HandCard.Buff), exactly where the Wheel of Fortune's discount already lives. A buffed card keeps
    /// its buff even if the Hermit dies before it's played.
    ///
    /// ISOLATION (confirmed by the user, 2026-09-15: "the Hermit should do its job when he is alone"):
    /// "as long as there are no units within a 2-tile radius", friend or foe. Checked fresh at every
    /// single draw — alone at that moment, the card is buffed; anyone within 2 tiles, it isn't.
    /// </summary>
    public sealed class HermitAbility : UnitAbility
    {
        public const int IsolationRadius = 2;
        public static readonly CardBuff DrawnCardBuff = new CardBuff(hp: 1, attack: 1, heal: 1);

        public override CardBuff DrawBuff(IMatchQuery query, UnitInstance self) =>
            IsIsolated(query, self) ? DrawnCardBuff : CardBuff.None;

        private static bool IsIsolated(IMatchQuery query, UnitInstance self)
        {
            var mine = query.Board.GetFootprintCoords(self.AnchorCoord, self.Footprint);
            if (mine == null) return false;

            foreach (var unit in query.AllUnits)
            {
                if (unit.Id == self.Id || unit.IsDead) continue;

                var theirs = query.Board.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
                if (theirs == null) continue;

                foreach (var a in mine)
                    foreach (var b in theirs)
                        if (a.DistanceTo(b) <= IsolationRadius) return false;
            }

            return true;
        }
    }
}
