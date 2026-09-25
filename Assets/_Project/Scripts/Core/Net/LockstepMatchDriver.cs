using System;
using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Core.Net
{
    public enum LockstepStatus
    {
        Connecting,   // transport still attaching
        WaitingSetup, // client connected, waiting for the host to describe the match
        Running,      // both simulations live
        Desynced,     // the two boards disagree — halted on purpose
        Ended,        // the other player left, or the transport closed cleanly
        Failed        // connection or protocol error; see StatusDetail
    }

    /// <summary>
    /// Plays one match across two machines, by the lockstep model: neither side sends board state,
    /// both send the commands their player issued and run the same simulation over them.
    ///
    /// <para><b>Why turn-based makes this far simpler than it usually is.</b> Lockstep normally needs
    /// tick synchronisation, because both players act continuously and the two machines must agree on
    /// what happened in which frame. Here only one player may legally act at a time, so there is
    /// nothing to interleave — a command is applied locally and sent, and the far machine applies it
    /// on arrival. TCP already guarantees the order.</para>
    ///
    /// <para><b>The one race that could exist, and why it cannot.</b> If both machines could advance
    /// the phase, the host might move to Combat while the client's Pass was still in flight; the host
    /// would then reject that Pass while the client had already applied it, and the boards would part
    /// company. So the clock belongs to the host alone (see OwnsClock). And the host cannot advance
    /// early even by accident, because the Action phase only reports itself complete after two passes
    /// — the second of which the host must already have received and applied.</para>
    ///
    /// <para><b>Trust model:</b> both machines run the whole simulation, so each holds the other's
    /// hand in memory. It is hidden from the UI, not from someone with a debugger. That was an
    /// accepted trade for friends playing each other; a game between strangers would need an
    /// authoritative host that sends each client only what it is allowed to see.</para>
    /// </summary>
    public sealed class LockstepMatchDriver : IMatchDriver, IDisposable
    {
        private readonly IMatchTransport _transport;
        private readonly ICardCatalog _catalog;
        private readonly bool _isHost;
        private readonly MatchSetup _hostSetup;

        private MatchSession _session;
        private bool _setupSent;
        private int _lastSyncedRound;
        private string _detail;

        public LockstepStatus Status { get; private set; }
        public MatchSession Session => _session;
        public bool IsReady => Status == LockstepStatus.Running && _session != null;
        public bool OwnsClock => _isHost;
        public string StatusDetail => _detail;

        /// <summary>The side this machine plays. The host is always A — an arbitrary but fixed rule, so neither machine has to negotiate it.</summary>
        public PlayerSide LocalSide => _isHost ? PlayerSide.A : PlayerSide.B;

        /// <summary>Set when a desync is detected, naming the first line on which the two boards disagree. The best (often only) clue you get.</summary>
        public string DesyncReport { get; private set; }

        private LockstepMatchDriver(IMatchTransport transport, ICardCatalog catalog, bool isHost, MatchSetup setup)
        {
            _transport = transport;
            _catalog = catalog;
            _isHost = isHost;
            _hostSetup = setup;
            Status = LockstepStatus.Connecting;
        }

        /// <summary>Hosts a match. This machine plays A, decides the entire setup, and owns the clock.</summary>
        public static LockstepMatchDriver Host(IMatchTransport transport, ICardCatalog catalog, MatchSetup setup) =>
            new LockstepMatchDriver(transport, catalog, true, setup ?? throw new ArgumentNullException(nameof(setup)));

        /// <summary>Joins a match. This machine plays B and adopts whatever setup the host sends.</summary>
        public static LockstepMatchDriver Join(IMatchTransport transport, ICardCatalog catalog) =>
            new LockstepMatchDriver(transport, catalog, false, null);

        public void Pump()
        {
            if (Status == LockstepStatus.Desynced || Status == LockstepStatus.Failed || Status == LockstepStatus.Ended)
                return;

            if (!FollowTransport()) return;

            // The host describes the match as soon as the pipe is up; the client can do nothing until
            // that arrives, which is why it is sent before either side looks at a command.
            if (_isHost && !_setupSent)
            {
                if (!_transport.Send(_hostSetup.Encode())) return;
                _setupSent = true;
                StartMatch(_hostSetup);
            }

            while (_transport.TryReceive(out string line))
                if (!Receive(line)) return;

            if (_isHost) SyncIfRoundChanged();
        }

        /// <summary>Mirrors the transport's state into ours. False means there is nothing further to do this frame.</summary>
        private bool FollowTransport()
        {
            switch (_transport.Status)
            {
                case TransportStatus.Failed:
                    Fail(_transport.StatusDetail ?? "The connection failed.");
                    return false;

                case TransportStatus.Closed:
                    // Anything still queued is drained first: the last thing a leaving player sent is
                    // often the most interesting, and discarding it would lose the end of the match.
                    while (_transport.TryReceive(out string pending)) Receive(pending);
                    Status = LockstepStatus.Ended;
                    _detail = _transport.StatusDetail ?? "The other player left.";
                    return false;

                case TransportStatus.Connected:
                    if (Status == LockstepStatus.Connecting)
                        Status = _isHost ? LockstepStatus.Connecting : LockstepStatus.WaitingSetup;
                    return true;

                default:
                    return false; // still listening or dialling
            }
        }

        private bool Receive(string line)
        {
            if (MatchSetup.IsSetupLine(line)) return ReceiveSetup(line);
            if (SyncMessage.IsSyncLine(line)) return ReceiveSync(line);
            return ReceiveCommand(line);
        }

        private bool ReceiveSetup(string line)
        {
            // A host that is sent a setup is talking to another host — both sides would deal their own
            // decks and nothing would line up. Better to say so than to limp on.
            if (_isHost) { Fail("The other machine is also hosting. One side must join instead."); return false; }
            if (_session != null) { Fail("A second match setup arrived mid-match."); return false; }

            if (!MatchSetup.TryDecode(line, out var setup, out string error)) { Fail(error); return false; }

            return StartMatch(setup);
        }

        private bool StartMatch(MatchSetup setup)
        {
            var deckA = Resolve(setup.DeckA, out string missing);
            var deckB = missing == null ? Resolve(setup.DeckB, out missing) : null;

            if (missing != null)
            {
                Fail($"This build has no card called \"{missing}\". The two machines are running different card databases.");
                return false;
            }

            var board = new HexGrid(setup.Columns, setup.Rows, setup.CornerCut);
            var match = new MatchState(board, deckA, deckB, setup.SoulMaxHp, new Random(setup.Seed));

            // Only this machine's own side is local, which is what makes MatchSession refuse to act
            // for the opponent and refuse to show their hand — the same seam that powers hot-seat,
            // pointed at one player instead of two.
            _session = new MatchSession(match, new[] { LocalSide }, privateHands: true);

            match.StartMatch();
            match.Rounds.AdvancePhase(); // RoundStart -> Action, identically on both machines

            _lastSyncedRound = match.Rounds.RoundNumber;
            Status = LockstepStatus.Running;
            _detail = null;
            return true;
        }

        private List<IPlayableCard> Resolve(IReadOnlyList<string> ids, out string missing)
        {
            var cards = new List<IPlayableCard>(ids.Count);
            foreach (var id in ids)
            {
                var card = _catalog.Find(id);
                if (card == null) { missing = id; return null; }
                cards.Add(card);
            }
            missing = null;
            return cards;
        }

        private bool ReceiveCommand(string line)
        {
            if (_session == null) { Fail("A command arrived before the match setup."); return false; }

            if (!MatchCommandCodec.TryDecode(line, out var command, out string error)) { Fail(error); return false; }

            // Applied as a REMOTE move: it already happened on the far machine, so it must not be
            // judged against this machine's idea of who is sitting here — only against the rules,
            // which still run in full. Without this the opponent's every legal move is rejected for
            // being the opponent's.
            var result = MatchCommandExecutor.Execute(_session, _catalog, command, MatchCommandOrigin.Remote);
            if (result.Applied) return true;

            // The far machine accepted this command or it would never have been sent. If we refuse it,
            // the two simulations have already diverged — reporting it here is far better than
            // silently dropping it and drifting further apart.
            Desync($"The other player's move was rejected here: {command} — {result.Reason}");
            return false;
        }

        private bool ReceiveSync(string line)
        {
            if (!SyncMessage.TryDecode(line, out var sync, out string error)) { Fail(error); return false; }
            if (_session == null) { Fail("A sync arrived before the match setup."); return false; }

            string mine = MatchFingerprint.Of(_session.Match);
            string difference = SyncMessage.FirstDifference(mine, sync.Fingerprint);
            if (difference == null) return true;

            Desync($"The two boards disagree at the start of round {sync.Round}.\n{difference}");
            return false;
        }

        /// <summary>Host only: publish our board whenever a new round has begun, so the client can check it still agrees.</summary>
        private void SyncIfRoundChanged()
        {
            if (_session == null) return;

            int round = _session.Match.Rounds.RoundNumber;
            if (round == _lastSyncedRound) return;

            _lastSyncedRound = round;
            _transport.Send(new SyncMessage(round, MatchFingerprint.Of(_session.Match)).Encode());
        }

        public MatchCommandResult Submit(MatchCommand command)
        {
            if (Status != LockstepStatus.Running || _session == null)
                return MatchCommandResult.Refused(_detail ?? "The match isn't running yet.");

            bool isClock = command.Type == MatchCommandType.AdvancePhase || command.Type == MatchCommandType.BeginNextRound;
            if (isClock && !OwnsClock)
                return MatchCommandResult.Refused("Only the host advances the round.");

            // Applied here first so a move the rules refuse never reaches the network at all — the far
            // machine must only ever see commands that actually happened.
            var result = MatchCommandExecutor.Execute(_session, _catalog, command);
            if (!result.Applied) return result;

            if (!_transport.Send(MatchCommandCodec.Encode(command)))
            {
                // Locally applied but not delivered: the boards are now different and no later message
                // can repair it. Stopping immediately is the honest outcome.
                Desync("A move could not be sent to the other player, so the two boards no longer match.");
                return MatchCommandResult.Refused(_detail);
            }

            if (_isHost) SyncIfRoundChanged();
            return result;
        }

        private void Fail(string detail)
        {
            Status = LockstepStatus.Failed;
            _detail = detail;
        }

        private void Desync(string report)
        {
            Status = LockstepStatus.Desynced;
            DesyncReport = report;
            _detail = "The two games have drifted apart and cannot continue. " + report;
        }

        public void Dispose() => _transport?.Dispose();
    }
}
