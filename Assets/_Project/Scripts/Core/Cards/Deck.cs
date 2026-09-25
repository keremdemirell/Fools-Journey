using System;
using System.Collections.Generic;

namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// One player's draw pile plus the cards that have left it. Takes its own System.Random rather
    /// than reaching for UnityEngine.Random — partly because Core can't touch UnityEngine at all,
    /// but mostly because an injectable RNG means a test can pass a fixed seed and get the exact
    /// same shuffle every run.
    /// </summary>
    public class Deck
    {
        private readonly List<IPlayableCard> _drawPile;
        private readonly List<IPlayableCard> _discardPile = new List<IPlayableCard>();
        private readonly Random _rng;

        public int Count => _drawPile.Count;
        public bool IsEmpty => _drawPile.Count == 0;
        public IReadOnlyList<IPlayableCard> DiscardPile => _discardPile;

        /// <summary>
        /// The undrawn cards, in draw order (top of the deck is the LAST entry). Exposed read-only for
        /// Death's replacement (GDD 11.XIII), which picks "a random unit from your deck" — a search of
        /// the pile, not a draw from the top. Note this reveals deck order to anything that looks, so a
        /// UI must never display it.
        /// </summary>
        public IReadOnlyList<IPlayableCard> DrawPile => _drawPile;

        /// <summary>Pulls one specific card out of the draw pile, wherever it sits. False if it isn't there.</summary>
        public bool TryRemove(IPlayableCard card) => _drawPile.Remove(card);

        public Deck(IEnumerable<IPlayableCard> cards, Random rng = null)
        {
            _drawPile = cards == null ? new List<IPlayableCard>() : new List<IPlayableCard>(cards);
            _rng = rng ?? new Random();
        }

        /// <summary>Fisher-Yates.</summary>
        public void Shuffle()
        {
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var swap = _drawPile[i];
                _drawPile[i] = _drawPile[j];
                _drawPile[j] = swap;
            }
        }

        /// <summary>
        /// Takes the top card, or null when the pile is empty. What SHOULD happen on an empty deck
        /// (fatigue damage? reshuffle the discard pile? instant loss?) isn't specified in the GDD —
        /// drawing simply does nothing for now rather than inventing a rule.
        /// </summary>
        public IPlayableCard Draw()
        {
            if (_drawPile.Count == 0) return null;

            int last = _drawPile.Count - 1;
            var card = _drawPile[last];
            _drawPile.RemoveAt(last);
            return card;
        }

        public void Discard(IPlayableCard card)
        {
            if (card != null) _discardPile.Add(card);
        }
    }
}
