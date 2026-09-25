using System;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// X. Wheel of Fortune (GDD 11.X) — "Discard your entire hand, draw that many cards. Heal Nexus by
    /// 5, gain 2 Pentacles. All cards drawn this turn cost 1 Mana less." Condition: the two Souls are
    /// at least 5 HP apart, in either direction.
    ///
    /// "That many" is the hand as it stands once the Wheel itself has left it — the Wheel is on the
    /// board by the time its ability resolves, so it isn't discarded and doesn't count.
    ///
    /// The discount (user decision) applies only to the cards the Wheel itself draws — not to the
    /// round's normal draw — and stays on each of those copies for as long as it's held, rather than
    /// expiring at the end of the round. It lives on the HandCard, not the card definition, so a
    /// second copy of the same card drawn normally is not discounted.
    ///
    /// The heal is a regular capped Heal (the card isn't given Over-Heal), and the +2 Pentacles land
    /// in THIS round's pool, where they're actually usable — unlike the Fool's burn, this effect
    /// happens during the Action phase, so "now" is a real moment to spend them.
    /// </summary>
    public sealed class WheelOfFortuneAbility : UnitAbility
    {
        public const int RequiredSoulHpGap = 5;
        public const int HealAmount = 5;
        public const int PentaclesGained = 2;
        public const int DrawDiscount = 1;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            int gap = Math.Abs(query.GetSoul(side).CurrentHp - query.GetSoul(side.Opponent()).CurrentHp);
            if (gap < RequiredSoulHpGap)
            {
                reason = $"Wheel of Fortune needs a Soul HP gap of at least {RequiredSoulHpGap} (currently {gap}).";
                return false;
            }

            reason = null;
            return true;
        }

        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            int discarded = ctx.DiscardHand(self.Owner);
            ctx.DrawCards(self.Owner, discarded, DrawDiscount);
            ctx.GetSoul(self.Owner).ApplyHeal(HealAmount);
            ctx.GainPentacles(self.Owner, PentaclesGained);
        }
    }
}
