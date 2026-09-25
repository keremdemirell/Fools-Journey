using UnityEngine;
using ArcanaWars.Core;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Cards.MajorArcana
{
    /// <summary>
    /// One of the 22 Major Arcana. Unlike Minor Arcana/Court Cards, there's no shared formula —
    /// every field here is hand-typed per card (see the generator's MajorArcanaData table for the
    /// GDD-sourced values). AbilityNotes documents, per card, exactly which parts of its GDD text
    /// are and aren't backed by working code yet — see GetSpawnStats: it only ever returns fields
    /// that behave CORRECTLY through existing generic systems; anything that would need bespoke
    /// logic (custom targeting, on-death triggers, hand/deck manipulation, etc.) is left at its
    /// zero/default rather than wired to something that would behave wrong.
    /// </summary>
    [CreateAssetMenu(menuName = "Arcana Wars/Major Arcana Card", fileName = "NewMajorArcanaCard")]
    public class MajorArcanaCardDefinition : ScriptableObject, IPlayableCard
    {
        [Header("Identity")]
        [SerializeField] private string cardId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite art;

        [Header("Stats (GDD Part 2, section 11)")]
        [SerializeField] private int cost;
        [SerializeField] private int startingHp;
        [SerializeField] private int minStartingHp = -1; // -1 = fixed HP; only The Fool uses a random range (1-4)
        [SerializeField] private int maxStartingHp = -1;
        [SerializeField] private FootprintShapeType footprint;
        [SerializeField] private int decayMultiplier = 1;
        [SerializeField] private bool isImmuneToAttackDamage;
        [SerializeField] private int instantDeathHpThreshold = -1; // -1 = no such rule; only The Tower uses it (GDD 16)
        [SerializeField] private int attackPower;
        [SerializeField] private int overAttackPower;
        [SerializeField] private int healPower;
        [SerializeField] private int overHealPower;
        [SerializeField] private int pentaclePower;
        [SerializeField] private int overPentaclePower;
        [SerializeField] private int leftNeighborHpBuffAmount;
        [SerializeField] private int selfHpBuffAmount;

        [Header("What's not implemented yet")]
        [TextArea(2, 8)]
        [SerializeField] private string abilityNotes;

        public string CardId => cardId;
        public string DefinitionId => cardId;
        public string DisplayName => displayName;
        public Sprite Art => art;
        public string AbilityNotes => abilityNotes;
        public bool HasRandomStartingHp => minStartingHp >= 0 && maxStartingHp >= minStartingHp;

        /// <summary>
        /// The printed HP, for anything that needs to *show* a card without playing it — a deckbuilder
        /// listing the pool, a card tooltip. Deliberately not routed through GetSpawnStats(), which
        /// rolls The Fool's random 1-4 on every call: a UI calling that each repaint would show an HP
        /// that flickers, and would also burn RNG draws that a seeded match needs to be reproducible.
        ///
        /// For The Fool (the only card with a range) read MinStartingHp..MaxStartingHp instead; this
        /// is its unrolled 0.
        /// </summary>
        public int StartingHp => startingHp;

        public int MinStartingHp => minStartingHp;
        public int MaxStartingHp => maxStartingHp;

        public CardKind Kind => CardKind.MajorArcana;

        /// <summary>Major Arcana stand outside the four elements entirely (GDD section 11), so this is always null — which is exactly what The Magician's "3 different elements in hand" condition needs it to be.</summary>
        public Element? Element => null;

        /// <summary>Every Major Arcana has a fixed printed cost — only the Aces are variable, and those are Court Cards.</summary>
        public bool HasVariableCost => false;

        public int GetCost(int manaCommitted = 0) => cost;

        /// <summary>
        /// manaCommitted is unused here; it exists on IPlayableCard for the Aces' sake.
        ///
        /// The Fool is the only card in the game whose HP is rolled (GDD 11.0), and that roll MUST
        /// come from the match's own Random — see IPlayableCard.GetSpawnStats. This used to call
        /// UnityEngine.Random, which is process-global: the match seed didn't control it, so the same
        /// seed produced different Fools on different runs and on different machines. A null rng
        /// rolls nothing and reports the minimum, so inspecting a card can never disturb a match.
        /// </summary>
        public UnitSpawnStats GetSpawnStats(int manaCommitted = 0, System.Random rng = null)
        {
            int hp = !HasRandomStartingHp ? startingHp
                   : rng == null ? minStartingHp
                   : rng.Next(minStartingHp, maxStartingHp + 1);
            return new UnitSpawnStats(
                cardId, hp, footprint,
                element: null, // Major Arcana have no element
                decayMultiplier: decayMultiplier,
                isImmuneToAttackDamage: isImmuneToAttackDamage,
                instantDeathHpThreshold: instantDeathHpThreshold,
                attackPower: attackPower,
                overAttackPower: overAttackPower,
                healPower: healPower,
                overHealPower: overHealPower,
                pentaclePower: pentaclePower,
                overPentaclePower: overPentaclePower,
                leftNeighborHpBuffAmount: leftNeighborHpBuffAmount,
                selfHpBuffAmount: selfHpBuffAmount);
        }

        /// <summary>Editor/generator use only — mutates the shared asset. Never call at runtime; see CardDefinition vs UnitInstance split.</summary>
        public void EditorApply(MajorArcanaStatBlock data)
        {
            cardId = data.CardId;
            displayName = data.DisplayName;
            cost = data.Cost;
            startingHp = data.StartingHp;
            minStartingHp = data.MinStartingHp;
            maxStartingHp = data.MaxStartingHp;
            footprint = data.Footprint;
            decayMultiplier = data.DecayMultiplier;
            isImmuneToAttackDamage = data.IsImmuneToAttackDamage;
            instantDeathHpThreshold = data.InstantDeathHpThreshold;
            attackPower = data.AttackPower;
            overAttackPower = data.OverAttackPower;
            healPower = data.HealPower;
            overHealPower = data.OverHealPower;
            pentaclePower = data.PentaclePower;
            overPentaclePower = data.OverPentaclePower;
            leftNeighborHpBuffAmount = data.LeftNeighborHpBuffAmount;
            selfHpBuffAmount = data.SelfHpBuffAmount;
            abilityNotes = data.AbilityNotes;
        }
    }
}
