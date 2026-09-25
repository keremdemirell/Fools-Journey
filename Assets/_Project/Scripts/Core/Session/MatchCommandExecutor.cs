using ArcanaWars.Core.Cards;

namespace ArcanaWars.Core.Session
{
    /// <summary>Whether a command was applied, and if not, why. Replay treats a refusal as a desync, so the reason has to be legible.</summary>
    public readonly struct MatchCommandResult
    {
        public readonly bool Applied;
        public readonly string Reason;

        private MatchCommandResult(bool applied, string reason)
        {
            Applied = applied;
            Reason = reason;
        }

        public static readonly MatchCommandResult Ok = new MatchCommandResult(true, null);
        public static MatchCommandResult Refused(string reason) => new MatchCommandResult(false, reason);
    }

    /// <summary>
    /// Applies a MatchCommand to a MatchSession. This is the seam between "a player did something"
    /// and "the simulation changed", and it is the only thing a network transport will need to call:
    /// receive bytes, decode a MatchCommand, hand it here.
    ///
    /// It is deliberately a thin translator with no rules of its own. Every check that matters —
    /// whose turn it is, whether a card is affordable, whether a placement is legal — already lives
    /// in MatchSession and the Core resolvers, and re-checking here would create a second opinion
    /// that could disagree with the first.
    ///
    /// <para>The catalog is required because a command names a card by id (see MatchCommand). An
    /// unknown id is refused rather than ignored: under lockstep a command the two machines disagree
    /// about is precisely the thing that must be loud.</para>
    /// </summary>
    /// <summary>Where a command came from. It changes which gate the command passes through, and nothing else — see MatchSession's remote-move section.</summary>
    public enum MatchCommandOrigin
    {
        /// <summary>A person acting at this machine. Must satisfy every check, including whether they are allowed to drive that side at all.</summary>
        Local,

        /// <summary>A move that already happened on another machine. Still subject to the game's rules, but not to this machine's notion of who is sitting here.</summary>
        Remote
    }

    public static class MatchCommandExecutor
    {
        public static MatchCommandResult Execute(MatchSession session, ICardCatalog catalog, MatchCommand command, MatchCommandOrigin origin = MatchCommandOrigin.Local)
        {
            if (session == null) return MatchCommandResult.Refused("No session.");

            bool remote = origin == MatchCommandOrigin.Remote;

            switch (command.Type)
            {
                case MatchCommandType.PlayCard:
                {
                    if (catalog == null) return MatchCommandResult.Refused("No card catalog to resolve the card id against.");

                    var card = catalog.Find(command.CardId);
                    if (card == null) return MatchCommandResult.Refused($"Unknown card id \"{command.CardId}\".");

                    var result = remote
                        ? session.ApplyRemotePlay(command.Side, card, command.Anchor, command.AttackColumn, command.ManaCommitted, command.Options)
                        : session.SubmitPlay(command.Side, card, command.Anchor, command.AttackColumn, command.ManaCommitted, command.Options);

                    return result.Success
                        ? MatchCommandResult.Ok
                        : MatchCommandResult.Refused(result.Reason ?? CardPlayFailureText.Describe(result.Failure));
                }

                case MatchCommandType.Pass:
                {
                    bool passed = remote
                        ? session.ApplyRemotePass(command.Side, out string passReason)
                        : session.SubmitPass(command.Side, out passReason);

                    return passed ? MatchCommandResult.Ok : MatchCommandResult.Refused(passReason);
                }

                case MatchCommandType.AdvancePhase:
                    session.AdvancePhase();
                    return MatchCommandResult.Ok;

                case MatchCommandType.BeginNextRound:
                    session.BeginNextRound();
                    return MatchCommandResult.Ok;

                case MatchCommandType.ResolveJudgement:
                {
                    bool ruled = remote
                        ? session.ApplyRemoteJudgement(command.JudgementId, command.Verdict)
                        : session.SubmitJudgement(command.Side, command.JudgementId, command.Verdict);

                    return ruled
                        ? MatchCommandResult.Ok
                        : MatchCommandResult.Refused($"Judgement #{command.JudgementId} could not be resolved.");
                }

                default:
                    return MatchCommandResult.Refused($"Unhandled command type {command.Type}.");
            }
        }
    }
}
