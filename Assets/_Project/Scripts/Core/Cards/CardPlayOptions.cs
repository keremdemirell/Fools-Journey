namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// The choices a player makes at the moment they play a card, beyond where it goes. It sits
    /// alongside the anchor tile and attack column, which are already choices of exactly this kind —
    /// that is the whole design: The Magician's element and The Hanged Man's / Death's target are
    /// picked BEFORE the play, the way a targeted spell works in most card games, so no mid-resolution
    /// "prompt the player and wait" machinery is needed.
    ///
    /// The one ability this can't cover is Judgement's "judge" (GDD 11.XX), which asks a question
    /// every time something dies, long after Judgement was played. That one genuinely needs a
    /// deferred-decision model, and is not built here.
    ///
    /// Which choices a card wants is published by its ability (UnitAbility.Choice), so a UI can ask
    /// for exactly those and nothing else. A card that takes no choices ignores this entirely.
    /// </summary>
    public readonly struct CardPlayOptions
    {
        /// <summary>The Magician: which element's "2 of" trait to take on.</summary>
        public readonly Element? ChosenElement;

        /// <summary>The Hanged Man / Death: which of your own units to sacrifice or kill. Null = no target.</summary>
        public readonly int? TargetUnitId;

        public CardPlayOptions(Element? chosenElement = null, int? targetUnitId = null)
        {
            ChosenElement = chosenElement;
            TargetUnitId = targetUnitId;
        }

        public static readonly CardPlayOptions None = default;
    }
}
