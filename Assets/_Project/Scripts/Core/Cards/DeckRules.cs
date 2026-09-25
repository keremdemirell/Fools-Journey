using System.Collections.Generic;

namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// What makes a deck legal. The deckbuilding counterpart to HandRules: one place for the numbers,
    /// so a balance change is one edit rather than a hunt.
    ///
    /// Two of the three rules come straight from the GDD. The third did not exist and was decided:
    ///
    ///  - **45 cards** — GDD section 1 ("players build custom 45-card decks"). Exactly 45, not "at
    ///    least": the mana curve and the HP=duration clock are both tuned against a known deck length,
    ///    and an unbounded deck would let a player dodge the empty-deck state entirely.
    ///  - **Major Arcana: 1 copy** — GDD section 11, stated outright ("only one copy of each may
    ///    exist in a deck"). They are the only cards the GDD gives a limit for.
    ///  - **Everything else: 3 copies** — NOT in the GDD; a design decision (user, 2026-09-16). The
    ///    GDD is silent on Minor Arcana and Court Cards, and the limit could not be left unset,
    ///    because "unlimited" makes a 45x-one-card deck legal and the cheap Minor Arcana (cost = HP,
    ///    so strictly linear) dominate under that.
    ///
    ///    Why 3 and not 1 or 2: the pool is 9 Minor values + 5 Court ranks = 14 unique cards per
    ///    element, plus the 22 Major Arcana, which are elementless and so available to every deck.
    ///    At a 1-copy limit a mono-element deck tops out at 14 + 22 = 36 cards and **cannot legally
    ///    reach 45** — which would quietly break the five cards whose whole design is
    ///    a reward for element dedication (the Queen's per-element HP bonus, the King's doubling, the
    ///    Empress's 2 Queens, the Emperor's 2 Kings, and by inversion Temperance's all-four condition,
    ///    which is only a meaningful *choice* if specialising is possible). At 2 copies it reaches
    ///    exactly 50, technically legal but with almost nothing left to cut. At 3 it reaches 64, which
    ///    leaves real deckbuilding decisions inside a mono-element shell.
    ///
    ///    Worth knowing when reading those numbers: at 3 copies the element cards *alone* come to 42,
    ///    so even a committed mono-element deck must round out its last 3 cards with Major Arcana. The
    ///    limit was set knowing that — a deck that is literally nothing but one element is not a shape
    ///    the game supports at any copy limit below 4.
    ///
    ///    Court Cards share the 3-copy limit rather than getting a tighter "elite" one: their printed
    ///    cost (11-14, against a base mana cap of 14) already limits how many can be played in a
    ///    match, so a second rule saying the same thing would be redundant.
    /// </summary>
    public static class DeckRules
    {
        /// <summary>GDD section 1. A legal deck holds exactly this many cards.</summary>
        public const int DeckSize = 45;

        /// <summary>Minor Arcana and Court Cards. Not a GDD number — see the class remarks for why it's 3.</summary>
        public const int StandardCopyLimit = 3;

        /// <summary>Major Arcana (GDD section 11): unique, one copy per deck.</summary>
        public const int UniqueCopyLimit = 1;

        /// <summary>
        /// The copy limit for a card of this tier. Keyed off CardKind rather than off the card itself
        /// so that Core needs no knowledge of what a Court rank or a Minor value is — the same reason
        /// CardKind exists at all.
        /// </summary>
        public static int MaxCopiesOf(CardKind kind) =>
            kind == CardKind.MajorArcana ? UniqueCopyLimit : StandardCopyLimit;

        /// <summary>
        /// Whether one more copy of this card could legally be added. Side-effect free and cheap, so a
        /// deckbuilder can call it per card per repaint to grey out the ones that are maxed — the same
        /// contract UnitAbility.CanPlay has, and for the same reason.
        ///
        /// Checks the copy limit and the deck size, which are the only two things adding a card can
        /// break. It does NOT check whether the card is in the catalog: you can only click a card the
        /// pool showed you, so a UI has one by construction.
        /// </summary>
        public static bool CanAdd(DeckList deck, IPlayableCard card)
        {
            if (deck == null || card == null) return false;
            if (deck.TotalCards >= DeckSize) return false;

            return deck.CountOf(card.DefinitionId) < MaxCopiesOf(card.Kind);
        }

        /// <summary>
        /// Every reason this deck isn't legal, or an empty result if it is. Needs the catalog because
        /// a deck list stores ids: the copy limit depends on the card's CardKind, which only the
        /// catalog can supply, and an id the catalog can't resolve is itself a problem worth naming
        /// (it means a card was removed or renamed under a saved deck).
        ///
        /// A null catalog is treated as "nothing resolves" rather than as a crash — a scene with an
        /// unassigned CardDatabase should get a readable list of complaints, not a NullReference.
        /// </summary>
        public static DeckValidationResult Validate(DeckList deck, ICardCatalog catalog)
        {
            var issues = new List<DeckIssue>();

            if (deck == null)
            {
                issues.Add(new DeckIssue(DeckIssueCode.WrongCardCount, null, $"No deck. A deck must hold exactly {DeckSize} cards."));
                return new DeckValidationResult(issues, 0);
            }

            int total = 0;

            foreach (var entry in deck.Entries)
            {
                total += entry.Count;

                var card = catalog?.Find(entry.CardId);
                if (card == null)
                {
                    issues.Add(new DeckIssue(
                        DeckIssueCode.UnknownCard,
                        entry.CardId,
                        $"\"{entry.CardId}\" is not a card in the database."));
                    continue;
                }

                int limit = MaxCopiesOf(card.Kind);
                if (entry.Count > limit)
                {
                    // Named rather than generic, because "Major Arcana are unique" and "3 copies max"
                    // are different rules from different sources and a player should be told which
                    // one they hit.
                    string why = limit == UniqueCopyLimit
                        ? "Major Arcana are unique — 1 copy per deck"
                        : $"limit is {limit} copies";

                    issues.Add(new DeckIssue(
                        DeckIssueCode.TooManyCopies,
                        entry.CardId,
                        $"{card.DisplayName}: {entry.Count} copies ({why})."));
                }
            }

            // Reported last so it reads as the summary line under the per-card problems, and phrased
            // with the shortfall spelled out because "43/45" is the one thing a player wants to know.
            if (total != DeckSize)
            {
                string direction = total < DeckSize
                    ? $"{DeckSize - total} more needed"
                    : $"{total - DeckSize} too many";

                issues.Add(new DeckIssue(
                    DeckIssueCode.WrongCardCount,
                    null,
                    $"Deck has {total} cards; a legal deck has exactly {DeckSize} ({direction})."));
            }

            return new DeckValidationResult(issues, total);
        }
    }
}
