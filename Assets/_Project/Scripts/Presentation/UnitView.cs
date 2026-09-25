using System.Collections.Generic;
using UnityEngine;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// The on-screen stand-in for one UnitInstance: a smaller hex per occupied tile, drawn slightly
    /// in front of the board. It holds only the unit's id and reads everything else from Core —
    /// nothing about the unit's state is duplicated here, so the view can never disagree with the
    /// simulation.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        public int UnitId { get; private set; }

        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();

        public static UnitView Create(Transform parent, UnitInstance unit, IReadOnlyList<HexCoord> footprint, Mesh mesh, float size, Color color) =>
            Create(parent, unit.Id, unit.DefinitionId, footprint, mesh, size, color);

        /// <summary>The form BoardView uses — it only ever has a snapshot of the unit, never the live UnitInstance.</summary>
        public static UnitView Create(Transform parent, int unitId, string definitionId, IReadOnlyList<HexCoord> footprint, Mesh mesh, float size, Color color)
        {
            var go = new GameObject($"Unit {unitId} ({definitionId})");
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<UnitView>();
            view.UnitId = unitId;

            // A multi-tile unit gets one hex per tile it occupies, so a 2x3 actually looks like a 2x3.
            foreach (var coord in footprint)
            {
                var piece = new GameObject($"Piece {coord.ToOffset()}");
                piece.transform.SetParent(go.transform, false);

                // Slightly toward the camera (-Z) so units draw over the board tiles.
                var pos = HexLayout.ToWorld(coord, size);
                piece.transform.localPosition = new Vector3(pos.x, pos.y, -0.1f);
                piece.transform.localScale = Vector3.one * 0.72f;

                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = piece.AddComponent<MeshRenderer>();
                r.sharedMaterial = ViewMaterials.Shared;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                ViewMaterials.SetColor(r, color);
                view._renderers.Add(r);
            }

            return view;
        }

        public void SetColor(Color color)
        {
            foreach (var r in _renderers)
                ViewMaterials.SetColor(r, color);
        }
    }
}
