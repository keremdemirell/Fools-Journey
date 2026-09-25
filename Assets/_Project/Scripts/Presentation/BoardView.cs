using System.Collections.Generic;
using UnityEngine;
using ArcanaWars.Core;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Session;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// The board in the world: hex tiles, unit pieces, and the camera framing them. Draws a
    /// MatchSnapshot and answers "which tile is under this screen point" — nothing else.
    ///
    /// <para>It never sees MatchState or the driver. Everything it draws comes from the snapshot the
    /// match screen hands it, so it cannot show something the session would have hidden, and it has
    /// no way to act on the match at all. Clicks are the match screen's business; this only
    /// translates a screen point into a tile.</para>
    ///
    /// <para>This is the non-UI half of the old debug IMGUI view, split out so the UI Toolkit screens
    /// can sit on top of it without either knowing how the other is built.</para>
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float hexSize = 0.5f;

        [Tooltip("Fraction of the screen height at the TOP kept clear of the board, for the top bar.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float reservedTop = 0.12f;

        [Tooltip("Fraction of the screen height at the BOTTOM kept clear of the board, for the hand.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float reservedBottom = 0.26f;

        [Tooltip("Leave empty to use the scene's Main Camera.")]
        [SerializeField] private Camera boardCamera;

        [Header("Colours")]
        [SerializeField] private Color playerASideColor = new Color(0.24f, 0.34f, 0.52f);
        [SerializeField] private Color playerBSideColor = new Color(0.52f, 0.28f, 0.30f);
        [SerializeField] private Color neutralSideColor = new Color(0.32f, 0.32f, 0.36f);
        [SerializeField] private Color wallColor = new Color(0.80f, 0.66f, 0.30f);
        [SerializeField] private Color greenedTint = new Color(0.35f, 0.75f, 0.40f);
        [SerializeField] private Color playerAUnitColor = new Color(0.42f, 0.68f, 1f);
        [SerializeField] private Color playerBUnitColor = new Color(1f, 0.5f, 0.45f);
        [SerializeField] private Color backgroundColor = new Color(0.10f, 0.10f, 0.13f);

        [Header("Placement feedback")]
        [SerializeField] private Color placementLegalColor = new Color(0.35f, 0.85f, 0.45f);
        [SerializeField] private Color placementIllegalColor = new Color(0.85f, 0.25f, 0.25f);

        [Tooltip("The unit a card is about to sacrifice or execute (The Hanged Man, Death).")]
        [SerializeField] private Color targetColor = new Color(0.95f, 0.80f, 0.30f);

        [Range(0f, 1f)]
        [SerializeField] private float placementTintWeight = 0.55f;

        private Mesh _hexMesh;
        private Transform _tileRoot;
        private Transform _unitRoot;

        private readonly Dictionary<HexCoord, TileView> _tiles = new Dictionary<HexCoord, TileView>();

        // The anchor each view was built at. A unit that moved (the Chariot) has its view rebuilt
        // rather than nudged — cheap, and it can't leave a piece behind on a tile it left.
        private readonly Dictionary<int, UnitView> _units = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, HexCoord> _unitAnchors = new Dictionary<int, HexCoord>();

        private HexCoord? _hovered;
        private Vector2Int _framedFor;

        // The tiles currently tinted, remembered so they can be cleared without re-tinting the whole
        // board every time the pointer moves one hex. Two independent reasons a tile can be tinted:
        // it is part of the card's footprint under the pointer, or it is the unit that card will act
        // on. They are kept apart because either can change without the other.
        private readonly List<HexCoord> _preview = new List<HexCoord>();
        private readonly List<HexCoord> _target = new List<HexCoord>();
        private readonly List<HexCoord> _tinted = new List<HexCoord>();
        private bool _previewLegal;

        public bool IsBuilt => _tileRoot != null;
        public float HexSize => hexSize;
        public Camera Camera => boardCamera;

        /// <summary>
        /// Goes up every time the camera is re-framed. UI pinned to board positions (the HP labels)
        /// watches this rather than the screen size: the re-frame happens in LateUpdate, after the UI
        /// has already ticked, so a screen-size check would place the labels using the old framing and
        /// then never revisit them.
        /// </summary>
        public int FramingVersion { get; private set; }

        public Color SideColor(PlayerSide side) => side == PlayerSide.A ? playerAUnitColor : playerBUnitColor;

        /// <summary>Lays the board out for the first time, from the shape the snapshot describes, and frames the camera on it.</summary>
        public void Build(MatchSnapshot snapshot)
        {
            Clear();

            if (_hexMesh == null) _hexMesh = HexMeshBuilder.Build(hexSize);
            if (boardCamera == null) boardCamera = Camera.main;

            _tileRoot = new GameObject("Tiles").transform;
            _tileRoot.SetParent(transform, false);
            _unitRoot = new GameObject("Units").transform;
            _unitRoot.SetParent(transform, false);

            foreach (var tile in snapshot.Tiles)
                _tiles[tile.Coord] = TileView.Create(_tileRoot, tile.Coord, _hexMesh, hexSize, TileColor(tile));

            FitCamera();
            Refresh(snapshot);
        }

        /// <summary>Brings tiles and units in line with a newer snapshot: tints (walls, greening), new units, dead units, moved units.</summary>
        public void Refresh(MatchSnapshot snapshot)
        {
            if (!IsBuilt) return;

            foreach (var tile in snapshot.Tiles)
            {
                if (!_tiles.TryGetValue(tile.Coord, out var view)) continue;
                view.SetBaseColor(TileColor(tile));
            }

            var live = new HashSet<int>();
            foreach (var unit in snapshot.Units)
            {
                live.Add(unit.Id);

                if (_units.TryGetValue(unit.Id, out var existing))
                {
                    if (_unitAnchors[unit.Id].Equals(unit.Anchor)) continue;
                    Destroy(existing.gameObject);
                }

                _units[unit.Id] = UnitView.Create(_unitRoot, unit.Id, unit.DefinitionId, unit.Footprint, _hexMesh, hexSize, SideColor(unit.Owner));
                _unitAnchors[unit.Id] = unit.Anchor;
            }

            var stale = new List<int>();
            foreach (var id in _units.Keys)
                if (!live.Contains(id)) stale.Add(id);

            foreach (var id in stale)
            {
                if (_units[id] != null) Destroy(_units[id].gameObject);
                _units.Remove(id);
                _unitAnchors.Remove(id);
            }
        }

        /// <summary>
        /// Tints the tiles a card would cover, green if the board would take it there and red if not.
        /// The tiles and the verdict both come from Core (MatchSession.PreviewPlacement) — this only
        /// paints them.
        /// </summary>
        public void SetPlacementPreview(IReadOnlyList<HexCoord> tiles, bool legal)
        {
            _preview.Clear();
            if (tiles != null) _preview.AddRange(tiles);
            _previewLegal = legal;
            RefreshTints();
        }

        public void ClearPlacementPreview()
        {
            if (_preview.Count == 0) return;
            _preview.Clear();
            RefreshTints();
        }

        /// <summary>Marks the unit a card is about to act on — the one The Hanged Man sacrifices, or Death executes. Null clears it.</summary>
        public void SetTargetHighlight(IReadOnlyList<HexCoord> tiles)
        {
            _target.Clear();
            if (tiles != null) _target.AddRange(tiles);
            RefreshTints();
        }

        /// <summary>
        /// Repaints both tint sets. The placement preview is applied last so that, where a card would
        /// be placed over its own target, the answer the player needs (can it go here?) wins.
        /// </summary>
        private void RefreshTints()
        {
            foreach (var coord in _tinted)
                if (_tiles.TryGetValue(coord, out var view)) view.ClearTint();

            _tinted.Clear();

            foreach (var coord in _target)
            {
                if (!_tiles.TryGetValue(coord, out var view)) continue;
                view.SetTint(targetColor, placementTintWeight);
                _tinted.Add(coord);
            }

            var previewColor = _previewLegal ? placementLegalColor : placementIllegalColor;
            foreach (var coord in _preview)
            {
                if (!_tiles.TryGetValue(coord, out var view)) continue;
                view.SetTint(previewColor, placementTintWeight);
                _tinted.Add(coord);
            }
        }

        /// <summary>Re-frames when the window is resized; the framing depends on the screen's aspect.</summary>
        private void LateUpdate()
        {
            if (IsBuilt && _framedFor != new Vector2Int(Screen.width, Screen.height)) FitCamera();
        }

        /// <summary>Removes every tile and unit — for leaving a match.</summary>
        public void Clear()
        {
            if (_tileRoot != null) Destroy(_tileRoot.gameObject);
            if (_unitRoot != null) Destroy(_unitRoot.gameObject);
            _tileRoot = null;
            _unitRoot = null;
            _tiles.Clear();
            _units.Clear();
            _unitAnchors.Clear();
            _preview.Clear();
            _target.Clear();
            _tinted.Clear();
            _hovered = null;
        }

        /// <summary>
        /// The tile under a screen point (pixels, origin bottom-left — what Input.mousePosition
        /// gives), or null. Nearest tile centre wins; on a hex grid the nearest centre IS the hex
        /// under the point, so this needs no colliders and no coordinate rounding.
        /// </summary>
        public HexCoord? TileAtScreenPoint(Vector2 screenPoint)
        {
            if (!IsBuilt || boardCamera == null) return null;

            var world = boardCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -boardCamera.transform.position.z));

            HexCoord? best = null;
            float bestDistance = hexSize * hexSize;

            foreach (var pair in _tiles)
            {
                float d = ((Vector2)(pair.Value.transform.position - world)).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pair.Key;
                }
            }

            return best;
        }

        /// <summary>Where a tile's centre lands on screen, in pixels with origin bottom-left. For UI that follows the board (HP labels).</summary>
        public Vector2 TileToScreenPoint(HexCoord coord)
        {
            var world = transform.TransformPoint(HexLayout.ToWorld(coord, hexSize));
            return boardCamera != null ? (Vector2)boardCamera.WorldToScreenPoint(world) : Vector2.zero;
        }

        /// <summary>
        /// The screen point at the middle of a footprint. Used instead of the anchor because a
        /// multi-tile unit's anchor is a corner (and the 7-tile cluster's is its centre) — a label on
        /// the anchor would sit off to one side of a Queen and squarely on top of her neighbour.
        /// </summary>
        public Vector2 FootprintCentreScreenPoint(IReadOnlyList<HexCoord> footprint)
        {
            if (footprint == null || footprint.Count == 0) return Vector2.zero;

            var sum = Vector3.zero;
            foreach (var coord in footprint) sum += HexLayout.ToWorld(coord, hexSize);

            var world = transform.TransformPoint(sum / footprint.Count);
            return boardCamera != null ? (Vector2)boardCamera.WorldToScreenPoint(world) : Vector2.zero;
        }

        /// <summary>Lightens one tile under the pointer. Null clears it.</summary>
        public void SetHover(HexCoord? coord)
        {
            if (System.Nullable.Equals(_hovered, coord)) return;

            if (_hovered.HasValue && _tiles.TryGetValue(_hovered.Value, out var old)) old.SetHighlighted(false);
            _hovered = coord;
            if (_hovered.HasValue && _tiles.TryGetValue(_hovered.Value, out var now)) now.SetHighlighted(true);
        }

        private Color TileColor(TileSnapshot tile)
        {
            if (tile.IsWalled) return wallColor;

            var side = tile.Side == PlayerSide.A ? playerASideColor
                     : tile.Side == PlayerSide.B ? playerBSideColor
                     : neutralSideColor;

            return tile.IsGreened ? Color.Lerp(side, greenedTint, 0.5f) : side;
        }

        /// <summary>
        /// Frames the whole board inside the part of the screen the UI leaves free — below the top
        /// bar, above the hand — rather than the whole screen, so no tile ends up under a panel
        /// where it can be seen but not clicked.
        /// </summary>
        private void FitCamera()
        {
            if (boardCamera == null)
            {
                var go = new GameObject("Board Camera") { tag = "MainCamera" };
                boardCamera = go.AddComponent<Camera>();
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var tile in _tiles.Values)
            {
                var p = tile.transform.position;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            var centre = (min + max) * 0.5f;
            float halfHeight = (max.y - min.y) * 0.5f + hexSize * 1.5f;
            float halfWidth = (max.x - min.x) * 0.5f + hexSize * 1.5f;

            // The board gets this fraction of the screen height; its centre sits in the middle of it.
            float band = Mathf.Max(0.2f, 1f - reservedTop - reservedBottom);
            float bandCentre = reservedBottom + band * 0.5f;

            float size = Mathf.Max(halfHeight / band, halfWidth / Mathf.Max(boardCamera.aspect, 0.01f));

            boardCamera.orthographic = true;
            boardCamera.orthographicSize = size;
            boardCamera.transform.SetPositionAndRotation(
                new Vector3(centre.x, centre.y - (bandCentre - 0.5f) * 2f * size, -10f),
                Quaternion.identity);
            boardCamera.backgroundColor = backgroundColor;
            boardCamera.clearFlags = CameraClearFlags.SolidColor;

            _framedFor = new Vector2Int(Screen.width, Screen.height);
            FramingVersion++;
        }
    }
}
