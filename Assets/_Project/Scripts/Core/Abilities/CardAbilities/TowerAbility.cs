using System.Collections.Generic;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XVI. The Tower (GDD 11.XVI) — 23 HP, decays 2/turn, dies instantly to any attack damage once
    /// at 5 HP or below, and explodes on death.
    ///
    /// Only the explosion lives here. The double decay is DecayMultiplier=2 and the instant-death
    /// rule is InstantDeathHpThreshold=5, both plain unit stats — they belong at UnitInstance's
    /// damage/decay funnel where nothing else has to remember them, not in an ability hook that
    /// would only get to see the damage after it had already been applied by the wrong rule.
    ///
    /// Explosion radius reading: the card text is "1 damage to all friendly units in a 1-tile radius
    /// and all enemy units", which could mean every enemy unit on the board. Its own design note
    /// says "its explosion damages everything NEARBY", so the radius is applied to both sides here.
    /// The two readings differ enormously in power (a board-wide 1-damage sweep would let a Tower
    /// clear an entire decayed enemy board on the way out) — flagged as a decision, easily reversed
    /// by dropping the owner check below.
    /// </summary>
    public sealed class TowerAbility : UnitAbility
    {
        public const int FragileHpThreshold = 5;
        public const int ExplosionRadius = 1;
        public const int ExplosionDamage = 1;

        public override void OnOwnDeath(IAbilityContext ctx, UnitDeath death)
        {
            var caught = BoardQueries.UnitsWithinRadius(
                ctx.Board, ctx.Units, death.Coords, ExplosionRadius, death.Unit.Id);

            // The dead Tower is passed as the attacker so the damage is attributed to it — that is
            // what lets The Devil's "whenever an enemy unit takes damage" trigger see an explosion.
            foreach (var unit in new List<UnitInstance>(caught))
                ctx.DealUnitDamage(unit, ExplosionDamage, death.Unit);
        }
    }
}
