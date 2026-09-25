using ArcanaWars.Core.Board;

namespace ArcanaWars.Core.Units
{
    /// <summary>
    /// One unit's part in a move: where its anchor goes and which column it attacks from afterwards.
    /// Moves are applied in batches (UnitRegistry.TryMoveUnits) because the only mover so far, The
    /// Chariot, can swap places with the friendly units it pushes — and a swap can't be done one unit
    /// at a time without each blocking the other.
    /// </summary>
    public readonly struct UnitMove
    {
        public readonly int UnitId;
        public readonly HexCoord NewAnchor;
        public readonly int NewAttackColumn;

        public UnitMove(int unitId, HexCoord newAnchor, int newAttackColumn)
        {
            UnitId = unitId;
            NewAnchor = newAnchor;
            NewAttackColumn = newAttackColumn;
        }
    }
}
