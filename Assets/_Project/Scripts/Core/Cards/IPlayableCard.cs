using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// What Core needs to know about a card in order to hold it in a hand and play it — nothing
    /// more. Core cannot reference the Cards assembly (ScriptableObject is a UnityEngine type and
    /// Core.asmdef forbids engine references), so this is the contract that lets the dependency run
    /// the other way: Cards implements this, Core only ever talks to the interface.
    ///
    /// Note this namespace (ArcanaWars.Core.Cards) is the card *contract*; the ArcanaWars.Cards
    /// assembly is the card *data*. Different things, deliberately kept apart.
    /// </summary>
    public interface IPlayableCard
    {
        /// <summary>Stable identifier, e.g. "minor_Swords_5" or "8_strength". Becomes UnitSpawnStats.DefinitionId.</summary>
        string DefinitionId { get; }

        string DisplayName { get; }

        /// <summary>
        /// Tier/rank, in Core-readable terms. Only exists so play conditions that count history
        /// ("2+ Queens played", "8 units played") can be evaluated without Core knowing what a
        /// CourtRank is — see CardKind.
        /// </summary>
        CardKind Kind { get; }

        /// <summary>
        /// The card's element, or null for a card that has none (every Major Arcana). Needed by
        /// The Magician's "3 different elements in hand" condition and Temperance's "one of each
        /// element alive on the board" — the latter via UnitSpawnStats.Element, which this feeds.
        /// </summary>
        Element? Element { get; }

        /// <summary>True only for the Aces (GDD 10.1), whose cost is whatever mana the player chooses to commit. UI uses this to know whether to prompt for an amount.</summary>
        bool HasVariableCost { get; }

        /// <summary>manaCommitted is ignored by every card except an Ace.</summary>
        int GetCost(int manaCommitted = 0);

        /// <summary>
        /// manaCommitted is ignored by every card except an Ace.
        ///
        /// <para><b>rng must be the match's own Random whenever the result will actually reach the
        /// board, and null whenever it will not.</b> Exactly one card rolls dice here — The Fool's
        /// 1-4 starting HP (GDD 11.0) — and where that roll comes from decides whether a match is
        /// reproducible. It used to come from UnityEngine.Random, which is process-global and not
        /// controlled by the match seed at all: two clients replaying the same commands rolled
        /// different Fools, and even a single seeded match replayed differently.</para>
        ///
        /// <para>Passing null returns the card's minimum stats and rolls nothing, which is what every
        /// caller that is only *inspecting* a card wants — a deckbuilder drawing a tooltip, or Death
        /// scanning the deck for a replacement that fits (a footprint doesn't depend on HP). Those
        /// callers must not consume the match RNG, or simply opening a menu would change the game.</para>
        /// </summary>
        UnitSpawnStats GetSpawnStats(int manaCommitted = 0, System.Random rng = null);
    }
}
