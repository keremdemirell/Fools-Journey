using System;

namespace ArcanaWars.Core.Economy
{
    /// <summary>The fixed base Pentacle curve from GDD 7.1. Same curve for both players, indexed by the shared round number.</summary>
    public static class ManaCurve
    {
        private static readonly int[] BaseByRound = { 3, 6, 9, 12 }; // rounds 1-4
        public const int Cap = 14; // reached at round 5+

        public static int BaseMana(int roundNumber)
        {
            if (roundNumber < 1) throw new ArgumentOutOfRangeException(nameof(roundNumber), "Round numbers start at 1.");
            return roundNumber <= BaseByRound.Length ? BaseByRound[roundNumber - 1] : Cap;
        }
    }
}
