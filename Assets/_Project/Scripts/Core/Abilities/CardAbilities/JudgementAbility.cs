using System;
using System.Collections.Generic;
using ArcanaWars.Core.Board;
using ArcanaWars.Core.Cards;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// XX. Judgement (GDD 11.XX) — "If the enemy Soul has more HP than yours, it matches your Soul HP.
    /// Then, spawns random previously died units on all empty tiles for both sides. If any friendly or
    /// foe unit dies, you can 'judge' them — deciding to return it to your hand or discard it
    /// permanently." Over-Pentacle 1 (a plain stat).
    ///
    /// EQUALIZE: the enemy Soul is lowered to yours with Soul.LowerTo — setting a Soul equal to another
    /// isn't a hit, so it fires no damage triggers and The Devil's immunity doesn't stop it.
    ///
    /// RESURRECT: each side's empty tiles are filled from THAT side's own graveyard, in random order,
    /// each dead unit at most once (it leaves the graveyard when it returns), until nothing more fits.
    /// "Previously died" includes tokens (3 of Swords, Trees). They come back as SPAWNS, like Death's
    /// replacements: unpaid, not in match history, their on-play effects don't fire.
    ///
    /// JUDGE: while Judgement lives, every death — friend or foe, sacrifices included — is queued for
    /// its owner to rule on (see PendingJudgement for why it's a queue rather than a pause). RETURN puts
    /// that unit's card into the JUDGE's hand, as written ("return it to YOUR hand") — so judging an
    /// enemy steals its card. DISCARD removes it from the graveyard for good. Judgement doesn't judge
    /// its own death: it has already left the board, and only survivors hear about deaths.
    /// </summary>
    public sealed class JudgementAbility : UnitAbility
    {
        public override void OnPlay(IAbilityContext ctx, UnitInstance self, HandCard heldCard, CardPlayOptions options)
        {
            var mine = ctx.GetSoul(self.Owner);
            var theirs = ctx.GetSoul(self.Owner.Opponent());
            if (theirs.CurrentHp > mine.CurrentHp)
                theirs.LowerTo(mine.CurrentHp);

            Resurrect(ctx, self.Owner);
            Resurrect(ctx, self.Owner.Opponent());
        }

        public override void OnAnyUnitDied(IAbilityContext ctx, UnitInstance self, UnitDeath death)
        {
            if (death.GraveyardEntry != null)
                ctx.QueueJudgement(self.Owner, death.GraveyardEntry);
        }

        private static void Resurrect(IAbilityContext ctx, PlayerSide side)
        {
            var entries = new List<GraveyardEntry>(ctx.GetGraveyard(side));
            Shuffle(entries, ctx.Rng);

            foreach (var entry in entries)
            {
                var anchors = new List<HexCoord>();
                foreach (var tile in ctx.Board.AllTiles)
                {
                    if (tile.Side != side || !tile.IsEmpty) continue;
                    if (BoardQueries.FitsReplacing(ctx, side, tile.Coord, entry.Stats.Footprint, -1, null))
                        anchors.Add(tile.Coord);
                }

                if (anchors.Count == 0) continue; // this one doesn't fit anywhere any more; a smaller one still might

                var anchor = anchors[ctx.Rng.Next(anchors.Count)];
                if (ctx.TrySpawnUnit(side, anchor, anchor.ToOffset().Col, entry.Stats, out _, entry.SourceCard))
                    ctx.RemoveFromGraveyard(entry);
            }
        }

        /// <summary>Fisher-Yates on the match RNG, so a seeded match resurrects the same units in the same places.</summary>
        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
