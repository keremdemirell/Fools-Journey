using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using ArcanaWars.Cards;
using ArcanaWars.Cards.Decks;
using ArcanaWars.Cards.MajorArcana;
using ArcanaWars.Core;
using ArcanaWars.Core.Cards;

namespace ArcanaWars.Presentation.UI
{
    /// <summary>
    /// Building a deck: the card pool on the left, the deck on the right, and a live legality check
    /// between them.
    ///
    /// <para>It holds no rules. What a legal deck is comes from <see cref="DeckRules"/> — 45 cards,
    /// 3 copies of anything with a rank, 1 of each Major Arcana — and this only shows the answer and
    /// the reasons. Decks are saved as JSON through DeckStorage, which is the only route that works
    /// in a built game, since a ScriptableObject cannot be written outside the Editor.</para>
    /// </summary>
    public sealed class DeckBuilderScreen : UiScreen
    {
        private enum Tier { All, Minor, Court, Major }

        /// <summary>
        /// One pool row's unchanging facts, read exactly once when the screen is built.
        ///
        /// <para>Not a micro-optimisation — correctness. GetSpawnStats() rolls The Fool's random 1-4
        /// HP on every call, so asking a card for its stats on each redraw would make its HP flicker
        /// and would burn randomness besides. Everything here is printed on the card and never
        /// changes.</para>
        /// </summary>
        private struct PoolCard
        {
            public IPlayableCard Card;
            public string Label;
            public Element? Element;
            public Tier Tier;
        }

        private readonly CardDatabase _catalog;
        private readonly List<PoolCard> _pool = new List<PoolCard>();

        private readonly TextField _deckName;
        private readonly Label _deckCount;
        private readonly Label _validity;
        private readonly Label _message;
        private readonly VisualElement _poolList;
        private readonly VisualElement _deckList;
        private readonly VisualElement _tierFilters;
        private readonly VisualElement _elementFilters;
        private readonly TextField _search;

        private readonly VisualElement _loadPanel;
        private readonly VisualElement _savedList;

        private DeckList _deck = new DeckList();
        private Tier _tier = Tier.All;
        private Element? _element;

        public event Action BackRequested;

        public DeckBuilderScreen(VisualTreeAsset layout, VisualElement host, CardDatabase catalog) : base(layout, host)
        {
            _catalog = catalog;

            _deckName = Require<TextField>("deck-name");
            _deckCount = Require<Label>("deck-count");
            _validity = Require<Label>("deck-validity");
            _message = Require<Label>("builder-message");
            _poolList = Require<VisualElement>("pool-list");
            _deckList = Require<VisualElement>("deck-list");
            _tierFilters = Require<VisualElement>("tier-filters");
            _elementFilters = Require<VisualElement>("element-filters");
            _search = Require<TextField>("search-field");

            _loadPanel = Require<VisualElement>("load-panel");
            _savedList = Require<VisualElement>("saved-list");

            Require<Button>("back-button").clicked += () => BackRequested?.Invoke();
            Require<Button>("new-button").clicked += () => { _deck = new DeckList(); Say("Started a new deck."); RefreshAll(); };
            Require<Button>("clear-button").clicked += () => { _deck.Clear(); Say("Cleared the deck."); RefreshAll(); };
            Require<Button>("fill-button").clicked += FillRandomly;
            Require<Button>("save-button").clicked += Save;
            Require<Button>("load-button").clicked += () => { RefreshSavedDecks(); SetVisible(_loadPanel, true); };
            Require<Button>("load-close").clicked += () => SetVisible(_loadPanel, false);

            _deckName.RegisterValueChangedCallback(evt => _deck.Name = evt.newValue);
            _search.RegisterValueChangedCallback(_ => RefreshPool());

            BuildPool();
            BuildFilters();
        }

        /// <summary>Opens the builder, optionally on an existing deck.</summary>
        public void Open(DeckList deck = null)
        {
            _deck = deck ?? _deck ?? new DeckList();
            SetVisible(_loadPanel, false);
            Say("");
            RefreshAll();
            Show();
        }

        // --- The pool -----------------------------------------------------------------------------

        private void BuildPool()
        {
            foreach (var card in _catalog.AllCards())
            {
                if (card == null) continue;

                _pool.Add(new PoolCard
                {
                    Card = card,
                    Label = Describe(card),
                    Element = card.Element,
                    Tier = TierOf(card.Kind),
                });
            }

            // Grouped the way a player thinks about the pool — tier, then element, then up the cost
            // curve — rather than in CardDatabase order, which is generator order.
            _pool.Sort((a, b) =>
            {
                int byTier = a.Tier.CompareTo(b.Tier);
                if (byTier != 0) return byTier;

                int byElement = ElementOrder(a.Element).CompareTo(ElementOrder(b.Element));
                if (byElement != 0) return byElement;

                int byCost = a.Card.GetCost().CompareTo(b.Card.GetCost());
                return byCost != 0 ? byCost : string.CompareOrdinal(a.Card.DisplayName, b.Card.DisplayName);
            });
        }

        private void BuildFilters()
        {
            foreach (Tier tier in Enum.GetValues(typeof(Tier)))
            {
                var value = tier;
                _tierFilters.Add(FilterButton(tier == Tier.All ? "All" : tier.ToString(), () => { _tier = value; RefreshFilters(); RefreshPool(); }));
            }

            _elementFilters.Add(FilterButton("Any element", () => { _element = null; RefreshFilters(); RefreshPool(); }));

            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                var value = element;
                _elementFilters.Add(FilterButton(element.ToString(), () => { _element = value; RefreshFilters(); RefreshPool(); }));
            }

            RefreshFilters();
        }

        private static Button FilterButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("button");
            button.AddToClassList("button--small");
            return button;
        }

        /// <summary>Marks which filter is active. Index order matches how the buttons were added above.</summary>
        private void RefreshFilters()
        {
            for (int i = 0; i < _tierFilters.childCount; i++)
                _tierFilters[i].EnableInClassList("button--chosen", i == (int)_tier);

            for (int i = 0; i < _elementFilters.childCount; i++)
            {
                bool active = i == 0 ? !_element.HasValue : _element.HasValue && (int)_element.Value == i - 1;
                _elementFilters[i].EnableInClassList("button--chosen", active);
            }
        }

        private void RefreshPool()
        {
            _poolList.Clear();
            string search = _search.value ?? "";

            foreach (var entry in _pool)
            {
                if (_tier != Tier.All && entry.Tier != _tier) continue;
                if (_element.HasValue && entry.Element != _element.Value) continue;
                if (search.Length > 0 && entry.Card.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                int held = _deck.CountOf(entry.Card.DefinitionId);
                int limit = DeckRules.MaxCopiesOf(entry.Card.Kind);
                bool canAdd = DeckRules.CanAdd(_deck, entry.Card);

                var row = new VisualElement();
                row.AddToClassList("builder-row");
                row.EnableInClassList("builder-row--maxed", !canAdd);

                var label = new Label($"{entry.Label}");
                label.AddToClassList("builder-row__name");
                row.Add(label);

                var count = new Label($"{held}/{limit}");
                count.AddToClassList("builder-row__count");
                row.Add(count);

                var card = entry.Card;
                var add = new Button(() => Add(card)) { text = "+" };
                add.AddToClassList("button");
                add.AddToClassList("button--tiny");

                // Disabled rather than hidden, so a maxed card stays where the player expects it and
                // the count beside it explains why the button does nothing.
                add.SetEnabled(canAdd);
                row.Add(add);

                _poolList.Add(row);
            }
        }

        // --- The deck -----------------------------------------------------------------------------

        private void RefreshDeck()
        {
            if (_deckName.value != _deck.Name) _deckName.SetValueWithoutNotify(_deck.Name ?? "");

            var validation = DeckRules.Validate(_deck, _catalog);

            _deckCount.text = $"{_deck.TotalCards} / {DeckRules.DeckSize} cards";
            _deckCount.EnableInClassList("builder__count--legal", validation.IsValid);

            _validity.text = validation.IsValid ? "Legal deck — ready to play." : validation.Summary();
            _validity.EnableInClassList("builder__validity--legal", validation.IsValid);

            _deckList.Clear();

            foreach (var entry in new List<DeckEntry>(_deck.Entries))
            {
                var card = _catalog.Find(entry.CardId);

                var row = new VisualElement();
                row.AddToClassList("builder-row");

                var label = new Label(card != null ? card.DisplayName : $"{entry.CardId} (unknown card)");
                label.AddToClassList("builder-row__name");
                row.Add(label);

                var count = new Label($"{entry.Count}x");
                count.AddToClassList("builder-row__count");
                row.Add(count);

                var add = new Button(() => Add(card)) { text = "+" };
                add.AddToClassList("button");
                add.AddToClassList("button--tiny");
                add.SetEnabled(card != null && DeckRules.CanAdd(_deck, card));
                row.Add(add);

                var id = entry.CardId;
                var remove = new Button(() => { _deck.Remove(id); Say(""); RefreshAll(); }) { text = "−" };
                remove.AddToClassList("button");
                remove.AddToClassList("button--tiny");
                row.Add(remove);

                _deckList.Add(row);
            }
        }

        private void Add(IPlayableCard card)
        {
            if (card == null) return;

            // Asks DeckRules rather than trusting that the button was drawn disabled — the same card
            // can be added from two places on this screen, and a rule enforced at only some of its
            // call sites is one that will eventually be missed at one of them.
            if (!DeckRules.CanAdd(_deck, card))
            {
                Say(_deck.TotalCards >= DeckRules.DeckSize
                    ? $"The deck is already {DeckRules.DeckSize} cards."
                    : $"{card.DisplayName}: already at the {DeckRules.MaxCopiesOf(card.Kind)}-copy limit.");
                return;
            }

            _deck.Add(card.DefinitionId);
            Say("");
            RefreshAll();
        }

        /// <summary>
        /// Tops the deck up to a legal 45 at random, keeping whatever is already in it. A convenience
        /// for getting a playable deck without 45 clicks, not a game feature.
        /// </summary>
        private void FillRandomly()
        {
            var rng = new System.Random();
            int guard = DeckRules.DeckSize * 100; // The pool can run out of legal additions; never spin forever.

            while (_deck.TotalCards < DeckRules.DeckSize && guard-- > 0)
            {
                var card = _pool[rng.Next(_pool.Count)].Card;
                if (DeckRules.CanAdd(_deck, card)) _deck.Add(card.DefinitionId);
            }

            Say(_deck.TotalCards == DeckRules.DeckSize
                ? "Filled to 45."
                : $"Could only reach {_deck.TotalCards} — the pool has no more legal additions.");

            RefreshAll();
        }

        // --- Saving and loading -------------------------------------------------------------------

        private void Save()
        {
            if (!DeckStorage.TrySave(_deck, out string error))
            {
                Say(error);
                return;
            }

            // Saving an unfinished deck is allowed — a half-built deck is the normal state of a
            // deckbuilder — but it would be picked in the lobby and refused there, so say so now.
            var validation = DeckRules.Validate(_deck, _catalog);
            Say(validation.IsValid
                ? $"Saved \"{_deck.Name}\". It can be picked in the lobby."
                : $"Saved \"{_deck.Name}\", but it is not legal yet ({_deck.TotalCards}/{DeckRules.DeckSize}) and can't start a match.");
        }

        private void RefreshSavedDecks()
        {
            _savedList.Clear();

            var names = DeckStorage.SavedDeckNames();
            if (names.Count == 0)
            {
                var empty = new Label("No saved decks yet.");
                empty.AddToClassList("muted");
                _savedList.Add(empty);
                return;
            }

            foreach (var name in names)
            {
                var row = new VisualElement();
                row.AddToClassList("builder-row");

                var label = new Label(name);
                label.AddToClassList("builder-row__name");
                row.Add(label);

                var deckName = name;

                var load = new Button(() => LoadDeck(deckName)) { text = "Load" };
                load.AddToClassList("button");
                load.AddToClassList("button--small");
                row.Add(load);

                var delete = new Button(() => DeleteDeck(deckName)) { text = "Delete" };
                delete.AddToClassList("button");
                delete.AddToClassList("button--small");
                row.Add(delete);

                _savedList.Add(row);
            }
        }

        private void LoadDeck(string name)
        {
            var loaded = DeckStorage.TryLoad(name, out string error);
            if (loaded == null)
            {
                Say(error);
                return;
            }

            _deck = loaded;
            Say($"Loaded \"{name}\".");
            SetVisible(_loadPanel, false);
            RefreshAll();
        }

        private void DeleteDeck(string name)
        {
            Say(DeckStorage.TryDelete(name, out string error) ? $"Deleted \"{name}\"." : error);
            RefreshSavedDecks();
        }

        // --- Small helpers ------------------------------------------------------------------------

        private void RefreshAll()
        {
            RefreshDeck();
            RefreshPool();
        }

        private void Say(string text) => _message.text = text ?? "";

        /// <summary>
        /// "5 of Swords — cost 5, 5 HP". The two cards whose printed numbers aren't a single value are
        /// said in words rather than shown as a misleading number: an Ace's cost is chosen when it is
        /// played (GDD 10.1), and The Fool's HP is rolled on play (GDD 11.0).
        /// </summary>
        private static string Describe(IPlayableCard card)
        {
            string cost = card.HasVariableCost ? "cost 1 or all mana" : $"cost {card.GetCost()}";
            string hp;

            if (card.HasVariableCost) hp = "HP = mana spent";
            else if (card is MajorArcanaCardDefinition major && major.HasRandomStartingHp) hp = $"{major.MinStartingHp}-{major.MaxStartingHp} HP";
            else hp = $"{card.GetSpawnStats().StartingHp} HP"; // null rng: inspecting a card must never consume randomness

            return $"{card.DisplayName} — {cost}, {hp}";
        }

        private static int ElementOrder(Element? element) => element.HasValue ? (int)element.Value : int.MaxValue;

        private static Tier TierOf(CardKind kind)
        {
            if (kind == CardKind.MinorArcana) return Tier.Minor;
            return kind == CardKind.MajorArcana ? Tier.Major : Tier.Court;
        }
    }
}
