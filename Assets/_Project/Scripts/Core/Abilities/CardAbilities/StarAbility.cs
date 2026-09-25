using System.Collections.Generic;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XVII. The Star (GDD 11.XVII) — "Upon play and every turn, heals all units on the board
    /// (except itself) for 1 HP." Condition: there are no full-HP units on the board.
    ///
    /// It heals ENEMY units too — that's explicit in the card's own design note ("a global healer
    /// that sustains the entire board — including enemy units"), and it's what makes the Star a
    /// real decision rather than a free board-wide Wand.
    ///
    /// Condition reading: an empty board has no full-HP units on it, which would technically satisfy
    /// the text and make the Star freely playable on turn one — but the card's design note says
    /// plainly that it "can't be played on a fresh board." So this requires at least one unit to
    /// exist AND none of them to be undamaged. See UnitInstance.IsAtFullHp for why "undamaged" is
    /// >= starting HP rather than == it.
    /// </summary>
    public sealed class StarAbility : UnitAbility
    {
        public const int HealAmount = 1;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            bool anyUnit = false;
            foreach (var unit in query.AllUnits)
            {
                if (unit.IsDead) continue;
                anyUnit = true;
                if (unit.IsAtFullHp)
                {
                    reason = "The Star needs every unit on the board to be damaged.";
                    return false;
                }
            }

            if (!anyUnit)
            {
                reason = "The Star cannot be played on an empty board.";
                return false;
            }

            reason = null;
            return true;
        }

        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options) => HealEveryoneElse(ctx, self);

        public override void OnDecayTick(IAbilityContext ctx, UnitInstance self) => HealEveryoneElse(ctx, self);

        private static void HealEveryoneElse(IAbilityContext ctx, UnitInstance self)
        {
            var targets = new List<UnitInstance>(ctx.AllUnits);
            foreach (var unit in targets)
            {
                if (unit.Id == self.Id || unit.IsDead) continue;
                unit.ApplyHeal(HealAmount);
            }
        }
    }
}
