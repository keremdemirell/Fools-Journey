using System.Collections.Generic;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Match
{
    /// <summary>
    /// One dead unit, remembered well enough to bring it back (Judgement's resurrection) or hand its
    /// card to someone (Judgement's "return it to your hand").
    ///
    /// A class rather than a struct because it has identity: a pending judgement and a graveyard both
    /// point at the same entry, and "has this one already been resurrected?" is answered by asking
    /// whether the graveyard still holds that exact object.
    /// </summary>
    public sealed class GraveyardEntry
    {
        /// <summary>The stats it was originally created with (UnitInstance.OriginStats) — what comes back if it's resurrected.</summary>
        public UnitSpawnStats Stats { get; }

        /// <summary>The card it came from, or null for a token that was never a card (a 3 of Swords, the Tree, the right-hand Lover) — those can't be returned to hand.</summary>
        public IPlayableCard SourceCard { get; }

        public PlayerSide Owner { get; }
        public int RoundDied { get; }
        public string DefinitionId => Stats.DefinitionId;

        public GraveyardEntry(UnitSpawnStats stats, IPlayableCard sourceCard, PlayerSide owner, int roundDied)
        {
            Stats = stats;
            SourceCard = sourceCard;
            Owner = owner;
            RoundDied = roundDied;
        }
    }

    /// <summary>One player's dead. Every death lands here; resurrection and judgement take entries back out.</summary>
    public class Graveyard
    {
        private readonly List<GraveyardEntry> _entries = new List<GraveyardEntry>();

        public IReadOnlyList<GraveyardEntry> Entries => _entries;
        public int Count => _entries.Count;

        public void Add(GraveyardEntry entry)
        {
            if (entry != null) _entries.Add(entry);
        }

        public bool Remove(GraveyardEntry entry) => _entries.Remove(entry);
        public bool Contains(GraveyardEntry entry) => _entries.Contains(entry);
    }
}
