using UnityEngine;
using ArcanaWars.Core.Cards;

namespace ArcanaWars.Cards.Decks
{
    /// <summary>
    /// A deck stored as a project asset, so it can be dragged onto a component in the Inspector the
    /// way CardDatabase is. This is the format for decks that ship with the game — starter decks, the
    /// AI's decks, a fixed deck for reproducing a bug — not for decks a player builds while playing.
    ///
    /// The split is forced, not stylistic: **a ScriptableObject asset cannot be created or written at
    /// runtime in a built player.** AssetDatabase is Editor-only, and a built game has no project
    /// folder to write into. So an in-game deckbuilder has to save somewhere else, which is what
    /// DeckStorage is for. Both hold the same SerializedDeck, so a deck can move between them.
    ///
    /// Entries are card ids rather than object references because the id is the shared format across
    /// both routes — and a JSON file has no way to hold a reference. The ids are the ones in
    /// CardDatabase: "minor_Swords_5", "king_Cups", "8_strength". Hand-typing them is not the intended
    /// workflow; build the deck in DeckBuilderView and save it, then convert.
    /// </summary>
    [CreateAssetMenu(menuName = "Arcana Wars/Deck", fileName = "NewDeck")]
    public class DeckAsset : ScriptableObject
    {
        [SerializeField] private SerializedDeck deck = new SerializedDeck();

        /// <summary>A fresh DeckList with this asset's contents. A copy every time — editing it can't write back to the shared asset by accident.</summary>
        public DeckList ToDeckList()
        {
            var list = deck != null ? deck.ToDeckList() : new DeckList();

            // The asset's own file name is the better default: a duplicated asset keeps the original's
            // serialized deckName, which would then show two different decks under one name.
            if (string.IsNullOrEmpty(list.Name) || list.Name == "New Deck") list.Name = name;

            return list;
        }

        /// <summary>Editor/tooling use only — mutates the shared asset. Never call at runtime; see the CardDefinition vs UnitInstance split for why that matters.</summary>
        public void EditorSetContents(DeckList source)
        {
            deck = SerializedDeck.From(source);
        }
    }
}
