using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using ArcanaWars.Core.Cards;

namespace ArcanaWars.Cards.Decks
{
    /// <summary>
    /// Saving and loading player-built decks as JSON files, one per deck, under the platform's
    /// persistent data folder.
    ///
    /// This is the half of persistence that works in a shipped game: unlike a DeckAsset, a file here
    /// can be written at runtime, on any platform, by a player who has no project folder. DeckAsset
    /// covers the other half (decks authored in the Editor and referenced in a scene). They share
    /// SerializedDeck, so the same deck can be saved either way.
    ///
    /// Everything here reports failure rather than throwing. Disk I/O fails for ordinary reasons a
    /// game has to survive — a full disk, a read-only folder, a file open in another program — and a
    /// deckbuilder should show "couldn't save" and let the player try again, not lose their deck to an
    /// unhandled exception.
    /// </summary>
    public static class DeckStorage
    {
        public const string FileExtension = ".deck.json";

        /// <summary>Where decks live. Resolved per call rather than cached because Application.persistentDataPath isn't valid before Unity finishes initialising.</summary>
        public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "Decks");

        /// <summary>The file a deck with this name would be saved to. Public so a UI can show the player where their deck went.</summary>
        public static string PathFor(string deckName) =>
            Path.Combine(DirectoryPath, SanitiseFileName(deckName) + FileExtension);

        public static bool Exists(string deckName) => File.Exists(PathFor(deckName));

        /// <summary>
        /// The names of every saved deck, for a load menu. Reads file names only — it does not parse
        /// the files, so a corrupt deck still appears in the list and fails at load time with a real
        /// message, rather than vanishing without explanation.
        /// </summary>
        public static List<string> SavedDeckNames()
        {
            var names = new List<string>();

            try
            {
                if (!Directory.Exists(DirectoryPath)) return names;

                foreach (var path in Directory.GetFiles(DirectoryPath, "*" + FileExtension))
                {
                    string file = Path.GetFileName(path);
                    names.Add(file.Substring(0, file.Length - FileExtension.Length));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"DeckStorage: couldn't list saved decks in {DirectoryPath} — {e.Message}");
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>
        /// Writes the deck, overwriting any deck saved under the same name. Saves whatever it is
        /// given, legal or not: half-built decks are the normal state of a deckbuilder, and refusing
        /// to save one would mean losing work every time a player stops partway. Legality is checked
        /// when a deck is used to start a match, not when it's written down.
        /// </summary>
        public static bool TrySave(DeckList deck, out string error)
        {
            error = null;

            if (deck == null)
            {
                error = "No deck to save.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(deck.Name))
            {
                error = "Give the deck a name first.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(PathFor(deck.Name), ToJson(deck), Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                error = $"Couldn't save \"{deck.Name}\": {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// Reads a saved deck, or returns null with a reason. Null covers both "no such file" and "the
        /// file is not a deck" — the caller gets the difference from the error text.
        /// </summary>
        public static DeckList TryLoad(string deckName, out string error)
        {
            error = null;
            string path = PathFor(deckName);

            try
            {
                if (!File.Exists(path))
                {
                    error = $"No saved deck named \"{deckName}\".";
                    return null;
                }

                var data = FromJson(File.ReadAllText(path, Encoding.UTF8));
                if (data == null)
                {
                    error = $"\"{deckName}\" isn't a readable deck file.";
                    return null;
                }

                var deck = data.ToDeckList();

                // The file name is authoritative over the name stored inside: a player who renames the
                // file on disk expects the deck to be called that, and it's the name the load menu
                // showed them.
                deck.Name = deckName;
                return deck;
            }
            catch (Exception e)
            {
                error = $"Couldn't read \"{deckName}\": {e.Message}";
                return null;
            }
        }

        public static bool TryDelete(string deckName, out string error)
        {
            error = null;

            try
            {
                string path = PathFor(deckName);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                error = $"Couldn't delete \"{deckName}\": {e.Message}";
                return false;
            }
        }

        /// <summary>Exposed separately from the file writing so a deck can also be copied to the clipboard, sent over a network, or embedded in a bug report.</summary>
        public static string ToJson(DeckList deck) => JsonUtility.ToJson(SerializedDeck.From(deck), prettyPrint: true);

        /// <summary>Null if the text isn't a deck. JsonUtility returns an object with default fields for unrelated JSON rather than failing, so the entries list is what actually tells us.</summary>
        public static SerializedDeck FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var data = JsonUtility.FromJson<SerializedDeck>(json);
                return data?.entries == null ? null : data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Turns a player-typed deck name into something safe to use as a file name. Deck names are
        /// free text, and free text reaching a file path is how you get a deck called "../../save"
        /// overwriting something it shouldn't — so every character that isn't plainly safe becomes an
        /// underscore, rather than filtering a blocklist of the ones known to be dangerous.
        /// </summary>
        public static string SanitiseFileName(string deckName)
        {
            if (string.IsNullOrWhiteSpace(deckName)) return "untitled";

            var safe = new StringBuilder(deckName.Length);
            foreach (char c in deckName.Trim())
                safe.Append(char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' ? c : '_');

            string result = safe.ToString().Trim();
            return result.Length == 0 ? "untitled" : result;
        }
    }
}
