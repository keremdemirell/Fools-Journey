namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// The agreed numbers for hand flow, in one place for the same reason ManaCurve holds the mana
    /// numbers — so a balance change is one edit, not a hunt through the codebase.
    ///
    /// Deck *size* used to live here too. It moved to DeckRules when real deckbuilding arrived: how
    /// big a legal deck is turned out to be a deck-construction rule, checked long before a match
    /// starts, not a fact about drawing cards during one.
    /// </summary>
    public static class HandRules
    {
        public const int StartingHandSize = 5;
        public const int DrawPerRound = 1;
        public const int MaxHandSize = 14;
    }
}
