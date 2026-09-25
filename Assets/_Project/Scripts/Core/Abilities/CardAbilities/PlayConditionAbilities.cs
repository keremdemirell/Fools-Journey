using System;
using System.Collections.Generic;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// Cards whose only bespoke behaviour is their play condition (GDD section 11): Temperance and The
    /// World. Their stat blocks already do everything else, so with these conditions they are complete.
    ///
    /// Six other cards (The Magician, The Emperor, Wheel of Fortune, The Hanged Man, The Devil) started
    /// life here as condition-only stubs, and each moved into its own ability class once the rest of the
    /// card was built, so every card has exactly one home.
    /// </summary>
    public static class PlayConditions
    {
        /// <summary>
        /// XIV. Temperance — "at least one card of each element currently alive on the board."
        ///
        /// Read as YOUR OWN units of each element, not anybody's. The card's design note calls it a
        /// reward for "generalist deckbuilding", which only holds if the board state being asked for
        /// is one you built; counting the opponent's units would let their mono-element deck satisfy
        /// half your condition for you. A Magician that chose an element counts as that element.
        /// </summary>
        public sealed class Temperance : UnitAbility
        {
            public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
            {
                var alive = new HashSet<Element>();
                foreach (var unit in query.AllUnits)
                    if (unit.Owner == side && !unit.IsDead && unit.Element != null)
                        alive.Add(unit.Element.Value);

                foreach (Element element in Enum.GetValues(typeof(Element)))
                {
                    if (!alive.Contains(element))
                    {
                        reason = $"Temperance needs one of each element alive on your board (missing {element}).";
                        return false;
                    }
                }

                reason = null;
                return true;
            }
        }

        /// <summary>
        /// XXI. The World — "The entire board must be completely empty."
        ///
        /// Read as YOUR OWN SIDE only (user decision, 2026-09-07). The literal whole-board reading
        /// was considered and rejected: The World's footprint covers only your own side (see
        /// HexGrid's note on EntireBoard), so "the board" it needs to move into is your half, and
        /// requiring the opponent to also have nothing on the table would make the card depend on
        /// their play rather than yours.
        ///
        /// Asked of the TILES on that side rather than of unit ownership, so it stays correct if a
        /// future card ever puts a unit on the far side of the board.
        /// </summary>
        public sealed class World : UnitAbility
        {
            public override bool CanPlay(IMatchQuery query, PlayerSide side, out string reason)
            {
                foreach (var tile in query.Board.AllTiles)
                {
                    if (tile.Side != side || tile.IsEmpty) continue;
                    reason = "The World needs your entire side of the board to be empty.";
                    return false;
                }

                reason = null;
                return true;
            }
        }
    }
}
