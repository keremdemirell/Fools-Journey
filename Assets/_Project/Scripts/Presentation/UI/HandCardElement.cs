using System;
using UnityEngine.UIElements;
using ArcanaWars.Core;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation.UI
{
    /// <summary>
    /// One card in the hand. Built in C# rather than from a UXML template because it is a repeated
    /// item — the same reason ListView builds its rows in code — while everything about how it looks
    /// stays in Theme.uss.
    ///
    /// <para>It holds no judgement of its own: whether it is affordable, whether its condition is met
    /// and what that condition says all arrive on the HandCardSnapshot, already decided by Core.</para>
    /// </summary>
    public sealed class HandCardElement : VisualElement
    {
        private readonly Label _name = new Label();
        private readonly Label _cost = new Label();
        private readonly Label _detail = new Label();

        /// <summary>The card this element is currently showing. What a play command names.</summary>
        public string DefinitionId { get; private set; }

        /// <summary>Its place in the hand at the last capture — what selection is remembered by.</summary>
        public int Index { get; private set; }

        /// <summary>Why it can't be played, or null. Shown when the player selects it.</summary>
        public string BlockedReason { get; private set; }

        public event Action<HandCardElement> Clicked;

        public HandCardElement()
        {
            AddToClassList("hand-card");

            _name.AddToClassList("hand-card__name");
            _cost.AddToClassList("hand-card__cost");
            _detail.AddToClassList("hand-card__detail");

            var header = new VisualElement();
            header.AddToClassList("hand-card__header");
            header.Add(_cost);
            header.Add(_name);

            Add(header);
            Add(_detail);

            // Clickable handles press-and-release on the same element, which a raw ClickEvent
            // subscription does not — a drag off the card should not count as a click.
            this.AddManipulator(new Clickable(() => Clicked?.Invoke(this)));
        }

        public void Bind(HandCardSnapshot card, bool selected)
        {
            DefinitionId = card.DefinitionId;
            Index = card.Index;
            BlockedReason = card.ConditionMet ? null : card.ConditionReason;

            _name.text = card.DisplayName;

            // An Ace's cost is "1 OR all your current mana" (GDD 10.1) — not a number until the
            // player picks a mode, which is the pay-1/pay-all choice coming in the next batch.
            _cost.text = card.HasVariableCost ? "1+" : card.Cost.ToString();

            _detail.text = DetailLine(card);
            tooltip = card.ConditionMet ? "" : card.ConditionReason;

            EnableInClassList("hand-card--selected", selected);
            EnableInClassList("hand-card--unaffordable", !card.Affordable);
            EnableInClassList("hand-card--blocked", !card.ConditionMet);
            EnableInClassList("hand-card--discounted", card.CostDiscount > 0);

            foreach (Element element in Enum.GetValues(typeof(Element)))
                EnableInClassList(ElementClass(element), card.Element == element);

            EnableInClassList("hand-card--major", card.Element == null);
        }

        private static string DetailLine(HandCardSnapshot card)
        {
            string kind = card.Element.HasValue ? card.Element.Value.ToString() : "Major Arcana";
            if (card.Kind != CardKind.MinorArcana && card.Kind != CardKind.MajorArcana)
                kind = $"{card.Kind} of {kind}";

            if (card.CostDiscount > 0) kind += $"   -{card.CostDiscount}";

            // The Hermit's draw buff rides on this specific copy (GDD 11.IX), so it belongs on the
            // card rather than anywhere on the board.
            if (!card.Buff.IsNone) kind += $"   +{card.Buff.Hp}hp/+{card.Buff.Attack}atk/+{card.Buff.Heal}heal";

            return kind;
        }

        private static string ElementClass(Element element) => "hand-card--" + element.ToString().ToLowerInvariant();
    }
}
