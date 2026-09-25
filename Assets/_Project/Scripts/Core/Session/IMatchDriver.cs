using ArcanaWars.Core.Cards;

namespace ArcanaWars.Core.Session
{
    /// <summary>
    /// Where a match's commands go. The one thing that differs between playing on this device and
    /// playing against someone across a network — and therefore the one thing the UI should have to
    /// know about.
    ///
    /// <para>MatchSession already answers "may this player act, and what may they see". This answers
    /// the question after it: when they do act, does the command simply run, or does it also have to
    /// reach another machine — and are there commands arriving from elsewhere that must be applied?
    /// Keeping those two concerns apart is what stops the networked case leaking into every screen.</para>
    ///
    /// <para>A driver is never a rules authority. It routes commands; MatchSession and the Core
    /// resolvers decide whether they are allowed.</para>
    /// </summary>
    public interface IMatchDriver
    {
        /// <summary>The live session, or null while a networked match is still connecting or waiting for setup.</summary>
        MatchSession Session { get; }

        /// <summary>True once Session exists and commands can be submitted. A UI shows a connection screen until this is true.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Whether this machine may issue the clock commands (AdvancePhase, BeginNextRound).
        ///
        /// Exactly one machine must own them, or both would advance the phase and the two simulations
        /// would run the round twice on one side. Locally that is trivially this machine; over a
        /// network it is the host, and the client simply waits for the clock to arrive.
        /// </summary>
        bool OwnsClock { get; }

        /// <summary>What went wrong, or what is being waited for. Shown to the player; null when there is nothing to say.</summary>
        string StatusDetail { get; }

        /// <summary>Runs a command on behalf of the local player, and sends it on if anyone else needs to see it.</summary>
        MatchCommandResult Submit(MatchCommand command);

        /// <summary>Called every frame. Applies anything that arrived from elsewhere. A no-op for a local match.</summary>
        void Pump();
    }

    /// <summary>
    /// Both players on this device. Submitting a command just runs it; nothing arrives from anywhere,
    /// so Pump does nothing and this machine owns the clock by default.
    /// </summary>
    public sealed class LocalMatchDriver : IMatchDriver
    {
        private readonly ICardCatalog _catalog;

        public MatchSession Session { get; }
        public bool IsReady => true;
        public bool OwnsClock => true;
        public string StatusDetail => null;

        public LocalMatchDriver(MatchSession session, ICardCatalog catalog)
        {
            Session = session;
            _catalog = catalog;
        }

        public MatchCommandResult Submit(MatchCommand command) =>
            MatchCommandExecutor.Execute(Session, _catalog, command);

        public void Pump() { }
    }
}
