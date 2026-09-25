using ArcanaWars.Core.Board;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// Stat lines that abilities need but can't get from a card — units conjured out of nothing, and
    /// the Minor Arcana element traits The Magician borrows.
    ///
    /// This is a duplication with a known owner: MinorArcanaRules (in the Cards assembly) is the real
    /// authority on what a "3 of Swords" is and what each element's base trait does, and Core cannot
    /// reference it — Core can't see the Cards assembly at all. So the numbers are restated here, in
    /// ONE place, with this note attached. If the Minor Arcana formula (Cost = HP = X, each element's
    /// trait at power 1) ever changes, this file has to change with it; the harness asserts the values
    /// so the drift is at least loud.
    ///
    /// The alternative — passing card factories down from the Cards assembly into every ability —
    /// was rejected as a lot of plumbing for a handful of stat lines.
    /// </summary>
    public static class SpawnTemplates
    {
        public const string ThreeOfSwordsId = "minor_Swords_3";
        public const int ThreeOfSwordsHp = 3;
        public const int ThreeOfSwordsAttack = 1;
        public const int ThreeOfSwordsCost = 3;

        public const string HangedManTreeId = "hanged_man_tree";
        public const int HangedManTreeHp = 10;

        /// <summary>The "3 of Swords (HP: 3, Attack: 1)" Death leaves behind (GDD 11.XIII).</summary>
        public static UnitSpawnStats ThreeOfSwords() => new UnitSpawnStats(
            ThreeOfSwordsId,
            ThreeOfSwordsHp,
            FootprintShapeType.Single,
            Element.Swords,
            attackPower: ThreeOfSwordsAttack,
            printedCost: ThreeOfSwordsCost);

        /// <summary>
        /// The Hanged Man's Tree — "HP: 10, Size: 7 tiles, Heal: 1, Pentacle: 1" (GDD 11.XII). It has
        /// no element and was never a card, so its printed cost is 0: any card in the deck costs more,
        /// which is what lets Death "upgrade" a Tree.
        /// </summary>
        public static UnitSpawnStats HangedManTree() => new UnitSpawnStats(
            HangedManTreeId,
            HangedManTreeHp,
            FootprintShapeType.SevenCluster,
            healPower: 1,
            pentaclePower: 1,
            printedCost: 0);

        public const string LoversRightId = "6_lovers_right";
        public const int LoversHp = 3;

        /// <summary>
        /// The right-hand Lover — "Right unit: Attack 1", HP 3 (GDD 11.VI). The left-hand Lover is the
        /// card itself; this is the second unit it brings. Its printed cost is the card's, since both
        /// halves came from the same 4-cost card.
        /// </summary>
        public static UnitSpawnStats LoversRight(int printedCost) => new UnitSpawnStats(
            LoversRightId,
            LoversHp,
            FootprintShapeType.ThreeByOne,
            attackPower: 1,
            printedCost: printedCost);

        /// <summary>
        /// Gives a unit one element's base Minor Arcana trait at power 1, and that element — what
        /// "gains the effect of the 2 of [Element]" means for The Magician (user decision: the trait
        /// and the element, but NOT the 2 of Pentacles' doubled HP; the Magician keeps its printed 2).
        /// </summary>
        public static UnitSpawnStats WithElementTrait(UnitSpawnStats stats, Element element)
        {
            switch (element)
            {
                case Element.Swords: return stats.With(element: element, attackPower: 1);
                case Element.Cups: return stats.With(element: element, healPower: 1);
                case Element.Pentacles: return stats.With(element: element, pentaclePower: 1);
                case Element.Wands: return stats.With(element: element, leftNeighborHpBuffAmount: 1);
                default: return stats;
            }
        }
    }
}
