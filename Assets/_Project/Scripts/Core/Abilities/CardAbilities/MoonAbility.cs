using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    // See MoonAbility below — this file also holds the concealment half of the card, which is a
    // visibility rule rather than a simulation one.
    /// <summary>
    /// XVIII. The Moon (GDD 11.XVIII) — "Hides the HP values of all friendly units and your Soul from
    /// the opponent. Enemy attacks have a 50% chance to miss."
    ///
    /// Only the miss chance is a simulation rule, and it is the whole of this class: while a Moon is
    /// alive, every attack made by its owner's opponent rolls a coin first (MatchState.AttackMisses).
    /// The roll is per attack action rather than per unit hit, so a piercing Over-Attack either lands
    /// on the entire lane or misses all of it — one swing, one dodge.
    ///
    /// Hiding HP changes nothing about what happens, only about what one player is allowed to see, so
    /// it is not a hook on this class — it is answered by <see cref="ConcealsHpFor"/> when a screen's
    /// MatchSnapshot is captured. That was impossible while "how do two humans play" was unsettled
    /// (a single shared screen has no second viewer to hide anything from); with hot-seat and
    /// networked sessions both having a viewer, it is now a normal visibility question.
    ///
    /// The Moon's own Heal:1 is a plain stat and works through the generic system already.
    /// </summary>
    public sealed class MoonAbility : UnitAbility
    {
        /// <summary>The Moon does not conceal itself from a Sun — see MatchState.AttackMisses, which checks for a dispel before rolling.</summary>
        public override bool CausesEnemyAttacksToMiss(IMatchQuery query, UnitInstance self) => true;

        /// <summary>
        /// Whether that side's unit HP and Soul HP are hidden from its opponent right now: a live
        /// Moon of theirs, with no Sun on the board to cancel it (GDD 11.XVIII / 11.XIX).
        ///
        /// Keyed on the Moon's own id rather than on CausesEnemyAttacksToMiss, because concealment is
        /// this card's, not a property of everything that might ever cause a miss. Side-effect free
        /// and RNG-free: it is asked every time a screen redraws.
        /// </summary>
        public static bool ConcealsHpFor(IMatchQuery query, PlayerSide side)
        {
            if (query.BoardEffectsDispelled) return false;

            foreach (var unit in query.AllUnits)
                if (unit.Owner == side && !unit.IsDead && unit.DefinitionId == MajorArcanaIds.Moon)
                    return true;

            return false;
        }
    }
}
