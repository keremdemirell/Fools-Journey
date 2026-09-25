using System;
using System.Collections.Generic;

namespace ArcanaWars.Core.Board
{
    /// <summary>
    /// Axial hex coordinate (Q, R). S is derived (Q + R + S = 0) and kept only as a
    /// convenience for distance math, never stored.
    ///
    /// Everything that needs true hex adjacency/distance/rings (attack facing, the
    /// Hermit's 2-tile radius, the 7-tile Queen/King cluster) should use HexCoord
    /// directly. Everything that needs a rectangular block (unit footprints like
    /// 4x4 or 2x3, described in the GDD) should go through OffsetCoord instead —
    /// see ToOffset()/FromOffset(). Axial has no native notion of "rectangle", so
    /// footprints are generated in offset-space and converted back.
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static readonly HexCoord Zero = new HexCoord(0, 0);

        // Clockwise starting east. Order matters only for callers that care about
        // facing (e.g. "the tile directly across the board"); Neighbors() below
        // does not guarantee an order beyond this.
        private static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1)
        };

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.Q - b.Q, a.R - b.R);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

        public HexCoord Neighbor(int direction) => this + Directions[((direction % 6) + 6) % 6];

        public IEnumerable<HexCoord> Neighbors()
        {
            foreach (var d in Directions)
                yield return this + d;
        }

        public int DistanceTo(HexCoord other)
        {
            var d = this - other;
            return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.S)) / 2;
        }

        /// <summary>
        /// The hex ring at exactly the given radius around this coord.
        /// Radius 0 is just this tile; radius 1 is the 6 immediate neighbors
        /// (used for the Queen/King "7-tile cluster": Ring(0) + Ring(1)).
        /// </summary>
        public IEnumerable<HexCoord> Ring(int radius)
        {
            if (radius == 0)
            {
                yield return this;
                yield break;
            }

            var cursor = this + Directions[4] * radius; // start radius steps in direction 4
            for (int side = 0; side < 6; side++)
            {
                for (int step = 0; step < radius; step++)
                {
                    yield return cursor;
                    cursor = cursor.Neighbor(side);
                }
            }
        }

        // --- Offset conversion: "odd-r" horizontal layout (odd rows shoved right). ---
        // Confirmed layout: Souls sit at the top and bottom of the board, so rows split
        // the board into each player's side (plus a possible neutral middle row), and
        // columns are the attack lanes a unit advances/attacks along toward the enemy Soul.
        public static HexCoord FromOffset(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            return new HexCoord(q, r);
        }

        public OffsetCoord ToOffset()
        {
            int col = Q + (R - (R & 1)) / 2;
            int row = R;
            return new OffsetCoord(col, row);
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => (Q, R).GetHashCode();
        public override string ToString() => $"Hex({Q},{R})";

        public static HexCoord operator *(HexCoord a, int scalar) => new HexCoord(a.Q * scalar, a.R * scalar);
    }

    public readonly struct OffsetCoord : IEquatable<OffsetCoord>
    {
        public readonly int Col;
        public readonly int Row;

        public OffsetCoord(int col, int row)
        {
            Col = col;
            Row = row;
        }

        public HexCoord ToAxial() => HexCoord.FromOffset(Col, Row);

        public bool Equals(OffsetCoord other) => Col == other.Col && Row == other.Row;
        public override bool Equals(object obj) => obj is OffsetCoord other && Equals(other);
        public override int GetHashCode() => (Col, Row).GetHashCode();
        public override string ToString() => $"Offset(col:{Col},row:{Row})";
    }
}
