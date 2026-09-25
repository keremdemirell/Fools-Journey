using System.Collections.Generic;

namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// A way to look a card up by its DefinitionId. The same trick as IPlayableCard, one level up: a
    /// deck list is stored as card *ids* (so it can survive in a JSON file or a ScriptableObject
    /// without holding live object references), which means validating or playing that deck needs
    /// something that can turn "minor_Swords_5" back into a card. The only thing that can do that is
    /// CardDatabase — which lives in the Cards assembly, which Core cannot reference.
    ///
    /// So Core declares what it needs and CardDatabase implements it, exactly as with IPlayableCard.
    /// Anything else that can resolve ids (a test harness with a handful of fake cards, a future
    /// expansion-aware catalog that merges several databases) works just as well.
    /// </summary>
    public interface ICardCatalog
    {
        /// <summary>Every card that may legally appear in a deck list. This is the pool a deckbuilder shows.</summary>
        IReadOnlyList<IPlayableCard> AllCards { get; }

        /// <summary>The card with this DefinitionId, or null if the catalog has no such card.</summary>
        IPlayableCard Find(string definitionId);
    }
}
