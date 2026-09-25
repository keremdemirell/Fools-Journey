namespace ArcanaWars.Core.GameLoop
{
    /// <summary>
    /// Whose turn it is inside one Action phase, and when that phase is over.
    ///
    /// The GDD's loop diagram (section 2) describes the Action Phase as simultaneous — both players
    /// planning at once, hidden from each other. That was replaced (user decision, 2026-09-16) with
    /// strict alternation: one card per turn, then the turn passes. The rest of the round is
    /// untouched; Combat, Decay and WinCheck involve no decisions, so they still resolve for both
    /// players at once.
    ///
    /// Two consequences of that switch are worth knowing before changing anything here, because both
    /// are balance decisions encoded as code rather than accidents:
    ///
    ///  1. Blocking (GDD 5.2) stops being a guess. Under simultaneous play a defender had to guess
    ///     which column an attacker committed to; under alternation they simply watch and answer.
    ///     That makes ACTING LAST in a round an advantage, which is what the two rules below exist
    ///     to keep fair.
    ///  2. Passing is a real move, not a concession. Because a pass yields the turn without ending
    ///     your phase, "you commit first, I'll answer" is a legitimate line — and since mana does
    ///     not carry over (GDD 7.1), a player who passes early still gets to spend later.
    ///
    /// The two fairness rules:
    ///  - <b>Priority passing.</b> A pass yields the turn but does not lock you out. The phase ends
    ///    only when both players pass in succession, so nobody is handed a free unanswered final
    ///    placement. Any card played resets the count — a pass only counts while it is the last
    ///    thing that happened.
    ///  - <b>Alternating starter.</b> The player who moves first alternates by round, so whatever
    ///    edge first or last turns out to carry is split evenly instead of compounding all match.
    ///
    /// This class is deliberately separate from RoundManager, for the same reason CardPlayResolver is
    /// separate from MatchState: it is a small rule with edge cases (what a pass means, when the
    /// phase ends) that is worth being able to test on its own.
    /// </summary>
    public class ActionTurns
    {
        /// <summary>Consecutive passes that end the Action phase. Two, because there are two players — every player has declined in a row.</summary>
        private const int PassesToEndPhase = 2;

        /// <summary>Whose turn it is. Only meaningful while the Action phase is running; it holds its last value otherwise.</summary>
        public PlayerSide ActiveSide { get; private set; }

        /// <summary>True once both players have passed in succession — the Action phase has nothing left to offer and the round should advance to Combat.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>How many passes have happened back to back. Reset by any card being played.</summary>
        public int ConsecutivePasses { get; private set; }

        /// <summary>
        /// Who moves first in a given round: Player A on odd rounds, Player B on even ones. Round
        /// numbers start at 1 (see RoundManager.BeginNextRound), so round 1 opens with A.
        /// </summary>
        public static PlayerSide StarterFor(int roundNumber) => roundNumber % 2 == 1 ? PlayerSide.A : PlayerSide.B;

        /// <summary>Starts a fresh Action phase for the given round. Called by RoundManager on entering the phase, so it cannot be forgotten.</summary>
        public void Begin(int roundNumber)
        {
            ActiveSide = StarterFor(roundNumber);
            ConsecutivePasses = 0;
            IsComplete = false;
        }

        /// <summary>
        /// Records that the active player played a card. The pass count resets — a pass only ends the
        /// phase while it is still the most recent thing that happened — and the turn hands over.
        /// </summary>
        public void NoteActionTaken()
        {
            if (IsComplete) return;

            ConsecutivePasses = 0;
            ActiveSide = ActiveSide.Opponent();
        }

        /// <summary>
        /// Records that the active player passed. The second pass in a row ends the phase; the first
        /// merely hands the turn over, leaving the passer free to act again if their opponent does.
        /// </summary>
        public void NotePassed()
        {
            if (IsComplete) return;

            ConsecutivePasses++;
            if (ConsecutivePasses >= PassesToEndPhase)
            {
                IsComplete = true;
                return;
            }

            ActiveSide = ActiveSide.Opponent();
        }
    }
}
