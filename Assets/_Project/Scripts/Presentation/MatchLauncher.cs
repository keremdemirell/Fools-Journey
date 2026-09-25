using System.Collections.Generic;
using ArcanaWars.Cards;
using ArcanaWars.Cards.Decks;
using ArcanaWars.Core;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Net;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation
{
    /// <summary>Where one side's deck comes from: an asset, a deck saved by the deck builder, or — with neither — a random legal 45.</summary>
    public struct DeckSource
    {
        public DeckAsset Asset;
        public string SavedDeckName;

        public DeckSource(DeckAsset asset, string savedDeckName)
        {
            Asset = asset;
            SavedDeckName = savedDeckName;
        }

        public string Describe() =>
            Asset != null ? $"deck asset \"{Asset.name}\""
            : !string.IsNullOrWhiteSpace(SavedDeckName) ? $"saved deck \"{SavedDeckName}\""
            : "a random deck";
    }

    /// <summary>Board shape and seed for a match this machine creates. A joining client never uses one — the host's setup tells it.</summary>
    public struct MatchParameters
    {
        public int Columns;
        public int Rows;
        public int CornerCut;
        public int Seed;
    }

    /// <summary>
    /// Turns "play locally / host / join" into a running <see cref="IMatchDriver"/>. Everything a
    /// screen needs to START a match lives here, so the lobby screen only gathers the player's
    /// choices and never builds a MatchState itself — after this hands back a driver, nothing in
    /// Presentation touches the simulation except through it.
    ///
    /// <para>Moved out of the debug MatchView unchanged in behaviour: deck resolution order, the
    /// random-45 fallback, and the host dictating the whole setup (see MatchSetup).</para>
    /// </summary>
    public static class MatchLauncher
    {
        /// <summary>Both players at this device. Returns null and an explanation if a configured deck is illegal.</summary>
        public static IMatchDriver StartLocal(CardDatabase catalog, MatchParameters parameters, DeckSource deckA, DeckSource deckB, bool hotSeatPrivacy, out string error)
        {
            var rng = new System.Random(parameters.Seed);

            var cardsA = ResolveDeck(catalog, deckA, rng, out error);
            if (cardsA == null) { error = $"Player A: {error}"; return null; }

            var cardsB = ResolveDeck(catalog, deckB, rng, out error);
            if (cardsB == null) { error = $"Player B: {error}"; return null; }

            var match = new MatchState(new HexGrid(parameters.Columns, parameters.Rows, parameters.CornerCut), cardsA, cardsB, rng: rng);
            var session = hotSeatPrivacy ? MatchSession.HotSeat(match) : MatchSession.SharedScreen(match);

            match.StartMatch();
            session.AdvancePhase(); // RoundStart -> Action, so round 1 opens ready to play

            return new LocalMatchDriver(session, catalog);
        }

        /// <summary>
        /// Hosts a network match on this machine, which plays A. Both decks are settled here and sent
        /// to the client — including the client's own. See MatchSetup for why one machine must decide.
        /// </summary>
        public static IMatchDriver Host(CardDatabase catalog, MatchParameters parameters, DeckSource deckA, DeckSource deckB, int port, out string error)
        {
            var rng = new System.Random(parameters.Seed);

            var cardsA = ResolveDeck(catalog, deckA, rng, out error);
            if (cardsA == null) { error = $"Player A: {error}"; return null; }

            var cardsB = ResolveDeck(catalog, deckB, rng, out error);
            if (cardsB == null) { error = $"Player B: {error}"; return null; }

            var setup = new MatchSetup(parameters.Seed, parameters.Columns, parameters.Rows, parameters.CornerCut,
                Soul.StandardStartingHp, Ids(cardsA), Ids(cardsB));

            return LockstepMatchDriver.Host(TcpMatchTransport.Host(port), catalog, setup);
        }

        /// <summary>Joins a match — on the LAN, or through a tunnel over the internet. This machine plays B and adopts the host's setup wholesale.</summary>
        public static IMatchDriver Join(CardDatabase catalog, string address, int port, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Enter an address to join.";
                return null;
            }

            if (port <= 0 || port > 65535)
            {
                error = $"{port} isn't a valid port (1-65535).";
                return null;
            }

            return LockstepMatchDriver.Join(TcpMatchTransport.Join(address.Trim(), port), catalog);
        }

        /// <summary>Closes a driver's connection, if it has one. Safe on null and on local drivers.</summary>
        public static void Shutdown(IMatchDriver driver) => (driver as System.IDisposable)?.Dispose();

        /// <summary>
        /// One side's deck, in priority order: the asset, then the saved deck, then a random legal 45.
        /// Returns null with a reason when a deck WAS configured and is missing or illegal — a match
        /// started on an illegal deck would be a silently different game, so it is refused rather
        /// than quietly substituted.
        /// </summary>
        public static List<IPlayableCard> ResolveDeck(CardDatabase catalog, DeckSource source, System.Random rng, out string error)
        {
            error = null;
            DeckList deck = null;

            if (source.Asset != null)
            {
                deck = source.Asset.ToDeckList();
            }
            else if (!string.IsNullOrWhiteSpace(source.SavedDeckName))
            {
                deck = DeckStorage.TryLoad(source.SavedDeckName, out string loadError);
                if (deck == null)
                {
                    error = $"{source.Describe()} could not be loaded — {loadError}";
                    return null;
                }
            }

            if (deck == null) return RandomLegalDeck(catalog, rng);

            var validation = DeckRules.Validate(deck, catalog);
            if (!validation.IsValid)
            {
                error = $"{source.Describe()} is not legal.\n{validation.Summary()}";
                return null;
            }

            return deck.ToCards(catalog);
        }

        /// <summary>
        /// The whole card pool shuffled and cut to 45. Legal without a check: every card appears at
        /// most once, within both the 3-copy limit and the Major Arcana's 1.
        /// </summary>
        private static List<IPlayableCard> RandomLegalDeck(CardDatabase catalog, System.Random rng)
        {
            var copy = catalog.AllCards();
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            if (copy.Count > DeckRules.DeckSize)
                copy.RemoveRange(DeckRules.DeckSize, copy.Count - DeckRules.DeckSize);

            return copy;
        }

        private static List<string> Ids(List<IPlayableCard> cards)
        {
            var ids = new List<string>(cards.Count);
            foreach (var card in cards) ids.Add(card.DefinitionId);
            return ids;
        }
    }
}
