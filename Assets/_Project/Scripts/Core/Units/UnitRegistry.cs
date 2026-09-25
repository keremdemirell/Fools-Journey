using System.Collections.Generic;
using System.Linq;
using ArcanaWars.Core.Board;

namespace ArcanaWars.Core.Units
{
    /// <summary>
    /// Ties units to the board: validates placement against HexGrid's occupancy/wall rules,
    /// keeps every occupied Tile.OccupantId in sync with the owning unit, and indexes units by
    /// id and by tile. Deliberately does not decide WHEN units die or move — RemoveUnit is an
    /// explicit call, made by whatever runs the Decay phase once that's built, not something
    /// this class triggers on its own.
    /// </summary>
    public class UnitRegistry
    {
        private readonly HexGrid _grid;
        private readonly Dictionary<int, UnitInstance> _units = new Dictionary<int, UnitInstance>();
        private int _nextId = 1;

        public UnitRegistry(HexGrid grid)
        {
            _grid = grid;
        }

        public IEnumerable<UnitInstance> AllUnits => _units.Values;

        /// <summary>
        /// Validates the footprint is free (via HexGrid.CanPlaceFootprint) AND that attackColumn is
        /// actually one of the columns the footprint occupies, then spawns and occupies it. Returns
        /// false and leaves `unit` null if either check fails.
        ///
        /// `roundPlaced` is stamped onto the unit for abilities that measure their own age (The
        /// Devil's doomsday clock) — see UnitInstance.RoundPlayed.
        /// </summary>
        /// <summary>
        /// Whether TryPlaceUnit would accept this shape here — the same two checks, asked without
        /// placing anything.
        ///
        /// It exists so a caller can find out that a play is doomed BEFORE doing anything
        /// irreversible, and the irreversible thing in question is consuming the match's RNG: The
        /// Fool's HP is rolled while building its stats (GDD 11.0), so a play rejected after that
        /// roll would advance the random stream without anything reaching the board. That makes a
        /// match unreplayable and, under lockstep networking, desyncs any client that filtered an
        /// illegal move locally instead of sending it. Note that neither check below looks at HP —
        /// which is exactly why the answer is trustworthy before the roll happens.
        /// </summary>
        public bool CanPlace(PlayerSide owner, HexCoord anchor, int attackColumn, FootprintShapeType footprint)
        {
            if (!_grid.CanPlaceFootprint(owner, anchor, footprint)) return false;

            var coords = _grid.GetFootprintCoords(anchor, footprint);
            return coords != null && coords.Any(c => c.ToOffset().Col == attackColumn);
        }

        public bool TryPlaceUnit(PlayerSide owner, HexCoord anchor, int attackColumn, UnitSpawnStats stats, out UnitInstance unit, int roundPlaced = 0)
        {
            if (!_grid.CanPlaceFootprint(owner, anchor, stats.Footprint))
            {
                unit = null;
                return false;
            }

            var footprintCoords = _grid.GetFootprintCoords(anchor, stats.Footprint);
            if (!footprintCoords.Any(c => c.ToOffset().Col == attackColumn))
            {
                unit = null;
                return false;
            }

            unit = new UnitInstance(_nextId++, owner, anchor, attackColumn, stats, roundPlaced);
            foreach (var coord in footprintCoords)
            {
                _grid.TryGetTile(coord, out var tile);
                tile.OccupantId = unit.Id;
            }

            _units[unit.Id] = unit;
            return true;
        }

        /// <summary>Clears the unit's occupied tiles and removes it from the registry. No-op if the id isn't registered.</summary>
        public void RemoveUnit(int unitId)
        {
            if (!_units.TryGetValue(unitId, out var unit)) return;

            foreach (var coord in _grid.GetFootprintCoords(unit.AnchorCoord, unit.Footprint))
            {
                if (_grid.TryGetTile(coord, out var tile) && tile.OccupantId == unitId)
                    tile.OccupantId = null;
            }

            _units.Remove(unitId);
        }

        /// <summary>
        /// Moves one or more units at once, all-or-nothing. Every destination tile must exist, be on
        /// the mover's own side, not be part of a wall, and be empty or held by another unit in the
        /// SAME batch — which is what lets two units swap places. Returns false without changing
        /// anything if any single move is illegal.
        ///
        /// A unit standing on any walled tile can't be moved at all: the Emperor's wall says "units
        /// inside cannot be moved" (GDD 11.IV), whoever built it. That's enforced here, at the one
        /// place movement happens, rather than left for each moving card to remember.
        /// </summary>
        public bool TryMoveUnits(IReadOnlyList<UnitMove> moves)
        {
            if (moves == null || moves.Count == 0) return false;

            var movingIds = new HashSet<int>();
            foreach (var move in moves)
                if (!movingIds.Add(move.UnitId)) return false; // the same unit twice in one batch

            var claimed = new HashSet<HexCoord>();
            var plans = new List<(UnitInstance unit, IReadOnlyList<HexCoord> from, IReadOnlyList<HexCoord> to, UnitMove move)>();

            foreach (var move in moves)
            {
                if (!_units.TryGetValue(move.UnitId, out var unit) || unit.IsDead) return false;

                var from = _grid.GetFootprintCoords(unit.AnchorCoord, unit.Footprint);
                foreach (var coord in from)
                    if (_grid.TryGetTile(coord, out var fromTile) && fromTile.IsWalled) return false;

                var to = _grid.GetFootprintCoords(move.NewAnchor, unit.Footprint);
                if (to == null || to.Count == 0) return false;

                bool columnInFootprint = false;
                foreach (var coord in to)
                {
                    if (!_grid.TryGetTile(coord, out var tile)) return false;
                    if (tile.Side != unit.Owner || tile.IsWalled) return false;
                    if (tile.OccupantId.HasValue && !movingIds.Contains(tile.OccupantId.Value)) return false;
                    if (!claimed.Add(coord)) return false; // two movers claiming one tile
                    if (coord.ToOffset().Col == move.NewAttackColumn) columnInFootprint = true;
                }

                if (!columnInFootprint) return false;
                plans.Add((unit, from, to, move));
            }

            // Vacate everything first, then occupy — otherwise a swap would have each unit find the
            // other still standing in its destination.
            foreach (var plan in plans)
                foreach (var coord in plan.from)
                    if (_grid.TryGetTile(coord, out var tile) && tile.OccupantId == plan.unit.Id)
                        tile.OccupantId = null;

            foreach (var plan in plans)
            {
                foreach (var coord in plan.to)
                {
                    _grid.TryGetTile(coord, out var tile);
                    tile.OccupantId = plan.unit.Id;
                }
                plan.unit.MoveTo(plan.move.NewAnchor, plan.move.NewAttackColumn);
            }

            return true;
        }

        public UnitInstance GetUnit(int unitId) => _units.TryGetValue(unitId, out var unit) ? unit : null;

        public UnitInstance GetUnitAt(HexCoord coord)
        {
            if (_grid.TryGetTile(coord, out var tile) && tile.OccupantId.HasValue)
                return GetUnit(tile.OccupantId.Value);
            return null;
        }
    }
}
