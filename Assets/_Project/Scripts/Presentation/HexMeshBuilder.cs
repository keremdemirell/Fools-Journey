using UnityEngine;

namespace ArcanaWars.Presentation
{
    /// <summary>
    /// Builds a flat pointy-top hexagon mesh in code, so the board needs no art assets to be
    /// visible. Triangles are emitted in both winding orders (12 instead of 6), which makes the
    /// hex double-sided — it costs nothing at this scale and removes the classic "my procedural
    /// mesh is invisible because the winding faces away from the camera" trap.
    /// </summary>
    public static class HexMeshBuilder
    {
        public static Mesh Build(float size)
        {
            var mesh = new Mesh { name = $"Hex_{size:0.###}" };

            // Centre plus 6 corners. Pointy-top: corner i sits at 60*i - 90 degrees, which puts
            // vertices at the top and bottom rather than the left and right.
            var vertices = new Vector3[7];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i - 90f);
                vertices[i + 1] = new Vector3(size * Mathf.Cos(angle), size * Mathf.Sin(angle), 0f);
            }

            var triangles = new int[6 * 3 * 2];
            int t = 0;
            for (int i = 0; i < 6; i++)
            {
                int a = i + 1;
                int b = (i + 1) % 6 + 1;

                triangles[t++] = 0;
                triangles[t++] = a;
                triangles[t++] = b;

                // Same triangle wound the other way, so it is visible from either side.
                triangles[t++] = 0;
                triangles[t++] = b;
                triangles[t++] = a;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
