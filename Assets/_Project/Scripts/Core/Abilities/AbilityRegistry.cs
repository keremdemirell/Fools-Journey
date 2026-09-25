using System;
using System.Collections.Generic;
using ArcanaWars.Core.Cards;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// Maps a card's DefinitionId to its bespoke behaviour. A card with no entry here simply has no
    /// ability and no play condition, which is the correct description of every Minor Arcana, the
    /// Aces and Princes (whose traits are plain stats), and Strength (whose whole card is its stat block).
    ///
    /// Abilities are stored as shared singletons because every one of them is stateless — each reads
    /// the match through IAbilityContext and writes through it, keeping nothing of its own. That is a
    /// constraint, not an accident: a match reuses one instance across every play of that card, so
    /// anything remembered here would leak between plays. When a card needs memory, that state belongs
    /// on the UnitInstance (The Devil's clock is derived from UnitInstance.RoundPlayed for exactly
    /// this reason), on the tiles (the Emperor's wall), or on the match (Judgement's pending rulings) —
    /// never on the ability object. A Court ability's element is configuration fixed at construction,
    /// not memory, so it doesn't break the rule.
    /// </summary>
    public static class AbilityRegistry
    {
        private static readonly Dictionary<string, UnitAbility> Abilities = Build();

        private static Dictionary<string, UnitAbility> Build()
        {
            var abilities = new Dictionary<string, UnitAbility>
            {
                { MajorArcanaIds.Fool, new FoolAbility() },
                { MajorArcanaIds.Magician, new MagicianAbility() },
                { MajorArcanaIds.HighPriestess, new HighPriestessAbility() },
                { MajorArcanaIds.Empress, new EmpressAbility() },
                { MajorArcanaIds.Emperor, new EmperorAbility() },
                { MajorArcanaIds.Hierophant, new HierophantAbility() },
                { MajorArcanaIds.Lovers, new LoversAbility() },
                { MajorArcanaIds.Chariot, new ChariotAbility() },
                { MajorArcanaIds.Hermit, new HermitAbility() },
                { MajorArcanaIds.WheelOfFortune, new WheelOfFortuneAbility() },
                { MajorArcanaIds.Justice, new JusticeAbility() },
                { MajorArcanaIds.HangedMan, new HangedManAbility() },
                { MajorArcanaIds.Death, new DeathAbility() },
                { MajorArcanaIds.Temperance, new PlayConditions.Temperance() },
                { MajorArcanaIds.Devil, new DevilAbility() },
                { MajorArcanaIds.Tower, new TowerAbility() },
                { MajorArcanaIds.Star, new StarAbility() },
                { MajorArcanaIds.Moon, new MoonAbility() },
                { MajorArcanaIds.Sun, new SunAbility() },
                { MajorArcanaIds.Judgement, new JudgementAbility() },
                { MajorArcanaIds.World, new PlayConditions.World() },
            };

            // Court Cards (GDD 10): one Knight, Queen and King ability per element.
            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                abilities[CourtIds.For(CardKind.Knight, element)] = new KnightAbility(element);
                abilities[CourtIds.For(CardKind.Queen, element)] = new QueenAbility(element);
                abilities[CourtIds.For(CardKind.King, element)] = new KingAbility(element);
            }

            return abilities;
        }

        public static bool TryGet(string definitionId, out UnitAbility ability)
        {
            if (string.IsNullOrEmpty(definitionId))
            {
                ability = null;
                return false;
            }

            return Abilities.TryGetValue(definitionId, out ability);
        }

        /// <summary>Which play-time choice a card wants, for a UI deciding what to ask. None for any card without an ability.</summary>
        public static PlayChoice ChoiceFor(string definitionId) =>
            TryGet(definitionId, out var ability) ? ability.Choice : PlayChoice.None;

        /// <summary>Every id that currently has behaviour attached — used by the offline harness to check the registry hasn't drifted from the card data.</summary>
        public static IEnumerable<string> RegisteredIds => Abilities.Keys;
    }
}
