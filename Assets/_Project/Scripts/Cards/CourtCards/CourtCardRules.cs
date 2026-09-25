using ArcanaWars.Core;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Cards.CourtCards
{
    /// <summary>
    /// GDD section 10: the stat formulas for all 20 Court Cards. Nothing here is hand-typed per card.
    ///
    /// Their special abilities live in Core alongside every other card ability (see UnitAbility for
    /// why), keyed by the ids this class produces:
    ///  - Ace: its "1 OR all your current mana" cost is enforced by CardPlayResolver, the one place
    ///    that knows how much mana is left. Its stats here just follow whatever was committed.
    ///  - Knight: KnightAbility — damage aimed at your units of its element lands on the Knight.
    ///  - Prince: nothing bespoke; its "Over" trait is a plain stat and works through generic systems.
    ///  - Queen: QueenAbility — +1 HP per card of her element you've played this match.
    ///  - King: KingAbility — on play, doubles the HP of your units of his element.
    ///  - Queen/King of Wands: their left-neighbour buff reaches OUTSIDE their 7-tile cluster, via the
    ///    footprint-aware MatchState.GetLeftNeighbor.
    /// </summary>
    public static class CourtCardRules
    {
        public const int AceMinMana = 1;

        /// <summary>Delegates to Core's CourtIds so the card assets and the ability registry can never disagree about an id.</summary>
        public static string GetDefinitionId(CourtRank rank, Element element) => CourtIds.For(GetCardKind(rank), element);

        /// <summary>
        /// Maps a Court rank onto Core's CardKind. The two enums are deliberately separate types even
        /// though they currently line up: CourtRank is card data (this assembly), CardKind is what
        /// Core is allowed to know about card data, and Core cannot reference this assembly at all.
        /// </summary>
        public static CardKind GetCardKind(CourtRank rank)
        {
            switch (rank)
            {
                case CourtRank.Ace: return CardKind.Ace;
                case CourtRank.Knight: return CardKind.Knight;
                case CourtRank.Prince: return CardKind.Prince;
                case CourtRank.Queen: return CardKind.Queen;
                case CourtRank.King: return CardKind.King;
                default: return CardKind.MinorArcana;
            }
        }

        public static int GetCost(CourtRank rank, int manaCommittedForAce = 0)
        {
            switch (rank)
            {
                case CourtRank.Ace: return System.Math.Max(AceMinMana, manaCommittedForAce);
                case CourtRank.Knight: return 11;
                case CourtRank.Prince: return 12;
                case CourtRank.Queen: return 13;
                case CourtRank.King: return 14;
                default: return 0;
            }
        }

        public static UnitSpawnStats ComputeStats(CourtRank rank, Element element, int manaCommittedForAce = 0)
        {
            switch (rank)
            {
                case CourtRank.Ace: return ComputeAceStats(element, manaCommittedForAce);
                case CourtRank.Knight: return ComputeKnightStats(element);
                case CourtRank.Prince: return ComputePrinceStats(element);
                case CourtRank.Queen: return ComputeQueenBaseStats(element);
                case CourtRank.King: return ComputeKingBaseStats(element);
                default: return ComputeKnightStats(element);
            }
        }

        /// <summary>Ace: cost = HP = however much mana the player commits (GDD 10.1), base element trait at power 1.</summary>
        private static UnitSpawnStats ComputeAceStats(Element element, int manaCommitted)
        {
            int hp = System.Math.Max(AceMinMana, manaCommitted);
            return WithElementTrait(GetDefinitionId(CourtRank.Ace, element), hp, element);
        }

        /// <summary>Knight: cost 11, HP 11 (GDD 10.2). No base keyword — guarding its element IS the card (KnightAbility).</summary>
        private static UnitSpawnStats ComputeKnightStats(Element element)
        {
            return new UnitSpawnStats(GetDefinitionId(CourtRank.Knight, element), 11, FootprintShapeType.Single, element);
        }

        /// <summary>Prince: cost 12, HP 12, the "Over" version of the element trait at power 1 (GDD 10.3). Wands has no named Over-keyword, so per its own card text it gets left-neighbor +1 AND self +1 instead.</summary>
        private static UnitSpawnStats ComputePrinceStats(Element element)
        {
            const int hp = 12;
            string id = GetDefinitionId(CourtRank.Prince, element);
            switch (element)
            {
                case Element.Swords: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, overAttackPower: 1);
                case Element.Cups: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, overHealPower: 1);
                case Element.Pentacles: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, overPentaclePower: 1);
                case Element.Wands: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element, leftNeighborHpBuffAmount: 1, selfHpBuffAmount: 1);
                default: return new UnitSpawnStats(id, hp, FootprintShapeType.Single, element);
            }
        }

        /// <summary>Queen: cost 13, base 13 HP (GDD 10.4). Her bonus HP depends on match history, so QueenAbility adds it at play time.</summary>
        private static UnitSpawnStats ComputeQueenBaseStats(Element element)
        {
            return WithElementTrait(GetDefinitionId(CourtRank.Queen, element), 13, element, FootprintShapeType.SevenCluster);
        }

        /// <summary>King: cost 14, HP 14 (GDD 10.5). His doubling is an on-play effect (KingAbility).</summary>
        private static UnitSpawnStats ComputeKingBaseStats(Element element)
        {
            return WithElementTrait(GetDefinitionId(CourtRank.King, element), 14, element, FootprintShapeType.SevenCluster);
        }

        private static UnitSpawnStats WithElementTrait(string id, int hp, Element element, FootprintShapeType footprint = FootprintShapeType.Single)
        {
            switch (element)
            {
                case Element.Swords: return new UnitSpawnStats(id, hp, footprint, element, attackPower: 1);
                case Element.Cups: return new UnitSpawnStats(id, hp, footprint, element, healPower: 1);
                case Element.Pentacles: return new UnitSpawnStats(id, hp, footprint, element, pentaclePower: 1);
                case Element.Wands: return new UnitSpawnStats(id, hp, footprint, element, leftNeighborHpBuffAmount: 1);
                default: return new UnitSpawnStats(id, hp, footprint, element);
            }
        }
    }
}
