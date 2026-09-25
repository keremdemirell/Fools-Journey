using System.Collections.Generic;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// Knight of [Element] (GDD 10.2) — "While on the board, all incoming damage to cards of its element
    /// is redirected to the Knight instead."
    ///
    /// Only YOUR units of its element are guarded; an enemy's Swords unit isn't the Knight of Swords'
    /// business. "All incoming damage" is taken at its word: combat hits, Justice's strikes, The Sun's
    /// nuke and a Tower's explosion all go through MatchState.DealUnitDamage, which asks every live unit
    /// whether it absorbs the hit before applying it — so the Knight catches every one of them. Decay
    /// isn't damage, and neither is a sacrifice or Death's execution, so none of those are redirected.
    ///
    /// The damage lands on the Knight exactly as if it had been hit, so a piercing Over-Attack down a
    /// lane of Swords units hits the Knight once per unit it passed through — which is the card's own
    /// design note ("a Knight protecting many small element cards will die faster") working as written.
    /// Once the Knight dies, its element takes its own hits again.
    ///
    /// One ability object per element: the element is configuration, not memory, so the registry's
    /// "abilities are stateless" rule still holds.
    /// </summary>
    public sealed class KnightAbility : UnitAbility
    {
        private readonly Element _element;

        public KnightAbility(Element element)
        {
            _element = element;
        }

        public override bool AbsorbsDamageFor(IMatchQuery query, UnitInstance self, UnitInstance target) =>
            target.Id != self.Id && target.Owner == self.Owner && target.Element == _element;
    }

    /// <summary>
    /// Queen of [Element] (GDD 10.4) — "When played, permanently gains extra HP equal to the total number
    /// of cards of her specific element played so far in the match."
    ///
    /// Counted from YOUR match history — her design note says she rewards "element dedication" in your
    /// deck, and "a deck heavy with one element" is yours. "So far" means before her: the count is taken
    /// before her own play is recorded. The bonus goes into her StartingHp, like the Empress's green-tile
    /// bonus, so a 13 + 5 Queen is a genuinely 18 HP unit that counts as undamaged.
    ///
    /// Only real plays count — a spawned 3 of Swords was never played, and a Magician that chose her
    /// element was a Major Arcana card with no element when it was played.
    /// </summary>
    public sealed class QueenAbility : UnitAbility
    {
        private readonly Element _element;

        public QueenAbility(Element element)
        {
            _element = element;
        }

        public override UnitSpawnStats ModifySpawnStats(IMatchQuery query, PlayRequest request, UnitSpawnStats stats) =>
            stats.WithBonusHp(query.GetHistory(request.Side).CountOfElement(_element));
    }

    /// <summary>
    /// King of [Element] (GDD 10.5) — "When played, immediately doubles the current HP of all friendly
    /// units of his element on the board."
    ///
    /// Your units only, his element only, and not himself — he's arriving, not "on the board" already;
    /// doubling his own 14 would make him a 28 HP card, which is nothing the card text describes.
    /// Doubling is an uncapped heal of the unit's current HP, so a 3 HP Swords unit becomes 6, and since
    /// HP is lifespan (GDD 6.1) that's twice as many turns left to act — the design note's "doubles
    /// their remaining total damage output".
    /// </summary>
    public sealed class KingAbility : UnitAbility
    {
        private readonly Element _element;

        public KingAbility(Element element)
        {
            _element = element;
        }

        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            foreach (var unit in new List<UnitInstance>(ctx.AllUnits))
            {
                if (unit.Id == self.Id || unit.IsDead) continue;
                if (unit.Owner != self.Owner || unit.Element != _element) continue;

                unit.ApplyHeal(unit.CurrentHp);
            }
        }
    }
}
