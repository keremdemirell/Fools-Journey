using System.Collections.Generic;

namespace ArcanaWars.Core.Cards
{
    /// <summary>Why a deck list isn't legal. A code as well as a message so a UI can react to the kind of problem, not parse prose.</summary>
    public enum DeckIssueCode
    {
        /// <summary>The deck doesn't hold exactly DeckRules.DeckSize cards. Deck-wide, so CardId is null.</summary>
        WrongCardCount,

        /// <summary>More copies of one card than DeckRules.MaxCopiesOf allows for its tier.</summary>
        TooManyCopies,

        /// <summary>An id the catalog has never heard of — a card removed from the database, or a hand-edited save file.</summary>
        UnknownCard
    }

    /// <summary>
    /// One reason a deck is illegal, carrying enough for a UI to both print it and highlight the card
    /// it's about. Message is composed in Core rather than in the view so that every surface — the
    /// deckbuilder, MatchView's startup check, a test harness — words the same problem the same way.
    /// </summary>
    public readonly struct DeckIssue
    {
        public readonly DeckIssueCode Code;

        /// <summary>The card at fault, or null for a problem with the deck as a whole.</summary>
        public readonly string CardId;

        public readonly string Message;

        public DeckIssue(DeckIssueCode code, string cardId, string message)
        {
            Code = code;
            CardId = cardId;
            Message = message;
        }

        public override string ToString() => Message;
    }

    /// <summary>
    /// The verdict on one deck list. A list of problems rather than a single bool-plus-reason, because
    /// a deckbuilder wants to show everything wrong at once — fixing one issue only to be told about
    /// the next is the worst version of this screen.
    /// </summary>
    public class DeckValidationResult
    {
        private static readonly DeckIssue[] NoIssues = new DeckIssue[0];

        public IReadOnlyList<DeckIssue> Issues { get; }

        /// <summary>Total copies the deck holds, counted during validation so a caller doesn't recount.</summary>
        public int TotalCards { get; }

        public bool IsValid => Issues.Count == 0;

        public DeckValidationResult(IReadOnlyList<DeckIssue> issues, int totalCards)
        {
            Issues = issues ?? NoIssues;
            TotalCards = totalCards;
        }

        /// <summary>Every issue on one line each — for a log message or a tooltip.</summary>
        public string Summary()
        {
            if (IsValid) return $"Legal deck ({TotalCards} cards).";

            var text = new System.Text.StringBuilder();
            for (int i = 0; i < Issues.Count; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append("• ").Append(Issues[i].Message);
            }

            return text.ToString();
        }
    }
}
