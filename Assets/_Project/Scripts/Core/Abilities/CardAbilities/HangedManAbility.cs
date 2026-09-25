using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XII. The Hanged Man (GDD 11.XII) — "Sacrifice a chosen friendly unit. It is replaced by a 'Tree'
    /// (HP: 10, Size: 7 tiles, Heal: 1, Pentacle: 1)." Condition: your Soul is below the opponent's.
    ///
    /// The Tree is a 7-tile cluster centred on the sacrificed unit's anchor. Whether it fits is checked
    /// in ValidatePlay, before any mana is spent: every one of those 7 tiles must be free (or belong to
    /// the unit being sacrificed), on your side, and not where the Hanged Man itself is about to stand.
    /// A sacrifice that couldn't grow its Tree is refused rather than half-performed.
    ///
    /// The sacrifice is a REAL death (user decision): a sacrificed Fool still burns next round's mana,
    /// a sacrificed Tower still explodes — which makes the Hanged Man a way to detonate your own Tower
    /// on demand. The one exception is Death's 3-of-Swords passive, which stands down for this unit
    /// because the Tree is taking that ground (see UnitDeath.IsReplacement).
    ///
    /// The target is optional: with nothing worth sacrificing, the Hanged Man still enters as a plain
    /// Heal 1 / Pentacle 1 unit.
    /// </summary>
    public sealed class HangedManAbility : UnitAbility
    {
        public override PlayChoice Choice => PlayChoice.AllyTarget;
        public override bool ChoiceIsOptional => true;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            int mine = query.GetSoul(side).CurrentHp;
            int theirs = query.GetSoul(side.Opponent()).CurrentHp;
            if (mine >= theirs)
            {
                reason = $"The Hanged Man needs your Soul below the opponent's ({mine} vs {theirs}).";
                return false;
            }

            reason = null;
            return true;
        }

        public override bool ValidatePlay(IMatchQuery query, PlayRequest request, out string reason)
        {
            if (request.Options.TargetUnitId == null)
            {
                reason = null;
                return true;
            }

            var target = query.GetUnit(request.Options.TargetUnitId.Value);
            if (target == null || target.IsDead)
            {
                reason = "The Hanged Man's target is no longer on the board.";
                return false;
            }

            if (target.Owner != request.Side)
            {
                reason = "The Hanged Man can only sacrifice one of your own units.";
                return false;
            }

            if (!BoardQueries.FitsReplacing(query, request.Side, target.AnchorCoord, FootprintShapeType.SevenCluster, target.Id, request.Footprint))
            {
                reason = "The Tree needs all 6 tiles around that unit to be free and on your side.";
                return false;
            }

            reason = null;
            return true;
        }

        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            if (options.TargetUnitId == null) return;

            var target = ctx.GetUnit(options.TargetUnitId.Value);
            if (target == null || target.IsDead || target.Owner != self.Owner) return;

            var centre = target.AnchorCoord;
            ctx.KillUnit(target, beingReplaced: true);
            ctx.TrySpawnUnit(self.Owner, centre, centre.ToOffset().Col, SpawnTemplates.HangedManTree(), out _);
        }
    }
}
