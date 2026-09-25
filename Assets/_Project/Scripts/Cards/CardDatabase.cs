using System.Collections.Generic;
using UnityEngine;
using ArcanaWars.Core.Cards;
using ArcanaWars.Cards.CourtCards;
using ArcanaWars.Cards.MajorArcana;
using ArcanaWars.Cards.MinorArcana;

namespace ArcanaWars.Cards
{
    /// <summary>
    /// One asset holding references to every generated card, so a scene needs a single Inspector
    /// reference instead of 78. Populated by the card database generator; not hand-maintained.
    ///
    /// Implements Core's ICardCatalog so deck lists — which store card *ids*, not object references,
    /// because they have to survive being written to a file — can be turned back into real cards for
    /// validation and for starting a match.
    /// </summary>
    [CreateAssetMenu(menuName = "Arcana Wars/Card Database", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject, ICardCatalog
    {
        [SerializeField] private List<MinorArcanaCardDefinition> minorArcana = new List<MinorArcanaCardDefinition>();
        [SerializeField] private List<CourtCardDefinition> courtCards = new List<CourtCardDefinition>();
        [SerializeField] private List<MajorArcanaCardDefinition> majorArcana = new List<MajorArcanaCardDefinition>();

        public IReadOnlyList<MinorArcanaCardDefinition> MinorArcana => minorArcana;
        public IReadOnlyList<CourtCardDefinition> CourtCards => courtCards;
        public IReadOnlyList<MajorArcanaCardDefinition> MajorArcana => majorArcana;

        public int Count => minorArcana.Count + courtCards.Count + majorArcana.Count;

        /// <summary>Every card as the Core-facing contract — this is what a deck is built from.</summary>
        public List<IPlayableCard> AllCards()
        {
            var all = new List<IPlayableCard>(Count);
            foreach (var c in minorArcana) if (c != null) all.Add(c);
            foreach (var c in courtCards) if (c != null) all.Add(c);
            foreach (var c in majorArcana) if (c != null) all.Add(c);
            return all;
        }

        // --- ICardCatalog ------------------------------------------------------------------------

        // Built on first lookup, not in OnEnable: the generator rewrites the three lists on a live
        // asset, and an index built at load would then point at the old contents for the rest of the
        // session. _indexedCount is what notices that and forces a rebuild.
        private Dictionary<string, IPlayableCard> _byId;
        private int _indexedCount = -1;

        /// <summary>
        /// Explicit interface implementation, for the same reason the card definitions do it with
        /// Element: the class already has an AllCards() *method* with a different return type, and a
        /// property of the same name would collide with it. Callers holding a CardDatabase keep using
        /// AllCards(); callers holding an ICardCatalog get this.
        /// </summary>
        IReadOnlyList<IPlayableCard> ICardCatalog.AllCards => AllCards();

        /// <summary>The card with this id, or null. O(1) after the first call — a deckbuilder resolves ids on every repaint.</summary>
        public IPlayableCard Find(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId)) return null;

            if (_byId == null || _indexedCount != Count)
            {
                _byId = new Dictionary<string, IPlayableCard>(Count);
                foreach (var card in AllCards())
                {
                    // Last one wins rather than throwing on a duplicate id: a duplicate means a
                    // malformed database, which is the generator's problem to prevent, and a
                    // deckbuilder crashing on load is a worse way to find out.
                    if (!string.IsNullOrEmpty(card.DefinitionId)) _byId[card.DefinitionId] = card;
                }
                _indexedCount = Count;
            }

            return _byId.TryGetValue(definitionId, out var found) ? found : null;
        }

        /// <summary>Editor/generator use only — mutates the shared asset.</summary>
        public void EditorSetContents(
            List<MinorArcanaCardDefinition> minor,
            List<CourtCardDefinition> court,
            List<MajorArcanaCardDefinition> major)
        {
            minorArcana = minor;
            courtCards = court;
            majorArcana = major;
        }
    }
}
