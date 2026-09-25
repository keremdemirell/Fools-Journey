using UnityEngine;
using ArcanaWars.Core;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Cards.CourtCards
{
    /// <summary>One of the 20 Court Cards. Holds only identity (Element, Rank) — stats come from CourtCardRules.</summary>
    [CreateAssetMenu(menuName = "Arcana Wars/Court Card", fileName = "NewCourtCard")]
    public class CourtCardDefinition : ScriptableObject, IPlayableCard
    {
        [SerializeField] private Element element;
        [SerializeField] private CourtRank rank;
        [SerializeField] private Sprite art;

        public Element Element => element;
        public CourtRank Rank => rank;
        public Sprite Art => art;
        public string DisplayName => $"{rank} of {element}";
        public string DefinitionId => CourtCardRules.GetDefinitionId(rank, element);

        /// <summary>Court ranks are mirrored one-for-one into Core's CardKind so conditions like the Empress's "2 Queens played" can be evaluated there — see CardKind.</summary>
        public CardKind Kind => CourtCardRules.GetCardKind(rank);

        /// <summary>Explicit implementation for the same reason as MinorArcanaCardDefinition's — the interface's Element is nullable, a Court Card's never is.</summary>
        Element? IPlayableCard.Element => element;

        /// <summary>Only the Ace has a cost the player chooses (GDD 10.1) — this is what tells the UI to prompt for an amount.</summary>
        public bool HasVariableCost => rank == CourtRank.Ace;

        /// <summary>manaCommitted is ignored for every rank except Ace.</summary>
        public int GetCost(int manaCommitted = 0) => CourtCardRules.GetCost(rank, manaCommitted);

        /// <summary>rng is unused — a Court Card's stats are a formula on rank, element and (for an Ace) the mana committed. See IPlayableCard for why rng is on the contract at all.</summary>
        public UnitSpawnStats GetSpawnStats(int manaCommitted = 0, System.Random rng = null) => CourtCardRules.ComputeStats(rank, element, manaCommitted);

        /// <summary>Editor/generator use only — mutates the shared asset. Never call at runtime; see CardDefinition vs UnitInstance split.</summary>
        public void EditorSetIdentity(Element newElement, CourtRank newRank)
        {
            element = newElement;
            rank = newRank;
        }
    }
}
