using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XIII. Death (GDD 11.XIII) — two effects:
    ///  1. On play: "Kills a chosen unit. A random unit from your deck with a higher cost spawns in its
    ///     place."
    ///  2. While on the board: "whenever ANY unit dies, a '3 of Swords' (HP: 3, Attack: 1) spawns on
    ///     its tile."
    ///
    /// THE KILL (user decisions): the target must be one of YOUR units — the design note frames it as
    /// "upgrade a cheap unit into something powerful". The replacement is drawn at random from the
    /// deck cards whose printed cost is higher than the target's AND whose footprint fits where the
    /// target stands; if no such card exists, that unit can't be targeted at all, so no mana is ever
    /// spent on a kill that yields nothing. The kill is a real death (the Fool burns, the Tower
    /// explodes) but Death's own passive stands down for it, since the replacement takes the tile.
    /// The target is optional — Death can simply be played as a unit.
    ///
    /// The replacement is SPAWNED, not played: it isn't paid for, doesn't enter match history, and its
    /// own on-play ability doesn't fire. Its ongoing abilities (per-turn, combat, on-death) do work,
    /// since those key off the unit's definition id like any other unit's.
    ///
    /// THE PASSIVE — ALLIES ONLY (user decision, overriding the GDD's "ANY unit" and its design note's
    /// "friend or foe"): only units belonging to Death's own controller become a 3 of Swords; an enemy
    /// dying does nothing. Since allies always die on their owner's side (GDD 4.3), the spawn is always
    /// a legal placement. An allied 3 of Swords that later dies spawns another one, making Death's
    /// board self-replacing for as long as Death survives (8 HP) — a strong but bounded effect. It
    /// cannot loop within a single death pass: the spawn arrives at full HP, so it is not dead when the
    /// pass re-scans. Death does not replace itself: it has already left the board when its own death
    /// is announced, and OnAnyUnitDied only runs for survivors.
    /// </summary>
    public sealed class DeathAbility : UnitAbility
    {
        public override PlayChoice Choice => PlayChoice.AllyTarget;
        public override bool ChoiceIsOptional => true;

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
                reason = "Death's target is no longer on the board.";
                return false;
            }

            if (target.Owner != request.Side)
            {
                reason = "Death can only target one of your own units.";
                return false;
            }

            if (FittingReplacements(query, request.Side, target.AnchorCoord, target.PrintedCost, target.Id, request.Footprint).Count == 0)
            {
                reason = $"No card in your deck costs more than {target.PrintedCost} and fits where that unit stands.";
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

            var anchor = target.AnchorCoord;
            int costToBeat = target.PrintedCost;

            ctx.KillUnit(target, beingReplaced: true);

            // Re-checked AFTER the kill rather than trusting validation: the target's own on-death
            // effects (a Tower exploding) have run in between. Death's own tiles are occupied by now,
            // so nothing needs reserving — a candidate that overlaps them simply fails to fit.
            var candidates = FittingReplacements(ctx, self.Owner, anchor, costToBeat, replacedUnitId: -1, reserved: null);
            if (candidates.Count == 0) return;

            var pick = candidates[ctx.Rng.Next(candidates.Count)];
            // The match RNG, because this unit actually reaches the board — a replacement Fool must
            // roll its HP from the seeded stream like any other Fool. See IPlayableCard.GetSpawnStats.
            var stats = pick.GetSpawnStats(0, ctx.Rng).With(printedCost: pick.GetCost());

            // Spawned first, taken from the deck second, so a card can never vanish from the deck
            // without arriving on the board.
            if (ctx.TrySpawnUnit(self.Owner, anchor, anchor.ToOffset().Col, stats, out _, pick))
                ctx.TryTakeFromDeck(self.Owner, pick);
        }

        public override void OnAnyUnitDied(IAbilityContext ctx, UnitInstance self, UnitDeath death)
        {
            if (death.IsReplacement) return;
            if (death.Unit.Owner != self.Owner) return;
            if (death.Coords == null || death.Coords.Count == 0) return;

            // A multi-tile unit leaves several tiles behind but only one replacement. The anchor is
            // preferred so the spawn lands somewhere predictable; any other freed tile will do if the
            // anchor is somehow taken (a cascade of deaths resolving in the same pass).
            foreach (var coord in Ordered(death))
            {
                if (!ctx.Board.TryGetTile(coord, out var tile) || !tile.IsEmpty) continue;

                int column = coord.ToOffset().Col;
                if (ctx.TrySpawnUnit(self.Owner, coord, column, SpawnTemplates.ThreeOfSwords(), out _))
                    return;
            }
        }

        /// <summary>
        /// Every deck card that could legally replace the target: printed cost strictly higher, and a
        /// footprint that fits at the target's anchor. Duplicates are kept on purpose — two copies of a
        /// card in the deck make it twice as likely to be picked, as a "random unit from your deck"
        /// should. Variable-cost cards (the Aces) are priced at their minimum, since nothing was
        /// committed to them.
        /// </summary>
        private static List<IPlayableCard> FittingReplacements(
            IMatchQuery query, PlayerSide side, HexCoord anchor, int costToBeat, int replacedUnitId, IReadOnlyList<HexCoord> reserved)
        {
            var result = new List<IPlayableCard>();
            foreach (var card in query.GetDeckCards(side))
            {
                if (card.GetCost() <= costToBeat) continue;

                // No RNG: this only asks how big a card is, and it runs from ValidatePlay as well as
                // from the play itself. Rolling here would mean merely *checking* whether Death has a
                // legal target consumed randomness — and would do it once per deck card scanned.
                var shape = card.GetSpawnStats().Footprint;
                if (BoardQueries.FitsReplacing(query, side, anchor, shape, replacedUnitId, reserved))
                    result.Add(card);
            }
            return result;
        }

        /// <summary>The dead unit's tiles, anchor first.</summary>
        private static IEnumerable<HexCoord> Ordered(UnitDeath death)
        {
            yield return death.Unit.AnchorCoord;

            foreach (var coord in death.Coords)
                if (coord != death.Unit.AnchorCoord)
                    yield return coord;
        }
    }
}
