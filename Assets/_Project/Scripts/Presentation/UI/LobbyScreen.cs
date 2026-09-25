using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using ArcanaWars.Cards.Decks;
using ArcanaWars.Core.Net;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation.UI
{
    /// <summary>What the player picked in the lobby. Everything MatchLauncher needs to start a match, and nothing else.</summary>
    public struct LobbySelection
    {
        public bool HotSeatPrivacy;
        public DeckSource DeckA;
        public DeckSource DeckB;
        public string Address;
        public int Port;
    }

    /// <summary>The values the lobby starts with, taken from GameApp's Inspector fields.</summary>
    public struct LobbyDefaults
    {
        public DeckAsset DeckAssetA;
        public DeckAsset DeckAssetB;
        public string SavedDeckNameA;
        public string SavedDeckNameB;
        public string Address;
        public int Port;
        public bool HotSeatPrivacy;
    }

    /// <summary>
    /// The first screen: play here, host, or join — plus which deck each side brings.
    ///
    /// <para>It builds nothing itself. It gathers choices, raises one of three events, and then shows
    /// what the connection is doing until GameApp swaps in the match. That split exists because a
    /// joining client cannot draw a board at all until the host's setup arrives — it does not even
    /// know the board's shape yet (see MatchSetup).</para>
    ///
    /// <para><b>The status line is re-read every frame, not set once.</b> The old IMGUI lobby set its
    /// text when Connect was pressed and never again, so "connecting", "connected, waiting for the
    /// host", "failed" and "desynced" all looked identical — the bug that cost an evening during the
    /// first real LAN test. Anything that describes an operation in flight has to be pulled, not
    /// pushed.</para>
    /// </summary>
    public sealed class LobbyScreen : UiScreen
    {
        private readonly LobbyDefaults _defaults;

        private readonly VisualElement _setupPanel;
        private readonly VisualElement _connectingPanel;

        private readonly DropdownField _deckA;
        private readonly DropdownField _deckB;
        private readonly Toggle _hotSeat;
        private readonly TextField _address;
        private readonly TextField _port;
        private readonly Label _error;

        private readonly Label _connectingTitle;
        private readonly Label _connectingStatus;
        private readonly Button _cancel;

        private readonly List<DeckSource> _deckSourcesA = new List<DeckSource>();
        private readonly List<DeckSource> _deckSourcesB = new List<DeckSource>();

        private IMatchDriver _connecting;

        public event Action<LobbySelection> PlayHereRequested;
        public event Action<LobbySelection> HostRequested;
        public event Action<LobbySelection> JoinRequested;
        public event Action CancelRequested;
        public event Action DeckBuilderRequested;

        public LobbyScreen(VisualTreeAsset layout, VisualElement host, LobbyDefaults defaults) : base(layout, host)
        {
            _defaults = defaults;

            _setupPanel = Require<VisualElement>("lobby-setup");
            _connectingPanel = Require<VisualElement>("lobby-connecting");

            _deckA = Require<DropdownField>("deck-a");
            _deckB = Require<DropdownField>("deck-b");
            _hotSeat = Require<Toggle>("hotseat-toggle");
            _address = Require<TextField>("address-field");
            _port = Require<TextField>("port-field");
            _error = Require<Label>("lobby-error");

            _connectingTitle = Require<Label>("connecting-title");
            _connectingStatus = Require<Label>("connecting-status");
            _cancel = Require<Button>("cancel-button");

            Require<Button>("play-here-button").clicked += () => Raise(PlayHereRequested);
            Require<Button>("host-button").clicked += () => Raise(HostRequested);
            Require<Button>("join-button").clicked += () => Raise(JoinRequested);
            _cancel.clicked += () => CancelRequested?.Invoke();
            Require<Button>("deck-builder-button").clicked += () => DeckBuilderRequested?.Invoke();

            FillDeckChoices();

            _hotSeat.value = defaults.HotSeatPrivacy;
            _address.value = defaults.Address ?? "127.0.0.1";
            _port.value = defaults.Port.ToString();
        }

        /// <summary>Back to the form — on first show, after cancelling, and when a match ends.</summary>
        public void ShowSetup()
        {
            _connecting = null;
            _error.text = "";

            // Re-read the saved decks every time: the deck builder is one screen away, so a deck
            // saved a minute ago has to be selectable without restarting the game.
            FillDeckChoices();

            SetVisible(_setupPanel, true);
            SetVisible(_connectingPanel, false);
            Show();
        }

        /// <summary>Shows what the connection is doing. GameApp swaps to the match screen once the driver is ready.</summary>
        public void ShowConnecting(IMatchDriver driver, string title)
        {
            _connecting = driver;
            _connectingTitle.text = title;
            _connectingStatus.text = "";
            SetVisible(_setupPanel, false);
            SetVisible(_connectingPanel, true);
            Show();
        }

        /// <summary>Puts a failure back in front of the player, on the form where they can fix it.</summary>
        public void ShowError(string message)
        {
            ShowSetup();
            _error.text = message;
        }

        public override void Tick()
        {
            if (_connecting == null) return;
            _connectingStatus.text = StatusLine(_connecting);
        }

        /// <summary>
        /// What is actually happening right now, read live from the driver. A host sits in
        /// "Listening" until someone arrives; a client goes Connecting → WaitingSetup → Running.
        /// </summary>
        private static string StatusLine(IMatchDriver driver)
        {
            if (!(driver is LockstepMatchDriver lockstep))
                return driver.IsReady ? "Ready." : driver.StatusDetail ?? "Starting...";

            switch (lockstep.Status)
            {
                case LockstepStatus.Connecting: return "Opening the connection...";
                case LockstepStatus.WaitingSetup: return "Connected. Waiting for the host to describe the match...";
                case LockstepStatus.Running: return "Ready.";
                case LockstepStatus.Failed: return $"Failed: {lockstep.StatusDetail}";
                case LockstepStatus.Desynced: return $"Desynced: {lockstep.StatusDetail}";
                case LockstepStatus.Ended: return $"Ended: {lockstep.StatusDetail}";
                default: return lockstep.StatusDetail ?? lockstep.Status.ToString();
            }
        }

        private void Raise(Action<LobbySelection> request)
        {
            _error.text = "";

            if (!int.TryParse((_port.value ?? "").Trim(), out int port) || port <= 0 || port > 65535)
            {
                _error.text = $"\"{_port.value}\" isn't a valid port (a number from 1 to 65535).";
                return;
            }

            request?.Invoke(new LobbySelection
            {
                HotSeatPrivacy = _hotSeat.value,
                DeckA = SelectedDeck(_deckA, _deckSourcesA),
                DeckB = SelectedDeck(_deckB, _deckSourcesB),
                Address = (_address.value ?? "").Trim(),
                Port = port,
            });
        }

        private static DeckSource SelectedDeck(DropdownField field, List<DeckSource> sources)
        {
            int index = field.index;
            return index >= 0 && index < sources.Count ? sources[index] : default;
        }

        /// <summary>
        /// Offers each side a random deck, the DeckAsset wired up in the Inspector if there is one,
        /// and every deck saved by the deck builder. Saved decks are read from disk here rather than
        /// listed ahead of time, so a deck built minutes ago is in the list.
        /// </summary>
        private void FillDeckChoices()
        {
            Fill(_deckA, _deckSourcesA, _defaults.DeckAssetA, _defaults.SavedDeckNameA);
            Fill(_deckB, _deckSourcesB, _defaults.DeckAssetB, _defaults.SavedDeckNameB);
        }

        private static void Fill(DropdownField field, List<DeckSource> sources, DeckAsset asset, string savedName)
        {
            string previous = field.choices != null && field.index >= 0 && field.index < field.choices.Count ? field.value : null;

            var choices = new List<string>();
            sources.Clear();

            choices.Add("Random deck");
            sources.Add(new DeckSource(null, null));

            if (asset != null)
            {
                choices.Add($"{asset.name} (asset)");
                sources.Add(new DeckSource(asset, null));
            }

            foreach (var name in DeckStorage.SavedDeckNames())
            {
                choices.Add(name);
                sources.Add(new DeckSource(null, name));
            }

            field.choices = choices;

            // Keep what the player had picked if it still exists; otherwise start on whatever the
            // Inspector configured, so the lobby opens on the same match the scene would have
            // started on its own.
            int index = 0;
            int keep = previous != null ? choices.IndexOf(previous) : -1;

            if (keep >= 0) index = keep;
            else if (asset != null) index = 1;
            else if (!string.IsNullOrWhiteSpace(savedName))
            {
                int found = choices.IndexOf(savedName);
                if (found >= 0) index = found;
            }

            field.index = index;
        }
    }
}
