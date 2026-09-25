using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// V. The Hierophant (GDD 11.V) — "+1 HP every turn to all friendly units positioned in front of
    /// it." Condition: 8 units played previously this match.
    ///
    /// This is the Wands buff (GDD 9.4) widened from one-to-one into one-to-many, and it runs in the
    /// same Decay-phase pass for the same reason: a unit that decayed to 0 this round is already off
    /// the board and correctly misses out, while a survivor gets its decay cancelled exactly the way
    /// a Wand would cancel it.
    ///
    /// "In front of" is BoardQueries' definition — same columns, past its own front edge, toward the
    /// enemy — which is deliberately the identical geometry its own units attack along.
    /// </summary>
    public sealed class HierophantAbility : UnitAbility
    {
        public const int RequiredUnitsPlayed = 8;
        public const int BuffAmount = 1;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            // Every card in this game spawns a unit, so "units played" is the whole play history.
            // "previously" means before this one, and CanPlay runs before the play is recorded.
            int played = query.GetHistory(side).TotalPlayed;
            if (played < RequiredUnitsPlayed)
            {
                reason = $"The Hierophant needs {RequiredUnitsPlayed} units played this match (you have played {played}).";
                return false;
            }

            reason = null;
            return true;
        }

        public override void OnDecayTick(IAbilityContext ctx, UnitInstance self)
        {
            foreach (var ally in BoardQueries.FriendlyUnitsInFront(ctx, ctx.Units, self))
                ally.ApplyHeal(BuffAmount);
        }
    }
}
