using System.Collections.Generic;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// I. The Magician (GDD 11.I) — "Upon play, choose an element. The Magician gains the effect of the
    /// '2 of [Element]' card of that chosen element." Condition: at least 3 different elements in hand.
    ///
    /// What it gains (user decision): that element's base trait at power 1 — Attack 1, Heal 1,
    /// Pentacle 1, or the Wand buff — AND the element itself, so a Cups Magician counts as a Cups unit
    /// for Temperance's "one of each element" condition. It does NOT take the 2 of Pentacles' doubled
    /// 4 HP; it keeps its own printed 2. That matches the design note's "effectively a 2 of Anything".
    ///
    /// The element is picked at play time and applied through ModifySpawnStats, so the unit arrives on
    /// the board already being what it chose to be — nothing is patched onto it afterwards.
    /// </summary>
    public sealed class MagicianAbility : UnitAbility
    {
        public const int RequiredDistinctElements = 3;

        public override PlayChoice Choice => PlayChoice.Element;

        public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
        {
            var seen = new HashSet<Element>();
            foreach (var entry in query.GetHand(side))
                if (entry.Card?.Element != null) seen.Add(entry.Card.Element.Value);

            if (seen.Count < RequiredDistinctElements)
            {
                reason = $"The Magician needs {RequiredDistinctElements} different elements in hand (you have {seen.Count}).";
                return false;
            }

            reason = null;
            return true;
        }

        public override bool ValidatePlay(IMatchQuery query, PlayRequest request, out string reason)
        {
            if (request.Options.ChosenElement == null)
            {
                reason = "Choose an element for The Magician to become.";
                return false;
            }

            reason = null;
            return true;
        }

        public override UnitSpawnStats ModifySpawnStats(IMatchQuery query, PlayRequest request, UnitSpawnStats stats) =>
            request.Options.ChosenElement == null ? stats : SpawnTemplates.WithElementTrait(stats, request.Options.ChosenElement.Value);
    }
}
