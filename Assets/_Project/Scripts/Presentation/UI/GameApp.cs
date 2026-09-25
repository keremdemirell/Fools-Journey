using UnityEngine;
using UnityEngine.UIElements;
using ArcanaWars.Cards;
using ArcanaWars.Cards.Decks;
using ArcanaWars.Core.Net;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation.UI
{
    /// <summary>
    /// The composition root for the real UI: owns the UIDocument, every screen, and the match
    /// driver. Screens never create each other or a driver; they raise an event and this decides,
    /// which keeps the navigation flow (lobby → match → lobby) in one readable place.
    ///
    /// <para><b>Nothing is built in Start.</b> A client joining a network match is TOLD the board's
    /// shape by the host, so until a driver reports ready there is nothing to draw — which is why
    /// the lobby runs first and the match screen only takes over once <see cref="IMatchDriver.IsReady"/>
    /// turns true. Anything added to startup has to respect that.</para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameApp : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CardDatabase cardDatabase;

        [Header("Scene")]
        [SerializeField] private BoardView boardView;

        [Header("Screen layouts (UXML)")]
        [SerializeField] private VisualTreeAsset lobbyScreenLayout;
        [SerializeField] private VisualTreeAsset matchScreenLayout;

        [Header("Board")]
        [SerializeField] private int columns = 18;
        [SerializeField] private int rows = 8;
        [SerializeField] private int cornerCut = 1;

        [Header("Default decks")]
        [Tooltip("Offered as the starting choice in the lobby. The player can pick a saved deck there instead.")]
        [SerializeField] private DeckAsset playerADeck;
        [SerializeField] private DeckAsset playerBDeck;

        [Tooltip("Name of a deck saved in the deck builder. Used only when no Deck asset is assigned above.")]
        [SerializeField] private string playerASavedDeckName;
        [SerializeField] private string playerBSavedDeckName;

        [Header("Setup")]
        [SerializeField] private int randomSeed = 1234;

        [Tooltip("The lobby's starting value for 'hide hands between turns'.")]
        [SerializeField] private bool hotSeatPrivacy = true;

        [Header("Network")]
        [Tooltip("Port used when hosting, and the default when joining. Both machines must agree.")]
        [SerializeField] private int networkPort = TcpMatchTransport.DefaultPort;

        [Tooltip("Address to join. 127.0.0.1 tests two copies on this machine — it reports a failed connection faster than \"localhost\", which tries IPv6 first.")]
        [SerializeField] private string joinAddress = "127.0.0.1";

        private LobbyScreen _lobbyScreen;
        private MatchScreen _matchScreen;
        private UiScreen _current;
        private IMatchDriver _driver;

        private void Start()
        {
            if (cardDatabase == null || boardView == null || matchScreenLayout == null || lobbyScreenLayout == null)
            {
                Debug.LogError("GameApp: assign Card Database, Board View, Lobby Screen Layout and Match Screen Layout in the Inspector.", this);
                enabled = false;
                return;
            }

            var root = GetComponent<UIDocument>().rootVisualElement;

            // The document's root covers the whole screen but draws nothing. Left pickable, it would
            // count as "UI under the pointer" everywhere and the board could never be clicked.
            root.pickingMode = PickingMode.Ignore;

            _lobbyScreen = new LobbyScreen(lobbyScreenLayout, root, new LobbyDefaults
            {
                DeckAssetA = playerADeck,
                DeckAssetB = playerBDeck,
                SavedDeckNameA = playerASavedDeckName,
                SavedDeckNameB = playerBSavedDeckName,
                Address = joinAddress,
                Port = networkPort,
                HotSeatPrivacy = hotSeatPrivacy,
            });

            _lobbyScreen.PlayHereRequested += OnPlayHere;
            _lobbyScreen.HostRequested += OnHost;
            _lobbyScreen.JoinRequested += OnJoin;
            _lobbyScreen.CancelRequested += ReturnToLobby;

            _matchScreen = new MatchScreen(matchScreenLayout, root, boardView, cardDatabase);
            _matchScreen.ExitRequested += ReturnToLobby;

            ShowScreen(_lobbyScreen);
            _lobbyScreen.ShowSetup();
        }

        private void Update()
        {
            // Anything that arrived from the other machine is applied here, on the main thread. The
            // socket's own thread only ever fills a queue — see IMatchTransport.
            _driver?.Pump();
            _current?.Tick();

            // The hand-off from lobby to match: the frame a driver has a session to show.
            if (_driver != null && _driver.IsReady && _current == _lobbyScreen)
                ShowScreen(_matchScreen);
        }

        // --- Starting a match ---------------------------------------------------------------------

        private void OnPlayHere(LobbySelection selection)
        {
            var driver = MatchLauncher.StartLocal(cardDatabase, Parameters(), selection.DeckA, selection.DeckB, selection.HotSeatPrivacy, out string error);
            Begin(driver, error, "Starting...");
        }

        private void OnHost(LobbySelection selection)
        {
            var driver = MatchLauncher.Host(cardDatabase, Parameters(), selection.DeckA, selection.DeckB, selection.Port, out string error);
            Begin(driver, error, $"Waiting for an opponent on port {selection.Port}...");
        }

        private void OnJoin(LobbySelection selection)
        {
            var driver = MatchLauncher.Join(cardDatabase, selection.Address, selection.Port, out string error);
            Begin(driver, error, $"Connecting to {selection.Address}:{selection.Port}...");
        }

        /// <summary>
        /// Takes whatever the launcher produced. A local match is ready immediately and Update swaps
        /// the screen next frame; a network one sits on the lobby's status panel until it is.
        /// </summary>
        private void Begin(IMatchDriver driver, string error, string connectingTitle)
        {
            if (driver == null)
            {
                // An illegal deck stops here rather than being quietly swapped for another: a match
                // started on a different deck than the one chosen is a silently different game.
                _lobbyScreen.ShowError(error ?? "Couldn't start the match.");
                return;
            }

            _driver = driver;
            _matchScreen.Attach(driver);
            _lobbyScreen.ShowConnecting(driver, connectingTitle);
        }

        private MatchParameters Parameters() =>
            new MatchParameters { Columns = columns, Rows = rows, CornerCut = cornerCut, Seed = randomSeed };

        // --- Leaving a match ----------------------------------------------------------------------

        /// <summary>Ends whatever is running and goes back to the lobby: after a match finishes, on Cancel, and when a connection has failed.</summary>
        private void ReturnToLobby()
        {
            _matchScreen.Detach();
            MatchLauncher.Shutdown(_driver);
            _driver = null;

            // A rematch from the same seed would deal the identical opening, which is useful for
            // debugging and dull to play. Moving it on costs nothing and is easy to override in the
            // Inspector when a repeatable match IS what's wanted.
            randomSeed++;

            ShowScreen(_lobbyScreen);
            _lobbyScreen.ShowSetup();
        }

        private void ShowScreen(UiScreen screen)
        {
            if (_current == screen) return;

            _current?.Hide();
            _current = screen;
            _current.Show();
        }

        /// <summary>A listening socket outlives the scene unless it's closed explicitly, holding the port until the Editor restarts.</summary>
        private void OnDestroy()
        {
            MatchLauncher.Shutdown(_driver);
            _driver = null;
        }
    }
}
