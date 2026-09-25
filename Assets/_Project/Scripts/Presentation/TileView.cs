using UnityEngine;
using ArcanaWars.Core.Board;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// One hex on screen. Knows which board coordinate it represents and how to tint itself; owns no
    /// game rules.
    ///
    /// <para>Three layers of colour, applied in this order: the tile's own colour (side, wall,
    /// greening), then a tint (the footprint of the card being placed — green if it fits, red if
    /// not), then the hover highlight. They stack rather than overwrite, so a tile can be part of a
    /// legal placement AND under the pointer at once and still read as both.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TileView : MonoBehaviour
    {
        public HexCoord Coord { get; private set; }

        private MeshRenderer _renderer;
        private Color _baseColor;
        private Color _tintColor;
        private float _tintWeight;
        private bool _highlighted;

        public static TileView Create(Transform parent, HexCoord coord, Mesh mesh, float size, Color color)
        {
            var go = new GameObject($"Tile {coord.ToOffset()}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = HexLayout.ToWorld(coord, size);

            var view = go.AddComponent<TileView>();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            view._renderer = go.GetComponent<MeshRenderer>();
            view._renderer.sharedMaterial = ViewMaterials.Shared;
            view._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view._renderer.receiveShadows = false;

            view.Coord = coord;
            view.SetBaseColor(color);
            return view;
        }

        /// <summary>The tile's own colour — its side, or a wall, or greening. Keeps any tint and hover on top of it.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            Apply();
        }

        /// <summary>Placement feedback. Weight 0 clears it.</summary>
        public void SetTint(Color color, float weight)
        {
            _tintColor = color;
            _tintWeight = Mathf.Clamp01(weight);
            Apply();
        }

        public void ClearTint() => SetTint(Color.clear, 0f);

        /// <summary>Temporary lightening under the pointer.</summary>
        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            Apply();
        }

        private void Apply()
        {
            var color = _tintWeight > 0f ? Color.Lerp(_baseColor, _tintColor, _tintWeight) : _baseColor;
            if (_highlighted) color = Color.Lerp(color, Color.white, 0.45f);
            ViewMaterials.SetColor(_renderer, color);
        }
    }
}
