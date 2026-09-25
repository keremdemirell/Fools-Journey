namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// What tier/rank a card is, expressed in terms Core can actually read.
    ///
    /// Core needs this because several abilities gate on what has been PLAYED, not on what is on
    /// the board: the Empress needs "2+ Queens played", the Emperor "2+ Kings", the Hierophant
    /// "8 units played". Court ranks live in the Cards assembly (CourtRank), which Core cannot
    /// reference, so the ranks that matter are mirrored here and each card definition reports its
    /// own Kind through IPlayableCard.
    ///
    /// The Court ranks are listed individually rather than as a single "CourtCard" value precisely
    /// because "2 Queens" and "2 Kings" are different conditions — collapsing them would make both
    /// uncheckable.
    /// </summary>
    public enum CardKind
    {
        MinorArcana,
        Ace,
        Knight,
        Prince,
        Queen,
        King,
        MajorArcana
    }
}
