using System;

namespace ArcanaWars.Core.Economy
{
    /// <summary>
    /// One player's mana for the current round. Fully recomputed every round — mana never carries
    /// over (GDD 7.1) — from three sources:
    ///  - the fixed base curve (ManaCurve),
    ///  - the stacking +1/round bonus from Pentacle-keyword units on board (GDD 7.2), which is
    ///    capped by the same 14 hard cap as the base curve,
    ///  - Over-Pentacle bonuses (GDD 7.3), the only thing that can push the total past 14.
    ///
    /// A pending burn (The Fool's death penalty, GDD 11.0) is then subtracted from the result, never
    /// below zero. It is applied here, at the start of the round after the Fool died, rather than at
    /// the moment of death — see IAbilityContext.AddPendingManaBurn.
    ///
    /// This class doesn't know about units — how many Pentacle-keyword units are on board, or how
    /// much Over-Pentacle bonus is active, is counted by whatever owns the board state and passed
    /// into StartRound each round.
    /// </summary>
    public class PentacleLedger
    {
        public int RoundNumber { get; private set; }
        public int Available { get; private set; }
        public int Spent { get; private set; }
        public int Remaining => Available - Spent;

        public void StartRound(int roundNumber, int pentacleKeywordCount, int overPentacleBonus, int manaBurn = 0)
        {
            if (pentacleKeywordCount < 0) throw new ArgumentOutOfRangeException(nameof(pentacleKeywordCount));
            if (overPentacleBonus < 0) throw new ArgumentOutOfRangeException(nameof(overPentacleBonus));
            if (manaBurn < 0) throw new ArgumentOutOfRangeException(nameof(manaBurn));

            RoundNumber = roundNumber;
            int cappedBase = Math.Min(ManaCurve.BaseMana(roundNumber) + pentacleKeywordCount, ManaCurve.Cap);

            // The burn comes off AFTER the cap, so a Fool's death costs the same 1-6 Pentacles whether
            // or not the player was already sitting at the cap — otherwise a capped player would shrug
            // it off entirely and the penalty would only ever bite the player who could least afford it.
            Available = Math.Max(0, cappedBase + overPentacleBonus - manaBurn);
            Spent = 0;
        }

        /// <summary>
        /// Adds Pentacles to THIS round's pool mid-round — Wheel of Fortune's "gain 2 Pentacles"
        /// (GDD 11.X). Deliberately not capped at 14: the cap governs what a round starts with, and
        /// GDD 7.3's rule that Over-Pentacles are "the only way to reach 15" can't be broken by this
        /// anyway, because the Wheel costs 6 to gain 2 — the pool only ever goes down on net.
        /// </summary>
        public void AddPentacles(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Available += amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Remaining) return false;
            Spent += amount;
            return true;
        }
    }
}
