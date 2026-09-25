using System.Collections.Generic;
using UnityEngine;
using ArcanaWars.Cards;
using ArcanaWars.Cards.Decks;
using ArcanaWars.Core;
using ArcanaWars.Core.Abilities;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.GameLoop;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Net;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// The bridge between the Core simulation and the screen. It owns a MatchState, draws the board,
    /// and translates clicks into MatchState.PlayCard calls — but it holds no rules of its own. Every
    /// number shown here is read live from Core, so the view cannot drift out of sync with the game.
    ///
    /// The on-screen HUD is deliberately IMGUI (OnGUI): it needs zero scene setup, which makes this a
    /// debug view you can run immediately. Real UI comes later.
    /// </summary>
    public class MatchView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CardDatabase cardDatabase;

        [Header("Board")]
        [SerializeField] private int columns = 18;
        [SerializeField] private int rows = 8;
        [SerializeField] private int cornerCut = 1;
        [SerializeField] private float hexSize = 0.5f;

        [Header("Colours")]
        [SerializeField] private Color playerASideColor = new Color(0.24f, 0.34f, 0.52f);
        [SerializeField] private Color playerBSideColor = new Color(0.52f, 0.28f, 0.30f);
        [SerializeField] private Color neutralSideColor = new Color(0.32f, 0.32f, 0.36f);
        [SerializeField] private Color wallColor = new Color(0.80f, 0.66f, 0.30f);
        [SerializeField] private Color playerAUnitColor = new Color(0.42f, 0.68f, 1f);
        [SerializeField] private Color playerBUnitColor = new Color(1f, 0.5f, 0.45f);

        // Each side's deck is resolved in priority order: the DeckAsset if one is assigned, else the
        // named deck saved by DeckBuilderView, else a random legal 45 so the scene still runs with
        // nothing wired up at all. See ResolveDeck.
        [Header("Decks")]
        [SerializeField] private DeckAsset playerADeck;
        [SerializeField] private DeckAsset playerBDeck;

        [Tooltip("Name of a deck saved in the deck builder. Used only when no Deck asset is assigned above.")]
        [SerializeField] private string playerASavedDeckName;
        [SerializeField] private string playerBSavedDeckName;

        [Header("Setup")]
        [SerializeField] private int randomSeed = 1234;

        [Tooltip("Hot seat: only the player whose turn it is sees their hand, with a handover screen between turns. " +
                 "Uncheck for a shared screen where both hands are visible at once — no privacy, but no handover taps either, which is far quicker for testing. " +
                 "Ignored for network games, where the opponent's hand is never on this machine's screen anyway.")]
        [SerializeField] private bool hotSeatPrivacy = true;

        [Header("Network")]
        [Tooltip("Port used when hosting, and the default when joining. Both machines must agree.")]
        [SerializeField] private int networkPort = TcpMatchTransport.DefaultPort;

        [Tooltip("Address to join. Use 127.0.0.1 to test two copies on this machine — it reports a failed connection faster than \"localhost\", which tries IPv6 first.")]
        [SerializeField] private string joinAddress = "127.0.0.1";

        /// <summary>The port typed into the Join field at runtime. Starts as networkPort's value (right for LAN, where both sides agree on it ahead of time) but the player can overwrite it — needed for a tunnel, whose public port is assigned and not the same as the host's own listening port.</summary>
        private string _joinPortText;

        private MatchState _match;
        private MatchSession _session;

        // The one thing that differs between a local match and a networked one. Every player action
        // goes through it; the rest of this view cannot tell the two apart.
        private IMatchDriver _driver;
        private TcpMatchTransport _transport;

        private bool _viewsBuilt;
        private string _lobbyMessage;
        private Camera _camera;
        private Mesh _hexMesh;
        private Transform _boardRoot;
        private Transform _unitRoot;

        private readonly Dictionary<HexCoord, TileView> _tiles = new Dictionary<HexCoord, TileView>();
        private readonly Dictionary<int, UnitView> _unitViews = new Dictionary<int, UnitView>();

        private int _selectedCard = -1;
        private string _lastMessage = "Click a card, then click a tile on your side.";
        private TileView _hovered;
        private Vector2 _handScrollPos;

        // Play-time choices for the selected card (see Core's CardPlayOptions). A targeting card takes
        // two board clicks — first the unit it acts on, then where it goes — so the view has to
        // remember the first click; "skipped" records an explicit "no target" so the next click places.
        private Element? _chosenElement;
        private int? _chosenTargetId;
        private bool _targetSkipped;

        // An Ace's cost is "1 OR all your current mana" (GDD 10.1) — this picks which.
        private bool _payAllForAce;

        /// <summary>
        /// Was a fixed 330x620 Rect — fine on a big Game view, but clipped or unreachable on a
        /// small/zoomed one (which is exactly what a docked Editor Game tab often is), cutting off
        /// text and making tiles behind it unclickable. Sized from the actual screen now, so it
        /// never claims space that doesn't exist.
        /// </summary>
        private Rect ComputeHudRect() => new Rect(10, 10, Mathf.Min(340f, Screen.width - 20f), Mathf.Min(620f, Screen.height - 20f));

        private void Start()
        {
            if (cardDatabase == null)
            {
                Debug.LogError("MatchView: no CardDatabase assigned. Run Tools > Arcana Wars > Generate Card Database, then drag the generated CardDatabase asset onto this component.", this);
                enabled = false;
                return;
            }

            // The board isn't built here any more: a client joining a network game is told the board's
            // shape by the host, so there is nothing to draw until a match actually exists. The lobby
            // in OnGUI decides which kind of match that will be.
            _hexMesh = HexMeshBuilder.Build(hexSize);
            _lobbyMessage = "Choose how to play.";
        }

        /// <summary>Both players at this device. Builds the match here and wraps it in the local driver.</summary>
        private void StartLocalMatch()
        {
            var rng = new System.Random(randomSeed);

            var deckA = ResolveDeck(PlayerSide.A, playerADeck, playerASavedDeckName, rng);
            var deckB = ResolveDeck(PlayerSide.B, playerBDeck, playerBSavedDeckName, rng);
            if (deckA == null || deckB == null)
            {
                _lobbyMessage = "A configured deck is not legal — see the Console. Fix it in the deck builder, or clear the field to use a random deck.";
                return;
            }

            var match = new MatchState(new HexGrid(columns, rows, cornerCut), deckA, deckB, rng: rng);
            var session = hotSeatPrivacy ? MatchSession.HotSeat(match) : MatchSession.SharedScreen(match);

            match.StartMatch();
            session.AdvancePhase(); // RoundStart -> Action, so round 1 opens ready to play

            _driver = new LocalMatchDriver(session, cardDatabase);
        }

        /// <summary>
        /// Hosts a LAN match. The host decides everything about the match and sends it — see
        /// MatchSetup for why one machine has to, rather than both agreeing.
        /// </summary>
        private void StartHosting()
        {
            var rng = new System.Random(randomSeed);

            var deckA = ResolveDeck(PlayerSide.A, playerADeck, playerASavedDeckName, rng);
            var deckB = ResolveDeck(PlayerSide.B, playerBDeck, playerBSavedDeckName, rng);
            if (deckA == null || deckB == null)
            {
                _lobbyMessage = "A configured deck is not legal — see the Console. Fix it before hosting.";
                return;
            }

            // Both decks are settled here, by the host, and sent to the client — including the deck
            // the client would otherwise have chosen. See MatchSetup for why one machine must decide.
            var setup = new MatchSetup(randomSeed, columns, rows, cornerCut, Soul.StandardStartingHp, Ids(deckA), Ids(deckB));

            _transport = TcpMatchTransport.Host(networkPort);
            _driver = LockstepMatchDriver.Host(_transport, cardDatabase, setup);
            _lobbyMessage = $"Waiting for an opponent on port {networkPort}...";
        }

        /// <summary>Joins a match — on the LAN, or through a tunnel over the internet. This machine plays B and adopts the host's setup, including its deck — see MatchSetup.</summary>
        private void StartJoining()
        {
            if (!int.TryParse((_joinPortText ?? "").Trim(), out int port) || port <= 0 || port > 65535)
            {
                _lobbyMessage = $"\"{_joinPortText}\" isn't a valid port (must be a number, 1-65535).";
                return;
            }

            if (string.IsNullOrWhiteSpace(joinAddress))
            {
                _lobbyMessage = "Enter an address to join.";
                return;
            }

            _transport = TcpMatchTransport.Join(joinAddress.Trim(), port);
            _driver = LockstepMatchDriver.Join(_transport, cardDatabase);
            _lobbyMessage = $"Connecting to {joinAddress.Trim()}:{port}...";
        }

        private static List<string> Ids(List<IPlayableCard> cards)
        {
            var ids = new List<string>(cards.Count);
            foreach (var card in cards) ids.Add(card.DefinitionId);
            return ids;
        }

        /// <summary>
        /// Called once, the frame a match actually exists. For a local game that's immediate; for a
        /// client it's whenever the host's setup arrives, which is why none of this can happen in Start.
        /// </summary>
        private void OnMatchReady()
        {
            _viewsBuilt = true;
            _session = _driver.Session;
            _match = _session.Match;
            _match.MatchEnded += OnMatchEnded;

            BuildBoardViews(_match.Board);
            FitCamera();
            RefreshUnitViews();

            _lastMessage = "Click a card, then click a tile on your side.";
        }

        /// <summary>
        /// Picks one side's deck: an assigned DeckAsset first, then a deck saved by the deck builder,
        /// then the random fallback. Returns null only when a deck WAS configured and turned out to be
        /// illegal — a match started on an illegal deck would be a silently different game, so that
        /// stops the scene the way a missing CardDatabase does rather than quietly substituting one.
        /// </summary>
        private List<IPlayableCard> ResolveDeck(PlayerSide side, DeckAsset asset, string savedDeckName, System.Random rng)
        {
            DeckList deck = null;
            string source = null;

            if (asset != null)
            {
                deck = asset.ToDeckList();
                source = $"deck asset \"{asset.name}\"";
            }
            else if (!string.IsNullOrWhiteSpace(savedDeckName))
            {
                deck = DeckStorage.TryLoad(savedDeckName, out string error);
                source = $"saved deck \"{savedDeckName}\"";

                if (deck == null)
                {
                    Debug.LogError($"MatchView: {side}'s {source} could not be loaded — {error}", this);
                    return null;
                }
            }

            if (deck == null)
            {
                Debug.LogWarning($"MatchView: no deck configured for {side}; using a random legal 45. Build one with DeckBuilderView and assign it here.", this);
                return RandomLegalDeck(rng);
            }

            var validation = DeckRules.Validate(deck, cardDatabase);
            if (!validation.IsValid)
            {
                Debug.LogError($"MatchView: {side}'s {source} is not legal.\n{validation.Summary()}", this);
                return null;
            }

            return deck.ToCards(cardDatabase);
        }

        /// <summary>
        /// The fallback when no deck is configured: the whole card pool shuffled and cut to 45. This
        /// was the ONLY way a deck was built before deckbuilding existed, kept because it means the
        /// scene runs with nothing wired up.
        ///
        /// It happens to be legal under DeckRules without needing a check: every card appears at most
        /// once, which is within both the 3-copy limit and the Major Arcana's 1.
        /// </summary>
        private List<IPlayableCard> RandomLegalDeck(System.Random rng)
        {
            var copy = cardDatabase.AllCards();
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            if (copy.Count > DeckRules.DeckSize)
                copy.RemoveRange(DeckRules.DeckSize, copy.Count - DeckRules.DeckSize);

            return copy;
        }

        private void BuildBoardViews(HexGrid board)
        {
            _boardRoot = new GameObject("Board").transform;
            _boardRoot.SetParent(transform, false);

            _unitRoot = new GameObject("Units").transform;
            _unitRoot.SetParent(transform, false);

            foreach (var tile in board.AllTiles)
                _tiles[tile.Coord] = TileView.Create(_boardRoot, tile.Coord, _hexMesh, hexSize, TileColor(tile));
        }

        private Color TileColor(Tile tile)
        {
            if (tile.IsWalled) return wallColor;
            return tile.Side == PlayerSide.A ? playerASideColor
                 : tile.Side == PlayerSide.B ? playerBSideColor
                 : neutralSideColor;
        }

        /// <summary>Re-tints every tile from the board, so an Emperor's wall shows up when it rises and disappears when it falls.</summary>
        private void RefreshTileColors()
        {
            foreach (var tile in _match.Board.AllTiles)
            {
                if (!_tiles.TryGetValue(tile.Coord, out var view)) continue;
                view.SetBaseColor(TileColor(tile));
                if (view == _hovered) view.SetHighlighted(true); // SetBaseColor just wiped the hover tint
            }
        }

        private void FitCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Match Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
            }

            // Frame whatever the board actually turned out to be, rather than hardcoding a position.
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var tile in _tiles.Values)
            {
                var p = tile.transform.position;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            var centre = (min + max) * 0.5f;
            float halfHeight = (max.y - min.y) * 0.5f + hexSize * 2f;
            float halfWidth = (max.x - min.x) * 0.5f + hexSize * 2f;

            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(_camera.aspect, 0.01f));
            _camera.transform.position = new Vector3(centre.x, centre.y, -10f);
            _camera.transform.rotation = Quaternion.identity;
            _camera.backgroundColor = new Color(0.10f, 0.10f, 0.13f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void Update()
        {
            // Anything that arrived from the other machine is applied here, on the main thread. The
            // socket's own thread only ever fills a queue — see IMatchTransport.
            _driver?.Pump();

            if (_driver != null && !_viewsBuilt && _driver.IsReady) OnMatchReady();

            if (_match == null || _camera == null) return;

            // Behind the handover curtain the board is inert: the outgoing player must not be able
            // to keep playing, and the incoming one hasn't confirmed they're looking yet.
            if (_session.HandoverPending)
            {
                if (_hovered != null) { _hovered.SetHighlighted(false); _hovered = null; }
                return;
            }

            var mouse = Input.mousePosition;
            bool overHud = ComputeHudRect().Contains(new Vector2(mouse.x, Screen.height - mouse.y));

            var tile = overHud ? null : TileAtScreenPoint(mouse);

            if (_hovered != tile)
            {
                if (_hovered != null) _hovered.SetHighlighted(false);
                _hovered = tile;
                if (_hovered != null) _hovered.SetHighlighted(true);
            }

            if (!overHud && tile != null && Input.GetMouseButtonDown(0))
                TryPlaySelectedCard(tile);
        }

        /// <summary>
        /// Nearest tile centre wins. That isn't an approximation: on a hex grid the nearest centre
        /// IS the hex you clicked, so this needs no colliders and no coordinate rounding.
        /// </summary>
        private TileView TileAtScreenPoint(Vector3 screenPoint)
        {
            var world = _camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -_camera.transform.position.z));

            TileView best = null;
            float bestDistance = hexSize * hexSize;

            foreach (var tile in _tiles.Values)
            {
                float d = ((Vector2)(tile.transform.position - world)).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = tile;
                }
            }

            return best;
        }

        /// <summary>The side at the controls. Null when nobody at this screen may act — the phase is wrong, the Action phase is over, or the turn belongs elsewhere.</summary>
        private PlayerSide? ActingSide => _session.ControllingSide;

        /// <summary>The side the screen is drawn for. Distinct from ActingSide: between phases there is no acting side, but the HUD still has to show somebody's hand.</summary>
        private PlayerSide ViewerSide => _session.ViewerSide;

        private void TryPlaySelectedCard(TileView tile)
        {
            if (ActingSide == null)
            {
                _lastMessage = _match.Rounds.CurrentPhase == GamePhase.Action
                    ? $"It is {_match.Rounds.ActiveSide}'s turn."
                    : $"Cards are only played during the Action phase (currently {_match.Rounds.CurrentPhase}).";
                return;
            }

            var side = ActingSide.Value;
            var player = _match.GetPlayer(side);

            if (_selectedCard < 0 || _selectedCard >= player.Hand.Count)
            {
                _lastMessage = "Select a card from your hand first.";
                return;
            }

            var card = player.Hand.Cards[_selectedCard].Card;
            var choice = AbilityRegistry.ChoiceFor(card.DefinitionId);

            // First click of a targeting card picks the unit it acts on; the second click places it.
            if (choice == PlayChoice.AllyTarget && _chosenTargetId == null && !_targetSkipped)
            {
                var clicked = _match.Units.GetUnitAt(tile.Coord);
                if (clicked == null || clicked.Owner != side)
                {
                    _lastMessage = $"{card.DisplayName}: click one of YOUR units to target it, or press 'No target'.";
                    return;
                }

                _chosenTargetId = clicked.Id;
                _lastMessage = $"Target chosen. Now click where {card.DisplayName} itself goes.";
                return;
            }

            if (choice == PlayChoice.Element && _chosenElement == null)
            {
                _lastMessage = $"Choose an element for {card.DisplayName} first.";
                return;
            }

            // For a 1x1 the clicked column is the only option; for anything wider, the anchor's own
            // column is always part of the footprint, so this is always a legal choice.
            int attackColumn = tile.Coord.ToOffset().Col;
            var options = new CardPlayOptions(_chosenElement, _chosenTargetId);

            int manaCommitted = card.HasVariableCost && _payAllForAce ? player.Mana.Remaining : 0;

            var result = _driver.Submit(MatchCommand.PlayCard(side, card.DefinitionId, tile.Coord, attackColumn, manaCommitted, options));

            // Condition and choice failures carry their own explanation ("The Empress needs 2 Queens
            // played this match"), which is far more useful than the generic category name.
            _lastMessage = result.Applied
                ? $"Played {card.DisplayName}."
                : $"Couldn't play {card.DisplayName}: {result.Reason}";

            if (result.Applied)
            {
                ResetSelection();
                RefreshUnitViews();
            }
        }

        private void ResetSelection()
        {
            _selectedCard = -1;
            _chosenElement = null;
            _chosenTargetId = null;
            _targetSkipped = false;
            _payAllForAce = false;
        }

        /// <summary>
        /// Judgement's pending rulings for whoever is at the screen: one row per dead unit, with
        /// Return (to your hand) and Discard. There's no deadline in Core, so these simply wait here
        /// until answered — deliberately not gated to the judge's own turn, since a ruling costs no
        /// turn and a deadline is the one thing that could deadlock the match. Return is greyed out
        /// for tokens that were never a card.
        /// </summary>
        private void DrawJudgementPanel()
        {
            bool headerShown = false;

            foreach (var pending in new List<PendingJudgement>(_match.PendingJudgements))
            {
                if (pending.Judge != ViewerSide) continue;

                if (!headerShown)
                {
                    GUILayout.Label("<b>Judgement</b> — return to your hand, or discard for good?", RichLabel());
                    headerShown = true;
                }

                string name = pending.Entry.SourceCard != null ? pending.Entry.SourceCard.DisplayName : pending.Entry.DefinitionId;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{name} ({pending.Entry.Owner})");

                GUI.enabled = pending.CanReturnToHand;
                if (GUILayout.Button("Return")) _driver.Submit(MatchCommand.ResolveJudgement(ViewerSide, pending.Id, JudgementVerdict.ReturnToHand));
                GUI.enabled = true;

                if (GUILayout.Button("Discard")) _driver.Submit(MatchCommand.ResolveJudgement(ViewerSide, pending.Id, JudgementVerdict.Discard));
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>Shows the controls for whatever the selected card wants decided — an element, or a target.</summary>
        private void DrawChoicePanel(PlayerState player)
        {
            if (_selectedCard < 0 || _selectedCard >= player.Hand.Count) return;

            var card = player.Hand.Cards[_selectedCard].Card;

            if (card.HasVariableCost)
            {
                GUILayout.Label($"Pay: {(_payAllForAce ? $"all ({player.Mana.Remaining})" : "1")}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Pay 1")) _payAllForAce = false;
                if (GUILayout.Button($"Pay all ({player.Mana.Remaining})")) _payAllForAce = true;
                GUILayout.EndHorizontal();
            }

            switch (AbilityRegistry.ChoiceFor(card.DefinitionId))
            {
                case PlayChoice.Element:
                    GUILayout.Label($"Element: {(_chosenElement.HasValue ? _chosenElement.Value.ToString() : "choose one")}");
                    GUILayout.BeginHorizontal();
                    foreach (Element element in System.Enum.GetValues(typeof(Element)))
                        if (GUILayout.Button(element.ToString())) _chosenElement = element;
                    GUILayout.EndHorizontal();
                    break;

                case PlayChoice.AllyTarget:
                    string target = _chosenTargetId.HasValue ? $"unit #{_chosenTargetId.Value}"
                                  : _targetSkipped ? "none"
                                  : "click one of your units";
                    GUILayout.Label($"Target: {target}");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("No target")) { _chosenTargetId = null; _targetSkipped = true; }
                    if (GUILayout.Button("Re-pick")) { _chosenTargetId = null; _targetSkipped = false; }
                    GUILayout.EndHorizontal();
                    break;
            }
        }

        /// <summary>
        /// Rebuilds views to match the simulation: spawn views for new units, destroy views for units Core
        /// has removed. Also re-tints the tiles, since walls only change when units arrive or die — the
        /// same moments this is already called.
        /// </summary>
        private void RefreshUnitViews()
        {
            RefreshTileColors();

            var live = new HashSet<int>();

            foreach (var unit in _match.Units.AllUnits)
            {
                live.Add(unit.Id);
                if (_unitViews.ContainsKey(unit.Id)) continue;

                var footprint = _match.Board.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
                var color = unit.Owner == PlayerSide.A ? playerAUnitColor : playerBUnitColor;
                _unitViews[unit.Id] = UnitView.Create(_unitRoot, unit, footprint, _hexMesh, hexSize, color);
            }

            var stale = new List<int>();
            foreach (var id in _unitViews.Keys)
                if (!live.Contains(id)) stale.Add(id);

            foreach (var id in stale)
            {
                if (_unitViews[id] != null) Destroy(_unitViews[id].gameObject);
                _unitViews.Remove(id);
            }
        }

        private void OnMatchEnded(PlayerSide? winner)
        {
            _lastMessage = winner.HasValue ? $"Match over — {winner.Value} wins!" : "Match over — draw.";
        }

        private void OnGUI()
        {
            // No match yet: either nothing has been chosen, or a network game is still connecting.
            if (_match == null)
            {
                DrawLobby();
                return;
            }

            // The curtain owns the whole screen: the board is public information, but handing the
            // device over is clearer if there is exactly one thing to look at and one thing to press.
            if (_session.HandoverPending)
            {
                DrawHandoverCurtain();
                return;
            }

            GUILayout.BeginArea(ComputeHudRect(), GUI.skin.box);

            var player = _match.GetPlayer(ViewerSide);
            var opponent = _match.GetOpponent(ViewerSide);

            GUILayout.Label($"<b>Round {_match.Rounds.RoundNumber} — {_match.Rounds.CurrentPhase}</b>", RichLabel());
            GUILayout.Label(TurnLine(), RichLabel());
            DrawConnectionLine();
            GUILayout.Label($"You ({ViewerSide})   Soul {player.Soul.CurrentHp}/{player.Soul.MaxHp}   Pentacles {player.Mana.Remaining}/{player.Mana.Available}");
            GUILayout.Label($"Opponent ({opponent.Side})   Soul {opponent.Soul.CurrentHp}/{opponent.Soul.MaxHp}");
            GUILayout.Label($"Deck {player.Deck.Count}   Hand {player.Hand.Count}/{HandRules.MaxHandSize}");

            GUILayout.Space(6);

            DrawPhaseControls();

            GUILayout.Space(6);
            GUILayout.Label($"<i>{_lastMessage}</i>", RichLabel());
            DrawChoicePanel(player);
            DrawJudgementPanel();
            GUILayout.Space(6);

            // Asked rather than assumed: the session decides whose hand this screen may draw, and it
            // is the only place that rule lives. Today it can only refuse behind the curtain (already
            // handled above), but a session with a remote opponent would refuse here too.
            if (!_session.CanSeeHand(ViewerSide))
            {
                GUILayout.Label("<b>Hand</b> — hidden.", RichLabel());
                GUILayout.EndArea();
                DrawUnitLabels();
                return;
            }

            GUILayout.Label($"<b>Hand</b>", RichLabel());

            // Scrollable rather than a plain loop: the hand can hold up to HandRules.MaxHandSize
            // (14) cards, which won't fit in a fixed-height panel — a plain GUILayout list would
            // just render past the bottom and become unreadable/unclickable instead of scrolling.
            _handScrollPos = GUILayout.BeginScrollView(_handScrollPos, GUILayout.ExpandHeight(true));
            for (int i = 0; i < player.Hand.Count; i++)
            {
                var held = player.Hand.Cards[i];
                var card = held.Card;
                int cost = held.EffectiveCost();
                bool affordable = cost <= player.Mana.Remaining;
                string discount = held.CostDiscount > 0 ? $" (-{held.CostDiscount})" : "";
                string buff = held.Buff.IsNone ? "" : $"  +{held.Buff.Hp}hp/+{held.Buff.Attack}atk/+{held.Buff.Heal}heal";
                string costText = card.HasVariableCost ? "1 or all" : $"{cost}{discount}";
                string label = $"{(i == _selectedCard ? "> " : "")}{card.DisplayName}  ({costText}){buff}";

                var previous = GUI.color;
                if (!affordable) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                if (GUILayout.Button(label))
                {
                    ResetSelection();
                    _selectedCard = i;
                }
                GUI.color = previous;
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();

            DrawUnitLabels();
        }

        /// <summary>
        /// Shown until a match exists: pick local or network, then the connection's progress. A client
        /// sits here until the host's setup arrives, because until then it doesn't even know the shape
        /// of the board it would be drawing.
        /// </summary>
        private void DrawLobby()
        {
            var area = new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.24f, 420f, 380f);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("<b>Arcana Wars</b>", RichLabel());
            GUILayout.Space(8);

            if (_driver == null)
            {
                if (GUILayout.Button("Play on this device", GUILayout.Height(34))) StartLocalMatch();

                GUILayout.Space(10);
                GUILayout.Label("<b>Play over the network</b>", RichLabel());

                if (GUILayout.Button($"Host a game (port {networkPort})", GUILayout.Height(28))) StartHosting();

                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Address", GUILayout.Width(60));
                joinAddress = GUILayout.TextField(joinAddress ?? "");
                GUILayout.EndHorizontal();

                // A separate, typeable port — not just the Inspector's fixed networkPort. A LAN
                // join always uses the same port both sides agreed on ahead of time, but a tunnel
                // (ngrok and similar) hands out a different, unpredictable port on every public
                // address it opens, so the player has to be able to enter one at runtime.
                GUILayout.BeginHorizontal();
                GUILayout.Label("Port", GUILayout.Width(60));
                _joinPortText = GUILayout.TextField(_joinPortText ?? networkPort.ToString());
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Join a game", GUILayout.Height(28))) StartJoining();

                GUILayout.Space(8);
                GUILayout.Label("<i>On the same network: the host's address is its local IP (run 'ipconfig' to find it), and the port matches what the host is hosting on. " +
                                 "Use 127.0.0.1 to test two copies on this machine. Over the internet with a tunnel (e.g. ngrok): enter the address and port IT gives you — they will not match the host's own numbers.</i>", RichLabel());
            }
            else
            {
                // _lobbyMessage is only ever the FIRST line — what was requested, not what is
                // currently happening. It used to be the only text shown here, which meant the
                // screen kept saying "Connecting..." even once the connection had actually
                // progressed past that (or failed), because nothing updated it. This live line is
                // the fix: it reads the driver's actual status every frame.
                GUILayout.Label(_lobbyMessage ?? "Connecting...", RichLabel());
                GUILayout.Label(LobbyStatusLine(), RichLabel());

                GUILayout.Space(8);
                if (GUILayout.Button("Cancel")) ShutDownDriver();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// What is actually happening right now, read live from the driver every frame. The lobby
        /// used to show only the static "Connecting to X..." request text and never anything after
        /// it, which made a real connection failure and a healthy-but-slow connection look identical
        /// — both were silent.
        /// </summary>
        private string LobbyStatusLine()
        {
            if (!(_driver is LockstepMatchDriver lockstep)) return "";

            switch (lockstep.Status)
            {
                case LockstepStatus.Connecting: return "<i>Opening the connection...</i>";
                case LockstepStatus.WaitingSetup: return "<i>Connected. Waiting for the host to describe the match...</i>";
                case LockstepStatus.Failed: return $"<b>Failed:</b> {lockstep.StatusDetail}";
                case LockstepStatus.Desynced: return $"<b>Desynced:</b> {lockstep.StatusDetail}";
                case LockstepStatus.Ended: return $"<b>Ended:</b> {lockstep.StatusDetail}";
                default: return "";
            }
        }

        private void ShutDownDriver()
        {
            (_driver as System.IDisposable)?.Dispose();
            _transport = null;
            _driver = null;
            _lobbyMessage = "Choose how to play.";
        }

        /// <summary>The socket outlives the scene unless it's closed explicitly, which would leave the port held until the Editor is restarted.</summary>
        private void OnDestroy() => ShutDownDriver();

        /// <summary>
        /// For a network game, whether the other machine is still there and still agrees with us.
        /// Silent for a local match, and silent while a network match is healthy — it only speaks up
        /// when something has gone wrong, because a desync that isn't shown is a desync two players
        /// will spend an hour arguing about.
        /// </summary>
        private void DrawConnectionLine()
        {
            if (!(_driver is LockstepMatchDriver lockstep)) return;

            switch (lockstep.Status)
            {
                case LockstepStatus.Running:
                    return;

                case LockstepStatus.Desynced:
                    GUILayout.Label($"<b>Desynced.</b> The two games no longer match and cannot continue.\n<i>{lockstep.DesyncReport}</i>", RichLabel());
                    return;

                case LockstepStatus.Ended:
                    GUILayout.Label($"<b>{lockstep.StatusDetail ?? "The other player left."}</b>", RichLabel());
                    return;

                default:
                    GUILayout.Label($"<b>Connection {lockstep.Status}.</b> <i>{lockstep.StatusDetail}</i>", RichLabel());
                    return;
            }
        }

        /// <summary>The one line that answers "can I do something right now, and if not, why not".</summary>
        private string TurnLine()
        {
            if (_match.IsOver)
                return _match.Winner.HasValue ? $"<b>{_match.Winner.Value} wins.</b>" : "<b>Draw.</b>";

            if (_match.Rounds.CurrentPhase != GamePhase.Action)
                return $"<i>{_match.Rounds.CurrentPhase} — no cards are played in this phase.</i>";

            if (_match.Rounds.ActionComplete)
                return "<i>Both players passed. Continue to Combat.</i>";

            string passes = _match.Rounds.ConsecutivePasses > 0 ? "   (opponent passed — pass too to end the phase)" : "";

            return ActingSide == ViewerSide
                ? $"<b>YOUR TURN ({ViewerSide})</b>{passes}"
                : $"<i>Waiting for {_match.Rounds.ActiveSide}.</i>";
        }

        /// <summary>
        /// The phase buttons. During the Action phase the only way forward is to pass — a player must
        /// not be able to skip straight to Combat, since that would rob their opponent of the turns
        /// they are still owed. Every other phase is a plain step.
        /// </summary>
        private void DrawPhaseControls()
        {
            if (_match.IsOver)
            {
                GUILayout.Label(_match.Winner.HasValue
                    ? $"<b>Match over — {_match.Winner.Value} wins.</b>"
                    : "<b>Match over — a draw.</b>", RichLabel());
                GUILayout.Label("<i>Press Play again in the Editor to start a new match.</i>", RichLabel());
                return;
            }

            var phase = _match.Rounds.CurrentPhase;

            GUILayout.BeginHorizontal();

            if (phase == GamePhase.Action && !_match.Rounds.ActionComplete)
            {
                GUI.enabled = _session.CanAct;
                if (GUILayout.Button(_match.Rounds.ConsecutivePasses > 0 ? "Pass (ends phase)" : "Pass"))
                {
                    var result = _driver.Submit(MatchCommand.Pass(ViewerSide));
                    if (result.Applied)
                    {
                        _lastMessage = $"{ViewerSide} passed.";
                        ResetSelection();
                    }
                    else
                    {
                        _lastMessage = result.Reason;
                    }
                }
                GUI.enabled = true;
            }
            else if (!_driver.OwnsClock)
            {
                // Exactly one machine advances the round, or both would and one side would run it
                // twice — see IMatchDriver.OwnsClock. On a client there is genuinely nothing to press.
                GUILayout.Label("<i>Waiting for the host to continue...</i>", RichLabel());
            }
            else if (phase == GamePhase.Action)
            {
                if (GUILayout.Button("Continue to Combat")) StepPhase(MatchCommand.AdvancePhase());
            }
            else if (phase == GamePhase.WinCheck)
            {
                if (GUILayout.Button("Next Round")) StepPhase(MatchCommand.BeginNextRound());
            }
            else if (GUILayout.Button($"Advance to {NextPhaseName()}"))
            {
                StepPhase(MatchCommand.AdvancePhase());
            }

            GUILayout.EndHorizontal();
        }

        private void StepPhase(MatchCommand command)
        {
            var result = _driver.Submit(command);
            if (!result.Applied) _lastMessage = result.Reason;

            ResetSelection();
            RefreshUnitViews();
        }

        /// <summary>
        /// The hot-seat handover: a full-screen cover so the outgoing player's hand is off the screen
        /// before the incoming one looks at it. This is the whole privacy mechanism — the board, both
        /// Souls and both mana pools are public in a face-to-face game, and only the hand is not.
        /// </summary>
        private void DrawHandoverCurtain()
        {
            var full = new Rect(0, 0, Screen.width, Screen.height);

            var previous = GUI.color;
            GUI.color = new Color(0.07f, 0.07f, 0.10f, 0.98f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = previous;

            var incoming = _match.Rounds.ActiveSide;

            var title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = true
            };
            title.normal.textColor = Color.white;

            var subtitle = new GUIStyle(title) { fontSize = 15, fontStyle = FontStyle.Normal };
            subtitle.normal.textColor = new Color(0.75f, 0.75f, 0.80f);

            GUI.Label(new Rect(0, Screen.height * 0.32f, Screen.width, 40f), $"Pass the device to Player {incoming}", title);
            GUI.Label(new Rect(0, Screen.height * 0.32f + 44f, Screen.width, 30f), _lastMessage, subtitle);

            var button = new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.52f, 260f, 46f);
            if (GUI.Button(button, $"I'm Player {incoming} — Ready"))
            {
                _session.AcknowledgeHandover();

                // The incoming player must not inherit whatever the outgoing one had half-selected.
                ResetSelection();
                _lastMessage = "Click a card, then click a tile on your side.";
            }
        }

        private string NextPhaseName()
        {
            switch (_match.Rounds.CurrentPhase)
            {
                case GamePhase.RoundStart: return "Action";
                case GamePhase.Action: return "Combat";
                case GamePhase.Combat: return "Decay";
                case GamePhase.Decay: return "Win Check";
                default: return "next";
            }
        }

        /// <summary>HP drawn as screen-space labels rather than world text, which avoids depending on any font/shader setup.</summary>
        private void DrawUnitLabels()
        {
            if (_camera == null) return;

            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;

            foreach (var unit in _match.Units.AllUnits)
            {
                var world = HexLayout.ToWorld(unit.AnchorCoord, hexSize);
                var screen = _camera.WorldToScreenPoint(world);
                if (screen.z < 0f) continue;

                var rect = new Rect(screen.x - 20f, Screen.height - screen.y - 10f, 40f, 20f);
                GUI.Label(rect, unit.CurrentHp.ToString(), style);
            }
        }

        private static GUIStyle RichLabel()
        {
            return new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        }
    }
}
