using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.GameLoop;
using ArcanaWars.Core.Match;

namespace ArcanaWars.Core.Session
{
    /// <summary>
    /// How two humans share one match: who is allowed to act right now, whose eyes the screen is
    /// being drawn for, and the single funnel every player action goes through.
    ///
    /// <para><b>Why this exists at all.</b> Before it, the view called MatchState directly and read
    /// the live simulation object for everything it drew. That is fine for one person at one screen
    /// and impossible for anything else: a second player at a second screen has no MatchState to
    /// read. Routing actions through SubmitX and visibility through CanSee gives one seam where that
    /// difference can live later — a networked session becomes a different implementation behind the
    /// same calls, rather than a rewrite of every UI call site. Building the real UI against this
    /// instead of against MatchState is the whole point; it is cheap now and expensive to retrofit.</para>
    ///
    /// <para><b>What it is NOT.</b> It holds no game rules. Whether a play is legal is still
    /// CardPlayResolver's answer and whose turn it is is still ActionTurns' — this only decides
    /// which human at which screen is currently allowed to ask, and what they may look at while
    /// they do. If a rule ever appears in here, it is in the wrong place.</para>
    ///
    /// <para>It lives in Core rather than Presentation for two reasons: it is engine-free and worth
    /// testing offline like everything else here, and "what may this player see" is a rules question
    /// in this game, not a UI one — The Moon (GDD 11.XVIII) hides HP from the opponent as a printed
    /// ability. It sits one layer above Match, so the dependency still runs one way.</para>
    /// </summary>
    public class MatchSession
    {
        public MatchState Match { get; }

        /// <summary>
        /// The sides a human at this screen may drive. Hot-seat passes both; a network client would
        /// pass one. A side that isn't here can never be acted for, whatever the turn order says.
        /// </summary>
        public IReadOnlyList<PlayerSide> LocalSides { get; }

        /// <summary>
        /// Whether hands are kept private between local players. True for hot-seat on one device
        /// (only the active player's hand is drawn, with a handover gate between turns); false for a
        /// shared screen where both players can see everything and simply agree not to look.
        /// </summary>
        public bool PrivateHands { get; }

        /// <summary>Whose eyes the screen is currently drawn for. Follows the turn, but only once the incoming player has acknowledged the handover.</summary>
        public PlayerSide ViewerSide { get; private set; }

        /// <summary>
        /// True while the turn has moved to the other local player but they haven't taken the
        /// controls yet. The view draws its "pass the device" curtain on this and hides everything
        /// private until AcknowledgeHandover is called.
        ///
        /// Only ever set when PrivateHands is on and both players are local — there is nothing to
        /// hand over otherwise.
        /// </summary>
        public bool HandoverPending { get; private set; }

        /// <summary>
        /// Goes up by one every time something a player could see may have changed: every applied
        /// command, local or remote, and every acknowledged handover. A screen keeps the last value
        /// it drew and redraws only when this moves — which is complete, not a heuristic, because
        /// every change to a match after it starts goes through one of this class's methods.
        ///
        /// Refused commands leave it alone, the same way they leave the match alone.
        /// </summary>
        public int Revision { get; private set; }

        public MatchSession(MatchState match, IReadOnlyList<PlayerSide> localSides = null, bool privateHands = true)
        {
            Match = match;
            LocalSides = localSides ?? new[] { PlayerSide.A, PlayerSide.B };
            PrivateHands = privateHands;
            ViewerSide = LocalSides.Count > 0 ? LocalSides[0] : PlayerSide.A;
        }

        /// <summary>A hot-seat session: both players share this screen, hands kept private behind a handover gate.</summary>
        public static MatchSession HotSeat(MatchState match) =>
            new MatchSession(match, new[] { PlayerSide.A, PlayerSide.B }, privateHands: true);

        /// <summary>A shared-screen session: both hands drawn at once, honour system. Useful for playtesting and for two people at one desk.</summary>
        public static MatchSession SharedScreen(MatchState match) =>
            new MatchSession(match, new[] { PlayerSide.A, PlayerSide.B }, privateHands: false);

        public bool IsLocal(PlayerSide side)
        {
            for (int i = 0; i < LocalSides.Count; i++)
                if (LocalSides[i] == side) return true;
            return false;
        }

        // --- What the screen may show ------------------------------------------------------------

        /// <summary>
        /// Whether this screen may draw that side's hand. Private information: a hand is only ever
        /// visible to its owner, and not even then while a handover is pending — that gate exists
        /// precisely so the outgoing player's cards are off screen before the next one looks.
        ///
        /// The board, both Souls and both mana pools are public and have no equivalent check; they
        /// are visible to both players in a face-to-face game too.
        /// </summary>
        public bool CanSeeHand(PlayerSide side)
        {
            if (!IsLocal(side)) return false;
            if (!PrivateHands) return true;
            return !HandoverPending && side == ViewerSide;
        }

        /// <summary>
        /// Where a card would land if it were played at this anchor, and whether the board would
        /// take it. For pointer feedback: a UI tints these tiles as the player moves the mouse.
        ///
        /// <para><b>A hint, not a verdict.</b> It answers only the two questions that depend on the
        /// tile under the pointer — the shape, and whether those tiles are free, on your own side and
        /// unwalled. Cost, turn order and the card's own conditions are NOT checked here; the play
        /// itself is the authority on those, and duplicating them would be a second copy of the rules
        /// that can disagree with the first.</para>
        ///
        /// <para>Never touches the RNG: the footprint comes from the same null-rng preview
        /// CardPlayResolver uses, so hovering over a Fool can't roll its HP.</para>
        /// </summary>
        public PlacementPreview PreviewPlacement(PlayerSide side, IPlayableCard card, HexCoord anchor, int attackColumn, int manaCommitted = 0)
        {
            if (card == null) return default;

            var shape = card.GetSpawnStats(manaCommitted).Footprint;
            var shapeTiles = Match.Board.GetFootprintCoords(anchor, shape);
            bool fits = shapeTiles != null && Match.Units.CanPlace(side, anchor, attackColumn, shape);

            // A shape resolves as pure geometry, so one anchored near an edge includes coordinates
            // that aren't on the board at all. Those are dropped here so the preview only ever names
            // real tiles — the placement is refused anyway, and a caller tinting tiles shouldn't have
            // to know which of them exist.
            List<HexCoord> tiles = null;
            if (shapeTiles != null)
            {
                tiles = new List<HexCoord>(shapeTiles.Count);
                foreach (var coord in shapeTiles)
                    if (Match.Board.Contains(coord)) tiles.Add(coord);
            }

            return new PlacementPreview(tiles, fits);
        }

        // --- Who may act -------------------------------------------------------------------------

        /// <summary>The side that may act right now, or null if nobody can (wrong phase, phase already over, or the turn belongs to someone not at this screen).</summary>
        public PlayerSide? ControllingSide
        {
            get
            {
                if (Match.IsOver) return null;
                if (Match.Rounds.CurrentPhase != GamePhase.Action) return null;
                if (Match.Rounds.ActionComplete) return null;

                var active = Match.Rounds.ActiveSide;
                if (!IsLocal(active)) return null;
                if (PrivateHands && (HandoverPending || active != ViewerSide)) return null;

                return active;
            }
        }

        public bool CanAct => ControllingSide.HasValue;

        /// <summary>Hands the controls to whoever the turn now belongs to. Called by the view when the incoming player confirms they are looking.</summary>
        public void AcknowledgeHandover()
        {
            if (!HandoverPending) return;

            ViewerSide = Match.Rounds.ActiveSide;
            HandoverPending = false;
            Revision++;
        }

        /// <summary>
        /// Whether this screen may answer Judgement rulings for that side right now. The rulings
        /// themselves are public (everyone saw the unit die); deciding them is the judge's, from the
        /// judge's own seat — so the same presence rule as a hand, without the turn check, since a
        /// ruling costs no turn.
        /// </summary>
        public bool CanJudge(PlayerSide side)
        {
            if (!IsLocal(side)) return false;
            return !PrivateHands || (!HandoverPending && side == ViewerSide);
        }

        // --- The command funnel ------------------------------------------------------------------
        // Everything a player can do goes through one of these five. They are deliberately the exact
        // set a networked session would have to serialise, which is what makes that a later swap
        // rather than a later rewrite.

        /// <summary>Plays a card for the controlling player. Refused — without touching the match — if this screen isn't allowed to act for that side right now.</summary>
        public CardPlayResult SubmitPlay(PlayerSide side, IPlayableCard card, HexCoord anchor, int attackColumn, int manaCommitted = 0, CardPlayOptions options = default)
        {
            if (ControllingSide != side)
                return CardPlayResult.Failed(CardPlayFailure.NotYourTurn, NotControllingReason(side));

            var result = Match.PlayCard(side, card, anchor, attackColumn, manaCommitted, options);
            if (result.Success) Changed();
            return result;
        }

        public bool SubmitPass(PlayerSide side, out string reason)
        {
            if (ControllingSide != side)
            {
                reason = NotControllingReason(side);
                return false;
            }

            if (!Match.TryPass(side, out reason)) return false;

            Changed();
            return true;
        }

        // --- Moves made somewhere else -----------------------------------------------------------
        // A move that arrived from another machine has ALREADY happened there. The checks above exist
        // to stop a person driving a side they aren't sitting at; applying them to a remote move would
        // reject the opponent's perfectly legal play for the crime of being the opponent's. So these
        // skip the local-presence gate — and only that gate. Everything that makes a move legal in the
        // first place (whose turn it is, cost, placement, play conditions) still runs, because it
        // lives in MatchState and the resolvers, not here. If one of those refuses a move the far
        // machine accepted, the two simulations have already diverged and the caller must treat it as
        // the desync it is rather than shrugging it off.

        /// <summary>Applies a card play made by a player on another machine.</summary>
        public CardPlayResult ApplyRemotePlay(PlayerSide side, IPlayableCard card, HexCoord anchor, int attackColumn, int manaCommitted = 0, CardPlayOptions options = default)
        {
            var result = Match.PlayCard(side, card, anchor, attackColumn, manaCommitted, options);
            if (result.Success) Changed();
            return result;
        }

        /// <summary>Applies a pass made by a player on another machine.</summary>
        public bool ApplyRemotePass(PlayerSide side, out string reason)
        {
            if (!Match.TryPass(side, out reason)) return false;
            Changed();
            return true;
        }

        /// <summary>Applies a Judgement ruling made by a player on another machine.</summary>
        public bool ApplyRemoteJudgement(int judgementId, JudgementVerdict verdict)
        {
            if (!Match.ResolveJudgement(judgementId, verdict)) return false;
            Revision++;
            return true;
        }

        /// <summary>Steps the match clock on. The view calls this when the Action phase is complete, and to walk through Combat/Decay/WinCheck.</summary>
        public void AdvancePhase()
        {
            Match.Rounds.AdvancePhase();
            Changed();
        }

        public void BeginNextRound()
        {
            Match.BeginNextRound();
            Changed();
        }

        /// <summary>Answers one of Judgement's pending rulings (GDD 11.XX). Only the judge may rule, and only from their own seat.</summary>
        public bool SubmitJudgement(PlayerSide side, int judgementId, JudgementVerdict verdict)
        {
            if (!CanJudge(side)) return false;

            foreach (var pending in Match.PendingJudgements)
                if (pending.Id == judgementId && pending.Judge != side) return false;

            if (!Match.ResolveJudgement(judgementId, verdict)) return false;
            Revision++;
            return true;
        }

        /// <summary>After any applied command: note the change for screens, then see whether the device must be handed over.</summary>
        private void Changed()
        {
            Revision++;
            RefreshHandover();
        }

        /// <summary>
        /// Raises the curtain whenever the turn has moved to the other local player. Called after
        /// every command rather than computed on demand, because "the turn changed" is an event —
        /// once acknowledged, ViewerSide catches up and there is nothing left to detect.
        ///
        /// Outside the Action phase there is no active player, so no handover is raised; the next
        /// one comes when the following Action phase opens.
        /// </summary>
        private void RefreshHandover()
        {
            if (HandoverPending) return;
            if (Match.Rounds.CurrentPhase != GamePhase.Action || Match.Rounds.ActionComplete) return;

            var active = Match.Rounds.ActiveSide;
            if (active == ViewerSide || !IsLocal(active)) return;

            // With no privacy there is nothing to hide and nobody to wait for, so the screen simply
            // follows the turn. Without this a shared-screen session would keep showing the player
            // who opened the match, since nothing else ever moves ViewerSide.
            if (!PrivateHands) ViewerSide = active;
            else HandoverPending = true;
        }

        private string NotControllingReason(PlayerSide side)
        {
            if (Match.IsOver) return "The match is over.";
            if (Match.Rounds.CurrentPhase != GamePhase.Action) return $"Cards are only played during the Action phase (currently {Match.Rounds.CurrentPhase}).";
            if (Match.Rounds.ActionComplete) return "Both players have passed; the Action phase is over.";
            if (HandoverPending) return $"Waiting for {Match.Rounds.ActiveSide} to take the controls.";
            if (!IsLocal(side)) return $"{side} is not played from this screen.";
            return $"It is {Match.Rounds.ActiveSide}'s turn.";
        }
    }
}
