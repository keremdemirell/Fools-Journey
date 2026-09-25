using System.Collections.Generic;
using System.Text;
using ArcanaWars.Core.Match;
using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Session
{
    /// <summary>
    /// A complete, order-stable text summary of everything a match consists of.
    ///
    /// <para><b>Why this exists.</b> Lockstep networking is only safe if the same commands applied
    /// to the same starting state produce the same result on two machines. "Seems to work" is not a
    /// test of that — the failure mode is a slow drift that shows up rounds later as two players
    /// seeing different boards. So the way to know is to run a match, replay its commands from the
    /// same seed, and compare everything, then compare again after every single command so a
    /// divergence is pinned to the command that caused it rather than found at the end.</para>
    ///
    /// <para><b>Order stability is the whole trick.</b> Units live in a Dictionary, whose iteration
    /// order the runtime does not promise, so everything here is sorted by a stable key before being
    /// written. A fingerprint that sorted nothing would report false differences, and — far worse —
    /// could report a match as identical while hiding a real one behind a coincidental ordering.</para>
    ///
    /// <para>Text rather than a hash on purpose: when two fingerprints differ, you want to see which
    /// line differs. A 64-bit number tells you only that your evening is ruined.</para>
    /// </summary>
    public static class MatchFingerprint
    {
        public static string Of(MatchState match)
        {
            var sb = new StringBuilder();

            sb.Append("round=").Append(match.Rounds.RoundNumber)
              .Append(" phase=").Append(match.Rounds.CurrentPhase)
              .Append(" active=").Append(match.Rounds.ActiveSide)
              .Append(" actionComplete=").Append(match.Rounds.ActionComplete)
              .Append(" passes=").Append(match.Rounds.ConsecutivePasses)
              .Append(" over=").Append(match.IsOver)
              .Append(" winner=").Append(match.Winner.HasValue ? match.Winner.Value.ToString() : "-")
              .AppendLine();

            AppendPlayer(sb, match, PlayerSide.A);
            AppendPlayer(sb, match, PlayerSide.B);
            AppendUnits(sb, match);
            AppendBoard(sb, match);
            AppendJudgements(sb, match);

            return sb.ToString();
        }

        private static void AppendPlayer(StringBuilder sb, MatchState match, PlayerSide side)
        {
            var player = match.GetPlayer(side);

            sb.Append(side).Append(": soul=").Append(player.Soul.CurrentHp).Append('/').Append(player.Soul.MaxHp)
              .Append(" mana=").Append(player.Mana.Remaining).Append('/').Append(player.Mana.Available)
              .Append(" spent=").Append(player.Mana.Spent)
              .Append(" burnOwed=").Append(player.PendingManaBurn)
              .Append(" deck=").Append(player.Deck.Count)
              .AppendLine();

            // Hand order is real state, not an implementation detail: it decides which copy of a card
            // a play resolves to, so two machines disagreeing about it is a genuine desync.
            sb.Append("  hand:");
            foreach (var held in player.Hand.Cards)
            {
                sb.Append(' ').Append(held.Card.DefinitionId)
                  .Append("(r").Append(held.RoundEntered)
                  .Append(",d").Append(held.CostDiscount)
                  .Append(",b").Append(held.Buff.Hp).Append('/').Append(held.Buff.Attack).Append('/').Append(held.Buff.Heal)
                  .Append(')');
            }
            sb.AppendLine();

            // Draw-pile ORDER matters more than its contents — a shuffle that lands differently is
            // the single most likely way two machines diverge, and it stays invisible until a draw.
            sb.Append("  deckOrder:");
            foreach (var card in player.Deck.DrawPile) sb.Append(' ').Append(card.DefinitionId);
            sb.AppendLine();

            sb.Append("  graveyard:");
            foreach (var entry in player.Graveyard.Entries)
                sb.Append(' ').Append(entry.DefinitionId).Append("(r").Append(entry.RoundDied).Append(')');
            sb.AppendLine();
        }

        private static void AppendUnits(StringBuilder sb, MatchState match)
        {
            var units = new List<UnitInstance>(match.Units.AllUnits);
            units.Sort((x, y) => x.Id.CompareTo(y.Id)); // Dictionary order is not promised; unit id is stable and unique

            sb.Append("units(").Append(units.Count).Append("):").AppendLine();
            foreach (var unit in units)
            {
                var offset = unit.AnchorCoord.ToOffset();
                sb.Append("  #").Append(unit.Id)
                  .Append(' ').Append(unit.DefinitionId)
                  .Append(' ').Append(unit.Owner)
                  .Append(" hp=").Append(unit.CurrentHp).Append('/').Append(unit.StartingHp)
                  .Append(" at=(").Append(offset.Col).Append(',').Append(offset.Row).Append(')')
                  .Append(" col=").Append(unit.AttackColumn)
                  .Append(" atk=").Append(unit.AttackPower).Append('/').Append(unit.OverAttackPower)
                  .Append(" heal=").Append(unit.HealPower).Append('/').Append(unit.OverHealPower)
                  .Append(" pent=").Append(unit.PentaclePower).Append('/').Append(unit.OverPentaclePower)
                  .Append(" round=").Append(unit.RoundPlayed)
                  .AppendLine();
            }
        }

        /// <summary>Walls and greened tiles are board state that outlives the unit that made them (the Empress's greening in particular), so they belong in the fingerprint.</summary>
        private static void AppendBoard(StringBuilder sb, MatchState match)
        {
            var walled = new List<string>();
            var greened = new List<string>();

            foreach (var tile in match.Board.AllTiles)
            {
                var offset = tile.Coord.ToOffset();
                string at = $"({offset.Col},{offset.Row})";
                if (tile.IsWalled) walled.Add(at);
                if (tile.IsGreened) greened.Add(at);
            }

            walled.Sort(System.StringComparer.Ordinal);
            greened.Sort(System.StringComparer.Ordinal);

            sb.Append("walls:").Append(string.Join(" ", walled)).AppendLine();
            sb.Append("green:").Append(string.Join(" ", greened)).AppendLine();
        }

        private static void AppendJudgements(StringBuilder sb, MatchState match)
        {
            sb.Append("judgements:");
            foreach (var pending in match.PendingJudgements)
                sb.Append(' ').Append('#').Append(pending.Id).Append(':').Append(pending.Judge).Append(':').Append(pending.Entry.DefinitionId);
            sb.AppendLine();
        }
    }
}
