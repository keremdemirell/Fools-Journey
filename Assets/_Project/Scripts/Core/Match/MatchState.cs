using System;
using System.Collections.Generic;
using System.Linq;
using ArcanaWars.Core.Abilities;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Combat;
using ArcanaWars.Core.GameLoop;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Match
{
    /// <summary>
    /// Composition root for one running match: owns the board, the units on it, both players, and
    /// the round clock, and wires each phase to the system that handles it.
    ///
    /// It also implements IAbilityContext, which makes it the single thing card abilities are allowed
    /// to talk to — they get the interface rather than this class, so the surface they can touch stays
    /// small and reviewable (see IAbilityContext).
    /// </summary>
    public class MatchState : IAbilityContext
    {
        /// <summary>
        /// Ceiling on how many times a single death pass may cascade. Death puts a 3 of Swords on the
        /// tile of every allied unit that dies, so a death can create a unit; that unit arrives at full
        /// HP and can't die in the same pass, but the guard stays as cheap insurance against a future
        /// card that could.
        /// </summary>
        private const int MaxDeathCascadePasses = 64;

        /// <summary>
        /// Ceiling on how deep damage triggers may nest. Justice reflects Soul damage onto the
        /// attacker, and The Devil turns damage to an enemy unit into damage to that enemy's Soul —
        /// so two of these facing each other across the board can bounce a single hit back and forth.
        /// Beyond this depth the damage is still applied, but it stops firing further triggers, which
        /// ends the chain without silently swallowing the hit that started it.
        /// </summary>
        private const int MaxTriggerDepth = 8;

        public HexGrid Board { get; }
        public UnitRegistry Units { get; }
        public RoundManager Rounds { get; }
        public PlayerState PlayerA { get; }
        public PlayerState PlayerB { get; }

        private readonly AttackResolver _attackResolver;
        private readonly CardPlayResolver _cardPlayResolver;
        private readonly Random _rng;
        private int _triggerDepth;

        /// <summary>Units killed in order to be replaced (sacrifices, executions) whose death hasn't been announced yet — see UnitDeath.IsReplacement.</summary>
        private readonly HashSet<int> _replacedUnitIds = new HashSet<int>();

        /// <summary>
        /// Which card each unit on the board stands for, by unit id. Kept here rather than on the unit
        /// because UnitInstance lives in the Units layer, below the Cards contract, and must not know
        /// what a card is. Only Judgement needs it — to hand a dead unit's card back to someone.
        /// </summary>
        private readonly Dictionary<int, IPlayableCard> _sourceCards = new Dictionary<int, IPlayableCard>();

        private readonly List<PendingJudgement> _pendingJudgements = new List<PendingJudgement>();
        private int _nextJudgementId = 1;

        /// <summary>
        /// Fires once, from WinCheck, with the winning side. Null means a draw — both Souls hit 0 in
        /// the same round, which is possible because both players' attacks resolve before the check.
        ///
        /// The result is also latched on IsOver/Winner, so anything that wasn't subscribed at the
        /// moment it fired can still find out.
        /// </summary>
        public event Action<PlayerSide?> MatchEnded;

        /// <summary>True once a Soul has hit 0 and the result has been decided. Nothing further can be played, passed or advanced.</summary>
        public bool IsOver { get; private set; }

        /// <summary>The winner, or null for a draw OR for a match still in progress — check IsOver to tell those two apart.</summary>
        public PlayerSide? Winner { get; private set; }

        /// <summary>Fires for every unit death, after it has left the board. Presentation uses it for effects; the on-death card abilities are dispatched separately, below.</summary>
        public event Action<UnitDeath> UnitDied;

        public MatchState(
            HexGrid board,
            IEnumerable<IPlayableCard> deckA = null,
            IEnumerable<IPlayableCard> deckB = null,
            int soulMaxHp = Soul.StandardStartingHp,
            Random rng = null)
        {
            Board = board;
            Units = new UnitRegistry(board);
            Rounds = new RoundManager();

            _rng = rng ?? new Random();
            PlayerA = new PlayerState(PlayerSide.A, new Deck(deckA, _rng), soulMaxHp);
            PlayerB = new PlayerState(PlayerSide.B, new Deck(deckB, _rng), soulMaxHp);

            _attackResolver = new AttackResolver(Board, Units, this);
            _cardPlayResolver = new CardPlayResolver(Rounds, Board, Units, this, _rng);

            Rounds.PhaseChanged += OnPhaseChanged;
        }

        public PlayerState GetPlayer(PlayerSide side) => side == PlayerSide.A ? PlayerA : PlayerB;
        public PlayerState GetOpponent(PlayerSide side) => side == PlayerSide.A ? PlayerB : PlayerA;

        // --- IMatchQuery -------------------------------------------------------------------------

        public int RoundNumber => Rounds.RoundNumber;
        public IEnumerable<UnitInstance> AllUnits => Units.AllUnits;
        public Random Rng => _rng;
        public Soul GetSoul(PlayerSide side) => GetPlayer(side).Soul;
        public IReadOnlyList<HandCard> GetHand(PlayerSide side) => GetPlayer(side).Hand.Cards;
        public MatchHistory GetHistory(PlayerSide side) => GetPlayer(side).History;
        public void AddPendingManaBurn(PlayerSide side, int amount) => GetPlayer(side).AddPendingManaBurn(amount);
        public UnitInstance GetUnit(int unitId) => Units.GetUnit(unitId);
        public IReadOnlyList<IPlayableCard> GetDeckCards(PlayerSide side) => GetPlayer(side).Deck.DrawPile;
        public IReadOnlyList<GraveyardEntry> GetGraveyard(PlayerSide side) => GetPlayer(side).Graveyard.Entries;

        /// <summary>Scanned live rather than cached, for the same reason mana is recomputed each round: the moment the unit granting it leaves the board, the effect must stop.</summary>
        public bool IsSoulImmune(PlayerSide side) =>
            AnyLiveAbility((unit, ability) => unit.Owner == side && ability.GrantsSoulImmunity(this, unit));

        public bool BoardEffectsDispelled =>
            AnyLiveAbility((unit, ability) => ability.DispelsBoardEffects(this, unit));

        private bool AnyLiveAbility(Func<UnitInstance, UnitAbility, bool> predicate)
        {
            foreach (var unit in Units.AllUnits)
            {
                if (unit.IsDead) continue;
                if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability) && predicate(unit, ability))
                    return true;
            }
            return false;
        }

        // --- ICombatDamage -----------------------------------------------------------------------

        /// <summary>
        /// The Moon's 50% miss chance (GDD 11.XVIII), rolled once per attack action. A Sun anywhere on
        /// the board cancels it outright before any dice are thrown — the Sun "cancels all illusions"
        /// (GDD 11.XIX), and the Moon's concealment is the only illusion the simulation actually has.
        /// </summary>
        public bool AttackMisses(UnitInstance attacker)
        {
            if (attacker == null) return false;
            if (BoardEffectsDispelled) return false;

            var defendingSide = attacker.Owner.Opponent();
            bool defended = AnyLiveAbility((unit, ability) =>
                unit.Owner == defendingSide && ability.CausesEnemyAttacksToMiss(this, unit));

            return defended && _rng.Next(2) == 0;
        }

        /// <summary>
        /// Applies damage to a unit and fires the resulting triggers. Damage that was shrugged off
        /// entirely (Strength's immunity) reports nothing, so a trigger phrased as "whenever a unit
        /// takes damage" correctly doesn't fire when no damage was actually taken.
        ///
        /// This is also the funnel that ability damage uses (The Tower's explosion), so Strength's
        /// immunity and The Tower's own fragility threshold apply there exactly as they do in combat.
        /// </summary>
        public void DealUnitDamage(UnitInstance target, int amount, UnitInstance attacker)
        {
            if (target == null || target.IsDead || amount <= 0) return;

            // A Knight takes hits meant for its element (GDD 10.2). Redirected once, before anything is
            // applied, so every rule below — immunity, triggers — sees the Knight as the unit that was hit.
            target = DamageTargetAfterRedirect(target);

            int before = target.CurrentHp;
            target.ApplyCombatDamage(amount);
            int dealt = before - target.CurrentHp;
            if (dealt <= 0) return;

            FireTriggers((ability, unit) => ability.OnUnitDamaged(this, unit, target, dealt, attacker));
        }

        /// <summary>The live unit that absorbs hits meant for `target` (a Knight of its element), or `target` itself. Never chains: a redirected hit is not redirected again.</summary>
        private UnitInstance DamageTargetAfterRedirect(UnitInstance target)
        {
            foreach (var unit in Units.AllUnits)
            {
                if (unit.IsDead || unit.Id == target.Id) continue;
                if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability) && ability.AbsorbsDamageFor(this, unit, target))
                    return unit;
            }
            return target;
        }

        /// <summary>
        /// Applies damage to a Soul and fires the resulting triggers. Refused outright while that Soul
        /// is immune (The Devil, GDD 11.XV) unless the caller explicitly bypasses it — which only The
        /// Devil's own doomsday payment does.
        /// </summary>
        public void DealSoulDamage(PlayerSide side, int amount, UnitInstance attacker, bool bypassImmunity = false)
        {
            if (amount <= 0) return;
            if (!bypassImmunity && IsSoulImmune(side)) return;

            GetSoul(side).ApplyDamage(amount);

            FireTriggers((ability, unit) => ability.OnSoulDamaged(this, unit, side, amount, attacker));
        }

        /// <summary>
        /// Runs one damage trigger across every live unit's ability, with a depth guard so two cards
        /// that feed each other can't recurse forever. The board is snapshotted first: a trigger may
        /// kill or spawn units, and the set that gets to react is the set that existed when the damage
        /// landed.
        /// </summary>
        private void FireTriggers(Action<UnitAbility, UnitInstance> fire)
        {
            if (_triggerDepth >= MaxTriggerDepth) return;

            _triggerDepth++;
            try
            {
                foreach (var unit in Units.AllUnits.ToList())
                {
                    if (unit.IsDead) continue;
                    if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability))
                        fire(ability, unit);
                }
            }
            finally
            {
                _triggerDepth--;
            }
        }

        // --- IAbilityContext ---------------------------------------------------------------------

        public bool TrySpawnUnit(PlayerSide owner, HexCoord anchor, int attackColumn, UnitSpawnStats stats, out UnitInstance spawned, IPlayableCard sourceCard = null)
        {
            if (!Units.TryPlaceUnit(owner, anchor, attackColumn, stats, out spawned, Rounds.RoundNumber))
                return false;

            if (sourceCard != null) _sourceCards[spawned.Id] = sourceCard;
            NotifyPlaced(spawned);
            return true;
        }

        /// <summary>Resolves the death on the spot, so a replacing ability can place its replacement on the tiles this frees as soon as it returns.</summary>
        public void KillUnit(UnitInstance unit, bool beingReplaced)
        {
            if (unit == null || Units.GetUnit(unit.Id) == null) return;

            if (beingReplaced) _replacedUnitIds.Add(unit.Id);
            unit.Kill();
            ResolveDeaths();
        }

        public bool TryTakeFromDeck(PlayerSide side, IPlayableCard card) => GetPlayer(side).Deck.TryRemove(card);

        public int DiscardHand(PlayerSide side)
        {
            var player = GetPlayer(side);
            var cards = player.Hand.RemoveAll();
            foreach (var card in cards)
                player.Deck.Discard(card);
            return cards.Count;
        }

        public int DrawCards(PlayerSide side, int count, int costDiscount) =>
            GetPlayer(side).DrawCards(count, Rounds.RoundNumber, costDiscount, DrawBuffFor(side));

        public void GainPentacles(PlayerSide side, int amount) => GetPlayer(side).Mana.AddPentacles(amount);

        public void QueueJudgement(PlayerSide judge, GraveyardEntry entry)
        {
            if (entry == null) return;
            _pendingJudgements.Add(new PendingJudgement(_nextJudgementId++, judge, entry));
        }

        public bool RemoveFromGraveyard(GraveyardEntry entry) =>
            entry != null && GetPlayer(entry.Owner).Graveyard.Remove(entry);

        // --- Judgement's rulings (public: the UI answers these) -----------------------------------

        /// <summary>Deaths waiting for Judgement's owner to rule on, oldest first. See PendingJudgement.</summary>
        public IReadOnlyList<PendingJudgement> PendingJudgements => _pendingJudgements;

        /// <summary>
        /// Rules on one pending judgement. RETURN puts the dead unit's card into the judge's hand (a
        /// full hand burns it to the judge's discard pile, same as an overdraw); DISCARD sends the card
        /// to its owner's discard pile. Either way the unit leaves its graveyard for good. Returns false
        /// if the id is unknown, if RETURN is asked of a token with no card, or if the unit already left
        /// the graveyard some other way (in which case the stale ruling is simply dropped).
        /// </summary>
        public bool ResolveJudgement(int judgementId, JudgementVerdict verdict)
        {
            var pending = _pendingJudgements.Find(p => p.Id == judgementId);
            if (pending == null) return false;

            var owner = GetPlayer(pending.Entry.Owner);
            if (!owner.Graveyard.Contains(pending.Entry))
            {
                _pendingJudgements.Remove(pending);
                return false;
            }

            if (verdict == JudgementVerdict.ReturnToHand)
            {
                if (!pending.CanReturnToHand) return false;

                var judge = GetPlayer(pending.Judge);
                if (!judge.Hand.TryAdd(pending.Entry.SourceCard, Rounds.RoundNumber))
                    judge.Deck.Discard(pending.Entry.SourceCard);
            }
            else if (pending.Entry.SourceCard != null)
            {
                owner.Deck.Discard(pending.Entry.SourceCard);
            }

            owner.Graveyard.Remove(pending.Entry);
            _pendingJudgements.Remove(pending);
            return true;
        }

        // --- Match flow ---------------------------------------------------------------------------

        /// <summary>Shuffles both decks, deals opening hands, and begins round 1.</summary>
        public void StartMatch()
        {
            PlayerA.Deck.Shuffle();
            PlayerB.Deck.Shuffle();

            // Opening hands are stamped as round 1 even though the clock has not started — see
            // HandCard.RoundEntered: it is that stamp which makes The High Priestess's own GDD
            // worked example come out right.
            PlayerA.DrawCards(HandRules.StartingHandSize, 1);
            PlayerB.DrawCards(HandRules.StartingHandSize, 1);

            BeginNextRound();
        }

        /// <summary>Advances the clock. Mana and draws follow automatically from the RoundStart phase handler below. Ignored once the match is decided — a finished match does not get another round.</summary>
        public void BeginNextRound()
        {
            if (IsOver) return;
            Rounds.BeginNextRound();
        }

        /// <summary>
        /// Plays a card from that player's hand, then fires the resulting unit's on-play ability and
        /// tells every other unit it has arrived. See CardPlayResult for why a play can be rejected —
        /// including a failed play condition or an illegal play-time choice, both checked before
        /// anything is spent.
        /// </summary>
        public CardPlayResult PlayCard(PlayerSide side, IPlayableCard card, HexCoord anchor, int attackColumn, int manaCommitted = 0, CardPlayOptions options = default)
        {
            if (IsOver) return CardPlayResult.Failed(CardPlayFailure.MatchOver, "The match is over.");

            var result = _cardPlayResolver.Play(GetPlayer(side), card, anchor, attackColumn, manaCommitted, options);
            if (!result.Success) return result;

            _sourceCards[result.Unit.Id] = card;

            if (AbilityRegistry.TryGet(result.Unit.DefinitionId, out var ability))
                ability.OnPlay(this, result.Unit, result.PlayedFrom, result.Options);

            NotifyPlaced(result.Unit);

            // An on-play kill (Death, the Hanged Man) resolves its own death on the spot; this catches
            // anything else an on-play effect might leave at 0 HP.
            ResolveDeaths();

            // Last, so the whole play has resolved before the turn changes hands. Only on success:
            // a rejected play costs the player nothing, and that includes their turn.
            Rounds.NoteActionTaken();
            return result;
        }

        /// <summary>
        /// Gives up the active player's turn without playing anything. Two passes in a row end the
        /// Action phase (see ActionTurns) — at which point Rounds.ActionComplete goes true.
        ///
        /// This deliberately does NOT advance the phase itself, even though it knows the phase is
        /// over. Auto-advancing would mean a pass synchronously runs combat, decay, every death
        /// trigger and the win check before returning, which turns one player tapping "Pass" into
        /// the most side-effect-heavy call in the codebase and leaves the caller refreshing a view
        /// of a board that already moved on. Whoever drives the match watches ActionComplete and
        /// calls AdvancePhase — see MatchView.
        /// </summary>
        public bool TryPass(PlayerSide side, out string reason)
        {
            if (IsOver)
            {
                reason = "The match is over.";
                return false;
            }

            if (Rounds.CurrentPhase != GamePhase.Action)
            {
                reason = $"There is no turn to pass during {Rounds.CurrentPhase}.";
                return false;
            }

            if (Rounds.ActionComplete)
            {
                reason = "Both players have passed; the Action phase is already over.";
                return false;
            }

            if (Rounds.ActiveSide != side)
            {
                reason = $"It is {Rounds.ActiveSide}'s turn.";
                return false;
            }

            Rounds.NotePassed();
            reason = null;
            return true;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.RoundStart: ProcessRoundStart(); break;
                case GamePhase.Combat: ProcessCombat(); break;
                case GamePhase.Decay: ProcessDecay(); break;
                case GamePhase.WinCheck: ProcessWinCheck(); break;
            }
        }

        /// <summary>The GDD's first loop box, literally: "Receive Pentacles (Mana), Draw Card(s)".</summary>
        private void ProcessRoundStart()
        {
            GrantMana(PlayerA);
            GrantMana(PlayerB);

            PlayerA.DrawCards(HandRules.DrawPerRound, Rounds.RoundNumber, 0, DrawBuffFor(PlayerSide.A));
            PlayerB.DrawCards(HandRules.DrawPerRound, Rounds.RoundNumber, 0, DrawBuffFor(PlayerSide.B));
        }

        /// <summary>
        /// The buff every card `side` draws right now would carry — the sum of what that side's live
        /// units grant (The Hermit, while isolated). Asked at the moment of drawing, not cached, so the
        /// Hermit's isolation is judged against the board as it stands then.
        /// </summary>
        private CardBuff DrawBuffFor(PlayerSide side)
        {
            var total = CardBuff.None;
            foreach (var unit in Units.AllUnits)
            {
                if (unit.Owner != side || unit.IsDead) continue;
                if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability))
                    total += ability.DrawBuff(this, unit);
            }
            return total;
        }

        /// <summary>
        /// Recomputed from the live board every round rather than accumulated, which is what makes
        /// "generates +1 Pentacle per turn while it remains on the board" (GDD 7.2) true for free —
        /// a unit that died stops contributing the moment it leaves. Any Pentacle debt owed from a
        /// Fool's death last round is settled here, at the one point where a round's mana is decided.
        /// </summary>
        private void GrantMana(PlayerState player)
        {
            int pentacle = SumPower(player.Side, u => u.PentaclePower);
            int overPentacle = SumPower(player.Side, u => u.OverPentaclePower);
            player.Mana.StartRound(Rounds.RoundNumber, pentacle, overPentacle, player.ConsumePendingManaBurn());
        }

        private int SumPower(PlayerSide side, Func<UnitInstance, int> selector)
        {
            int total = 0;
            foreach (var unit in Units.AllUnits)
                if (unit.Owner == side)
                    total += selector(unit);
            return total;
        }

        /// <summary>
        /// Three steps. First, anything that acts "before attacking" (The Chariot repositioning).
        /// Then ordinary lane combat. Then the cards that attack by their own rules (Justice, The Sun) —
        /// with the dead swept in between, so a bespoke attacker never spends its shot on a unit that
        /// lane combat already killed.
        ///
        /// Running the custom step after lane combat is a deliberate ordering the GDD doesn't make for
        /// us: Justice picking "the highest HP unit" after normal combat means it reads the board its
        /// opponent's attacks have already shaped, which is the more predictable of the two orders.
        /// </summary>
        private void ProcessCombat()
        {
            foreach (var unit in Units.AllUnits.ToList())
            {
                if (unit.IsDead) continue;
                if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability))
                    ability.OnBeforeCombat(this, unit);
            }

            _attackResolver.ResolveCombat();

            // Combat casualties are cleared here rather than lingering until Decay, so on-death
            // abilities (The Tower's explosion) fire at the moment the card says they do.
            ResolveDeaths();

            foreach (var unit in Units.AllUnits.ToList())
            {
                if (unit.IsDead) continue;
                if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability))
                    ability.OnCombat(this, unit);
            }

            ResolveDeaths();
        }

        /// <summary>
        /// Follows the loop diagram's own ordering (GDD section 2) exactly: decay, THEN remove
        /// units at 0 HP, THEN passive Heal/Buff triggers from whoever survived — a unit that
        /// decays to 0 this round is gone before it gets a chance to heal its Soul or buff a
        /// neighbor on the way out.
        /// </summary>
        private void ProcessDecay()
        {
            foreach (var unit in Units.AllUnits.ToList())
                unit.ApplyDecay();

            ResolveDeaths();

            foreach (var unit in Units.AllUnits.ToList())
            {
                if (unit.IsDead) continue;

                if (unit.HealPower > 0 || unit.OverHealPower > 0)
                {
                    var soul = GetPlayer(unit.Owner).Soul;
                    if (unit.HealPower > 0) soul.ApplyHeal(unit.HealPower);
                    if (unit.OverHealPower > 0) soul.ApplyOverHeal(unit.OverHealPower);
                }

                if (unit.SelfHpBuffAmount > 0)
                    unit.ApplyHeal(unit.SelfHpBuffAmount);

                if (unit.LeftNeighborHpBuffAmount > 0)
                {
                    var neighbor = GetLeftNeighbor(unit);
                    if (neighbor != null && neighbor.Owner == unit.Owner)
                        neighbor.ApplyHeal(unit.LeftNeighborHpBuffAmount);
                }

                // Card-specific per-turn effects (the Hierophant's frontline buff, The Star's board
                // heal, The Devil's doomsday clock) run in this same survivors-only pass, alongside
                // the universal Wand-style buffs they are variations of.
                if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability))
                    ability.OnDecayTick(this, unit);
            }

            ResolveDeaths();
        }

        /// <summary>
        /// Removes every unit at 0 HP and fires its death triggers, repeating while those triggers
        /// kill anything else (a Tower's explosion can take a neighbouring Tower with it).
        ///
        /// The order within one death matters: the unit leaves the board FIRST, then it's recorded in
        /// its owner's graveyard, then its own on-death ability runs, then every survivor's "any unit
        /// died" hook. Removing first is what frees the tiles Death spawns onto; recording before the
        /// hooks is what lets Judgement queue a ruling on exactly this graveyard entry.
        /// </summary>
        private void ResolveDeaths()
        {
            for (int pass = 0; pass < MaxDeathCascadePasses; pass++)
            {
                var dead = Units.AllUnits.Where(u => u.IsDead).ToList();
                if (dead.Count == 0) return;

                foreach (var unit in dead)
                {
                    // An ability that kills from inside a death trigger (KillUnit) resolves that death
                    // in a nested pass; skip anything already handled there, or its hooks fire twice.
                    if (Units.GetUnit(unit.Id) == null) continue;

                    var coords = Board.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
                    var occupied = coords == null ? new List<HexCoord>() : new List<HexCoord>(coords);

                    Units.RemoveUnit(unit.Id);

                    _sourceCards.TryGetValue(unit.Id, out var sourceCard);
                    _sourceCards.Remove(unit.Id);

                    var entry = new GraveyardEntry(unit.OriginStats, sourceCard, unit.Owner, Rounds.RoundNumber);
                    GetPlayer(unit.Owner).Graveyard.Add(entry);

                    var death = new UnitDeath(unit, occupied, _replacedUnitIds.Remove(unit.Id), entry);

                    if (AbilityRegistry.TryGet(unit.DefinitionId, out var ownAbility))
                        ownAbility.OnOwnDeath(this, death);

                    foreach (var survivor in Units.AllUnits.ToList())
                    {
                        if (survivor.IsDead) continue;
                        if (AbilityRegistry.TryGet(survivor.DefinitionId, out var watcher))
                            watcher.OnAnyUnitDied(this, survivor, death);
                    }

                    UnitDied?.Invoke(death);
                }
            }
        }

        /// <summary>Tells every other live unit's ability that `placed` just entered the board (The Lovers' gap).</summary>
        private void NotifyPlaced(UnitInstance placed)
        {
            if (placed == null || placed.IsDead) return;

            foreach (var unit in Units.AllUnits.ToList())
            {
                if (unit.Id == placed.Id || unit.IsDead) continue;
                if (AbilityRegistry.TryGet(unit.DefinitionId, out var ability))
                    ability.OnUnitPlaced(this, unit, placed);
            }
        }

        /// <summary>
        /// The unit on the tile immediately left of this unit's LEFTMOST tile in its anchor's row — the
        /// Wands trait's "the unit to its left" (GDD 9.4).
        ///
        /// It used to be "left of the anchor", which is identical for 1x1 units and for rectangles
        /// (whose anchor is their top-left tile), but wrong for the 7-tile cluster of the Queen and King
        /// of Wands: their anchor is the cluster's centre, so "left of the anchor" landed on one of their
        /// OWN tiles and the buff went nowhere. Stepping off the leftmost tile of the row fixes that
        /// without changing the answer for any other shape.
        /// </summary>
        private UnitInstance GetLeftNeighbor(UnitInstance unit)
        {
            var anchor = unit.AnchorCoord.ToOffset();
            int leftmost = anchor.Col;

            var footprint = Board.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
            if (footprint != null)
            {
                foreach (var coord in footprint)
                {
                    var offset = coord.ToOffset();
                    if (offset.Row == anchor.Row && offset.Col < leftmost) leftmost = offset.Col;
                }
            }

            var leftCoord = new OffsetCoord(leftmost - 1, anchor.Row).ToAxial();
            return Units.GetUnitAt(leftCoord);
        }

        /// <summary>
        /// Latches the result. MatchEnded used to be the ONLY record that a match had finished — a
        /// fire-and-forget event with nothing holding its answer — so anything that missed it, or
        /// asked afterwards, had no way to know the match was over. The match happily continued into
        /// another round past a win.
        /// </summary>
        private void ProcessWinCheck()
        {
            if (IsOver) return;

            bool aDefeated = PlayerA.Soul.IsDefeated;
            bool bDefeated = PlayerB.Soul.IsDefeated;
            if (!aDefeated && !bDefeated) return;

            IsOver = true;

            // Both Souls at 0 in the same round is a draw (no GDD ruling; settled earlier in the
            // project). Winner stays null in that case, which is why IsOver is a separate flag and
            // not just "Winner != null".
            Winner = aDefeated && bDefeated ? (PlayerSide?)null
                   : aDefeated ? PlayerSide.B
                   : PlayerSide.A;

            MatchEnded?.Invoke(Winner);
        }
    }
}
