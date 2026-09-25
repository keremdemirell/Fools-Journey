using System;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Economy;
using ArcanaWars.Core.Match;

namespace ArcanaWars.Core
{
    /// <summary>
    /// Everything belonging to one player: their Soul, deck, hand, mana, and the log of what they've
    /// played. Bundled together because the alternative — parallel PlayerA/PlayerB fields for each of
    /// those, spread across MatchState and RoundManager — makes every lookup a ternary and every new
    /// per-player thing a pair of fields to keep in sync.
    /// </summary>
    public class PlayerState
    {
        public PlayerSide Side { get; }
        public Soul Soul { get; }
        public Deck Deck { get; }
        public Hand Hand { get; }
        public PentacleLedger Mana { get; }

        /// <summary>Everything this player has played this match. Several Major Arcana conditions are about history, not board state — see MatchHistory.</summary>
        public MatchHistory History { get; }

        /// <summary>Every unit of this player's that has died this match and hasn't been brought back or judged away — Judgement's raw material (GDD 11.XX).</summary>
        public Graveyard Graveyard { get; }

        /// <summary>
        /// Pentacles owed against the NEXT round's pool — currently only The Fool's death penalty
        /// (GDD 11.0). Accumulates, so two Fools dying in the same round both bite; cleared as it is
        /// applied at the following round's start.
        /// </summary>
        public int PendingManaBurn { get; private set; }

        public PlayerState(PlayerSide side, Deck deck, int soulMaxHp = Soul.StandardStartingHp, int maxHandSize = HandRules.MaxHandSize)
        {
            Side = side;
            Soul = new Soul(side, soulMaxHp);
            Deck = deck ?? new Deck(null);
            Hand = new Hand(maxHandSize);
            Mana = new PentacleLedger();
            History = new MatchHistory();
            Graveyard = new Graveyard();
        }

        public void AddPendingManaBurn(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            PendingManaBurn += amount;
        }

        /// <summary>Consumes and returns the accrued burn. Called once per round by whoever grants mana, so a debt can never be charged twice.</summary>
        public int ConsumePendingManaBurn()
        {
            int burn = PendingManaBurn;
            PendingManaBurn = 0;
            return burn;
        }

        /// <summary>
        /// Draws up to `count` cards, stamping each with the round it entered the hand (needed by
        /// The High Priestess — see HandCard) and an optional per-copy cost discount (Wheel of
        /// Fortune). Returns how many actually reached the hand — fewer if the deck ran out, or if the
        /// hand was full: a card drawn into a full hand is burned to the discard pile (the standard TCG
        /// rule; the GDD doesn't specify this case).
        /// </summary>
        public int DrawCards(int count, int roundNumber, int costDiscount = 0, CardBuff buff = default)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                var card = Deck.Draw();
                if (card == null) break; // deck exhausted

                if (Hand.TryAdd(card, roundNumber, costDiscount, buff)) drawn++;
                else Deck.Discard(card); // burned — hand was full
            }
            return drawn;
        }
    }
}
