using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XV. The Devil (GDD 11.XV) — "Your Soul becomes immune to damage. Whenever an enemy unit takes
    /// damage, The Devil deals 1 damage to the enemy Soul. Warning: If the game does not end within
    /// 3 turns after playing The Devil, your Soul takes 20 fatal damage." Condition: your Soul below
    /// 5 HP.
    ///
    /// The three halves are wired to three different places for a reason:
    ///  - Immunity is a live query (GrantsSoulImmunity), so it ends the instant the Devil leaves the
    ///    board rather than needing anything to remember to switch it off.
    ///  - The damage trigger hangs off the universal damage funnel, so it fires for ANY damage an
    ///    enemy unit takes — the Devil's own Over-Attack, someone else's attack, a Tower explosion.
    ///    That breadth is the point: the design note promises "enormous pressure".
    ///  - The doomsday clock is derived from UnitInstance.RoundPlayed rather than counted down, so
    ///    there is no per-unit state to keep in sync (and none to leak between plays — abilities are
    ///    shared singletons, see AbilityRegistry).
    ///
    /// The doomsday payment is the one thing in the game that bypasses Soul immunity. It has to: the
    /// card calls it "fatal", and the only Soul it can hit is the one the Devil itself is protecting,
    /// so respecting the immunity would make the drawback unreachable and the card strictly free.
    /// </summary>
    public sealed class DevilAbility : UnitAbility
    {
        /// <summary>GDD 11.XV: playable only with your Nexus below 5 HP. Strictly below, not at.</summary>
        public const int MaxSoulHpToPlay = 5;

        /// <summary>
        /// Rounds after the one it was played in before the bill comes due. Played on round N, the
        /// payment lands at the end of round N+3 — reading "within 3 turns after playing" as the
        /// three turns that follow the play. One constant to change if that should be tighter.
        /// </summary>
        public const int DoomsdayTurns = 3;

        public const int DoomsdayDamage = 20;
        public const int DamageTriggerSoulDamage = 1;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            int mine = query.GetSoul(side).CurrentHp;
            if (mine >= MaxSoulHpToPlay)
            {
                reason = $"The Devil needs your Soul below {MaxSoulHpToPlay} HP (currently {mine}).";
                return false;
            }

            reason = null;
            return true;
        }

        public override bool GrantsSoulImmunity(IMatchQuery query, UnitInstance self) => true;

        /// <summary>"Whenever an enemy unit takes damage, The Devil deals 1 damage to the enemy Soul."</summary>
        public override void OnUnitDamaged(IAbilityContext ctx, UnitInstance self, UnitInstance target, int amount, UnitInstance attacker)
        {
            if (target == null || target.Owner == self.Owner) return;

            // Soul damage, never unit damage — which is also why this can't feed itself into a loop.
            ctx.DealSoulDamage(target.Owner, DamageTriggerSoulDamage, self);
        }

        /// <summary>
        /// The deadline. Runs in the Decay phase's survivors pass, so it lands at the very end of the
        /// round — after that round's combat has had its chance to actually win the game, which is
        /// precisely the race the card is describing.
        /// </summary>
        public override void OnDecayTick(IAbilityContext ctx, UnitInstance self)
        {
            if (ctx.RoundNumber - self.RoundPlayed < DoomsdayTurns) return;

            ctx.DealSoulDamage(self.Owner, DoomsdayDamage, attacker: null, bypassImmunity: true);
        }
    }
}
