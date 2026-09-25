namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// One card sitting in a hand, plus the per-copy state a held card can carry: the round it
    /// arrived, and any cost discount attached to this particular copy.
    ///
    /// Hand stores entries rather than bare IPlayableCard references because two copies of the same
    /// definition can now genuinely differ — one drawn on turn 1, one drawn by a Wheel of Fortune
    /// on turn 6 at a discount.
    /// </summary>
    public readonly struct HandCard
    {
        public readonly IPlayableCard Card;

        /// <summary>
        /// The round number during which this card entered the hand. Opening-hand cards are recorded
        /// as round 1, not round 0 — the match hasn't started when they're dealt, but GDD 11.II's own
        /// example ("held since Turn 1, played on Turn 8, heals 8") only works out to 8 if a card you
        /// started with counts as having arrived on turn 1: 1 + (8 - 1).
        /// </summary>
        public readonly int RoundEntered;

        /// <summary>
        /// Pentacles knocked off this copy's cost — Wheel of Fortune's "all cards drawn this turn cost
        /// 1 Mana less" (GDD 11.X). It belongs to the copy, not the definition, and lasts for as long
        /// as the copy stays in hand (user decision): only the cards the Wheel itself drew carry it.
        /// </summary>
        public readonly int CostDiscount;

        /// <summary>Stats this copy gains when played — The Hermit's draw buff. See CardBuff.</summary>
        public readonly CardBuff Buff;

        public HandCard(IPlayableCard card, int roundEntered, int costDiscount = 0, CardBuff buff = default)
        {
            Card = card;
            RoundEntered = roundEntered;
            CostDiscount = costDiscount;
            Buff = buff;
        }

        /// <summary>How many full turns this card has been held, as GDD 11.II counts them.</summary>
        public int TurnsHeld(int currentRound) => currentRound - RoundEntered;

        /// <summary>
        /// What this copy actually costs to play. Never below 0. Variable-cost cards (the Aces) ignore
        /// the discount: an Ace's cost IS its HP (GDD 10.1), so a discount would be a free HP point
        /// rather than a cheaper card.
        /// </summary>
        public int EffectiveCost(int manaCommitted = 0)
        {
            int printed = Card.GetCost(manaCommitted);
            if (Card.HasVariableCost) return printed;
            int cost = printed - CostDiscount;
            return cost < 0 ? 0 : cost;
        }
    }
}
