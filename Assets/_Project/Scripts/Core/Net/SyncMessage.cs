using System;
using System.Globalization;
using System.Text;

namespace ArcanaWars.Core.Net
{
    /// <summary>
    /// The host's view of the board at a round boundary, sent so the client can check that its own
    /// simulation still agrees.
    ///
    /// <para><b>Why bother when the commands are already reliable and ordered.</b> Lockstep's failure
    /// mode is not a lost message — TCP handles that — it is the two simulations quietly computing
    /// different answers from the same input, because of a stray RNG draw, an unordered iteration, or
    /// two builds with subtly different rules. Nothing alerts you. The boards simply drift, and the
    /// first symptom is a player insisting their unit is alive while the other watches it die. This
    /// turns that into an immediate, specific error at the round it began.</para>
    ///
    /// <para><b>It carries the whole fingerprint, not a hash — deliberately.</b> A hash tells you
    /// that you have a desync; the full text tells you which line differs, which is the entire
    /// difference between a five-minute fix and a lost evening. The cost is a few kilobytes once per
    /// round, against a round that takes a human many seconds to play. On a LAN that is free.</para>
    ///
    /// <para>Both machines must fingerprint at the same instant for the comparison to mean anything.
    /// That instant is "just after a round begins": the host sends this immediately after applying
    /// its own BeginNextRound, and because TCP preserves order the client processes that same command
    /// first and this message second, with no opportunity to act in between.</para>
    /// </summary>
    public sealed class SyncMessage
    {
        public const string Prefix = "sync";

        public int Round { get; }
        public string Fingerprint { get; }

        public SyncMessage(int round, string fingerprint)
        {
            Round = round;
            Fingerprint = fingerprint ?? string.Empty;
        }

        public static bool IsSyncLine(string line) =>
            line != null && line.StartsWith(Prefix + "|", StringComparison.Ordinal);

        public string Encode() =>
            Prefix + "|" + Round.ToString(CultureInfo.InvariantCulture) + "|" + Escape(Fingerprint);

        public static bool TryDecode(string line, out SyncMessage message, out string error)
        {
            message = null;

            if (!IsSyncLine(line)) { error = "Not a sync line."; return false; }

            // Split on the first two separators only: the fingerprint is escaped but may still
            // legitimately contain '|' in future, and splitting the whole line would corrupt it.
            int firstBar = line.IndexOf('|');
            int secondBar = line.IndexOf('|', firstBar + 1);
            if (secondBar < 0) { error = "Malformed sync line."; return false; }

            string roundText = line.Substring(firstBar + 1, secondBar - firstBar - 1);
            if (!int.TryParse(roundText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int round))
            {
                error = $"Unreadable sync round \"{roundText}\".";
                return false;
            }

            message = new SyncMessage(round, Unescape(line.Substring(secondBar + 1)));
            error = null;
            return true;
        }

        /// <summary>
        /// A fingerprint is multi-line and the transport frames messages by newline, so the newlines
        /// have to go. Backslash is escaped first, or unescaping would turn a literal "\" followed by
        /// "n" back into a line break that was never there.
        /// </summary>
        private static string Escape(string text) =>
            text.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n");

        private static string Unescape(string text)
        {
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\\' || i + 1 >= text.Length) { sb.Append(text[i]); continue; }

                char next = text[++i];
                if (next == 'n') sb.Append('\n');
                else if (next == '\\') sb.Append('\\');
                else { sb.Append('\\'); sb.Append(next); } // unknown escape: leave it as it was
            }
            return sb.ToString();
        }

        /// <summary>The first line that differs between two fingerprints, formatted for a log, or null if they agree.</summary>
        public static string FirstDifference(string mine, string theirs)
        {
            var a = (mine ?? "").Split('\n');
            var b = (theirs ?? "").Split('\n');

            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                string la = i < a.Length ? a[i].TrimEnd() : "<missing>";
                string lb = i < b.Length ? b[i].TrimEnd() : "<missing>";
                if (la != lb) return $"line {i}\n  this machine: {la}\n  other machine: {lb}";
            }

            return null;
        }
    }
}
