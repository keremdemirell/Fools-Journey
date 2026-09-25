using ArcanaWars.Core.Board;

namespace ArcanaWars.Core.Units
{
    /// <summary>
    /// The resolved stats needed to spawn one unit instance on the board. This is what a future
    /// card-play system will produce from a card definition (cost already spent, dynamic stats
    /// like the Fool's random HP or the Ace's mana-scaled HP already rolled/resolved) — Core.Units
    /// doesn't know about card definitions or ScriptableObjects, only this plain result.
    /// </summary>
    public readonly struct UnitSpawnStats
    {
        public readonly string DefinitionId;
        public readonly int StartingHp;
        public readonly FootprintShapeType Footprint;
        public readonly Element? Element;            // null for the Major Arcana, which have no element; drives Temperance's "one of each element alive" condition and (later) the Knight's redirect
        public readonly int DecayMultiplier;         // 1 = normal, 2 = The Tower (GDD 16)
        public readonly bool IsImmuneToAttackDamage;  // Strength (GDD 8) — still takes normal decay
        public readonly int InstantDeathHpThreshold;  // The Tower (GDD 16): at or below this HP, ANY attack damage is lethal regardless of amount. -1 = no such rule (everything else)
        public readonly int AttackPower;
        public readonly int OverAttackPower; // Prince of Swords, The World (GDD 5.3, 8) — pierces a blocker to ALSO hit the enemy Soul
        public readonly int HealPower;
        public readonly int OverHealPower;   // Prince of Cups, The World (GDD 8) — like Soul.ApplyHeal vs ApplyOverHeal, this is the uncapped variant
        public readonly int PentaclePower;
        public readonly int OverPentaclePower; // Prince of Pentacles, Judgement, The World (GDD 7.3)
        public readonly int LeftNeighborHpBuffAmount; // Wands' base trait (GDD 9.4): +N HP/turn to the friendly unit immediately to its left — normally 1, but The World's self-heal analogue needed this to be an amount, not a bool
        public readonly int SelfHpBuffAmount;          // Prince of Wands (+1), The World (+4) — also grants itself HP/turn (GDD 10.3, 21)

        /// <summary>
        /// The printed cost of the card this unit came from — what it says on the card, not what was
        /// actually paid, so a Wheel of Fortune discount doesn't make a unit "cheaper" after the fact.
        /// Death (GDD 11.XIII) needs it: its replacement must be "a random unit from your deck with a
        /// higher cost" than the unit it killed, and a unit on the board otherwise has no idea what it
        /// cost. Stamped by CardPlayResolver for played cards; spawned units (a 3 of Swords, the Hanged
        /// Man's Tree) carry their own.
        /// </summary>
        public readonly int PrintedCost;

        public UnitSpawnStats(
            string definitionId,
            int startingHp,
            FootprintShapeType footprint,
            Element? element = null,
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
            int printedCost = 0)
        {
            DefinitionId = definitionId;
            StartingHp = startingHp;
            Footprint = footprint;
            Element = element;
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
            PrintedCost = printedCost;
        }

        /// <summary>
        /// A copy with some fields replaced; a null argument means "keep the current value". This is
        /// the one place the full constructor is re-called, so adding a field later means updating
        /// one method rather than every "copy but change X" helper. (A C# `with` expression would do
        /// this for free, but that needs C# 10 for structs and Unity is on C# 9.)
        /// </summary>
        public UnitSpawnStats With(
            int? startingHp = null,
            Element? element = null,
            int? attackPower = null,
            int? healPower = null,
            int? pentaclePower = null,
            int? leftNeighborHpBuffAmount = null,
            int? printedCost = null)
        {
            return new UnitSpawnStats(
                DefinitionId,
                startingHp ?? StartingHp,
                Footprint,
                element ?? Element,
                DecayMultiplier,
                IsImmuneToAttackDamage,
                InstantDeathHpThreshold,
                attackPower ?? AttackPower,
                OverAttackPower,
                healPower ?? HealPower,
                OverHealPower,
                pentaclePower ?? PentaclePower,
                OverPentaclePower,
                leftNeighborHpBuffAmount ?? LeftNeighborHpBuffAmount,
                SelfHpBuffAmount,
                printedCost ?? PrintedCost);
        }

        /// <summary>
        /// A copy with extra starting HP — The Empress's greened tiles (GDD 11.III), which grant
        /// +3 HP to friendly cards played on them. Applied before the unit exists, so the bonus is
        /// part of StartingHp itself: a 4 HP card played on green really is a 7 HP unit, and "is it
        /// at full HP?" (The Star's condition) compares against 7, not 4.
        /// </summary>
        public UnitSpawnStats WithBonusHp(int bonus) => bonus == 0 ? this : With(startingHp: StartingHp + bonus);
    }
}
