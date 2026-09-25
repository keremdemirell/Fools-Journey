namespace ArcanaWars.Core.Match
{
    /// <summary>What Judgement's owner decides about a dead unit (GDD 11.XX).</summary>
    public enum JudgementVerdict
    {
        ReturnToHand, // its card goes into the JUDGE's hand — judging an enemy steals its card
        Discard       // it leaves the graveyard for good
    }

    /// <summary>
    /// A death waiting for Judgement's owner to rule on it: "If any friendly or foe unit dies, you can
    /// 'judge' them — deciding to return it to your hand or discard it permanently."
    ///
    /// This is the one decision in the game that can't be made at play time (CardPlayOptions), because
    /// it arises every time something dies, long after Judgement was played. So deaths are queued on
    /// MatchState.PendingJudgements and answered through MatchState.ResolveJudgement, whenever the
    /// player gets to them. There is no deadline: an unanswered judgement simply leaves the unit in its
    /// graveyard, exactly as if Judgement had never been on the board. That keeps the simulation from
    /// ever having to stop and wait — which matters for a simultaneous-round game whose "how do two
    /// people take turns at one screen" question isn't settled yet.
    /// </summary>
    public sealed class PendingJudgement
    {
        public int Id { get; }

        /// <summary>The player who gets to decide — Judgement's owner, whoever the dead unit belonged to.</summary>
        public PlayerSide Judge { get; }

        public GraveyardEntry Entry { get; }

        /// <summary>Tokens that were never a card have nothing to return; they can only be discarded.</summary>
        public bool CanReturnToHand => Entry.SourceCard != null;

        public PendingJudgement(int id, PlayerSide judge, GraveyardEntry entry)
        {
            Id = id;
            Judge = judge;
            Entry = entry;
        }
    }
}
