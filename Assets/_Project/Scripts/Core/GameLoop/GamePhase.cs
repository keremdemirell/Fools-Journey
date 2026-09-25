namespace ArcanaWars.Core.GameLoop
{
    /// <summary>
    /// The 5 phases from the GDD's core loop (section 2), one full cycle per round.
    ///
    /// Only the Action phase is turn-based. The other four involve no player decisions, so they
    /// still resolve for both players at once exactly as the GDD's diagram describes — it is only
    /// the Action phase that departed from it (see ActionTurns).
    /// </summary>
    public enum GamePhase
    {
        RoundStart, // grant Pentacles, draw card(s) — both players, simultaneously
        Action,     // players alternate, one card or a pass per turn, until both pass — see ActionTurns
        Combat,     // attacks resolve, blocking is applied
        Decay,      // ALL units on the board lose 1 HP; deaths and passive triggers happen
        WinCheck    // is either Soul at 0?
    }
}
