using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XIX. The Sun (GDD 11.XIX) — "Cancels all illusions, stealth, and armor on the board. Every
    /// turn, deals damage equal to its current HP to the enemy unit with the highest HP."
    ///
    /// The damage scales DOWN as the Sun decays, exactly as its design note describes: 6 on the turn
    /// it lands, then 5, then 4. Reading its HP at the moment it strikes is what produces that for
    /// free — there is no stored "power" to keep in sync.
    ///
    /// The dispel half is implemented as narrowly as the board allows: "illusions, stealth, and
    /// armor" have exactly one referent in the simulation today, The Moon's 50% miss chance, so a
    /// live Sun switches that off (see MatchState.AttackMisses). Deliberately NOT extended to
    /// Strength's damage immunity or The Devil's Soul immunity, however tempting "armor" sounds —
    /// both of those are the entire identity of their card rather than a status effect layered on
    /// top, and cancelling them would rewrite two cards from a word in a third. Flagged so it can be
    /// revisited if more concealment effects ever exist.
    ///
    /// Like Justice, the Sun leaves UnitSpawnStats.AttackPower at 0: its strike is not lane-based, so
    /// wiring it generically would add a wrong second attack.
    /// </summary>
    public sealed class SunAbility : UnitAbility
    {
        public override bool DispelsBoardEffects(IMatchQuery query, UnitInstance self) => true;

        public override void OnCombat(IAbilityContext ctx, UnitInstance self)
        {
            UnitInstance target = null;
            foreach (var unit in ctx.AllUnits)
            {
                if (unit.IsDead || unit.Owner == self.Owner) continue;

                // Ties go to the unit placed first, for the same replay-stability reason as Justice.
                if (target == null || unit.CurrentHp > target.CurrentHp ||
                    (unit.CurrentHp == target.CurrentHp && unit.Id < target.Id))
                {
                    target = unit;
                }
            }

            if (target == null) return; // nothing on the far side; the card gives it no Soul damage to fall back on

            int damage = self.CurrentHp;
            if (damage <= 0) return;

            if (ctx.AttackMisses(self)) return;
            ctx.DealUnitDamage(target, damage, self);
        }
    }
}
