using System;
using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// Everything a card ability may LOOK AT, and nothing it may change. Play conditions and choice
    /// validation get only this, because "may I play this card" must never have a side effect — a
    /// UI that greys out an unplayable card is asking on every card in hand, every frame it repaints.
    /// </summary>
    public interface IMatchQuery
    {
        int RoundNumber { get; }
        HexGrid Board { get; }
        IEnumerable<UnitInstance> AllUnits { get; }
        UnitInstance GetUnit(int unitId);
        Soul GetSoul(PlayerSide side);
        IReadOnlyList<HandCard> GetHand(PlayerSide side);
        MatchHistory GetHistory(PlayerSide side);

        /// <summary>
        /// A player's undrawn cards. Needed by Death's replacement (GDD 11.XIII), which looks through
        /// the deck for "a random unit with a higher cost". Hidden information — a UI must never show it.
        /// </summary>
        IReadOnlyList<IPlayableCard> GetDeckCards(PlayerSide side);

        /// <summary>A player's dead that are still eligible to come back (Judgement).</summary>
        IReadOnlyList<GraveyardEntry> GetGraveyard(PlayerSide side);

        /// <summary>
        /// True while some unit on `side` is protecting that Soul from all damage (The Devil, GDD
        /// 11.XV). Exposed as a query rather than hidden inside DealSoulDamage so an ability can
        /// reason about it — and so the one effect that must ignore it (The Devil's own doomsday
        /// payment) can say so explicitly instead of reaching around the rule.
        /// </summary>
        bool IsSoulImmune(PlayerSide side);

        /// <summary>
        /// True while a card is suppressing board-wide concealment effects — The Sun's "cancels all
        /// illusions, stealth, and armor" (GDD 11.XIX). Today the only such effect that exists is The
        /// Moon's miss chance, so this is effectively "is a Sun on the board".
        /// </summary>
        bool BoardEffectsDispelled { get; }
    }

    /// <summary>
    /// How damage is applied during combat, with the attacker carried alongside it.
    ///
    /// This exists because two cards need to know where damage CAME from, which the old code threw
    /// away: Justice reflects Soul damage "back to the attacker" (GDD 11.XI), and The Devil fires
    /// whenever an enemy unit takes damage (GDD 11.XV). Routing every point of damage through one
    /// pair of methods is what makes those triggers possible without each damage site remembering
    /// to fire them.
    ///
    /// AttackResolver takes this rather than the full context: resolving combat needs to deal damage
    /// and to ask whether an attack lands, and nothing else.
    /// </summary>
    public interface ICombatDamage
    {
        /// <summary>
        /// Rolls The Moon's 50% miss chance (GDD 11.XVIII) for one attack action by this attacker.
        /// Called once per attack, not once per unit hit, so a piercing Over-Attack either lands on
        /// the whole lane or misses it entirely. Always false when no Moon defends against this
        /// attacker, or when a Sun has dispelled it.
        /// </summary>
        bool AttackMisses(UnitInstance attacker);

        /// <summary>Damages a unit and fires the resulting triggers. `attacker` may be null for damage with no unit behind it.</summary>
        void DealUnitDamage(UnitInstance target, int amount, UnitInstance attacker);

        /// <summary>
        /// Damages a Soul and fires the resulting triggers. Blocked entirely while that Soul is
        /// immune (The Devil) unless `bypassImmunity` is set, which only The Devil's own doomsday
        /// payment does — the card calls that damage "fatal", so it has to pierce the very
        /// protection the card granted.
        /// </summary>
        void DealSoulDamage(PlayerSide side, int amount, UnitInstance attacker, bool bypassImmunity = false);
    }

    /// <summary>
    /// What an ability may actually DO. Implemented by MatchState, which is the only thing that owns
    /// all of this — but abilities are handed the interface rather than MatchState itself, so an
    /// ability can be exercised in isolation against a stub, and so the mutation surface stays
    /// small and visible instead of "the whole match, do what you like."
    ///
    /// Souls and Tiles are reachable through IMatchQuery and are mutable in their own right
    /// (Soul.ApplyHeal, Tile.IsGreened), so they need no forwarding methods here. Only operations
    /// that must be sequenced by the match itself appear below. Each is a general primitive rather
    /// than a card-shaped one ("discard a hand", not "do Wheel of Fortune"), so the next card that
    /// needs one doesn't have to grow this interface again.
    /// </summary>
    public interface IAbilityContext : IMatchQuery, ICombatDamage
    {
        /// <summary>The match's shared RNG. Abilities must use this rather than making their own, so a seeded match replays identically (see Deck's note on the same point).</summary>
        Random Rng { get; }

        UnitRegistry Units { get; }

        /// <summary>
        /// The Fool's death penalty (GDD 11.0). Deliberately a debt against the NEXT round rather
        /// than a subtraction from the current pool: mana doesn't carry over (GDD 7.1) and units
        /// can only die in Combat or Decay, both after the Action phase — so burning "your current
        /// Pentacles" at the moment of death would always be a no-op, and the card's own design
        /// note ("can cripple your economy at a critical moment") would never be true.
        /// </summary>
        void AddPendingManaBurn(PlayerSide side, int amount);

        /// <summary>
        /// Puts a new unit on the board mid-match, outside the normal card-play pipeline — Death's
        /// 3 of Swords, the Hanged Man's Tree. Placement rules still apply, so this returns false when
        /// the tiles aren't free; abilities must handle that rather than assume. `sourceCard` records the
        /// card the unit stands for, if any, so that if it later dies Judgement can return that card to
        /// hand; tokens pass null. A spawn counts as the unit being placed (the Lovers' gap sees it).
        /// </summary>
        bool TrySpawnUnit(PlayerSide owner, HexCoord anchor, int attackColumn, UnitSpawnStats stats, out UnitInstance spawned, IPlayableCard sourceCard = null);

        /// <summary>
        /// Kills a unit outright (a sacrifice or an execution, not damage) and runs the full death
        /// pipeline immediately, so its tiles are free when this returns. `beingReplaced` marks it as
        /// about to be replaced by something else — see UnitDeath.IsReplacement for what that changes.
        /// </summary>
        void KillUnit(UnitInstance unit, bool beingReplaced);

        /// <summary>Pulls one specific card out of a player's draw pile. False if it isn't there.</summary>
        bool TryTakeFromDeck(PlayerSide side, IPlayableCard card);

        /// <summary>Moves a player's whole hand to their discard pile. Returns how many cards that was.</summary>
        int DiscardHand(PlayerSide side);

        /// <summary>Draws cards for a player this round, each carrying an optional cost discount. Returns how many reached the hand.</summary>
        int DrawCards(PlayerSide side, int count, int costDiscount);

        /// <summary>Adds Pentacles to a player's pool for the current round. See PentacleLedger.AddPentacles on the cap.</summary>
        void GainPentacles(PlayerSide side, int amount);

        /// <summary>Asks `judge` to rule on a dead unit later — see PendingJudgement.</summary>
        void QueueJudgement(PlayerSide judge, GraveyardEntry entry);

        /// <summary>Takes an entry out of its owner's graveyard (it was resurrected). False if it was already gone.</summary>
        bool RemoveFromGraveyard(GraveyardEntry entry);
    }
}
