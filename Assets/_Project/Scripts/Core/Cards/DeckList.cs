using System.Collections.Generic;

namespace ArcanaWars.Core.Cards
{
    /// <summary>One line of a deck list: which card, and how many copies. Always a positive count — see DeckList.Add.</summary>
    public readonly struct DeckEntry
    {
        public readonly string CardId;
        public readonly int Count;

        public DeckEntry(string cardId, int count)
        {
            CardId = cardId;
            Count = count;
        }
    }

    /// <summary>
    /// A deck as the player *built* it: card ids plus copy counts, and nothing else. Deliberately not
    /// the same thing as Core.Cards.Deck, which is the shuffled draw pile of a match in progress —
    /// this is the recipe, that is the dish. Keeping them apart is what lets one saved deck start any
    /// number of matches without being rebuilt, and lets a deck be edited while no match exists.
    ///
    /// Stores ids rather than IPlayableCard references on purpose: a deck has to survive being written
    /// to a file and read back, and an object reference cannot. Turning the ids back into cards is
    /// ToCards(catalog), which is also the only place a deleted/renamed card can go missing — hence
    /// DeckRules.Validate reporting UnknownCard rather than ToCards throwing.
    ///
    /// Holds no rules. Whether a deck is *legal* is DeckRules' job; this class will happily hold 90
    /// copies of one card, because a half-built deck in a deckbuilder UI is an ordinary state to be in
    /// and refusing to represent it would make the UI fight the model.
    /// </summary>
    public class DeckList
    {
        private readonly List<DeckEntry> _entries = new List<DeckEntry>();

        /// <summary>Player-facing name. Also the file name a saved deck gets, so a storage layer must sanitise it.</summary>
        public string Name { get; set; }

        /// <summary>The lines of the list, in the order cards were first added. Counts are always &gt;= 1 and ids never repeat.</summary>
        public IReadOnlyList<DeckEntry> Entries => _entries;

        /// <summary>Distinct cards, not total copies — "27 cards" in a deckbuilder means TotalCards, not this.</summary>
        public int DistinctCards => _entries.Count;

        public DeckList(string name = "New Deck")
        {
            Name = name;
        }

        /// <summary>Total copies across every entry. This is the number DeckRules.DeckSize is measured against.</summary>
        public int TotalCards
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _entries.Count; i++) total += _entries[i].Count;
                return total;
            }
        }

        public int CountOf(string cardId)
        {
            int index = IndexOf(cardId);
            return index < 0 ? 0 : _entries[index].Count;
        }

        /// <summary>
        /// Adds copies, merging into the existing entry if the card is already listed. Returns how many
        /// were actually added, which is 0 for a blank id or a non-positive count — so a hand-mangled
        /// save file cannot inject a zero- or negative-count entry. Those copies are silently lost
        /// rather than throwing, and the deck then fails validation on TotalCards, which is the loud,
        /// player-visible version of the same complaint.
        ///
        /// Enforces no copy limit; DeckRules does that, and a UI should ask DeckRules.CanAdd first.
        /// </summary>
        public int Add(string cardId, int count = 1)
        {
            if (string.IsNullOrEmpty(cardId) || count <= 0) return 0;

            int index = IndexOf(cardId);
            if (index < 0) _entries.Add(new DeckEntry(cardId, count));
            else _entries[index] = new DeckEntry(cardId, _entries[index].Count + count);

            return count;
        }

        /// <summary>Removes copies, dropping the entry entirely when it hits zero. Returns how many were actually removed.</summary>
        public int Remove(string cardId, int count = 1)
        {
            if (count <= 0) return 0;

            int index = IndexOf(cardId);
            if (index < 0) return 0;

            int removed = count < _entries[index].Count ? count : _entries[index].Count;
            int left = _entries[index].Count - removed;

            if (left <= 0) _entries.RemoveAt(index);
            else _entries[index] = new DeckEntry(cardId, left);

            return removed;
        }

        public void Clear() => _entries.Clear();

        /// <summary>A separate object with the same contents — so a deckbuilder can edit a copy and discard the edits.</summary>
        public DeckList Clone()
        {
            var clone = new DeckList(Name);
            for (int i = 0; i < _entries.Count; i++) clone.Add(_entries[i].CardId, _entries[i].Count);
            return clone;
        }

        /// <summary>
        /// Expands the list into the actual cards a match will draw from — one IPlayableCard reference
        /// per copy, ready to hand to a Deck. Cards the catalog doesn't know are skipped rather than
        /// throwing; validate first if you care (you usually do), because an unnoticed skip is a deck
        /// that quietly starts a match short.
        ///
        /// Order is the deck-list order; that's fine because MatchState.StartMatch shuffles both decks
        /// before dealing.
        /// </summary>
        public List<IPlayableCard> ToCards(ICardCatalog catalog)
        {
            var cards = new List<IPlayableCard>(TotalCards);
            if (catalog == null) return cards;

            for (int i = 0; i < _entries.Count; i++)
            {
                var card = catalog.Find(_entries[i].CardId);
                if (card == null) continue;

                for (int copy = 0; copy < _entries[i].Count; copy++) cards.Add(card);
            }

            return cards;
        }

        private int IndexOf(string cardId)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].CardId == cardId) return i;

            return -1;
        }
    }
}
