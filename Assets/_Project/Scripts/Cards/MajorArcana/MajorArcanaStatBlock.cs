using ArcanaWars.Core.Board;

namespace ArcanaWars.Cards.MajorArcana
{
    /// <summary>
    /// Plain DTO mirroring MajorArcanaCardDefinition's fields — lets the card database generator
    /// hand a whole card's data to EditorApply in one call instead of a 17-parameter argument list.
    /// Lives in the non-editor Cards assembly (not Cards.Editor) because MajorArcanaCardDefinition,
    /// a runtime class, needs to reference this type in EditorApply's signature.
    /// </summary>
    public readonly struct MajorArcanaStatBlock
    {
        public readonly string CardId;
        public readonly string DisplayName;
        public readonly int Cost;
        public readonly int StartingHp;
        public readonly int MinStartingHp; // -1 = fixed HP, not random
        public readonly int MaxStartingHp;
        public readonly FootprintShapeType Footprint;
        public readonly int DecayMultiplier;
        public readonly bool IsImmuneToAttackDamage;
        public readonly int InstantDeathHpThreshold; // The Tower only (GDD 16): at or below this HP, any attack damage kills outright. -1 = no such rule
        public readonly int AttackPower;
        public readonly int OverAttackPower;
        public readonly int HealPower;
        public readonly int OverHealPower;
        public readonly int PentaclePower;
        public readonly int OverPentaclePower;
        public readonly int LeftNeighborHpBuffAmount;
        public readonly int SelfHpBuffAmount;
        public readonly string AbilityNotes;

        public MajorArcanaStatBlock(
            string cardId,
            string displayName,
            int cost,
            int startingHp,
            FootprintShapeType footprint,
            string abilityNotes,
            int decayMultiplier = 1,
            bool isImmuneToAttackDamage = false,
            int instantDeathHpThreshold = -1,
            int attackPower = 0,
            int overAttackPower = 0,
            int healPower = 0,
            int overHealPower = 0,
            int pentaclePower = 0,
            int overPentaclePower = 0,
            int leftNeighborHpBuffAmount = 0,
            int selfHpBuffAmount = 0,
            int minStartingHp = -1,
            int maxStartingHp = -1)
        {
            CardId = cardId;
            DisplayName = displayName;
            Cost = cost;
            StartingHp = startingHp;
            Footprint = footprint;
            AbilityNotes = abilityNotes;
            DecayMultiplier = decayMultiplier;
            IsImmuneToAttackDamage = isImmuneToAttackDamage;
            InstantDeathHpThreshold = instantDeathHpThreshold;
            AttackPower = attackPower;
            OverAttackPower = overAttackPower;
            HealPower = healPower;
            OverHealPower = overHealPower;
            PentaclePower = pentaclePower;
            OverPentaclePower = overPentaclePower;
            LeftNeighborHpBuffAmount = leftNeighborHpBuffAmount;
            SelfHpBuffAmount = selfHpBuffAmount;
            MinStartingHp = minStartingHp;
            MaxStartingHp = maxStartingHp;
        }
    }
}
