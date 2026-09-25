using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// II. The High Priestess (GDD 11.II) — "When played, heals your Nexus for (1 + the number of
    /// turns this card stayed in your hand)."
    ///
    /// A regular Heal, not an Over-Heal: the GDD names Over-Heal as specifically "the only way" past
    /// the Soul's cap (GDD 8), and this card isn't given that keyword, so it uses Soul.ApplyHeal and
    /// stops at MaxHp like any Cup would.
    ///
    /// The GDD's own worked example is the test: held from turn 1, played on turn 8, heals 8 —
    /// 1 + (8 - 1). See HandCard.RoundEntered for why an opening-hand card counts as arriving on
    /// round 1 rather than round 0.
    /// </summary>
    public sealed class HighPriestessAbility : UnitAbility
    {
        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            int heal = 1 + heldCard.TurnsHeld(ctx.RoundNumber);
            ctx.GetSoul(self.Owner).ApplyHeal(heal);
        }
    }
}
