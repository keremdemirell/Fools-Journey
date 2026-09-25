namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// The one definition of a Court Card's id — "knight_Swords", "queen_Cups", and so on.
    ///
    /// Core needs these ids to attach the Knight's, Queen's and King's abilities (AbilityRegistry), and
    /// the Cards assembly needs the same ids for the card assets themselves. Keeping two copies of the
    /// format would let them drift apart silently, with no error — an ability would just stop being
    /// found. So the format lives here, in Core, and CourtCardRules.GetDefinitionId calls it. (The
    /// dependency can only run that way round: Cards can see Core, Core can never see Cards.)
    ///
    /// The format is exactly what CourtCardRules always produced — rank name lower-cased, underscore,
    /// element name — so no existing asset or save changes.
    /// </summary>
    public static class CourtIds
    {
        public static string For(CardKind rank, Element element) => $"{rank.ToString().ToLowerInvariant()}_{element}";
    }
}
