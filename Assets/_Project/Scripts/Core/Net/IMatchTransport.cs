using System;

namespace ArcanaWars.Core.Net
{
    public enum TransportStatus
    {
        Idle,       // created, not started
        Listening,  // host, waiting for the other player to connect
        Connecting, // client, dialling the host
        Connected,  // both ends attached; messages can flow
        Failed,     // something went wrong — see StatusDetail
        Closed      // the other end hung up, or we did
    }

    /// <summary>
    /// A two-party pipe that carries lines of text, and nothing else.
    ///
    /// <para><b>Why the interface is this small.</b> Everything the game needs to say already
    /// serialises to one line (see MatchCommandCodec), so a transport's entire job is "get this
    /// string to the other machine, in order, without losing it". Keeping the contract at that level
    /// means the game layer never learns what a socket is, and swapping TCP for Unity Transport plus
    /// a relay later — which is what internet play will need — changes one class and nothing else.
    /// This is the same seam trick as MatchSession: the expensive part of networking is the code that
    /// assumes a transport, not the transport.</para>
    ///
    /// <para><b>Receiving is polled, not evented, on purpose.</b> A socket delivers on a background
    /// thread, and Unity forbids touching game state from one. Draining a queue from Update keeps
    /// every mutation on the main thread without a single lock in the game layer.</para>
    /// </summary>
    public interface IMatchTransport : IDisposable
    {
        TransportStatus Status { get; }

        /// <summary>Human-readable explanation for Failed/Closed, or null. Shown to the player, so it has to be a sentence rather than an exception dump.</summary>
        string StatusDetail { get; }

        /// <summary>Queues one line for delivery. False if the pipe isn't connected — never throws, because a dropped connection is an ordinary event, not an exceptional one.</summary>
        bool Send(string line);

        /// <summary>Takes the next received line, oldest first. False when nothing is waiting. Call it until it returns false.</summary>
        bool TryReceive(out string line);
    }
}
