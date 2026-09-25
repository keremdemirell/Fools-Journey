using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Cards
{
    /// <summary>Why a card play was rejected. A plain bool wouldn't be enough — the UI needs to say "you can't afford that" rather than just refusing silently.</summary>
    public enum CardPlayFailure
    {
        None,
        WrongPhase,        // cards are only playable during the Action phase
        NotYourTurn,       // it is the Action phase, but the other player holds the turn — see ActionTurns
        CardNotInHand,
        CannotAfford,      // cost exceeds the mana remaining this round
        IllegalPlacement,  // footprint doesn't fit, tiles occupied/walled, or attack column isn't part of the footprint
        ConditionNotMet,   // a Major Arcana play condition (GDD section 11) isn't satisfied — see CardPlayResult.Reason for which
        InvalidChoice,     // a play-time choice (element, target) is missing or illegal — see CardPlayResult.Reason
        MatchOver          // a Soul has already hit 0; the result is decided and nothing further can be played
    }

    /// <summary>
    /// Plain-English wording for each refusal, in Core so that every surface says the same thing.
    /// It used to live in the debug view, which was fine while that was the only screen; once a
    /// networked match could also report a refusal, a second copy would have started drifting.
    /// </summary>
    public static class CardPlayFailureText
    {
        public static string Describe(CardPlayFailure failure)
        {
            switch (failure)
            {
                case CardPlayFailure.WrongPhase: return "it isn't the Action phase.";
                case CardPlayFailure.NotYourTurn: return "it isn't your turn.";
                case CardPlayFailure.CardNotInHand: return "that card isn't in hand.";
                case CardPlayFailure.CannotAfford: return "not enough Pentacles.";
                case CardPlayFailure.IllegalPlacement: return "those tiles are blocked or off-board.";
                case CardPlayFailure.ConditionNotMet: return "its play condition isn't met.";
                case CardPlayFailure.InvalidChoice: return "that choice isn't legal.";
                case CardPlayFailure.MatchOver: return "the match is over.";
                default: return failure.ToString();
            }
        }
    }

    public readonly struct CardPlayResult
    {
        public readonly bool Success;
        public readonly CardPlayFailure Failure;

        /// <summary>The unit that reached the board. Null on failure.</summary>
        public readonly UnitInstance Unit;

        /// <summary>The hand entry that was spent, so a caller can see how long it had been held (The High Priestess). Default on failure.</summary>
        public readonly HandCard PlayedFrom;

        /// <summary>The play-time choices the play was made with, handed on to the card's on-play ability.</summary>
        public readonly CardPlayOptions Options;

        /// <summary>
        /// Human-readable explanation, filled in for ConditionNotMet and InvalidChoice. Those are the
        /// failures a player genuinely cannot deduce from the board — "you can't afford that" is obvious
        /// from the mana counter, "The Empress needs 2 Queens played" is not.
        /// </summary>
        public readonly string Reason;

        private CardPlayResult(bool success, CardPlayFailure failure, UnitInstance unit, HandCard playedFrom, CardPlayOptions options, string reason)
        {
            Success = success;
            Failure = failure;
            Unit = unit;
            PlayedFrom = playedFrom;
            Options = options;
            Reason = reason;
        }

        public static CardPlayResult Ok(UnitInstance unit, HandCard playedFrom, CardPlayOptions options) =>
            new CardPlayResult(true, CardPlayFailure.None, unit, playedFrom, options, null);

        public static CardPlayResult Failed(CardPlayFailure failure, string reason = null) =>
            new CardPlayResult(false, failure, null, default, default, reason);
    }
}
