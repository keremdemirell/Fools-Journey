using System.Collections.Generic;
using ArcanaWars.Core.Abilities;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.GameLoop;
using ArcanaWars.Core.Match;

namespace ArcanaWars.Core.Session
{
    /// <summary>
    /// Everything one screen may show about a match, copied out as plain data at one instant.
    ///
    /// <para><b>Why screens read this instead of MatchState.</b> MatchSession is the seam for what a
    /// player may DO; this is the same seam for what they may SEE. A screen that reads the live
    /// simulation has to remember, at every call site, which parts are private — and one forgotten
    /// call site shows a hand behind the curtain. Here the rule is applied once, at capture: a hand
    /// the session says this screen may not see simply isn't in the snapshot (it is null), so no
    /// screen can draw it by accident. It is also the shape an authoritative host would send a
    /// client if hidden information ever has to be protected from a debugger rather than just from
    /// the screen — screens built on this would not change.</para>
    ///
    /// <para>It holds no rules. Every value is read from the match or asked of the session or an
    /// ability's side-effect-free CanPlay; nothing is worked out here. Capture never touches the
    /// match RNG and never calls GetSpawnStats, so taking a snapshot can't change the game.</para>
    ///
    /// <para>Take a new one when <see cref="MatchSession.Revision"/> moves; see there for why that
    /// is enough.</para>
    /// </summary>
    public sealed class MatchSnapshot
    {
        public int Revision { get; private set; }

        public int Round { get; private set; }
        public GamePhase Phase { get; private set; }

        /// <summary>Whose turn it is in the Action phase. Meaningless outside it.</summary>
        public PlayerSide ActiveSide { get; private set; }

        /// <summary>Both players passed in a row; the Action phase is waiting for the clock to move on.</summary>
        public bool ActionComplete { get; private set; }

        public int ConsecutivePasses { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>Null on a draw, and while the match is still running.</summary>
        public PlayerSide? Winner { get; private set; }

        /// <summary>Whose eyes this screen is drawn for — see MatchSession.ViewerSide.</summary>
        public PlayerSide ViewerSide { get; private set; }

        /// <summary>The side a player at this screen may act for right now, or null if nobody here can.</summary>
        public PlayerSide? ControllingSide { get; private set; }

        public bool CanAct => ControllingSide.HasValue;

        /// <summary>The hot-seat curtain is up: the turn moved to the other local player and they haven't taken the controls yet.</summary>
        public bool HandoverPending { get; private set; }

        public bool PrivateHands { get; private set; }

        public int Columns { get; private set; }
        public int Rows { get; private set; }

        public PlayerSnapshot PlayerA { get; private set; }
        public PlayerSnapshot PlayerB { get; private set; }

        /// <summary>Every tile, ordered by row then column.</summary>
        public IReadOnlyList<TileSnapshot> Tiles { get; private set; }

        /// <summary>Every unit on the board, ordered by id so the order never depends on a dictionary.</summary>
        public IReadOnlyList<UnitSnapshot> Units { get; private set; }

        /// <summary>
        /// Judgement's pending rulings — all of them, since which unit died is public. Whether THIS
        /// screen may answer one is <see cref="JudgementSnapshot.CanRuleHere"/>.
        /// </summary>
        public IReadOnlyList<JudgementSnapshot> Judgements { get; private set; }

        private Dictionary<HexCoord, UnitSnapshot> _unitsByTile;

        public PlayerSnapshot Player(PlayerSide side) => side == PlayerSide.A ? PlayerA : PlayerB;
        public PlayerSnapshot Opponent(PlayerSide side) => side == PlayerSide.A ? PlayerB : PlayerA;

        /// <summary>The unit covering that tile, or null.</summary>
        public UnitSnapshot UnitAt(HexCoord coord) =>
            _unitsByTile.TryGetValue(coord, out var unit) ? unit : null;

        public UnitSnapshot Unit(int unitId)
        {
            foreach (var unit in Units)
                if (unit.Id == unitId) return unit;
            return null;
        }

        private MatchSnapshot() { }

        /// <summary>
        /// Copies out what this session's screen may see. The catalog only turns unit ids into
        /// display names; pass null and a unit's name falls back to its id.
        /// </summary>
        public static MatchSnapshot Capture(MatchSession session, ICardCatalog catalog = null)
        {
            var match = session.Match;
            var rounds = match.Rounds;

            var snapshot = new MatchSnapshot
            {
                Revision = session.Revision,
                Round = rounds.RoundNumber,
                Phase = rounds.CurrentPhase,
                ActiveSide = rounds.ActiveSide,
                ActionComplete = rounds.ActionComplete,
                ConsecutivePasses = rounds.ConsecutivePasses,
                IsOver = match.IsOver,
                Winner = match.Winner,
                ViewerSide = session.ViewerSide,
                ControllingSide = session.ControllingSide,
                HandoverPending = session.HandoverPending,
                PrivateHands = session.PrivateHands,
                Columns = match.Board.Columns,
                Rows = match.Board.Rows,
            };

            snapshot.PlayerA = CapturePlayer(session, PlayerSide.A);
            snapshot.PlayerB = CapturePlayer(session, PlayerSide.B);
            snapshot.Tiles = CaptureTiles(match);
            snapshot.Units = CaptureUnits(session, catalog, out snapshot._unitsByTile);
            snapshot.Judgements = CaptureJudgements(session);
            return snapshot;
        }

        private static PlayerSnapshot CapturePlayer(MatchSession session, PlayerSide side)
        {
            var match = session.Match;
            var player = match.GetPlayer(side);

            var snapshot = new PlayerSnapshot
            {
                Side = side,
                SoulHp = player.Soul.CurrentHp,
                SoulMaxHp = player.Soul.MaxHp,
                SoulImmune = match.IsSoulImmune(side),
                ManaRemaining = player.Mana.Remaining,
                ManaAvailable = player.Mana.Available,
                PendingManaBurn = player.PendingManaBurn,
                DeckCount = player.Deck.Count,
                HandCount = player.Hand.Count,
                HandMax = player.Hand.MaxSize,
                GraveyardCount = player.Graveyard.Count,
                SoulHpVisible = !HidesHpFrom(session, side),
            };

            // The one place a hand's contents leave the simulation. Card COUNT is public (you can see
            // how many cards your opponent is holding across a table); which cards they are is not.
            if (session.CanSeeHand(side))
            {
                var hand = new List<HandCardSnapshot>(player.Hand.Count);
                var cards = player.Hand.Cards;

                for (int i = 0; i < cards.Count; i++)
                    hand.Add(CaptureHandCard(match, player, cards[i], i));

                snapshot.Hand = hand;
            }

            return snapshot;
        }

        private static HandCardSnapshot CaptureHandCard(MatchState match, PlayerState player, HandCard held, int index)
        {
            var card = held.Card;
            int cost = held.EffectiveCost();

            // CanPlay is the same side-effect-free check CardPlayResolver runs, and exists precisely
            // so a UI can grey out unplayable cards every redraw — see UnitAbility.CanPlay.
            bool conditionMet = true;
            string conditionReason = null;
            PlayChoice choice = PlayChoice.None;
            bool choiceOptional = false;

            if (AbilityRegistry.TryGet(card.DefinitionId, out var ability))
            {
                conditionMet = ability.CanPlay(match, player.Side, out conditionReason);
                choice = ability.Choice;
                choiceOptional = ability.ChoiceIsOptional;
            }

            return new HandCardSnapshot
            {
                Index = index,
                DefinitionId = card.DefinitionId,
                DisplayName = card.DisplayName,
                Kind = card.Kind,
                Element = card.Element,
                HasVariableCost = card.HasVariableCost,
                Cost = cost,
                CostDiscount = held.CostDiscount,
                Buff = held.Buff,
                TurnsHeld = held.TurnsHeld(match.Rounds.RoundNumber),
                Choice = choice,
                ChoiceIsOptional = choiceOptional,
                Affordable = cost <= player.Mana.Remaining,
                ConditionMet = conditionMet,
                ConditionReason = conditionMet ? null : conditionReason,
            };
        }

        private static IReadOnlyList<TileSnapshot> CaptureTiles(MatchState match)
        {
            var tiles = new List<TileSnapshot>();

            foreach (var tile in match.Board.AllTiles)
            {
                var offset = tile.Coord.ToOffset();
                tiles.Add(new TileSnapshot
                {
                    Coord = tile.Coord,
                    Row = offset.Row,
                    Column = offset.Col,
                    Side = tile.Side,
                    IsWalled = tile.IsWalled,
                    IsGreened = tile.IsGreened,
                    OccupantId = tile.OccupantId,
                });
            }

            tiles.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
            return tiles;
        }

        /// <summary>
        /// The Moon (GDD 11.XVIII) hides its owner's unit HP and Soul HP from the opponent. Applied
        /// here rather than in the UI for the same reason hands are: a visibility rule enforced once,
        /// at capture, cannot be forgotten by one label somewhere.
        ///
        /// Tied to PrivateHands, like hands are: a shared screen where both players already see each
        /// other's cards has nothing to conceal, and concealing there would only make the game harder
        /// to read for two people who agreed to share a screen.
        /// </summary>
        private static bool HidesHpFrom(MatchSession session, PlayerSide owner) =>
            session.PrivateHands && owner != session.ViewerSide && MoonAbility.ConcealsHpFor(session.Match, owner);

        private static IReadOnlyList<UnitSnapshot> CaptureUnits(MatchSession session, ICardCatalog catalog, out Dictionary<HexCoord, UnitSnapshot> byTile)
        {
            var match = session.Match;
            var units = new List<UnitSnapshot>();
            byTile = new Dictionary<HexCoord, UnitSnapshot>();

            bool hideA = HidesHpFrom(session, PlayerSide.A);
            bool hideB = HidesHpFrom(session, PlayerSide.B);

            foreach (var unit in match.Units.AllUnits)
            {
                var card = catalog?.Find(unit.DefinitionId);

                var snapshot = new UnitSnapshot
                {
                    Id = unit.Id,
                    DefinitionId = unit.DefinitionId,
                    DisplayName = card != null ? card.DisplayName : unit.DefinitionId,
                    Owner = unit.Owner,
                    Anchor = unit.AnchorCoord,
                    Footprint = match.Board.GetFootprintCoords(unit.AnchorCoord, unit.Footprint),
                    AttackColumn = unit.AttackColumn,
                    CurrentHp = unit.CurrentHp,
                    StartingHp = unit.StartingHp,
                    HpVisible = !(unit.Owner == PlayerSide.A ? hideA : hideB),
                    Element = unit.Element,
                    RoundPlayed = unit.RoundPlayed,
                    AttackPower = unit.AttackPower,
                    OverAttackPower = unit.OverAttackPower,
                    HealPower = unit.HealPower,
                    OverHealPower = unit.OverHealPower,
                    PentaclePower = unit.PentaclePower,
                    OverPentaclePower = unit.OverPentaclePower,
                };

                units.Add(snapshot);
                foreach (var coord in snapshot.Footprint)
                    byTile[coord] = snapshot;
            }

            units.Sort((a, b) => a.Id.CompareTo(b.Id));
            return units;
        }

        private static IReadOnlyList<JudgementSnapshot> CaptureJudgements(MatchSession session)
        {
            var judgements = new List<JudgementSnapshot>();

            foreach (var pending in session.Match.PendingJudgements)
            {
                var entry = pending.Entry;
                judgements.Add(new JudgementSnapshot
                {
                    Id = pending.Id,
                    Judge = pending.Judge,
                    UnitOwner = entry.Owner,
                    DefinitionId = entry.DefinitionId,
                    DisplayName = entry.SourceCard != null ? entry.SourceCard.DisplayName : entry.DefinitionId,
                    CanReturnToHand = pending.CanReturnToHand,
                    CanRuleHere = session.CanJudge(pending.Judge),
                });
            }

            return judgements;
        }
    }

    /// <summary>One player's public numbers, plus their hand when — and only when — this screen may see it.</summary>
    public sealed class PlayerSnapshot
    {
        public PlayerSide Side { get; internal set; }
        public int SoulHp { get; internal set; }
        public int SoulMaxHp { get; internal set; }

        /// <summary>The Devil is protecting this Soul.</summary>
        public bool SoulImmune { get; internal set; }

        public int ManaRemaining { get; internal set; }
        public int ManaAvailable { get; internal set; }

        /// <summary>The Fool's burn, charged against next round's Pentacles.</summary>
        public int PendingManaBurn { get; internal set; }

        public int DeckCount { get; internal set; }
        public int HandCount { get; internal set; }
        public int HandMax { get; internal set; }
        public int GraveyardCount { get; internal set; }

        /// <summary>False while The Moon hides this player's Soul HP from the viewer (GDD 11.XVIII).</summary>
        public bool SoulHpVisible { get; internal set; }

        /// <summary>The cards in hand, in hand order. NULL when this screen may not see them — never an empty list standing in for "hidden".</summary>
        public IReadOnlyList<HandCardSnapshot> Hand { get; internal set; }

        public bool HandVisible => Hand != null;
    }

    /// <summary>One card in a visible hand, with everything a hand UI needs to draw it and grey it out.</summary>
    public sealed class HandCardSnapshot
    {
        /// <summary>Position in the hand at capture. For display and selection only — a play names its card by id.</summary>
        public int Index { get; internal set; }

        public string DefinitionId { get; internal set; }
        public string DisplayName { get; internal set; }
        public CardKind Kind { get; internal set; }
        public Element? Element { get; internal set; }

        /// <summary>An Ace: "1 or all your current mana" (GDD 10.1). <see cref="Cost"/> is then the minimum.</summary>
        public bool HasVariableCost { get; internal set; }

        /// <summary>What it costs now, after any Wheel of Fortune discount.</summary>
        public int Cost { get; internal set; }

        public int CostDiscount { get; internal set; }

        /// <summary>The Hermit's draw buff, if it had one.</summary>
        public CardBuff Buff { get; internal set; }

        /// <summary>Rounds held — what The High Priestess's heal is counting.</summary>
        public int TurnsHeld { get; internal set; }

        /// <summary>What must be decided when it is played: an element, an allied target, or nothing.</summary>
        public PlayChoice Choice { get; internal set; }

        public bool ChoiceIsOptional { get; internal set; }

        public bool Affordable { get; internal set; }

        /// <summary>The card's own play condition (GDD section 11). False for, e.g., an Empress before two Queens.</summary>
        public bool ConditionMet { get; internal set; }

        /// <summary>Why the condition fails, worded by the ability itself. Null when it's met.</summary>
        public string ConditionReason { get; internal set; }

        /// <summary>
        /// Affordable and its condition holds. NOT a promise the play will succeed — where it goes
        /// and what it targets are still checked when it's played. A hint for greying out, nothing more.
        /// </summary>
        public bool LooksPlayable => Affordable && ConditionMet;
    }

    public sealed class TileSnapshot
    {
        public HexCoord Coord { get; internal set; }
        public int Row { get; internal set; }
        public int Column { get; internal set; }

        /// <summary>Which player may place here; null for the neutral middle row.</summary>
        public PlayerSide? Side { get; internal set; }

        /// <summary>Under an Emperor's wall — nobody places here, nothing moves on or off.</summary>
        public bool IsWalled { get; internal set; }

        /// <summary>Greened by The Empress.</summary>
        public bool IsGreened { get; internal set; }

        public int? OccupantId { get; internal set; }
    }

    public sealed class UnitSnapshot
    {
        public int Id { get; internal set; }
        public string DefinitionId { get; internal set; }
        public string DisplayName { get; internal set; }
        public PlayerSide Owner { get; internal set; }
        public HexCoord Anchor { get; internal set; }
        public IReadOnlyList<HexCoord> Footprint { get; internal set; }
        public int AttackColumn { get; internal set; }
        public int CurrentHp { get; internal set; }
        public int StartingHp { get; internal set; }

        /// <summary>False while The Moon hides this unit's HP from the viewer (GDD 11.XVIII). The numbers above are still the real ones — don't draw them.</summary>
        public bool HpVisible { get; internal set; }

        public Element? Element { get; internal set; }
        public int RoundPlayed { get; internal set; }

        public int AttackPower { get; internal set; }
        public int OverAttackPower { get; internal set; }
        public int HealPower { get; internal set; }
        public int OverHealPower { get; internal set; }
        public int PentaclePower { get; internal set; }
        public int OverPentaclePower { get; internal set; }
    }

    public sealed class JudgementSnapshot
    {
        public int Id { get; internal set; }

        /// <summary>Who decides — Judgement's controller.</summary>
        public PlayerSide Judge { get; internal set; }

        /// <summary>Whose unit died. Can be the judge's opponent: judging a foe's unit back to hand steals it.</summary>
        public PlayerSide UnitOwner { get; internal set; }

        public string DefinitionId { get; internal set; }
        public string DisplayName { get; internal set; }

        /// <summary>False for tokens that were never a card (the Hanged Man's Tree) — they can only be discarded.</summary>
        public bool CanReturnToHand { get; internal set; }

        /// <summary>A player at this screen may answer it right now — see MatchSession.CanJudge.</summary>
        public bool CanRuleHere { get; internal set; }
    }
}
