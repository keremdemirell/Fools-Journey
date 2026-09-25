using System.Collections.Generic;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XI. Justice (GDD 11.XI) — "Reflects any damage taken by your Soul back to the attacker. Every
    /// turn, performs two attacks: one targeting the highest HP unit on the board, and one targeting
    /// the lowest HP unit."
    ///
    /// Its Attack:2 is deliberately NOT wired into UnitSpawnStats. The generic AttackResolver only
    /// knows lane targeting, so a wired AttackPower would make Justice ALSO take an ordinary swing
    /// down its column on top of the two attacks below — the exact "silently wrong" outcome the
    /// stat-wiring policy exists to prevent. The number lives here instead, where the targeting that
    /// belongs with it lives too.
    ///
    /// TARGETING (user decision, 2026-09-14): ENEMY units only. The card text says "the highest HP
    /// unit on the board", and a literal any-unit reading was implemented first — the user confirmed
    /// the design note's "threats" is the intent. With only one enemy unit on the board, that unit is
    /// both the highest and the lowest and takes both attacks.
    /// </summary>
    public sealed class JusticeAbility : UnitAbility
    {
        /// <summary>GDD 11.XI gives Justice Attack: 2. Each of its two attacks lands for this much.</summary>
        public const int AttackPower = 2;

        public override void OnCombat(IAbilityContext ctx, UnitInstance self)
        {
            var candidates = new List<UnitInstance>();
            foreach (var unit in ctx.AllUnits)
            {
                if (unit.IsDead || unit.Owner == self.Owner) continue;
                candidates.Add(unit);
            }

            if (candidates.Count == 0) return;

            var highest = FindExtreme(candidates, highestHp: true);
            var lowest = FindExtreme(candidates, highestHp: false);

            // Resolved as two separate attacks, each rolling The Moon's miss chance on its own —
            // they are two swings, not one split in half.
            Strike(ctx, self, highest);
            Strike(ctx, self, lowest);
        }

        /// <summary>
        /// "Reflects any damage taken by your Soul back to the attacker." Only damage to Justice's
        /// OWN Soul reflects, and only when a unit was responsible — damage with no attacker behind
        /// it (The Devil's doomsday payment) has nothing to bounce off.
        /// </summary>
        public override void OnSoulDamaged(IAbilityContext ctx, UnitInstance self, PlayerSide damagedSide, int amount, UnitInstance attacker)
        {
            if (damagedSide != self.Owner) return;
            if (attacker == null || attacker.IsDead) return;
            if (attacker.Owner == self.Owner) return; // nothing to punish if the damage was self-inflicted

            ctx.DealUnitDamage(attacker, amount, self);
        }

        private static void Strike(IAbilityContext ctx, UnitInstance self, UnitInstance target)
        {
            if (target == null || target.IsDead) return;
            if (ctx.AttackMisses(self)) return;
            ctx.DealUnitDamage(target, AttackPower, self);
        }

        /// <summary>
        /// Ties are broken by the lowest unit id, i.e. whichever was placed first. The GDD says
        /// nothing about ties, and an arbitrary-but-stable rule beats one that depends on dictionary
        /// ordering — a match replayed from the same seed has to play out the same way.
        /// </summary>
        private static UnitInstance FindExtreme(List<UnitInstance> candidates, bool highestHp)
        {
            UnitInstance best = null;
            foreach (var unit in candidates)
            {
                if (best == null)
                {
                    best = unit;
                    continue;
                }

                bool better = highestHp ? unit.CurrentHp > best.CurrentHp : unit.CurrentHp < best.CurrentHp;
                bool tie = unit.CurrentHp == best.CurrentHp && unit.Id < best.Id;
                if (better || tie) best = unit;
            }

            return best;
        }
    }
}
