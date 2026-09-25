namespace ArcanaWars.Core
{
    /// <summary>The two players in a 1v1 match. Shared by board, units, economy, and Soul state.</summary>
    public enum PlayerSide
    {
        A,
        B
    }

    public static class PlayerSideExtensions
    {
        public static PlayerSide Opponent(this PlayerSide side) => side == PlayerSide.A ? PlayerSide.B : PlayerSide.A;
    }
}
