using System.Collections.Generic;
using ArcanaWars.Core.Cards;

namespace ArcanaWars.Core.Match
{
    /// <summary>
    /// A log of every card one player has successfully played this match, in order. Several Major
    /// Arcana conditions are phrased as history, not board state — "must have played at least 2
    /// Queens previously" (Empress), "2 Kings" (Emperor), "8 units" (Hierophant) — and a board scan
    /// genuinely cannot answer those, because the Queens in question have usually decayed away by
    /// the time you want the Empress.
    ///
    /// It records the *card that was played*, not the unit that resulted, so a unit that later dies,
    /// is transformed (Hanged Man's Tree), or is resurrected (Judgement) never rewrites history.
    /// This is also what the Queen's own "+HP equal to cards of her element played so far" (GDD 10.4)
    /// will read from when that gets built.
    /// </summary>
    public class MatchHistory
    {
        private readonly List<PlayedCard> _played = new List<PlayedCard>();

        public IReadOnlyList<PlayedCard> Played => _played;

        /// <summary>Total cards played. Every card in this game spawns a unit, so this doubles as the Hierophant's "8 units played" count.</summary>
        public int TotalPlayed => _played.Count;

        public void Record(IPlayableCard card, int roundNumber)
        {
            if (card == null) return;
            _played.Add(new PlayedCard(card.DefinitionId, card.Kind, card.Element, roundNumber));
        }

        public int CountOfKind(CardKind kind)
        {
            int n = 0;
            foreach (var entry in _played)
                if (entry.Kind == kind) n++;
            return n;
        }

        public int CountOfElement(Element element)
        {
            int n = 0;
            foreach (var entry in _played)
                if (entry.Element == element) n++;
            return n;
        }

        public bool HasPlayed(string definitionId)
        {
            foreach (var entry in _played)
                if (entry.DefinitionId == definitionId) return true;
            return false;
        }

        public readonly struct PlayedCard
        {
            public readonly string DefinitionId;
            public readonly CardKind Kind;
            public readonly Element? Element;
            public readonly int RoundPlayed;

            public PlayedCard(string definitionId, CardKind kind, Element? element, int roundPlayed)
            {
                DefinitionId = definitionId;
                Kind = kind;
                Element = element;
                RoundPlayed = roundPlayed;
            }
        }
    }
}
