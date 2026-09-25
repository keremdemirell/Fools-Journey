using UnityEngine;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// One shared material for every hex, tinted per-object with a MaterialPropertyBlock rather
    /// than by creating hundreds of material instances.
    ///
    /// The shader is looked up with a fallback chain because the right name depends on the render
    /// pipeline: this project is URP, where the Built-in "Standard"/"Diffuse" shaders don't exist
    /// and a missing shader renders as bright magenta.
    /// </summary>
    public static class ViewMaterials
    {
        private static Material _shared;
        private static MaterialPropertyBlock _block;

        public static Material Shared
        {
            get
            {
                if (_shared == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Sprites/Default")
                              ?? Shader.Find("Unlit/Color");
                    _shared = new Material(shader) { name = "ArcanaWars_HexShared" };
                }
                return _shared;
            }
        }

        /// <summary>Tints one renderer. Sets both URP's _BaseColor and the legacy _Color so it works whichever shader the fallback chain landed on.</summary>
        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;

            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            renderer.SetPropertyBlock(_block);
        }
    }
}
