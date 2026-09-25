using System;
using System.Collections.Generic;
using ArcanaWars.Core.Cards;

namespace ArcanaWars.Cards.Decks
{
    /// <summary>One saved line of a deck: a card id and how many copies. The serialized twin of Core's DeckEntry.</summary>
    [Serializable]
    public struct SerializedDeckEntry
    {
        public string cardId;
        public int count;

        public SerializedDeckEntry(string cardId, int count)
        {
            this.cardId = cardId;
            this.count = count;
        }
    }

    /// <summary>
    /// A deck in the shape Unity can actually write to disk, and the single bridge between that shape
    /// and Core's DeckList.
    ///
    /// It exists because Core's DeckList can't be serialized directly: Unity's serializer (both for a
    /// ScriptableObject's fields and for JsonUtility) only handles public fields or ones marked
    /// [SerializeField] — and [SerializeField] is a UnityEngine type, which Core is forbidden from
    /// referencing. Writing DeckList that way would mean either breaking Core's engine-free rule or
    /// giving it public mutable fields with no way to keep its invariants (positive counts, no
    /// duplicate ids). So the storage shape lives here instead, and converts at the boundary.
    ///
    /// Both persistence routes share this one type on purpose — DeckAsset serializes it as a
    /// ScriptableObject field, DeckStorage serializes it as JSON — so a deck saved either way has the
    /// same contents and the same conversion code, and neither format can quietly drift from the
    /// other the way two hand-written converters would.
    /// </summary>
    [Serializable]
    public class SerializedDeck
    {
        public string deckName = "New Deck";
        public List<SerializedDeckEntry> entries = new List<SerializedDeckEntry>();

        public static SerializedDeck From(DeckList deck)
        {
            var data = new SerializedDeck();
            if (deck == null) return data;

            data.deckName = deck.Name;
            foreach (var entry in deck.Entries)
                data.entries.Add(new SerializedDeckEntry(entry.CardId, entry.Count));

            return data;
        }

        /// <summary>
        /// Back to a DeckList. Every entry goes through DeckList.Add, which merges repeats and drops
        /// blank ids and non-positive counts — so a hand-edited file can't produce a DeckList that
        /// breaks its own invariants. Anything dropped shows up as a short deck at validation, which
        /// is the visible version of the same complaint. Unknown ids survive this step on purpose:
        /// DeckRules.Validate names them, which is more useful than silently losing them here.
        /// </summary>
        public DeckList ToDeckList()
        {
            var deck = new DeckList(string.IsNullOrEmpty(deckName) ? "Untitled Deck" : deckName);
            if (entries == null) return deck;

            foreach (var entry in entries)
                deck.Add(entry.cardId, entry.count);

            return deck;
        }
    }
}
