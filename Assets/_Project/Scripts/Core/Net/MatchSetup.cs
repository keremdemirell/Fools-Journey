using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArcanaWars.Core.Net
{
    /// <summary>
    /// Everything two machines must agree on before the first command is played: the random seed,
    /// the board shape, both Souls' starting HP, and — above all — the exact contents and order of
    /// both decks.
    ///
    /// <para><b>Why the host dictates all of it rather than both sides agreeing.</b> Lockstep only
    /// works if the two simulations begin identical. Anything either machine decides for itself is a
    /// chance to differ: a locally-shuffled deck, a locally-seeded RNG, a board built from a
    /// different Inspector value. So the host builds this once, sends it, and the client adopts it
    /// wholesale. The client's own deck selection is deliberately overridden — which sounds wrong
    /// until you notice the alternative is two different games that look the same for a few rounds.
    /// (When the client should get to pick its own deck, the fix is for the host to ask for the deck
    /// list and fold it into this before sending, not for the client to substitute one later.)</para>
    ///
    /// <para>Decks are carried as fully expanded, ordered card ids rather than as a deck recipe, so
    /// neither machine has to rebuild a list from counts and agree on the order it came out in.
    /// Both sides then shuffle that identical list with the identical seed inside StartMatch, and
    /// arrive at the identical draw pile — which the replay tests already prove, since every one of
    /// them goes through StartMatch and its shuffle. 90 short ids is nothing to send once.</para>
    /// </summary>
    public sealed class MatchSetup
    {
        public const int Version = 1;

        /// <summary>Marks a setup line so a reader can tell it apart from a command without guessing.</summary>
        public const string Prefix = "setup";

        public int Seed { get; }
        public int Columns { get; }
        public int Rows { get; }
        public int CornerCut { get; }
        public int SoulMaxHp { get; }

        /// <summary>Player A's 45 cards, expanded and in order, before shuffling. The client builds its deck from exactly this list and shuffles it with the shared seed, landing on the same draw pile.</summary>
        public IReadOnlyList<string> DeckA { get; }
        public IReadOnlyList<string> DeckB { get; }

        public MatchSetup(int seed, int columns, int rows, int cornerCut, int soulMaxHp, IReadOnlyList<string> deckA, IReadOnlyList<string> deckB)
        {
            Seed = seed;
            Columns = columns;
            Rows = rows;
            CornerCut = cornerCut;
            SoulMaxHp = soulMaxHp;
            DeckA = deckA ?? Array.Empty<string>();
            DeckB = deckB ?? Array.Empty<string>();
        }

        public static bool IsSetupLine(string line) =>
            line != null && line.StartsWith(Prefix + "|", StringComparison.Ordinal);

        public string Encode()
        {
            return string.Join("|", new[]
            {
                Prefix,
                Version.ToString(CultureInfo.InvariantCulture),
                Seed.ToString(CultureInfo.InvariantCulture),
                Columns.ToString(CultureInfo.InvariantCulture),
                Rows.ToString(CultureInfo.InvariantCulture),
                CornerCut.ToString(CultureInfo.InvariantCulture),
                SoulMaxHp.ToString(CultureInfo.InvariantCulture),
                string.Join(",", DeckA),
                string.Join(",", DeckB)
            });
        }

        public static bool TryDecode(string line, out MatchSetup setup, out string error)
        {
            setup = null;

            if (!IsSetupLine(line)) { error = "Not a setup line."; return false; }

            var parts = line.Split('|');
            if (parts.Length != 9) { error = $"Expected 9 setup fields, got {parts.Length}."; return false; }

            if (!TryInt(parts[1], out int version)) { error = $"Unreadable setup version \"{parts[1]}\"."; return false; }
            if (version != Version)
            {
                error = $"Setup version {version} does not match this build's {Version}. " +
                        "The two machines are running different builds and would simulate differently.";
                return false;
            }

            if (!TryInt(parts[2], out int seed)) { error = "Unreadable seed."; return false; }
            if (!TryInt(parts[3], out int columns)) { error = "Unreadable column count."; return false; }
            if (!TryInt(parts[4], out int rows)) { error = "Unreadable row count."; return false; }
            if (!TryInt(parts[5], out int cornerCut)) { error = "Unreadable corner cut."; return false; }
            if (!TryInt(parts[6], out int soulHp)) { error = "Unreadable Soul HP."; return false; }

            var deckA = SplitDeck(parts[7]);
            var deckB = SplitDeck(parts[8]);

            if (deckA.Count == 0 || deckB.Count == 0) { error = "A setup carried an empty deck."; return false; }

            setup = new MatchSetup(seed, columns, rows, cornerCut, soulHp, deckA, deckB);
            error = null;
            return true;
        }

        private static List<string> SplitDeck(string field)
        {
            var cards = new List<string>();
            if (string.IsNullOrEmpty(field)) return cards;

            foreach (var id in field.Split(','))
                if (!string.IsNullOrEmpty(id)) cards.Add(id);

            return cards;
        }

        private static bool TryInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
