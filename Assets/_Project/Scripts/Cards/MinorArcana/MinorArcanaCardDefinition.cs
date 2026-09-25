using UnityEngine;
using ArcanaWars.Core;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Cards.MinorArcana
{
    /// <summary>
    /// One of the 36 Minor Arcana cards. Holds only real identity (which element, which value) —
    /// Cost/HP/keyword power are never hand-typed here, they're computed by MinorArcanaRules so a
    /// formula change can't drift out of sync across 36 separate assets.
    ///
    /// Implements IPlayableCard so Core can hold it in a hand and play it without ever knowing this
    /// is a ScriptableObject.
    /// </summary>
    [CreateAssetMenu(menuName = "Arcana Wars/Minor Arcana Card", fileName = "NewMinorArcanaCard")]
    public class MinorArcanaCardDefinition : ScriptableObject, IPlayableCard
    {
        [SerializeField] private Element element;
        [SerializeField, Range(MinorArcanaRules.MinValue, MinorArcanaRules.MaxValue)] private int value = MinorArcanaRules.MinValue;
        [SerializeField] private Sprite art;

        public Element Element => element;
        public int Value => value;
        public Sprite Art => art;
        public string DisplayName => $"{value} of {element}";
        public string DefinitionId => MinorArcanaRules.GetDefinitionId(element, value);
        public CardKind Kind => CardKind.MinorArcana;
        public bool HasVariableCost => false;

        /// <summary>
        /// Explicit interface implementation: IPlayableCard exposes a nullable Element (Major Arcana
        /// have none), while a Minor Arcana always has one and callers here should not have to unwrap
        /// an Optional that can never be empty. Both names resolve to the same field.
        /// </summary>
        Element? IPlayableCard.Element => element;

        public int GetCost(int manaCommitted = 0) => MinorArcanaRules.GetCost(value);

        /// <summary>manaCommitted and rng are both unused — a Minor Arcana's stats are a pure formula on its element and value (GDD 9). See IPlayableCard for why rng is on the contract at all.</summary>
        public UnitSpawnStats GetSpawnStats(int manaCommitted = 0, System.Random rng = null) => MinorArcanaRules.ComputeStats(element, value);

        /// <summary>Editor/generator use only — mutates the shared asset. Never call at runtime; see CardDefinition vs UnitInstance split.</summary>
        public void EditorSetIdentity(Element newElement, int newValue)
        {
            element = newElement;
            value = newValue;
        }
    }
}
