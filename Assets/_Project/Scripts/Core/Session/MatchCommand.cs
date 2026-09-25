using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Match;

namespace ArcanaWars.Core.Session
{
    public enum MatchCommandType
    {
        PlayCard,
        Pass,
        AdvancePhase,
        BeginNextRound,
        ResolveJudgement
    }

    /// <summary>
    /// One player action, as plain data rather than a method call. These are the five things
    /// MatchSession can be asked to do, turned into something that can be recorded, replayed, and
    /// eventually put on a wire.
    ///
    /// <para><b>Why it matters.</b> A networked match under the lockstep model doesn't send game
    /// state — it sends this, and both machines run the same simulation over the same ordered list
    /// of commands and arrive at the same board. That only works if a command fully determines what
    /// happens, which is why every field here is a primitive or a nullable primitive and nothing is
    /// an object reference. A reference means nothing on the far machine.</para>
    ///
    /// <para><b>The card is identified by id, not by reference</b>, which needs a word of
    /// explanation because Hand matches copies with ReferenceEquals. Decks are built from a shared
    /// catalog, so all three copies of "5 of Swords" in a deck are the same object; resolving the id
    /// through ICardCatalog therefore yields exactly the instance the hand is holding. Which of
    /// several held copies gets played is then decided by Hand's own deterministic preference rule
    /// (most discounted, then longest held), identically on both machines.</para>
    ///
    /// <para>The wire encoding itself is deliberately not here — that belongs with whatever transport
    /// is chosen. This type exists so that when a transport arrives, the work is mechanical.</para>
    /// </summary>
    public readonly struct MatchCommand
    {
        public readonly MatchCommandType Type;

        /// <summary>Who is acting. Unused by AdvancePhase and BeginNextRound, which belong to the match clock rather than to a player.</summary>
        public readonly PlayerSide Side;

        // --- PlayCard ---
        public readonly string CardId;
        public readonly int AnchorQ;
        public readonly int AnchorR;
        public readonly int AttackColumn;
        public readonly int ManaCommitted;
        public readonly Element? ChosenElement;
        public readonly int? TargetUnitId;

        // --- ResolveJudgement ---
        public readonly int JudgementId;
        public readonly JudgementVerdict Verdict;

        private MatchCommand(
            MatchCommandType type, PlayerSide side, string cardId, int anchorQ, int anchorR,
            int attackColumn, int manaCommitted, Element? chosenElement, int? targetUnitId,
            int judgementId, JudgementVerdict verdict)
        {
            Type = type;
            Side = side;
            CardId = cardId;
            AnchorQ = anchorQ;
            AnchorR = anchorR;
            AttackColumn = attackColumn;
            ManaCommitted = manaCommitted;
            ChosenElement = chosenElement;
            TargetUnitId = targetUnitId;
            JudgementId = judgementId;
            Verdict = verdict;
        }

        public HexCoord Anchor => new HexCoord(AnchorQ, AnchorR);
        public CardPlayOptions Options => new CardPlayOptions(ChosenElement, TargetUnitId);

        public static MatchCommand PlayCard(PlayerSide side, string cardId, HexCoord anchor, int attackColumn, int manaCommitted = 0, CardPlayOptions options = default) =>
            new MatchCommand(MatchCommandType.PlayCard, side, cardId, anchor.Q, anchor.R, attackColumn, manaCommitted,
                options.ChosenElement, options.TargetUnitId, 0, default);

        public static MatchCommand Pass(PlayerSide side) =>
            new MatchCommand(MatchCommandType.Pass, side, null, 0, 0, 0, 0, null, null, 0, default);

        public static MatchCommand AdvancePhase() =>
            new MatchCommand(MatchCommandType.AdvancePhase, PlayerSide.A, null, 0, 0, 0, 0, null, null, 0, default);

        public static MatchCommand BeginNextRound() =>
            new MatchCommand(MatchCommandType.BeginNextRound, PlayerSide.A, null, 0, 0, 0, 0, null, null, 0, default);

        public static MatchCommand ResolveJudgement(PlayerSide side, int judgementId, JudgementVerdict verdict) =>
            new MatchCommand(MatchCommandType.ResolveJudgement, side, null, 0, 0, 0, 0, null, null, judgementId, verdict);

        public override string ToString()
        {
            switch (Type)
            {
                case MatchCommandType.PlayCard:
                    string element = ChosenElement.HasValue ? $" element={ChosenElement.Value}" : "";
                    string target = TargetUnitId.HasValue ? $" target=#{TargetUnitId.Value}" : "";
                    return $"{Side} plays {CardId} @({AnchorQ},{AnchorR}) col={AttackColumn} mana={ManaCommitted}{element}{target}";
                case MatchCommandType.Pass: return $"{Side} passes";
                case MatchCommandType.AdvancePhase: return "advance phase";
                case MatchCommandType.BeginNextRound: return "begin next round";
                case MatchCommandType.ResolveJudgement: return $"{Side} judges #{JudgementId} -> {Verdict}";
                default: return Type.ToString();
            }
        }
    }
}
