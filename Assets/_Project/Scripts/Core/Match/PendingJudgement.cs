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
    /// player gets to them.
    ///
    /// <para><b>The deadline (user decision, 2026-09-25).</b> A ruling lapses when it has gone
    /// unanswered past <see cref="RoundsToDecide"/> full rounds — and lapsing does exactly what never
    /// answering used to do: the unit simply stays in its graveyard, as if Judgement had never been on
    /// the board. So the deadline changes WHEN a ruling becomes final, never WHAT happens. That
    /// distinction is what keeps it safe: nothing waits on a player, so an opponent who ignores their
    /// prompts (or disconnects mid-match) can still never stall the game — which was the whole reason
    /// there was no deadline before.</para>
    ///
    /// <para>Without one, a Judgement that lives several rounds queues a ruling for every death in
    /// them — decay alone kills units every round — so the queue only ever grew.</para>
    /// </summary>
    public sealed class PendingJudgement
    {
        /// <summary>
        /// How many further rounds a ruling survives after the round it was raised in.
        ///
        /// One, not zero, and the reason is the hot seat: deaths happen in Combat and Decay, at the
        /// END of a round, and in a hot-seat match the judge may not be the one holding the device
        /// then. One full round guarantees they get an Action phase with the controls — and therefore
        /// a chance to even see the ruling — before it lapses.
        /// </summary>
        public const int RoundsToDecide = 1;

        public int Id { get; }

        /// <summary>The player who gets to decide — Judgement's owner, whoever the dead unit belonged to.</summary>
        public PlayerSide Judge { get; }

        public GraveyardEntry Entry { get; }

        /// <summary>The round the unit died in.</summary>
        public int RoundRaised { get; }

        /// <summary>The last round in which this can still be answered. Once the match is past it, the ruling lapses.</summary>
        public int ExpiresAfterRound => RoundRaised + RoundsToDecide;

        /// <summary>Tokens that were never a card have nothing to return; they can only be discarded.</summary>
        public bool CanReturnToHand => Entry.SourceCard != null;

        public PendingJudgement(int id, PlayerSide judge, GraveyardEntry entry, int roundRaised = 0)
        {
            Id = id;
            Judge = judge;
            Entry = entry;
            RoundRaised = roundRaised;
        }

        public bool HasLapsed(int currentRound) => currentRound > ExpiresAfterRound;
    }
}
