using System.Collections.Generic;

namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// One player's held cards, each paired with its per-copy state (see HandCard). It used to store
    /// bare IPlayableCard references, on the reasoning that nothing about a card in hand was per-copy
    /// state — The High Priestess (GDD 11.II) is the card that made that stop being true, since her
    /// heal scales with how long THIS copy has been held, and Wheel of Fortune's discount followed.
    /// </summary>
    public class Hand
    {
        private readonly List<HandCard> _cards = new List<HandCard>();

        public int MaxSize { get; }
        public int Count => _cards.Count;
        public bool IsFull => _cards.Count >= MaxSize;
        public IReadOnlyList<HandCard> Cards => _cards;

        public Hand(int maxSize = HandRules.MaxHandSize)
        {
            MaxSize = maxSize;
        }

        /// <summary>False if the hand is already full — the caller decides what happens to the rejected card (PlayerState.DrawCards burns it, per the standard TCG rule).</summary>
        public bool TryAdd(IPlayableCard card, int roundEntered, int costDiscount = 0, CardBuff buff = default)
        {
            if (card == null || IsFull) return false;
            _cards.Add(new HandCard(card, roundEntered, costDiscount, buff));
            return true;
        }

        /// <summary>
        /// The copy a play of `card` would use, without removing it — so the cost check and the removal
        /// that follows it are guaranteed to agree on which copy that is.
        /// </summary>
        public bool TryPeek(IPlayableCard card, out HandCard entry)
        {
            int index = IndexOfPreferredCopy(card);
            entry = index >= 0 ? _cards[index] : default;
            return index >= 0;
        }

        /// <summary>Removes the same copy TryPeek reports, and returns it so the caller can read its RoundEntered and discount.</summary>
        public bool TryRemove(IPlayableCard card, out HandCard removed)
        {
            int index = IndexOfPreferredCopy(card);
            if (index < 0)
            {
                removed = default;
                return false;
            }

            removed = _cards[index];
            _cards.RemoveAt(index);
            return true;
        }

        public bool Remove(IPlayableCard card) => TryRemove(card, out _);

        public bool Contains(IPlayableCard card) => IndexOfPreferredCopy(card) >= 0;

        /// <summary>Empties the hand and hands back everything that was in it — Wheel of Fortune's "discard your entire hand" (GDD 11.X).</summary>
        public List<IPlayableCard> RemoveAll()
        {
            var taken = new List<IPlayableCard>(_cards.Count);
            foreach (var entry in _cards) taken.Add(entry.Card);
            _cards.Clear();
            return taken;
        }

        /// <summary>
        /// With two copies of the same definition in hand, which one does a play use? The most
        /// discounted, then the longest-held. Cheapest first is what any player would pick, and among
        /// equal prices the oldest keeps The High Priestess's count behaving the way a player holding
        /// her since turn 1 would expect.
        /// </summary>
        private int IndexOfPreferredCopy(IPlayableCard card)
        {
            int best = -1;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (!ReferenceEquals(_cards[i].Card, card)) continue;
                if (best < 0) { best = i; continue; }

                var candidate = _cards[i];
                var current = _cards[best];
                if (candidate.CostDiscount > current.CostDiscount ||
                    (candidate.CostDiscount == current.CostDiscount && candidate.RoundEntered < current.RoundEntered))
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
