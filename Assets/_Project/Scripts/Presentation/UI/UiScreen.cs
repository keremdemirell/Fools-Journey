using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcanaWars.Presentation.UI
{
    /// <summary>
    /// One full screen of UI (the lobby, the deck builder, the match), built from a UXML layout and
    /// shown or hidden as a whole. GameApp owns every screen and shows exactly one at a time; a
    /// screen's own overlays (curtain, pickers, panels) are layers inside its layout, not screens.
    ///
    /// <para>Plain C#, not a MonoBehaviour: a screen has no reason to exist in the scene hierarchy,
    /// and keeping it out means GameApp decides when it ticks instead of Unity's call order.</para>
    /// </summary>
    public abstract class UiScreen
    {
        /// <summary>The instantiated layout. Every lookup a screen does starts here.</summary>
        protected VisualElement Root { get; }

        public bool IsVisible => Root.style.display != DisplayStyle.None;

        protected UiScreen(VisualTreeAsset layout, VisualElement host)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout), $"{GetType().Name}: no UXML layout assigned. Drag it onto GameApp in the Inspector.");

            Root = layout.Instantiate();
            Root.AddToClassList("screen-host");

            // The instantiated container fills the screen but is invisible, so it must let clicks
            // through to the board wherever the layout itself doesn't draw anything.
            Root.pickingMode = PickingMode.Ignore;

            host.Add(Root);
            Root.style.display = DisplayStyle.None;
        }

        public virtual void Show() => Root.style.display = DisplayStyle.Flex;

        public virtual void Hide() => Root.style.display = DisplayStyle.None;

        /// <summary>Called by GameApp every frame while this screen is showing.</summary>
        public virtual void Tick() { }

        /// <summary>
        /// Finds a named element, or fails loudly naming it. A layout edited in UI Builder can lose or
        /// rename an element without any error at all; this turns that into a clear message at
        /// startup instead of a null reference the first time someone clicks.
        /// </summary>
        protected T Require<T>(string name, VisualElement scope = null) where T : VisualElement
        {
            var found = (scope ?? Root).Q<T>(name);
            if (found == null)
                throw new InvalidOperationException($"{GetType().Name}: the layout has no {typeof(T).Name} named \"{name}\". Check the element's Name in UI Builder.");
            return found;
        }

        protected static void SetVisible(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>
        /// Whether a screen point (Input.mousePosition: pixels, origin bottom-left) is over any drawn
        /// UI in this screen's panel. The board uses it so a click on a button never also lands on
        /// the tile behind it.
        ///
        /// Works because every full-screen container is set to picking-mode Ignore — only things that
        /// actually draw (panels, buttons, labels) can be picked.
        /// </summary>
        protected bool IsPointerOverUi(Vector2 screenPoint)
        {
            var panel = Root.panel;
            if (panel == null) return false;

            // UI Toolkit measures from the top-left, Input from the bottom-left.
            var flipped = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            var panelPoint = RuntimePanelUtils.ScreenToPanel(panel, flipped);
            return panel.Pick(panelPoint) != null;
        }
    }
}
