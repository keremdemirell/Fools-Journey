using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ArcanaWars.Core;
using ArcanaWars.Core.Abilities;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.GameLoop;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Net;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation.UI
{
    /// <summary>
    /// The match HUD and its overlays, on top of the BoardView.
    ///
    /// <para><b>How it reads and how it acts — the two rules this class exists to keep.</b> Every
    /// player action is a <see cref="MatchCommand"/> handed to <see cref="IMatchDriver.Submit"/>;
    /// everything drawn comes from a <see cref="MatchSnapshot"/>, which already has private
    /// information removed. There is no MatchState anywhere in this class. That is what lets the
    /// same screen run a hot-seat match and a networked one without knowing which it is.</para>
    ///
    /// <para><b>When it redraws.</b> Only when <see cref="MatchSession.Revision"/> moves, i.e. when
    /// some command was actually applied — locally, or arriving from the other machine in Pump. Two
    /// things change without a revision and so are handled every frame: the network connection, and
    /// the pointer. (This is the lesson from the old lobby's status text: always know what re-renders
    /// a label when its source changes, not just what sets it first.)</para>
    /// </summary>
    public sealed class MatchScreen : UiScreen
    {
        private readonly BoardView _board;
        private readonly ICardCatalog _catalog;

        private IMatchDriver _driver;
        private MatchSnapshot _snapshot;
        private int _drawnRevision = -1;

        // --- Layout ---
        private readonly PlayerPanel _panelA;
        private readonly PlayerPanel _panelB;
        private readonly Label _roundLabel;
        private readonly Label _turnLabel;
        private readonly Button _phaseButton;
        private readonly Label _clockWait;
        private readonly Label _statusLine;
        private readonly Label _connectionLine;
        private readonly VisualElement _connectionPanel;

        private readonly VisualElement _handRow;
        private readonly Label _handHidden;
        private readonly List<HandCardElement> _handCards = new List<HandCardElement>();

        private readonly PlayChoiceBar _choices;

        private readonly VisualElement _judgementPanel;
        private readonly VisualElement _judgementRows;
        private readonly Label _judgementTitle;

        private readonly VisualElement _endLayer;
        private readonly Label _endTitle;
        private readonly Label _endDetail;
        private readonly Button _endAgain;

        /// <summary>Raised when the player is finished with this match. Today it starts another; once there is a lobby, it goes back to it.</summary>
        public event Action ExitRequested;

        private readonly VisualElement _unitLabelLayer;
        private readonly List<Label> _unitLabels = new List<Label>();

        private readonly VisualElement _curtain;
        private readonly Label _curtainTitle;
        private readonly Label _curtainSubtitle;
        private readonly Button _curtainReady;

        private readonly VisualElement _waitingLayer;
        private readonly Label _waitingLabel;

        // --- Interaction state ---

        /// <summary>The selected card, by id, or null. Kept as an id rather than an index because the hand is rebuilt from every snapshot.</summary>
        private string _selectedCardId;
        private int _selectedIndex = -1;

        private HexCoord? _hoveredTile;
        private int _labelledFraming = -1;

        /// <summary>What the phase button does right now. Worked out when the snapshot is drawn, acted on when clicked.</summary>
        private enum PhaseAction { None, Pass, Advance, NextRound }
        private PhaseAction _phaseAction;

        public MatchScreen(VisualTreeAsset layout, VisualElement host, BoardView board, ICardCatalog catalog)
            : base(layout, host)
        {
            _board = board;
            _catalog = catalog;

            _panelA = new PlayerPanel(Require<VisualElement>("player-a"));
            _panelB = new PlayerPanel(Require<VisualElement>("player-b"));
            _roundLabel = Require<Label>("round-label");
            _turnLabel = Require<Label>("turn-label");
            _phaseButton = Require<Button>("phase-button");
            _clockWait = Require<Label>("clock-wait");
            _statusLine = Require<Label>("status-line");
            _connectionLine = Require<Label>("connection-line");
            _connectionPanel = Require<VisualElement>("connection-panel");
            Require<Button>("leave-button").clicked += () => ExitRequested?.Invoke();

            _handRow = Require<VisualElement>("hand-row");
            _handHidden = Require<Label>("hand-hidden");
            _unitLabelLayer = Require<VisualElement>("unit-labels");

            _curtain = Require<VisualElement>("handover-curtain");
            _curtainTitle = Require<Label>("curtain-title");
            _curtainSubtitle = Require<Label>("curtain-subtitle");
            _curtainReady = Require<Button>("curtain-ready");

            _waitingLayer = Require<VisualElement>("waiting-layer");
            _waitingLabel = Require<Label>("waiting-label");

            _judgementPanel = Require<VisualElement>("judgement-panel");
            _judgementRows = Require<VisualElement>("judgement-rows");
            _judgementTitle = Require<Label>("judgement-title");

            _endLayer = Require<VisualElement>("end-layer");
            _endTitle = Require<Label>("end-title");
            _endDetail = Require<Label>("end-detail");
            _endAgain = Require<Button>("end-again");

            _choices = new PlayChoiceBar(Require<VisualElement>("choice-bar"), Require<Label>("choice-prompt"), Require<VisualElement>("choice-buttons"));
            _choices.Changed += OnChoiceChanged;

            _phaseButton.clicked += OnPhaseButton;
            _curtainReady.clicked += OnCurtainReady;
            _endAgain.clicked += () => ExitRequested?.Invoke();
        }

        /// <summary>Starts showing a match. The driver may still be connecting; the screen waits for it.</summary>
        public void Attach(IMatchDriver driver)
        {
            _driver = driver;
            _snapshot = null;
            _drawnRevision = -1;
            _statusLine.text = "";
            ClearSelection();
        }

        /// <summary>Stops showing the match and clears the board. Does not close the driver — whoever made it does that.</summary>
        public void Detach()
        {
            _driver = null;
            _snapshot = null;
            _board.Clear();
        }

        public override void Tick()
        {
            if (_driver == null) return;

            if (!_driver.IsReady)
            {
                SetVisible(_waitingLayer, true);
                _waitingLabel.text = _driver.StatusDetail ?? "Waiting for the match to start...";
                _board.SetHover(null);
                return;
            }

            SetVisible(_waitingLayer, false);

            var session = _driver.Session;
            if (_snapshot == null || session.Revision != _drawnRevision)
            {
                _snapshot = MatchSnapshot.Capture(session, _catalog);
                _drawnRevision = _snapshot.Revision;

                if (!_board.IsBuilt) _board.Build(_snapshot);
                else _board.Refresh(_snapshot);

                Redraw();
            }

            RedrawConnectionLine();

            // The camera re-frames itself when the window changes size, so the labels pinned over
            // units have to be put back where the units now are.
            if (_board.FramingVersion != _labelledFraming) PositionUnitLabels();

            UpdatePointer();
        }

        // --- Drawing ------------------------------------------------------------------------------

        private void Redraw()
        {
            var s = _snapshot;

            _panelA.Draw(s.PlayerA, s);
            _panelB.Draw(s.PlayerB, s);

            _roundLabel.text = $"Round {s.Round} · {PhaseName(s.Phase)}";
            _turnLabel.text = TurnLine(s);
            _turnLabel.EnableInClassList("phase-panel__turn--yours", s.CanAct);

            RedrawPhaseControls(s);
            RedrawHand(s);
            RedrawChoices(s);
            RedrawJudgement(s);
            RedrawUnitLabels(s);
            RedrawCurtain(s);
            RedrawEndScreen(s);
        }

        /// <summary>The selected card as the latest snapshot describes it, or null if nothing is selected.</summary>
        private HandCardSnapshot SelectedCard(MatchSnapshot s)
        {
            var hand = s?.Player(s.ViewerSide).Hand;
            if (hand == null || _selectedCardId == null) return null;
            if (_selectedIndex < 0 || _selectedIndex >= hand.Count) return null;
            return hand[_selectedIndex].DefinitionId == _selectedCardId ? hand[_selectedIndex] : null;
        }

        private void RedrawChoices(MatchSnapshot s)
        {
            var card = SelectedCard(s);
            var target = _choices.TargetUnitId.HasValue ? s.Unit(_choices.TargetUnitId.Value) : null;

            _choices.Draw(card, s.Player(s.ViewerSide), target != null ? target.DisplayName : "that unit");
            _board.SetTargetHighlight(target?.Footprint);
        }

        /// <summary>
        /// Judgement's pending rulings (GDD 11.XX): one row per dead unit, Return to your hand or
        /// Discard for good. Deliberately not gated to the judge's own turn — a ruling costs no turn,
        /// and Core sets no deadline precisely so an unanswered one can never deadlock the match.
        ///
        /// Rows are rebuilt rather than pooled: rulings are rare, and the panel is usually empty.
        /// </summary>
        private void RedrawJudgement(MatchSnapshot s)
        {
            _judgementRows.Clear();
            int shown = 0;

            foreach (var ruling in s.Judgements)
            {
                if (!ruling.CanRuleHere) continue;
                shown++;

                var row = new VisualElement();
                row.AddToClassList("judgement-row");

                var label = new Label($"{ruling.DisplayName}  ({ruling.UnitOwner}'s)");
                label.AddToClassList("judgement-row__name");
                row.Add(label);

                // A token that was never a card (the Hanged Man's Tree) has nothing to return to a
                // hand, so that verdict is offered but disabled rather than hidden.
                var id = ruling.Id;
                var judge = ruling.Judge;

                var ret = new Button(() => Rule(judge, id, JudgementVerdict.ReturnToHand)) { text = "Return to hand" };
                ret.AddToClassList("button");
                ret.AddToClassList("button--small");
                ret.SetEnabled(ruling.CanReturnToHand);
                row.Add(ret);

                var discard = new Button(() => Rule(judge, id, JudgementVerdict.Discard)) { text = "Discard" };
                discard.AddToClassList("button");
                discard.AddToClassList("button--small");
                row.Add(discard);

                _judgementRows.Add(row);
            }

            // The count is in the title because the queue has no deadline: unanswered rulings simply
            // wait, and a player should be able to see how many are waiting without scrolling.
            _judgementTitle.text = shown > 1 ? $"Judgement ({shown})" : "Judgement";
            SetVisible(_judgementPanel, shown > 0);
        }

        /// <summary>
        /// Answers one ruling. Deliberately does NOT clear the selected card: a ruling costs no turn,
        /// so a player mid-way through choosing where to place something should not lose that because
        /// they answered a prompt that appeared beside it.
        /// </summary>
        private void Rule(PlayerSide judge, int judgementId, JudgementVerdict verdict)
        {
            string text = verdict == JudgementVerdict.ReturnToHand ? "Returned to your hand." : "Discarded for good.";
            Submit(MatchCommand.ResolveJudgement(judge, judgementId, verdict), text, clearSelection: false);
        }

        /// <summary>
        /// The end of the match. A full-screen layer, like the curtain: there is nothing left to do on
        /// the board, and the result is the only thing worth looking at.
        /// </summary>
        private void RedrawEndScreen(MatchSnapshot s)
        {
            SetVisible(_endLayer, s.IsOver);
            if (!s.IsOver) return;

            bool viewerWon = s.Winner == s.ViewerSide;

            _endTitle.text = !s.Winner.HasValue ? "A draw" : viewerWon ? "You win" : $"Player {s.Winner.Value} wins";
            _endTitle.EnableInClassList("end-layer__title--won", s.Winner.HasValue && viewerWon);
            _endTitle.EnableInClassList("end-layer__title--lost", s.Winner.HasValue && !viewerWon);

            // Both Souls, because "25 to 3" says more about how the match went than the winner's name.
            _endDetail.text = $"Round {s.Round}   ·   Player A {Mathf.Max(0, s.PlayerA.SoulHp)} Soul   ·   Player B {Mathf.Max(0, s.PlayerB.SoulHp)} Soul";
        }

        /// <summary>The one line that answers "can I do something right now, and if not, why not".</summary>
        private static string TurnLine(MatchSnapshot s)
        {
            if (s.IsOver) return s.Winner.HasValue ? $"Player {s.Winner.Value} wins" : "Draw";
            if (s.Phase != GamePhase.Action) return "No cards are played in this phase";
            if (s.ActionComplete) return "Both players passed";
            if (s.CanAct) return s.ConsecutivePasses > 0 ? "Your turn — opponent passed" : "Your turn";
            return $"Waiting for Player {s.ActiveSide}";
        }

        /// <summary>
        /// One button, whose job depends on the phase. During the Action phase the only way forward is
        /// to pass — skipping straight to Combat would rob the opponent of turns they are still owed.
        /// Outside it, whoever owns the clock steps the phase on; on a network client there is nothing
        /// to press, since exactly one machine may advance (see IMatchDriver.OwnsClock).
        /// </summary>
        private void RedrawPhaseControls(MatchSnapshot s)
        {
            _phaseAction = PhaseAction.None;
            string clockText = null;

            if (s.IsOver)
            {
                clockText = "The match is over.";
            }
            else if (s.Phase == GamePhase.Action && !s.ActionComplete)
            {
                _phaseAction = PhaseAction.Pass;
            }
            else if (!_driver.OwnsClock)
            {
                clockText = "Waiting for the host to continue...";
            }
            else if (s.Phase == GamePhase.WinCheck)
            {
                _phaseAction = PhaseAction.NextRound;
            }
            else
            {
                _phaseAction = PhaseAction.Advance;
            }

            SetVisible(_phaseButton, _phaseAction != PhaseAction.None);
            SetVisible(_clockWait, clockText != null);
            _clockWait.text = clockText ?? "";

            switch (_phaseAction)
            {
                case PhaseAction.Pass:
                    _phaseButton.text = s.ConsecutivePasses > 0 ? "Pass (ends phase)" : "Pass";
                    _phaseButton.SetEnabled(s.CanAct);
                    break;
                case PhaseAction.NextRound:
                    _phaseButton.text = "Next round";
                    _phaseButton.SetEnabled(true);
                    break;
                case PhaseAction.Advance:
                    _phaseButton.text = s.Phase == GamePhase.Action ? "Continue to Combat" : $"Continue to {PhaseName(NextPhase(s.Phase))}";
                    _phaseButton.SetEnabled(true);
                    break;
            }
        }

        /// <summary>
        /// The viewer's hand. Card elements are reused between redraws rather than rebuilt, so the
        /// row doesn't flicker and a card keeps its place as the hand shrinks and grows.
        ///
        /// Only ever the VIEWER's hand: whether that is visible at all was already decided at capture
        /// (MatchSession.CanSeeHand), and a null hand here means "not for your eyes", not "empty".
        /// </summary>
        private void RedrawHand(MatchSnapshot s)
        {
            var hand = s.Player(s.ViewerSide).Hand;

            SetVisible(_handHidden, hand == null);
            SetVisible(_handRow, hand != null);

            if (hand == null)
            {
                _handHidden.text = s.HandoverPending ? "" : $"Player {s.ViewerSide}'s hand is hidden.";
                ClearSelection();
                return;
            }

            // A selection only survives if that exact card is still in hand — it may have been
            // played, discarded by a Wheel of Fortune, or the turn may have changed hands.
            if (_selectedCardId != null &&
                (_selectedIndex < 0 || _selectedIndex >= hand.Count || hand[_selectedIndex].DefinitionId != _selectedCardId))
                ClearSelection();

            while (_handCards.Count < hand.Count)
            {
                var element = new HandCardElement();
                element.Clicked += OnHandCardClicked;
                _handCards.Add(element);
                _handRow.Add(element);
            }

            for (int i = 0; i < _handCards.Count; i++)
            {
                bool used = i < hand.Count;
                SetVisible(_handCards[i], used);
                if (used) _handCards[i].Bind(hand[i], hand[i].Index == _selectedIndex);
            }
        }

        /// <summary>
        /// HP over every unit. These are the numbers The Moon hides (GDD 11.XVIII): a concealed unit
        /// shows "?" because the snapshot said so — the real value is never sent to the label.
        /// </summary>
        private void RedrawUnitLabels(MatchSnapshot s)
        {
            while (_unitLabels.Count < s.Units.Count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("unit-hp");
                _unitLabels.Add(label);
                _unitLabelLayer.Add(label);
            }

            for (int i = 0; i < _unitLabels.Count; i++)
            {
                bool used = i < s.Units.Count;
                SetVisible(_unitLabels[i], used);
                if (!used) continue;

                var unit = s.Units[i];
                var label = _unitLabels[i];

                label.text = unit.HpVisible ? unit.CurrentHp.ToString() : "?";
                label.EnableInClassList("unit-hp--a", unit.Owner == PlayerSide.A);
                label.EnableInClassList("unit-hp--b", unit.Owner == PlayerSide.B);
            }

            PositionUnitLabels();
        }

        /// <summary>
        /// Pins each HP label over its unit. Screen pixels (origin bottom-left) have to be converted
        /// into this panel's own coordinates, which start top-left and are scaled by the PanelSettings
        /// reference resolution — RuntimePanelUtils does both.
        /// </summary>
        private void PositionUnitLabels()
        {
            if (_snapshot == null || Root.panel == null) return;

            for (int i = 0; i < _snapshot.Units.Count && i < _unitLabels.Count; i++)
            {
                var screen = _board.FootprintCentreScreenPoint(_snapshot.Units[i].Footprint);
                var panelPoint = RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(screen.x, Screen.height - screen.y));

                _unitLabels[i].style.left = panelPoint.x;
                _unitLabels[i].style.top = panelPoint.y;
            }

            _labelledFraming = _board.FramingVersion;
        }

        /// <summary>
        /// The hot-seat handover: a full-screen cover so the outgoing player's hand is off the screen
        /// before the incoming one looks. The snapshot already has no hand in it while this is up;
        /// the curtain is what tells the players why, and gives the incoming one a button to press.
        /// </summary>
        private void RedrawCurtain(MatchSnapshot s)
        {
            SetVisible(_curtain, s.HandoverPending);
            if (!s.HandoverPending) return;

            _curtainTitle.text = $"Pass the device to Player {s.ActiveSide}";
            _curtainSubtitle.text = _statusLine.text ?? "";
            _curtainReady.text = $"I'm Player {s.ActiveSide} — ready";
        }

        /// <summary>
        /// For a network game: silent while healthy, loud when not. Re-read every frame because a
        /// dropped connection or a desync changes nothing in the match — there is no Revision to
        /// notice it by.
        /// </summary>
        private void RedrawConnectionLine()
        {
            string text = null;

            if (_driver is LockstepMatchDriver lockstep)
            {
                switch (lockstep.Status)
                {
                    case LockstepStatus.Running: break;
                    case LockstepStatus.Desynced: text = $"Desynced — the two games no longer match and cannot continue.\n{lockstep.DesyncReport}"; break;
                    case LockstepStatus.Ended: text = lockstep.StatusDetail ?? "The other player left."; break;
                    default: text = $"Connection {lockstep.Status}. {lockstep.StatusDetail}"; break;
                }
            }

            SetVisible(_connectionPanel, text != null);
            if (text != null && _connectionLine.text != text) _connectionLine.text = text;
        }

        // --- Pointer ------------------------------------------------------------------------------

        /// <summary>
        /// Hover, placement preview and the click that plays a card. Runs every frame because the
        /// pointer moves without anything in the match changing.
        /// </summary>
        private void UpdatePointer()
        {
            if (_snapshot.HandoverPending || !IsVisible)
            {
                _board.SetHover(null);
                _board.ClearPlacementPreview();
                _hoveredTile = null;
                return;
            }

            var mouse = (Vector2)Input.mousePosition;
            var tile = IsPointerOverUi(mouse) ? null : _board.TileAtScreenPoint(mouse);

            if (!System.Nullable.Equals(tile, _hoveredTile))
            {
                _hoveredTile = tile;
                _board.SetHover(tile);
                RefreshPlacementPreview();
            }

            if (tile.HasValue && Input.GetMouseButtonDown(0)) TryPlaySelectedCard(tile.Value);
        }

        /// <summary>
        /// Tints the tiles the selected card would cover under the pointer. Core decides both the
        /// shape and whether it fits (MatchSession.PreviewPlacement); this only asks.
        /// </summary>
        private void RefreshPlacementPreview()
        {
            if (_selectedCardId == null || !_hoveredTile.HasValue || !_snapshot.ControllingSide.HasValue)
            {
                _board.ClearPlacementPreview();
                return;
            }

            var selected = SelectedCard(_snapshot);
            var card = _catalog.Find(_selectedCardId);

            // While the next click picks a target rather than a tile, a footprint preview would be
            // showing the answer to a question the player isn't being asked yet.
            if (card == null || _choices.AwaitingTargetClick(selected))
            {
                _board.ClearPlacementPreview();
                return;
            }

            var anchor = _hoveredTile.Value;
            int mana = _choices.ManaCommitted(selected, _snapshot.Player(_snapshot.ViewerSide));
            var preview = _driver.Session.PreviewPlacement(_snapshot.ControllingSide.Value, card, anchor, anchor.ToOffset().Col, mana);
            _board.SetPlacementPreview(preview.Tiles, preview.Fits);
        }

        /// <summary>Re-reads the choices after a button in the choice bar was pressed.</summary>
        private void OnChoiceChanged()
        {
            if (_snapshot == null) return;
            RedrawChoices(_snapshot);
            RefreshPlacementPreview();
        }

        private void OnHandCardClicked(HandCardElement element)
        {
            if (_selectedIndex == element.Index)
            {
                ClearSelection();
                _statusLine.text = "";
            }
            else
            {
                _selectedCardId = element.DefinitionId;
                _selectedIndex = element.Index;

                // Answers given for the previous card mean nothing for this one.
                _choices.Clear();
                _board.SetTargetHighlight(null);

                // A card whose condition isn't met can still be selected — the reason why is more
                // useful in hand than a card that simply refuses to respond to clicks.
                _statusLine.text = element.BlockedReason ?? "Click a tile on your side to place it.";
            }

            if (_snapshot != null)
            {
                RedrawHand(_snapshot);
                RedrawChoices(_snapshot);
            }

            RefreshPlacementPreview();
        }

        private void ClearSelection()
        {
            _selectedCardId = null;
            _selectedIndex = -1;
            _choices.Clear();
            _board.ClearPlacementPreview();
            _board.SetTargetHighlight(null);
        }

        /// <summary>
        /// Plays the selected card at the clicked tile. The attack column is the clicked tile's own
        /// column: for a 1x1 it is the only option, and for anything wider the anchor's column is
        /// always part of the footprint, so it is always a legal choice.
        ///
        /// Cards that want a play-time choice (The Magician's element, Death's target) are submitted
        /// as they are and refused by Core with its own wording. The pickers that answer them come in
        /// the next batch.
        /// </summary>
        private void TryPlaySelectedCard(HexCoord tile)
        {
            if (_selectedCardId == null)
            {
                _statusLine.text = _snapshot.CanAct ? "Select a card from your hand first." : "";
                return;
            }

            if (!_snapshot.ControllingSide.HasValue)
            {
                _statusLine.text = _snapshot.Phase == GamePhase.Action
                    ? $"It is Player {_snapshot.ActiveSide}'s turn."
                    : $"Cards are only played during the Action phase (currently {PhaseName(_snapshot.Phase)}).";
                return;
            }

            var side = _snapshot.ControllingSide.Value;
            var selected = SelectedCard(_snapshot);
            var name = _catalog.Find(_selectedCardId)?.DisplayName ?? _selectedCardId;

            // First click of a targeting card picks the unit it acts on; the second click places the
            // card itself. The target must be one of your own — The Hanged Man sacrifices a friendly
            // unit and Death executes one (user decision, 2026-09-14).
            if (_choices.AwaitingTargetClick(selected))
            {
                var clicked = _snapshot.UnitAt(tile);
                if (clicked == null || clicked.Owner != side)
                {
                    _statusLine.text = selected.ChoiceIsOptional
                        ? $"{name}: click one of YOUR units to target it, or play it without a target."
                        : $"{name}: click one of YOUR units to target it.";
                    return;
                }

                _choices.SetTarget(clicked.Id);
                _statusLine.text = $"{name} targets {clicked.DisplayName}. Now click where {name} itself goes.";
                return;
            }

            var options = new CardPlayOptions(_choices.ChosenElement, _choices.TargetUnitId);
            int mana = _choices.ManaCommitted(selected, _snapshot.Player(side));

            var result = _driver.Submit(MatchCommand.PlayCard(side, _selectedCardId, tile, tile.ToOffset().Col, mana, options));

            if (result.Applied)
            {
                _statusLine.text = $"Played {name}.";
                ClearSelection();
            }
            else
            {
                // Condition and choice failures carry their own explanation ("The Empress needs 2
                // Queens played this match"), which is far more useful than the category name.
                _statusLine.text = $"Couldn't play {name}: {result.Reason}";
            }
        }

        // --- Acting -------------------------------------------------------------------------------

        private void OnPhaseButton()
        {
            if (_driver == null || _snapshot == null) return;

            switch (_phaseAction)
            {
                case PhaseAction.Pass:
                    if (!_snapshot.ControllingSide.HasValue) return;
                    var side = _snapshot.ControllingSide.Value;
                    Submit(MatchCommand.Pass(side), $"Player {side} passed.");
                    break;
                case PhaseAction.Advance:
                    Submit(MatchCommand.AdvancePhase(), null);
                    break;
                case PhaseAction.NextRound:
                    Submit(MatchCommand.BeginNextRound(), null);
                    break;
            }
        }

        private void OnCurtainReady()
        {
            // Acknowledging a handover is not a game command — nothing about the match changes, and
            // only this device's screen has a curtain — so it goes to the session, not through Submit.
            _driver?.Session?.AcknowledgeHandover();
            _statusLine.text = "";
            ClearSelection();
        }

        /// <summary>Sends one command and shows the outcome. The screen redraws on its own next Tick if it was applied.</summary>
        private void Submit(MatchCommand command, string successText, bool clearSelection = true)
        {
            var result = _driver.Submit(command);
            _statusLine.text = result.Applied ? successText ?? "" : result.Reason;
            if (result.Applied && clearSelection) ClearSelection();
        }

        // --- Small helpers ------------------------------------------------------------------------

        private static GamePhase NextPhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.RoundStart: return GamePhase.Action;
                case GamePhase.Action: return GamePhase.Combat;
                case GamePhase.Combat: return GamePhase.Decay;
                case GamePhase.Decay: return GamePhase.WinCheck;
                default: return GamePhase.RoundStart;
            }
        }

        private static string PhaseName(GamePhase phase) => phase == GamePhase.WinCheck ? "Win Check" : phase.ToString();

        /// <summary>One player's public numbers in the top bar. Found by child name inside its own panel, so the A and B panels can share names.</summary>
        private sealed class PlayerPanel
        {
            private readonly VisualElement _root;
            private readonly Label _name;
            private readonly Label _soul;
            private readonly Label _mana;
            private readonly Label _cards;

            public PlayerPanel(VisualElement root)
            {
                _root = root;
                _name = Find("name");
                _soul = Find("soul");
                _mana = Find("mana");
                _cards = Find("cards");
            }

            private Label Find(string name) =>
                _root.Q<Label>(name) ?? throw new System.InvalidOperationException($"MatchScreen: player panel \"{_root.name}\" has no Label named \"{name}\".");

            public void Draw(PlayerSnapshot p, MatchSnapshot s)
            {
                bool viewer = p.Side == s.ViewerSide;
                bool active = !s.IsOver && s.Phase == GamePhase.Action && !s.ActionComplete && s.ActiveSide == p.Side;

                _name.text = viewer ? $"Player {p.Side} (you)" : $"Player {p.Side}";
                _root.EnableInClassList("player-panel--active", active);

                // Hidden by The Moon, exactly like the unit HP labels.
                string soul = p.SoulHpVisible ? $"{p.SoulHp}/{p.SoulMaxHp}" : $"?/{p.SoulMaxHp}";
                _soul.text = $"Soul {soul}{(p.SoulImmune ? "  · immune" : "")}";

                _mana.text = $"Pentacles {p.ManaRemaining}/{p.ManaAvailable}{(p.PendingManaBurn > 0 ? $"  · burn {p.PendingManaBurn} next round" : "")}";
                _cards.text = $"Hand {p.HandCount}/{p.HandMax}   Deck {p.DeckCount}   Fallen {p.GraveyardCount}";
            }
        }
    }
}
