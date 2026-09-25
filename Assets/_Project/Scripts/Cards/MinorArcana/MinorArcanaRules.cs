using ArcanaWars.Core;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Cards.MinorArcana
{
    /// <summary>GDD 9: X of [Element], value 2-10. Cost=X, HP=X (Pentacles: X*2), base element trait at power 1. One formula for all 36 cards — nothing here is hand-typed per card.</summary>
    public static class MinorArcanaRules
    {
        public const int MinValue = 2;
        public const int MaxValue = 10;

        public static int GetCost(int value) => value;

        public static string GetDefinitionId(Element element, int value) => $"minor_{element}_{value}";

        public static UnitSpawnStats ComputeStats(Element element, int value)
        {
            int hp = element == Element.Pentacles ? value * 2 : value;
            string id = GetDefinitionId(element, value);

            switch (element)
            {
                case Element.Swords: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, attackPower: 1);
                case Element.Cups: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, healPower: 1);
                case Element.Pentacles: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, pentaclePower: 1);
                case Element.Wands: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, leftNeighborHpBuffAmount: 1);
                default: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element);
            }
        }
    }
}
