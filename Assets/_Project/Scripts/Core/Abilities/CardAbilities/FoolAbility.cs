

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// 0. The Fool (GDD 11.0) — "When it dies, randomly consumes/burns 1 to 6 of your current
    /// Pentacles." No play condition; a free 1-4 HP body whose whole cost is paid on the way out.
    ///
    /// The burn is applied to the NEXT round's mana rather than the current pool — see
    /// IAbilityContext.AddPendingManaBurn for why the literal reading can never do anything.
    /// </summary>
    public sealed class FoolAbility : UnitAbility
    {
        public const int MinBurn = 1;
        public const int MaxBurn = 6;

        public override void OnOwnDeath(IAbilityContext ctx, UnitDeath death)
        {
            int burn = ctx.Rng.Next(MinBurn, MaxBurn + 1);
            ctx.AddPendingManaBurn(death.Unit.Owner, burn);
        }
    }
}
