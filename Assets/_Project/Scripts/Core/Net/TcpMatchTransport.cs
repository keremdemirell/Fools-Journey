using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ArcanaWars.Core.Net
{
    /// <summary>
    /// An IMatchTransport over a plain TCP connection: one host, one client, one socket.
    ///
    /// <para><b>Why plain TCP is the right tool here rather than a shortcut.</b> Lockstep needs
    /// exactly what TCP already guarantees — reliable, ordered delivery — and a turn-based 1v1 sends
    /// a handful of tiny messages per round, so none of the reasons games normally reach for UDP
    /// apply. The payoff is that this class is ordinary C# with no engine dependency, which means the
    /// offline harness can stand up a real connection between two of these and test the whole thing
    /// without Unity ever being involved.</para>
    ///
    /// <para><b>Framing:</b> messages are newline-delimited. TCP is a byte stream with no concept of
    /// a message, so something has to mark the boundaries, and a newline is safe here because every
    /// field the codecs emit is an integer, an enum name, or a card id — none can contain one.</para>
    ///
    /// <para><b>Threading:</b> connecting and reading both block, so they live on one background
    /// thread that only ever appends to a concurrent queue. Nothing here touches game state, and the
    /// game drains that queue from its own thread. That single rule is what keeps Unity's
    /// main-thread-only requirement satisfied without locks in the game layer.</para>
    ///
    /// <para><b>Reach:</b> this is a LAN transport. It connects two machines on the same network by
    /// address; it does not traverse a home router from outside. Internet play means a second
    /// IMatchTransport over a relay, which is why the interface exists.</para>
    /// </summary>
    public sealed class TcpMatchTransport : IMatchTransport
    {
        /// <summary>Arbitrary but memorable, and well clear of the registered range.</summary>
        public const int DefaultPort = 47820;

        /// <summary>
        /// How long to wait for a host to answer before giving up.
        ///
        /// Needed because the operating system's own patience is both long and wildly inconsistent:
        /// a refused connection to "localhost" takes about 4 seconds on Windows (it tries IPv6 ::1
        /// first, then falls back to IPv4), while an address on an unreachable subnet — exactly what
        /// a typo produces — can hang for twenty or more. Without a bound, a mistyped address leaves
        /// the player watching "Connecting..." with no idea anything is wrong.
        /// </summary>
        public const int ConnectTimeoutMs = 10000;

        private readonly ConcurrentQueue<string> _inbound = new ConcurrentQueue<string>();
        private readonly object _writeLock = new object();

        private readonly bool _isHost;
        private readonly string _remoteAddress;
        private readonly int _port;

        private TcpListener _listener;
        private TcpClient _client;
        private StreamWriter _writer;
        private Thread _thread;

        private volatile int _status = (int)TransportStatus.Idle;
        private volatile string _detail;
        private volatile bool _disposed;

        public TransportStatus Status => (TransportStatus)_status;
        public string StatusDetail => _detail;

        private TcpMatchTransport(bool isHost, string remoteAddress, int port)
        {
            _isHost = isHost;
            _remoteAddress = remoteAddress;
            _port = port;
        }

        /// <summary>Waits for the other player to connect. The host also decides the match setup — see MatchSetup for why one side must.</summary>
        public static TcpMatchTransport Host(int port = DefaultPort)
        {
            var transport = new TcpMatchTransport(true, null, port);
            transport.Start();
            return transport;
        }

        /// <summary>Connects to a host by address ("192.168.1.42", or "localhost" for two instances on one machine).</summary>
        public static TcpMatchTransport Join(string address, int port = DefaultPort)
        {
            var transport = new TcpMatchTransport(false, address, port);
            transport.Start();
            return transport;
        }

        private void Start()
        {
            _status = (int)(_isHost ? TransportStatus.Listening : TransportStatus.Connecting);
            _thread = new Thread(Run) { IsBackground = true, Name = "ArcanaWars TCP" };
            _thread.Start();
        }

        private void Run()
        {
            try
            {
                if (_isHost)
                {
                    _listener = new TcpListener(IPAddress.Any, _port);
                    _listener.Start();
                    _client = _listener.AcceptTcpClient();

                    // One opponent only. Releasing the port immediately means a second attempt to
                    // connect is refused outright rather than hanging half-open forever.
                    _listener.Stop();
                    _listener = null;
                }
                else
                {
                    _client = new TcpClient();

                    // Bounded rather than a plain blocking Connect — see ConnectTimeoutMs. The socket
                    // is closed on timeout so the underlying attempt is abandoned rather than left
                    // running behind a transport that has already reported failure.
                    var attempt = _client.ConnectAsync(_remoteAddress, _port);
                    if (!attempt.Wait(ConnectTimeoutMs))
                    {
                        try { _client.Close(); } catch { }
                        if (_disposed) return;

                        _detail = $"No answer from {_remoteAddress}:{_port} after {ConnectTimeoutMs / 1000} seconds. Check the address and that the host has started a game.";
                        _status = (int)TransportStatus.Failed;
                        return;
                    }
                }

                // Turn-based traffic is a few small messages a round, so Nagle's algorithm would sit
                // on each one waiting for company that never arrives. Latency matters, volume doesn't.
                _client.NoDelay = true;

                var stream = _client.GetStream();
                lock (_writeLock)
                {
                    _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                }

                _status = (int)TransportStatus.Connected;

                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                {
                    string line;
                    while (!_disposed && (line = reader.ReadLine()) != null)
                        _inbound.Enqueue(line);
                }

                if (!_disposed)
                {
                    _detail = "The other player disconnected.";
                    _status = (int)TransportStatus.Closed;
                }
            }
            catch (Exception ex)
            {
                // Disposing deliberately breaks the blocking accept/read, so the exception that
                // follows is expected rather than a failure worth reporting to the player.
                if (_disposed) return;

                _detail = Describe(ex);
                _status = (int)TransportStatus.Failed;
            }
        }

        private string Describe(Exception ex)
        {
            // ConnectAsync reports through Task.Wait, which wraps the real cause. Without unwrapping,
            // every connection failure would surface as "One or more errors occurred" instead of
            // something the player can act on.
            if (ex is AggregateException aggregate && aggregate.InnerException != null)
                ex = aggregate.InnerException;

            if (ex is SocketException socket)
            {
                switch (socket.SocketErrorCode)
                {
                    case SocketError.ConnectionRefused:
                        return $"No game is hosting at {_remoteAddress}:{_port}. Check the host has started and the address is right.";
                    case SocketError.TimedOut:
                    case SocketError.HostUnreachable:
                    case SocketError.NetworkUnreachable:
                        return $"Could not reach {_remoteAddress}:{_port}. Are both machines on the same network?";
                    case SocketError.AddressAlreadyInUse:
                        return $"Port {_port} is already in use on this machine — another copy may still be hosting.";
                    case SocketError.ConnectionReset:
                        return "The other player disconnected.";
                }
            }

            return ex.Message;
        }

        public bool Send(string line)
        {
            if (_disposed || Status != TransportStatus.Connected || line == null) return false;

            try
            {
                lock (_writeLock)
                {
                    if (_writer == null) return false;
                    _writer.WriteLine(line);
                }
                return true;
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    _detail = Describe(ex);
                    _status = (int)TransportStatus.Failed;
                }
                return false;
            }
        }

        public bool TryReceive(out string line) => _inbound.TryDequeue(out line);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _status = (int)TransportStatus.Closed;

            // Closing the socket is what unblocks the background thread, which is otherwise parked
            // inside AcceptTcpClient or ReadLine and would never notice it had been asked to stop.
            try { _listener?.Stop(); } catch { }
            try { _client?.Close(); } catch { }

            lock (_writeLock) { _writer = null; }

            try { _thread?.Join(500); } catch { }
        }
    }
}
