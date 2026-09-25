namespace ArcanaWars.Core.Board
{
    /// <summary>
    /// One hex tile's board-level state. Deliberately holds only board/terrain
    /// concerns (occupancy, ownership, terrain flags) — a unit's own stats live
    /// on its UnitInstance, not here, so a tile doesn't need to know unit internals.
    /// </summary>
    public class Tile
    {
        public HexCoord Coord { get; }

        /// <summary>Which player's deployment side this tile belongs to. Null for a neutral/contested tile, if any.</summary>
        public PlayerSide? Side { get; }

        /// <summary>Id of the unit instance occupying this tile, if any. A multi-tile unit occupies several tiles, each pointing at the same id.</summary>
        public int? OccupantId { get; set; }

        /// <summary>The Empress: friendly units played here gain +3 HP.</summary>
        public bool IsGreened { get; set; }

        // The Emperor's wall (GDD 11.IV). A wall tile is solid for EVERYONE — nobody can place a unit
        // on it or move one onto or off it, whoever built it (user's design, 2026-09-15). It used to
        // be counted per builder, back when the wall covered the Emperor's own columns and only
        // blocked the enemy; the new wall runs along both sides of the Emperor instead, so there's
        // nothing left to tell apart. Still a count rather than a bool: two Emperors can wall the same
        // column, and one coming down mustn't knock down the other's.
        private int _walls;

        /// <summary>Part of an Emperor's wall: no unit may be placed here, moved here, or moved away from here.</summary>
        public bool IsWalled => _walls > 0;

        public void AddWall() => _walls++;

        /// <summary>Never goes below zero, so tearing down a wall that was never raised (a spawned Emperor dying) is harmless.</summary>
        public void RemoveWall()
        {
            if (_walls > 0) _walls--;
        }

        public bool IsEmpty => OccupantId == null;

        public Tile(HexCoord coord, PlayerSide? side)
        {
            Coord = coord;
            Side = side;
        }
    }
}
