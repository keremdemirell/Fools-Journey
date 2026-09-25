using System;
using UnityEngine.UIElements;
using ArcanaWars.Core;
using ArcanaWars.Core.Abilities;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation.UI
{
    /// <summary>
    /// The decisions a card wants answered at the moment it is played, and the controls that answer
    /// them: The Magician's element, The Hanged Man's and Death's target, and an Ace's "1 or all"
    /// (GDD 10.1, 11.I, 11.XII, 11.XIII).
    ///
    /// <para><b>Why these are answered before the play rather than during it.</b> Core takes them as
    /// CardPlayOptions alongside the anchor tile and attack column, which were already choices of
    /// exactly this kind — so there is no "pause resolution and prompt" machinery anywhere, and a
    /// refused play has still touched nothing. This class is just the controls; which choice a card
    /// wants comes from the snapshot, which got it from the ability itself.</para>
    ///
    /// <para>The target is NOT picked here — it is picked by clicking a unit on the board, because
    /// pointing at the thing you mean is clearer than picking it from a list. This holds the answer
    /// and offers the ways out ("play it without a target", "pick a different one").</para>
    /// </summary>
    public sealed class PlayChoiceBar
    {
        private readonly VisualElement _root;
        private readonly Label _prompt;
        private readonly VisualElement _buttons;

        /// <summary>Raised when the player changes an answer, so the screen can redraw and re-tint the board.</summary>
        public event Action Changed;

        /// <summary>The Magician's element. Null until chosen.</summary>
        public Element? ChosenElement { get; private set; }

        /// <summary>The unit The Hanged Man sacrifices or Death executes. Null until one is clicked.</summary>
        public int? TargetUnitId { get; private set; }

        /// <summary>The player said "no target" explicitly — different from "hasn't chosen yet", which is what makes the next board click place the card.</summary>
        public bool TargetSkipped { get; private set; }

        /// <summary>An Ace paying every remaining Pentacle instead of its minimum.</summary>
        public bool PayAll { get; private set; }

        public PlayChoiceBar(VisualElement root, Label prompt, VisualElement buttons)
        {
            _root = root;
            _prompt = prompt;
            _buttons = buttons;
        }

        public void Clear()
        {
            ChosenElement = null;
            TargetUnitId = null;
            TargetSkipped = false;
            PayAll = false;
        }

        /// <summary>Records the unit the player clicked on the board.</summary>
        public void SetTarget(int unitId)
        {
            TargetUnitId = unitId;
            TargetSkipped = false;
            Changed?.Invoke();
        }

        /// <summary>True while the next board click should pick a target rather than place the card.</summary>
        public bool AwaitingTargetClick(HandCardSnapshot card) =>
            card != null && card.Choice == PlayChoice.AllyTarget && TargetUnitId == null && !TargetSkipped;

        /// <summary>
        /// What to commit for a variable-cost card. Zero for everything else, which is what Core reads
        /// as "the printed cost" — see CardPlayResolver, where an Ace committing anything between its
        /// minimum and all of it is refused as neither of the two modes the card allows.
        /// </summary>
        public int ManaCommitted(HandCardSnapshot card, PlayerSnapshot player) =>
            card != null && card.HasVariableCost && PayAll ? player.ManaRemaining : 0;

        /// <summary>
        /// Rebuilds the bar for the selected card. Hidden entirely for a card that asks nothing, which
        /// is most of them.
        /// </summary>
        public void Draw(HandCardSnapshot card, PlayerSnapshot player, string targetName)
        {
            bool wanted = card != null && (card.HasVariableCost || card.Choice != PlayChoice.None);
            _root.style.display = wanted ? DisplayStyle.Flex : DisplayStyle.None;
            if (!wanted) return;

            _buttons.Clear();

            if (card.HasVariableCost) DrawAceCost(card, player);

            switch (card.Choice)
            {
                case PlayChoice.Element: DrawElementChoice(card); break;
                case PlayChoice.AllyTarget: DrawTargetChoice(card, targetName); break;
                default:
                    if (card.HasVariableCost) _prompt.text = $"{card.DisplayName} — how much do you commit?";
                    break;
            }
        }

        private void DrawAceCost(HandCardSnapshot card, PlayerSnapshot player)
        {
            // "Cost: 1 OR All Current Mana" (GDD 10.1) — two modes, not a slider, and the HP that
            // comes out equals what was spent. Before this existed the all-in mode was unreachable.
            AddButton($"Pay {card.Cost}", !PayAll, () => { PayAll = false; Changed?.Invoke(); });
            AddButton($"Pay all ({player.ManaRemaining}) — {player.ManaRemaining} HP", PayAll, () => { PayAll = true; Changed?.Invoke(); });
        }

        private void DrawElementChoice(HandCardSnapshot card)
        {
            _prompt.text = ChosenElement.HasValue
                ? $"{card.DisplayName} takes the 2 of {ChosenElement.Value}'s effect."
                : $"{card.DisplayName} — choose an element.";

            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                var chosen = element;
                AddButton(element.ToString(), ChosenElement == chosen, () => { ChosenElement = chosen; Changed?.Invoke(); });
            }
        }

        private void DrawTargetChoice(HandCardSnapshot card, string targetName)
        {
            if (TargetUnitId.HasValue)
                _prompt.text = $"{card.DisplayName} targets {targetName}. Now click where it goes.";
            else if (TargetSkipped)
                _prompt.text = $"{card.DisplayName} — no target. Click where it goes.";
            else
                _prompt.text = $"{card.DisplayName} — click one of your own units to target it.";

            // Only offered when the ability says the choice is optional (it is for The Hanged Man and
            // Death — both can be played as a plain unit); never invented by the UI.
            if (card.ChoiceIsOptional && !TargetSkipped)
                AddButton("Play without a target", false, () => { TargetUnitId = null; TargetSkipped = true; Changed?.Invoke(); });

            if (TargetUnitId.HasValue || TargetSkipped)
                AddButton("Pick a different target", false, () => { TargetUnitId = null; TargetSkipped = false; Changed?.Invoke(); });
        }

        private void AddButton(string text, bool active, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("button");
            button.AddToClassList("button--small");
            button.EnableInClassList("button--chosen", active);
            _buttons.Add(button);
        }
    }
}
