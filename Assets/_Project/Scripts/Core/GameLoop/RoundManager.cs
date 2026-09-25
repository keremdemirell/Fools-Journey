using System;

namespace ArcanaWars.Core.GameLoop
{
    /// <summary>
    /// The match clock: what round is it, what phase are we in, and — inside the Action phase —
    /// whose turn it is.
    ///
    /// That last part is new (2026-09-16). The Action phase used to be simultaneous, so the players
    /// were fully symmetric and there was no "active player" to track; it now alternates one card at
    /// a time, which makes turn ownership genuine clock state. The rule itself lives in ActionTurns
    /// rather than here — read that class for what a pass means and why the starter alternates.
    ///
    /// It used to also own both players' mana ledgers, which was a second job it had no business
    /// doing — mana belongs to a player, not to the clock. That now lives on PlayerState, and
    /// MatchState grants it in response to the RoundStart phase (which is exactly what the GDD's
    /// loop diagram says that phase is for: "Receive Pentacles, Draw Card(s)").
    ///
    /// This class deliberately resolves nothing itself, and that extends to turns: it will report
    /// that the Action phase is complete, but it never advances the phase on its own. Whoever is
    /// driving the match calls AdvancePhase — see MatchState.Pass for why.
    /// </summary>
    public class RoundManager
    {
        public int RoundNumber { get; private set; }
        public GamePhase CurrentPhase { get; private set; }

        public event Action<int> RoundStarted;
        public event Action<GamePhase> PhaseChanged;

        private readonly ActionTurns _turns = new ActionTurns();
        private bool _started;

        /// <summary>Whose turn it is in the Action phase. Outside that phase it holds whatever it was last — check CurrentPhase before trusting it.</summary>
        public PlayerSide ActiveSide => _turns.ActiveSide;

        /// <summary>True once both players have passed in succession, meaning the Action phase is over and the round should move on to Combat.</summary>
        public bool ActionComplete => _turns.IsComplete;

        /// <summary>How many passes have happened back to back this Action phase. Any card played resets it.</summary>
        public int ConsecutivePasses => _turns.ConsecutivePasses;

        /// <summary>Increments the round number and enters RoundStart, firing PhaseChanged so subscribers can grant mana and deal draws.</summary>
        public void BeginNextRound()
        {
            if (_started && CurrentPhase != GamePhase.WinCheck)
                throw new InvalidOperationException($"Cannot start a new round mid-phase ({CurrentPhase}); advance through WinCheck first.");

            _started = true;
            RoundNumber++;
            SetPhase(GamePhase.RoundStart);
            RoundStarted?.Invoke(RoundNumber);
        }

        /// <summary>Moves to the next phase in sequence. Throws at WinCheck — call BeginNextRound to start the next round instead.</summary>
        public void AdvancePhase()
        {
            if (!_started)
                throw new InvalidOperationException("Call BeginNextRound before advancing phases.");

            switch (CurrentPhase)
            {
                case GamePhase.RoundStart: SetPhase(GamePhase.Action); break;
                case GamePhase.Action: SetPhase(GamePhase.Combat); break;
                case GamePhase.Combat: SetPhase(GamePhase.Decay); break;
                case GamePhase.Decay: SetPhase(GamePhase.WinCheck); break;
                case GamePhase.WinCheck:
                    throw new InvalidOperationException("Round is complete; call BeginNextRound to start the next one.");
            }
        }

        /// <summary>
        /// Records that the active player played a card, handing the turn over. Called by MatchState
        /// only after a play has actually succeeded — a rejected play costs nothing, turn included.
        /// </summary>
        public void NoteActionTaken()
        {
            if (CurrentPhase != GamePhase.Action)
                throw new InvalidOperationException($"Cards can only be played during the Action phase (currently {CurrentPhase}).");

            _turns.NoteActionTaken();
        }

        /// <summary>Records that the active player passed. Two in a row set ActionComplete; see ActionTurns.</summary>
        public void NotePassed()
        {
            if (CurrentPhase != GamePhase.Action)
                throw new InvalidOperationException($"There is no turn to pass outside the Action phase (currently {CurrentPhase}).");

            _turns.NotePassed();
        }

        private void SetPhase(GamePhase phase)
        {
            // Started here rather than by the caller so that entering the Action phase and having a
            // turn order can't come apart — every route into the phase goes through this one method.
            if (phase == GamePhase.Action) _turns.Begin(RoundNumber);

            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
