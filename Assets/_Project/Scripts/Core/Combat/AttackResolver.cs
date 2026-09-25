using System.Collections.Generic;
using ArcanaWars.Core.Abilities;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Combat
{
    /// <summary>
    /// Resolves one Combat phase (GDD section 5). Every unit with Attack/Over-Attack power deals
    /// its damage independently, every phase — there's no "only the frontmost unit fights"
    /// restriction, and a unit's own allies never block its line of attack, only enemies do
    /// (confirmed design decision).
    ///
    /// Targeting (GDD 5.1/5.2) happens along UnitInstance.AttackColumn — chosen explicitly at
    /// placement, not inferred from the footprint's anchor, since most footprints span more than
    /// one column. Attack hits the NEAREST live enemy in that column, or the Soul directly if the
    /// lane is clear. What counts as "the lane" is BoardQueries.EnemiesInLane, shared with The
    /// Chariot so the two can never disagree about it.
    ///
    /// Over-Attack (GDD 5.3, 8) "pierces through enemy blocking units" (plural) — it hits EVERY live
    /// enemy in the lane, once each, and separately always also hits the enemy Soul.
    ///
    /// This class decides WHO gets hit for how much. It doesn't apply the damage itself: every point
    /// goes through ICombatDamage so that the attacker travels with it, which is what lets Justice
    /// reflect Soul damage back at whoever dealt it and The Devil react to enemy units being hurt.
    /// Cards whose targeting isn't lane-based at all (Justice, The Sun) are not handled here — they
    /// run their own attack step through UnitAbility.OnCombat.
    /// </summary>
    public class AttackResolver
    {
        private readonly HexGrid _grid;
        private readonly UnitRegistry _units;
        private readonly ICombatDamage _damage;

        public AttackResolver(HexGrid grid, UnitRegistry units, ICombatDamage damage)
        {
            _grid = grid;
            _units = units;
            _damage = damage;
        }

        public void ResolveCombat()
        {
            // Snapshot the attacker list up front: targets are still looked up live against the
            // board as the loop proceeds (so a unit killed earlier this phase stops blocking for
            // attackers processed later), but the set of units who GET a turn to attack is fixed
            // at the start of the phase.
            var attackers = new List<UnitInstance>(_units.AllUnits);

            foreach (var attacker in attackers)
            {
                if (attacker.IsDead) continue; // may have died earlier this same phase from another attacker

                var defendingSide = attacker.Owner.Opponent();

                if (attacker.AttackPower > 0)
                {
                    // Rolled per attack action, so a unit with both Attack and Over-Attack gets two
                    // independent chances to be dodged — each is its own swing.
                    if (!_damage.AttackMisses(attacker))
                    {
                        var enemies = FindEnemiesInLane(attacker);
                        if (enemies.Count > 0)
                            _damage.DealUnitDamage(enemies[0], attacker.AttackPower, attacker);
                        else
                            _damage.DealSoulDamage(defendingSide, attacker.AttackPower, attacker);
                    }
                }

                if (attacker.OverAttackPower > 0)
                {
                    if (!_damage.AttackMisses(attacker))
                    {
                        var enemies = FindEnemiesInLane(attacker);
                        foreach (var enemy in enemies)
                            _damage.DealUnitDamage(enemy, attacker.OverAttackPower, attacker);
                        _damage.DealSoulDamage(defendingSide, attacker.OverAttackPower, attacker); // always also hits the Soul — that's the "pierce"
                    }
                }
            }
        }

        private List<UnitInstance> FindEnemiesInLane(UnitInstance attacker) =>
            BoardQueries.EnemiesInLane(
                _grid, _units, attacker.Owner,
                _grid.GetFootprintCoords(attacker.AnchorCoord, attacker.Footprint),
                attacker.AttackColumn);
    }
}
