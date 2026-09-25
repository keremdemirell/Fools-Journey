using System.Collections.Generic;
using ArcanaWars.Core.Abilities;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.GameLoop;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// Turns "this player wants to play this card, here, attacking along this column, with these
    /// choices" into a unit on the board — or a reason why not. Kept out of MatchState for the same
    /// reason AttackResolver is: the validation sequence is worth testing on its own.
    ///
    /// Ordering matters here, and each step is placed where it is deliberately:
    ///  1. phase / whose turn / in-hand — cheapest, and nothing else is meaningful without them;
    ///  2. play condition — before anything is spent or moved, because CanPlay must stay side-effect
    ///     free (a UI greys out unplayable cards by calling it constantly);
    ///  3. cost — against the specific copy in hand, which may carry a Wheel of Fortune discount;
    ///  4. play-time choices (element, target) — still before anything happens, so a sacrifice whose
    ///     Tree wouldn't fit is refused rather than discovered after the unit is already dead;
    ///  5. placement legality — asked BEFORE any randomness is spent and before mana is spent, so a
    ///     rejected placement leaves the match bit-for-bit unchanged;
    ///  6. the random roll (The Fool's HP) — only once the play is certain to happen;
    ///  7. only then does the unit reach the board, the card leave hand, mana leave the pool, and
    ///     the play enter history.
    ///
    /// Step 5 sitting before step 6 is load-bearing, not tidiness. A refused play that had already
    /// rolled dice would advance the match's random stream without anything happening, which makes
    /// the match unreplayable from its command list and desyncs lockstep networking the moment one
    /// client filters an illegal move locally instead of sending it. This was a real bug, caught by
    /// replaying recorded matches and finding they diverged.
    ///
    /// The unit's on-play ability is NOT fired here. This class decides whether a play is legal and
    /// makes it happen; what the card then DOES is the match's business (MatchState.PlayCard), and
    /// keeping the two apart is what stops an ability from being able to reject a play halfway
    /// through, after the mana is already gone.
    /// </summary>
    public class CardPlayResolver
    {
        private static readonly IReadOnlyList<HexCoord> NoTiles = new List<HexCoord>();

        private readonly RoundManager _rounds;
        private readonly HexGrid _grid;
        private readonly UnitRegistry _units;
        private readonly IMatchQuery _query;
        private readonly System.Random _rng;

        /// <summary>
        /// The RNG is taken directly rather than read off _query on purpose. It belongs to
        /// IAbilityContext, not IMatchQuery, precisely because IMatchQuery is the side-effect-free
        /// view that CanPlay and ValidatePlay receive — and those must never consume randomness, or
        /// a UI greying out a card would change the game. Widening IMatchQuery to reach the RNG would
        /// put it back within reach of exactly the two methods forbidden to use it.
        /// </summary>
        public CardPlayResolver(RoundManager rounds, HexGrid grid, UnitRegistry units, IMatchQuery query, System.Random rng)
        {
            _rounds = rounds;
            _grid = grid;
            _units = units;
            _query = query;
            _rng = rng;
        }

        /// <summary>manaCommitted is only meaningful for cards with HasVariableCost (the Aces); every other card ignores it.</summary>
        public CardPlayResult Play(PlayerState player, IPlayableCard card, HexCoord anchor, int attackColumn, int manaCommitted = 0, CardPlayOptions options = default)
        {
            if (_rounds.CurrentPhase != GamePhase.Action)
                return CardPlayResult.Failed(CardPlayFailure.WrongPhase);

            // The Action phase alternates one card at a time (see ActionTurns). Checked here, in the
            // one place every play already passes through, rather than at each calling surface —
            // a second copy of this rule in the UI is a rule that can disagree with itself.
            if (_rounds.ActiveSide != player.Side)
                return CardPlayResult.Failed(CardPlayFailure.NotYourTurn,
                    $"It is {_rounds.ActiveSide}'s turn.");

            if (card == null || !player.Hand.TryPeek(card, out var copy))
                return CardPlayResult.Failed(CardPlayFailure.CardNotInHand);

            // The Ace (GDD 10.1): "Cost: 1 OR All Current Mana" — two modes, not a slider. Committing
            // nothing (or the minimum) is the cheap mode; anything above that must be every Pentacle
            // left, or it's neither of the two things the card allows.
            if (card.HasVariableCost && manaCommitted > card.GetCost() && manaCommitted != player.Mana.Remaining)
            {
                return CardPlayResult.Failed(CardPlayFailure.InvalidChoice,
                    $"{card.DisplayName} costs either {card.GetCost()} or all your remaining Pentacles ({player.Mana.Remaining}).");
            }

            AbilityRegistry.TryGet(card.DefinitionId, out var ability);

            if (ability != null && !ability.CanPlay(_query, player.Side, out var conditionReason))
                return CardPlayResult.Failed(CardPlayFailure.ConditionNotMet, conditionReason);

            // An Ace asking to commit more mana than the player has simply prices itself out of
            // reach here — its cost IS the committed amount, which then fails the check below.
            int cost = copy.EffectiveCost(manaCommitted);
            if (cost > player.Mana.Remaining)
                return CardPlayResult.Failed(CardPlayFailure.CannotAfford);

            // The card's shape and non-random stats, resolved WITHOUT touching the RNG (see the null
            // rng contract on IPlayableCard.GetSpawnStats). Everything that can still reject the play
            // is decided from this preview, so that a refused play leaves the match completely
            // untouched — including its random stream.
            var preview = card.GetSpawnStats(manaCommitted);

            PlayRequest request = default;
            if (ability != null)
            {
                var footprint = _grid.GetFootprintCoords(anchor, preview.Footprint) ?? NoTiles;
                request = new PlayRequest(player.Side, anchor, footprint, options);

                if (!ability.ValidatePlay(_query, request, out var choiceReason))
                    return CardPlayResult.Failed(CardPlayFailure.InvalidChoice, choiceReason);
            }

            // Placement is checked here, ahead of the roll, rather than being discovered by
            // TryPlaceUnit below. Both of its rejection reasons depend only on the footprint and the
            // attack column, never on HP, so asking early gives the same answer — and it is what
            // stops a doomed play from advancing the RNG. Without this, a match could not be
            // replayed from its commands, because the attempts that failed moved the dice too.
            if (!_units.CanPlace(player.Side, anchor, attackColumn, preview.Footprint))
                return CardPlayResult.Failed(CardPlayFailure.IllegalPlacement);

            // From here the play WILL happen, so it is safe to spend randomness. Resolved exactly
            // once: calling GetSpawnStats twice with an rng would roll two different units and
            // quietly place the wrong one. The PRINTED cost is stamped on — not what was paid — so a
            // discount doesn't make the unit count as cheaper to Death later.
            var stats = card.GetSpawnStats(manaCommitted, _rng).With(printedCost: card.GetCost(manaCommitted));

            if (ability != null)
                stats = ability.ModifySpawnStats(_query, request, stats);

            // The Hermit's draw buff, carried by this specific copy since it was drawn.
            stats = copy.Buff.ApplyTo(stats);

            stats = stats.WithBonusHp(GreenTileBonus(anchor, stats.Footprint));

            if (!_units.TryPlaceUnit(player.Side, anchor, attackColumn, stats, out var unit, _rounds.RoundNumber))
                return CardPlayResult.Failed(CardPlayFailure.IllegalPlacement);

            // Only now that the unit is actually on the board does the card leave hand and mana leave the pool.
            player.Mana.TrySpend(cost);
            player.Hand.TryRemove(card, out var playedFrom);
            player.History.Record(card, _rounds.RoundNumber);

            return CardPlayResult.Ok(unit, playedFrom, options);
        }

        /// <summary>
        /// The Empress's greened tiles (GDD 11.III): +3 HP to a friendly card played on them. Granted
        /// once per unit, not once per green tile it covers — see EmpressAbility for why. Placement
        /// legality isn't checked here; if the footprint turns out to be illegal the bonus is simply
        /// never used, because TryPlaceUnit rejects the whole play.
        /// </summary>
        private int GreenTileBonus(HexCoord anchor, FootprintShapeType footprint)
        {
            var coords = _grid.GetFootprintCoords(anchor, footprint);
            if (coords == null) return 0;

            foreach (var coord in coords)
                if (_grid.TryGetTile(coord, out var tile) && tile.IsGreened)
                    return EmpressAbility.GreenTileHpBonus;

            return 0;
        }
    }
}
