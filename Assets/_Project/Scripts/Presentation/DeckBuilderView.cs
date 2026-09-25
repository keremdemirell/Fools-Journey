using System.Collections.Generic;
using UnityEngine;
using ArcanaWars.Cards;
using ArcanaWars.Cards.Decks;
using ArcanaWars.Cards.MajorArcana;
using ArcanaWars.Core;
using ArcanaWars.Core.Cards;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// The deckbuilding screen: the card pool on the left, the deck being built on the right, and a
    /// live legality check between them.
    ///
    /// IMGUI for the same reason MatchView is (see its remarks): it needs no scene setup beyond
    /// dropping this component on an empty GameObject, so it's usable the moment it compiles. Like
    /// MatchView it holds no rules — what a legal deck is comes from Core's DeckRules, and this only
    /// draws the answer. That's what lets a real UI replace this file later without re-deciding
    /// anything.
    ///
    /// Saves to DeckStorage (JSON, works in a build). Turning a saved deck into a DeckAsset for the
    /// Inspector is an Editor-only step — see DeckAssetImporter.
    /// </summary>
    public class DeckBuilderView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CardDatabase cardDatabase;

        /// <summary>Optional. If set, the builder opens with this deck loaded instead of an empty one.</summary>
        [SerializeField] private DeckAsset openWith;

        private enum TierFilter { All, Minor, Court, Major }

        /// <summary>
        /// One pool row's display text, computed once at startup rather than per repaint.
        ///
        /// This caching is not a micro-optimisation — it's correctness. GetSpawnStats() rolls The
        /// Fool's random 1-4 HP on every call, so asking a card for its stats each repaint would make
        /// its HP flicker on screen and would consume RNG draws besides. Everything shown here is a
        /// printed, unchanging fact about the card, so it is read exactly once.
        /// </summary>
        private struct PoolCard
        {
            public IPlayableCard Card;
            public string Label;
            public Element? Element;
            public TierFilter Tier;
        }

        private readonly List<PoolCard> _pool = new List<PoolCard>();
        private DeckList _deck;

        private TierFilter _tierFilter = TierFilter.All;
        private Element? _elementFilter;
        private string _search = "";

        private Vector2 _poolScroll;
        private Vector2 _deckScroll;
        private string _message = "";
        private List<string> _savedDecks = new List<string>();
        private bool _showLoadPanel;

        private void Start()
        {
            if (cardDatabase == null)
            {
                Debug.LogError("DeckBuilderView: no CardDatabase assigned. Drag the CardDatabase asset onto this component.", this);
                enabled = false;
                return;
            }

            BuildPool();
            _deck = openWith != null ? openWith.ToDeckList() : new DeckList();
            _savedDecks = DeckStorage.SavedDeckNames();
        }

        private void BuildPool()
        {
            foreach (var card in cardDatabase.AllCards())
            {
                if (card == null) continue;
                _pool.Add(new PoolCard
                {
                    Card = card,
                    Label = DescribeCard(card),
                    Element = card.Element,
                    Tier = TierOf(card.Kind)
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

        private static int ElementOrder(Element? element) => element.HasValue ? (int)element.Value : int.MaxValue;

        private static TierFilter TierOf(CardKind kind)
        {
            if (kind == CardKind.MinorArcana) return TierFilter.Minor;
            return kind == CardKind.MajorArcana ? TierFilter.Major : TierFilter.Court;
        }

        /// <summary>
        /// "5 of Swords — cost 5, 5 HP". The two cards whose printed numbers aren't a single value get
        /// said plainly rather than shown as a misleading number: an Ace's cost is a choice made when
        /// it's played (GDD 10.1), and The Fool's HP is a range rolled on play (GDD 11.0).
        /// </summary>
        private static string DescribeCard(IPlayableCard card)
        {
            string cost = card.HasVariableCost ? "cost 1 or all mana" : $"cost {card.GetCost()}";
            string hp;

            if (card.HasVariableCost) hp = "HP = mana spent";
            else if (card is MajorArcanaCardDefinition major && major.HasRandomStartingHp) hp = $"{major.MinStartingHp}-{major.MaxStartingHp} HP";
            else hp = $"{card.GetSpawnStats().StartingHp} HP"; // no RNG: inspecting a card must never consume the match's randomness

            return $"{card.DisplayName} — {cost}, {hp}";
        }

        private void OnGUI()
        {
            if (_deck == null) return;

            // Sized from the actual screen for the reason MatchView's ComputeHudRect documents: a
            // fixed Rect is unusable in a small or zoomed Game view. Two columns side by side, so the
            // pool and the deck are visible at the same time — the whole point of the screen.
            float margin = 10f;
            float width = (Screen.width - margin * 3f) * 0.5f;
            float height = Screen.height - margin * 2f;

            GUILayout.BeginArea(new Rect(margin, margin, width, height), GUI.skin.box);
            DrawPool();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(margin * 2f + width, margin, width, height), GUI.skin.box);
            DrawDeck();
            GUILayout.EndArea();
        }

        private void DrawPool()
        {
            GUILayout.Label("<b>Card Pool</b>", RichLabel());

            GUILayout.BeginHorizontal();
            foreach (TierFilter tier in System.Enum.GetValues(typeof(TierFilter)))
                if (GUILayout.Toggle(_tierFilter == tier, tier.ToString(), GUI.skin.button)) _tierFilter = tier;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_elementFilter.HasValue, "Any element", GUI.skin.button)) _elementFilter = null;
            foreach (Element element in System.Enum.GetValues(typeof(Element)))
                if (GUILayout.Toggle(_elementFilter == element, element.ToString(), GUI.skin.button)) _elementFilter = element;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50f));
            _search = GUILayout.TextField(_search ?? "");
            if (GUILayout.Button("x", GUILayout.Width(24f))) _search = "";
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            _poolScroll = GUILayout.BeginScrollView(_poolScroll, GUILayout.ExpandHeight(true));
            foreach (var entry in _pool)
            {
                if (!PassesFilters(entry)) continue;

                int held = _deck.CountOf(entry.Card.DefinitionId);
                int limit = DeckRules.MaxCopiesOf(entry.Card.Kind);
                bool canAdd = DeckRules.CanAdd(_deck, entry.Card);

                GUILayout.BeginHorizontal();

                var previous = GUI.color;
                if (!canAdd) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUILayout.Label($"{entry.Label}   [{held}/{limit}]", RichLabel());
                GUI.color = previous;

                GUILayout.FlexibleSpace();

                // Disabled rather than hidden, so a maxed card stays where the player expects it and
                // the [n/limit] counter explains why the button does nothing.
                GUI.enabled = canAdd;
                if (GUILayout.Button("+", GUILayout.Width(28f))) AddCard(entry.Card);
                GUI.enabled = true;

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private bool PassesFilters(PoolCard entry)
        {
            if (_tierFilter != TierFilter.All && entry.Tier != _tierFilter) return false;
            if (_elementFilter.HasValue && entry.Element != _elementFilter.Value) return false;

            if (!string.IsNullOrEmpty(_search) &&
                entry.Card.DisplayName.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) return false;

            return true;
        }

        private void DrawDeck()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Deck name", GUILayout.Width(75f));
            _deck.Name = GUILayout.TextField(_deck.Name ?? "");
            GUILayout.EndHorizontal();

            var validation = DeckRules.Validate(_deck, cardDatabase);

            // The count is the number a deckbuilder is read for, so it gets its own line and its own
            // colour — legal green, not-yet-legal amber — rather than being buried in the issue list.
            var countColor = validation.IsValid ? new Color(0.55f, 0.9f, 0.55f) : new Color(1f, 0.82f, 0.4f);
            GUILayout.Label($"<b><color=#{ColorUtility.ToHtmlStringRGB(countColor)}>{_deck.TotalCards} / {DeckRules.DeckSize} cards</color></b>", RichLabel());

            if (!validation.IsValid) GUILayout.Label($"<color=#ffb3b3>{validation.Summary()}</color>", RichLabel());
            else GUILayout.Label("<color=#8fe08f>Legal deck — ready to play.</color>", RichLabel());

            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New")) { _deck = new DeckList(); _message = "Started a new deck."; }
            if (GUILayout.Button("Fill randomly")) AutoFill();
            if (GUILayout.Button("Clear")) { _deck.Clear(); _message = "Cleared the deck."; }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                _message = DeckStorage.TrySave(_deck, out string error)
                    ? $"Saved \"{_deck.Name}\" to {DeckStorage.PathFor(_deck.Name)}"
                    : error;
                _savedDecks = DeckStorage.SavedDeckNames();
            }
            if (GUILayout.Button(_showLoadPanel ? "Hide saved" : $"Load ({_savedDecks.Count})"))
            {
                _showLoadPanel = !_showLoadPanel;
                if (_showLoadPanel) _savedDecks = DeckStorage.SavedDeckNames();
            }
            GUILayout.EndHorizontal();

            if (_showLoadPanel) DrawLoadPanel();

            if (!string.IsNullOrEmpty(_message))
            {
                GUILayout.Space(2f);
                GUILayout.Label($"<i>{_message}</i>", RichLabel());
            }

            GUILayout.Space(4f);
            GUILayout.Label("<b>Cards</b>", RichLabel());

            _deckScroll = GUILayout.BeginScrollView(_deckScroll, GUILayout.ExpandHeight(true));

            // Copied because the buttons in this loop mutate the deck, and mutating the list being
            // enumerated throws. A copy per repaint is cheap at 45 entries and keeps the click
            // handling immediate — deferring the change to after the loop would be a frame of lag on
            // every press.
            var entries = new List<DeckEntry>(_deck.Entries);
            foreach (var entry in entries)
            {
                var card = cardDatabase.Find(entry.CardId);

                GUILayout.BeginHorizontal();
                GUILayout.Label(card != null
                    ? $"{entry.Count}x  {card.DisplayName}"
                    : $"<color=#ffb3b3>{entry.Count}x  {entry.CardId} (unknown card)</color>", RichLabel());

                GUILayout.FlexibleSpace();

                GUI.enabled = card != null && DeckRules.CanAdd(_deck, card);
                if (GUILayout.Button("+", GUILayout.Width(28f))) AddCard(card);
                GUI.enabled = true;

                if (GUILayout.Button("-", GUILayout.Width(28f))) _deck.Remove(entry.CardId);

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawLoadPanel()
        {
            if (_savedDecks.Count == 0)
            {
                GUILayout.Label("<i>No saved decks yet.</i>", RichLabel());
                return;
            }

            foreach (var name in new List<string>(_savedDecks))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(name))
                {
                    var loaded = DeckStorage.TryLoad(name, out string error);
                    if (loaded != null)
                    {
                        _deck = loaded;
                        _message = $"Loaded \"{name}\".";
                        _showLoadPanel = false;
                    }
                    else _message = error;
                }

                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    if (!DeckStorage.TryDelete(name, out string error)) _message = error;
                    else _message = $"Deleted \"{name}\".";
                    _savedDecks = DeckStorage.SavedDeckNames();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void AddCard(IPlayableCard card)
        {
            if (card == null) return;

            // Asks DeckRules rather than trusting that the button was drawn disabled — the same card
            // can be added from two places here, and a rule enforced at only some of its call sites
            // is a rule that will eventually be missed at one of them.
            if (!DeckRules.CanAdd(_deck, card))
            {
                _message = _deck.TotalCards >= DeckRules.DeckSize
                    ? $"Deck is already {DeckRules.DeckSize} cards."
                    : $"{card.DisplayName}: already at the {DeckRules.MaxCopiesOf(card.Kind)}-copy limit.";
                return;
            }

            _deck.Add(card.DefinitionId);
            _message = "";
        }

        /// <summary>
        /// Tops the deck up to a legal 45 with random legal additions. A testing convenience, not a
        /// game feature — it's what makes this screen immediately useful for producing a deck to hand
        /// to MatchView without 45 clicks. Keeps whatever is already in the deck.
        /// </summary>
        private void AutoFill()
        {
            var candidates = new List<IPlayableCard>();
            foreach (var entry in _pool) candidates.Add(entry.Card);

            var rng = new System.Random();
            int guard = DeckRules.DeckSize * 100; // The pool can run out of legal additions; never spin forever.

            while (_deck.TotalCards < DeckRules.DeckSize && guard-- > 0)
            {
                var card = candidates[rng.Next(candidates.Count)];
                if (DeckRules.CanAdd(_deck, card)) _deck.Add(card.DefinitionId);
            }

            _message = _deck.TotalCards == DeckRules.DeckSize
                ? "Filled to 45."
                : $"Could only reach {_deck.TotalCards} — the pool has no more legal additions.";
        }

        private static GUIStyle RichLabel()
        {
            return new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        }
    }
}
