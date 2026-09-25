using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>What a card asks its player to decide at the moment it's played, beyond where it goes. See CardPlayOptions.</summary>
    public enum PlayChoice
    {
        None,
        Element,    // The Magician
        AllyTarget  // The Hanged Man, Death
    }

    /// <summary>
    /// Everything about a pending play that a choice-validating ability needs: who is playing it, where
    /// it will stand, and what they chose. `Footprint` is the tiles the played card ITSELF is about to
    /// occupy — a card that also puts a second unit on the board (the Hanged Man's Tree, Death's
    /// replacement) must make sure the two don't claim the same tiles.
    /// </summary>
    public readonly struct PlayRequest
    {
        public readonly PlayerSide Side;
        public readonly HexCoord Anchor;
        public readonly IReadOnlyList<HexCoord> Footprint;
        public readonly CardPlayOptions Options;

        public PlayRequest(PlayerSide side, HexCoord anchor, IReadOnlyList<HexCoord> footprint, CardPlayOptions options)
        {
            Side = side;
            Anchor = anchor;
            Footprint = footprint;
            Options = options;
        }
    }

    /// <summary>
    /// One card's bespoke behaviour — the part of a Major Arcana that no generic system can express.
    ///
    /// Why this lives in Core rather than next to the card data in the Cards assembly: abilities ARE
    /// the simulation, and Core's whole reason for forbidding UnityEngine references is that the
    /// simulation must be runnable (and testable) outside Unity with plain `dotnet run`. Putting
    /// ability code on the ScriptableObject would have been tidier layering on paper — data and
    /// behaviour together, Core never naming a specific card — but it would have moved the most
    /// intricate rules in the game into the one assembly the offline harness can't compile.
    ///
    /// The price is that AbilityRegistry maps card-id strings to instances, so Core does know those
    /// 22 ids exist. They're already stable identifiers Core handles (UnitSpawnStats.DefinitionId),
    /// so this is a known cost, not an accident.
    ///
    /// The ability implementations live in the CardAbilities/ folder but share this namespace on
    /// purpose: a nested ArcanaWars.Core.Abilities.Cards namespace would shadow ArcanaWars.Core.Cards
    /// from inside every ability file, so "Cards.HandCard" would silently resolve to the wrong place.
    ///
    /// An abstract class rather than an interface with default methods: every hook needs a do-nothing
    /// default, and default interface implementations are a newer-runtime feature this project has no
    /// reason to depend on.
    /// </summary>
    public abstract class UnitAbility
    {
        /// <summary>
        /// The card's play condition (GDD section 11). Returns true when the card may be played.
        /// `reason` is filled in on refusal so the UI can say WHY rather than just refusing — the
        /// same reasoning behind CardPlayFailure existing instead of a bare bool.
        /// </summary>
        public virtual bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            reason = null;
            return true;
        }

        /// <summary>Which play-time choice this card wants. A UI reads this to know what to ask for.</summary>
        public virtual PlayChoice Choice => PlayChoice.None;

        /// <summary>
        /// Whether the card can be played with its choice left blank. Targets are optional (a board
        /// with nothing worth sacrificing shouldn't make the card unplayable — its own stats still
        /// matter); The Magician's element is not, since "choose an element" always has an answer.
        /// </summary>
        public virtual bool ChoiceIsOptional => false;

        /// <summary>
        /// Checks the play-time choices BEFORE anything is spent, placed, or killed. Like CanPlay it
        /// must be side-effect free and must not touch the RNG — it's a yes/no about a play that might
        /// never happen, and a seeded match has to replay the same whether or not a UI asked first.
        /// </summary>
        public virtual bool ValidatePlay(IMatchQuery query, PlayRequest request, out string reason)
        {
            reason = null;
            return true;
        }

        /// <summary>
        /// Lets a card reshape the unit it's about to become, based on its choices — The Magician
        /// taking on its chosen element's trait. Runs before placement, so the unit enters the board
        /// already correct rather than being patched afterwards.
        /// </summary>
        /// Also used by the Queen, whose bonus HP depends on the match rather than on a choice — hence
        /// the query and the full request rather than just the options.
        public virtual UnitSpawnStats ModifySpawnStats(IMatchQuery query, PlayRequest request, UnitSpawnStats stats) => stats;

        /// <summary>
        /// Fires immediately after the unit reaches the board and the card has left hand. `heldCard`
        /// carries how long the played card sat in hand (The High Priestess); `options` carries the
        /// player's play-time choices (The Hanged Man's / Death's target), already validated.
        /// </summary>
        public virtual void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options) { }

        /// <summary>
        /// Fires for every OTHER live unit's ability whenever a unit ENTERS the board — played or
        /// spawned, but not moved. The Lovers' "any unit placed in the gap gains +1 Attack and +1 Heal"
        /// (GDD 11.VI).
        /// </summary>
        public virtual void OnUnitPlaced(IAbilityContext ctx, UnitInstance self, UnitInstance placed) { }

        /// <summary>
        /// Runs at the very start of the Combat phase, before a single attack resolves — The Chariot's
        /// "before attacking, checks for an enemy in front" and repositions (GDD 11.VII).
        /// </summary>
        public virtual void OnBeforeCombat(IAbilityContext ctx, UnitInstance self) { }

        /// <summary>
        /// A buff this unit gives every card its owner DRAWS while it's on the board — The Hermit.
        /// Asked at the moment of each draw, so a condition like the Hermit's isolation is judged then.
        /// </summary>
        public virtual CardBuff DrawBuff(IMatchQuery query, UnitInstance self) => CardBuff.None;

        /// <summary>
        /// A card's own attack step, run during the Combat phase after all ordinary lane attacks have
        /// resolved. This is where an attack that doesn't use lane targeting goes: Justice striking
        /// the highest- and lowest-HP enemies (GDD 11.XI), The Sun burning the biggest enemy for its
        /// own current HP (GDD 11.XIX).
        ///
        /// Cards that attack this way deliberately leave UnitSpawnStats.AttackPower at 0, or they
        /// would also take a normal lane swing on top — see MajorArcanaData's policy note.
        /// </summary>
        public virtual void OnCombat(IAbilityContext ctx, UnitInstance self) { }

        /// <summary>
        /// Fires during the Decay phase's passive-trigger pass, alongside Heal/Wand buffs — i.e.
        /// only for units that SURVIVED this round's decay, matching the GDD loop's own ordering.
        /// </summary>
        public virtual void OnDecayTick(IAbilityContext ctx, UnitInstance self) { }

        /// <summary>Fires when this unit itself dies, after it has been removed from the board (so its tiles are already free).</summary>
        public virtual void OnOwnDeath(IAbilityContext ctx, UnitDeath death) { }

        /// <summary>Fires on every OTHER unit's death, for each surviving unit that has an ability. This is Death's on-death hook (GDD 11.XIII).</summary>
        public virtual void OnAnyUnitDied(IAbilityContext ctx, UnitInstance self, UnitDeath death) { }

        /// <summary>
        /// Fires on every surviving unit's ability whenever ANY unit takes damage — The Devil's
        /// "whenever an enemy unit takes damage" (GDD 11.XV). `amount` is the damage actually dealt,
        /// so a target that shrugged it off (Strength) never reports any.
        /// </summary>
        public virtual void OnUnitDamaged(IAbilityContext ctx, UnitInstance self, UnitInstance target, int amount, UnitInstance attacker) { }

        /// <summary>
        /// Fires on every unit's ability whenever a Soul takes damage — Justice reflecting it "back
        /// to the attacker" (GDD 11.XI). `attacker` is null when nothing on the board was responsible,
        /// in which case there is nothing to reflect onto.
        /// </summary>
        public virtual void OnSoulDamaged(IAbilityContext ctx, UnitInstance self, PlayerSide damagedSide, int amount, UnitInstance attacker) { }

        /// <summary>
        /// True if damage aimed at `target` should land on THIS unit instead — the Knight guarding its
        /// element (GDD 10.2). Asked by MatchState.DealUnitDamage before any damage is applied.
        /// </summary>
        public virtual bool AbsorbsDamageFor(IMatchQuery query, UnitInstance self, UnitInstance target) => false;

        /// <summary>True while this unit makes its owner's Soul immune to damage (The Devil).</summary>
        public virtual bool GrantsSoulImmunity(IMatchQuery query, UnitInstance self) => false;

        /// <summary>True while this unit gives attacks made against its owner a chance to miss (The Moon).</summary>
        public virtual bool CausesEnemyAttacksToMiss(IMatchQuery query, UnitInstance self) => false;

        /// <summary>True while this unit cancels board-wide concealment effects (The Sun). Checked before any miss chance is rolled.</summary>
        public virtual bool DispelsBoardEffects(IMatchQuery query, UnitInstance self) => false;
    }

    /// <summary>
    /// A unit that just died, plus the tiles it was occupying. The coords have to be captured before
    /// removal clears them, because that's exactly what the on-death effects need: The Tower explodes
    /// around where it stood, and Death spawns a 3 of Swords "on its tile."
    /// </summary>
    public readonly struct UnitDeath
    {
        public readonly UnitInstance Unit;
        public readonly IReadOnlyList<HexCoord> Coords;

        /// <summary>
        /// True when this unit was killed in order to be replaced by something else — The Hanged Man's
        /// sacrifice, Death's execution. It is still a real death (user decision): the Fool still burns
        /// mana, the Tower still explodes. The flag exists for the one effect that would otherwise
        /// fight the replacement for the same tile: Death's own 3-of-Swords passive, which stands down.
        /// </summary>
        public readonly bool IsReplacement;

        /// <summary>The graveyard record this death created, so Judgement can queue a ruling on exactly this unit.</summary>
        public readonly GraveyardEntry GraveyardEntry;

        public UnitDeath(UnitInstance unit, IReadOnlyList<HexCoord> coords, bool isReplacement = false, GraveyardEntry graveyardEntry = null)
        {
            Unit = unit;
            Coords = coords;
            IsReplacement = isReplacement;
            GraveyardEntry = graveyardEntry;
        }
    }
}
