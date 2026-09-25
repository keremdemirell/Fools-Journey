using System;
using System.Globalization;
using ArcanaWars.Core.Match;

namespace ArcanaWars.Core.Session
{
    /// <summary>
    /// Turns a MatchCommand into a line of text and back. This is the wire format for lockstep play:
    /// the thing that actually travels between two machines.
    ///
    /// <para><b>Text, not binary, and that is a deliberate trade.</b> A turn-based 1v1 sends a
    /// handful of tiny messages per round, so bandwidth is irrelevant, and in exchange every message
    /// can be read in a log, pasted into a bug report, or diffed by eye when two machines disagree.
    /// Desync bugs are the hardest class of bug in networked games; anything that makes the traffic
    /// legible pays for itself many times over.</para>
    ///
    /// <para><b>The version prefix is not ceremony.</b> Under lockstep both machines must agree
    /// exactly on what a command means. Two builds with different card rules produce different
    /// simulations from identical commands, and the symptom is a board that silently drifts apart
    /// rather than an error. Refusing to decode an unknown version turns that into a loud failure at
    /// connection time, which is the only good moment for it to happen.</para>
    ///
    /// <para>This deliberately does NOT cover match setup — the seed, the two deck lists, and which
    /// player is which. Those have to be agreed before the first command, because two simulations
    /// starting from different decks will diverge no matter how perfect the command stream is. That
    /// handshake belongs with whatever transport is chosen.</para>
    /// </summary>
    public static class MatchCommandCodec
    {
        /// <summary>Bump this whenever the format or the meaning of a field changes.</summary>
        public const int Version = 1;

        private const char Separator = '|';
        private const string Null = "-";

        public static string Encode(MatchCommand command)
        {
            // Card ids are lowercase alphanumeric with underscores, so the separator cannot appear in
            // one. Checked rather than assumed: a card id that broke the format would produce a
            // command that decodes to something plausible but wrong, which is far worse than a throw.
            if (command.CardId != null && command.CardId.IndexOf(Separator) >= 0)
                throw new ArgumentException($"Card id \"{command.CardId}\" contains the field separator '{Separator}'.");

            return string.Join(Separator.ToString(), new[]
            {
                Version.ToString(CultureInfo.InvariantCulture),
                command.Type.ToString(),
                command.Side.ToString(),
                command.CardId ?? Null,
                Int(command.AnchorQ),
                Int(command.AnchorR),
                Int(command.AttackColumn),
                Int(command.ManaCommitted),
                command.ChosenElement.HasValue ? command.ChosenElement.Value.ToString() : Null,
                command.TargetUnitId.HasValue ? Int(command.TargetUnitId.Value) : Null,
                Int(command.JudgementId),
                command.Verdict.ToString()
            });
        }

        public static bool TryDecode(string line, out MatchCommand command, out string error)
        {
            command = default;

            if (string.IsNullOrWhiteSpace(line))
            {
                error = "Empty command.";
                return false;
            }

            var parts = line.Split(Separator);
            if (parts.Length != 12)
            {
                error = $"Expected 12 fields, got {parts.Length}.";
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
            {
                error = $"Unreadable version \"{parts[0]}\".";
                return false;
            }

            if (version != Version)
            {
                error = $"Command version {version} does not match this build's {Version}. " +
                        "The two machines are running different builds and would simulate differently.";
                return false;
            }

            if (!TryParseEnum(parts[1], out MatchCommandType type)) { error = $"Unknown command type \"{parts[1]}\"."; return false; }
            if (!TryParseEnum(parts[2], out PlayerSide side)) { error = $"Unknown player side \"{parts[2]}\"."; return false; }

            string cardId = parts[3] == Null ? null : parts[3];

            if (!TryInt(parts[4], out int anchorQ)) { error = $"Unreadable anchor Q \"{parts[4]}\"."; return false; }
            if (!TryInt(parts[5], out int anchorR)) { error = $"Unreadable anchor R \"{parts[5]}\"."; return false; }
            if (!TryInt(parts[6], out int column)) { error = $"Unreadable attack column \"{parts[6]}\"."; return false; }
            if (!TryInt(parts[7], out int mana)) { error = $"Unreadable mana \"{parts[7]}\"."; return false; }

            Element? element = null;
            if (parts[8] != Null)
            {
                if (!TryParseEnum(parts[8], out Element parsed)) { error = $"Unknown element \"{parts[8]}\"."; return false; }
                element = parsed;
            }

            int? target = null;
            if (parts[9] != Null)
            {
                if (!TryInt(parts[9], out int parsed)) { error = $"Unreadable target id \"{parts[9]}\"."; return false; }
                target = parsed;
            }

            if (!TryInt(parts[10], out int judgementId)) { error = $"Unreadable judgement id \"{parts[10]}\"."; return false; }
            if (!TryParseEnum(parts[11], out JudgementVerdict verdict)) { error = $"Unknown verdict \"{parts[11]}\"."; return false; }

            switch (type)
            {
                case MatchCommandType.PlayCard:
                    if (cardId == null) { error = "A PlayCard command carries no card id."; return false; }
                    command = MatchCommand.PlayCard(side, cardId, new Board.HexCoord(anchorQ, anchorR), column, mana, new Cards.CardPlayOptions(element, target));
                    break;

                case MatchCommandType.Pass: command = MatchCommand.Pass(side); break;
                case MatchCommandType.AdvancePhase: command = MatchCommand.AdvancePhase(); break;
                case MatchCommandType.BeginNextRound: command = MatchCommand.BeginNextRound(); break;
                case MatchCommandType.ResolveJudgement: command = MatchCommand.ResolveJudgement(side, judgementId, verdict); break;

                default: error = $"Unhandled command type {type}."; return false;
            }

            error = null;
            return true;
        }

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static bool TryInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// Names only — never the underlying number.
        ///
        /// Enum.TryParse happily accepts "3" and, because Element genuinely has a member numbered 3,
        /// Enum.IsDefined then waves it through. That would give the format two spellings for the
        /// same value and, worse, would let a corrupted or truncated field land on a real enum member
        /// instead of failing. Over a wire the only safe behaviour is to refuse anything that isn't
        /// the exact name, so a garbled message is a loud error rather than a plausible wrong move.
        /// </summary>
        private static bool TryParseEnum<T>(string text, out T value) where T : struct
        {
            value = default;
            if (string.IsNullOrEmpty(text)) return false;

            char first = text[0];
            if (char.IsDigit(first) || first == '-' || first == '+') return false;

            if (!Enum.TryParse(text, out T parsed)) return false;
            if (!Enum.IsDefined(typeof(T), parsed)) return false;

            value = parsed;
            return true;
        }
    }
}
