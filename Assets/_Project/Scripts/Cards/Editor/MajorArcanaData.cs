using ArcanaWars.Core.Board;
using ArcanaWars.Cards.MajorArcana;

namespace ArcanaWars.Cards.Editor
{
    /// <summary>
    /// The 22 Major Arcana, hand-transcribed from GDD Part 2 section 11. This is the one place
    /// their numbers are typed in — everything else in Cards computes stats from a formula.
    ///
    /// Policy for which fields are set to a real value vs left at 0/default: a stat is only wired
    /// if it behaves CORRECTLY through AttackResolver/MatchState's existing generic systems on its
    /// own. If a card's GDD stat would need bespoke logic to be correct (Justice's Attack:2 needs
    /// highest/lowest-HP targeting, not lane targeting), it's left at 0 rather than wired to
    /// something that would silently misbehave — see each card's AbilityNotes for specifics.
    ///
    /// A card's bespoke behaviour lives in Core (ArcanaWars.Core.Abilities), keyed by the CardId
    /// below, so the whole simulation stays runnable outside Unity — see UnitAbility for why that
    /// won out over putting ability code on the ScriptableObject. This table stays pure data.
    /// </summary>
    public static class MajorArcanaData
    {
        public static readonly MajorArcanaStatBlock[] All =
        {
            new MajorArcanaStatBlock(
                "0_fool", "The Fool", cost: 0, startingHp: 1, footprint: FootprintShapeType.Single,
                minStartingHp: 1, maxStartingHp: 4,
                abilityNotes: "IMPLEMENTED (FoolAbility). Random 1-4 starting HP is rolled on spawn; on death it burns 1-6 Pentacles. Deviation from the printed text, deliberate: the burn is charged against the NEXT round's mana pool rather than the current one. Mana doesn't carry over (GDD 7.1) and units only die in Combat or Decay, both after the Action phase, so 'burn your current Pentacles' would always have been a no-op and the card's own design note ('can cripple your economy at a critical moment') could never be true."),

            new MajorArcanaStatBlock(
                "1_magician", "The Magician", cost: 1, startingHp: 2, footprint: FootprintShapeType.Single,
                abilityNotes: "FULLY IMPLEMENTED (MagicianAbility). Condition: 3+ different elements in hand. The element is chosen AT PLAY TIME (CardPlayOptions.ChosenElement) and is required. It gains that element's base trait at power 1 (Attack 1 / Heal 1 / Pentacle 1 / Wand buff) AND becomes that element — so it counts toward Temperance — but keeps its own printed 2 HP rather than the 2 of Pentacles' 4 (user decision)."),

            new MajorArcanaStatBlock(
                "2_high_priestess", "The High Priestess", cost: 2, startingHp: 1, footprint: FootprintShapeType.OneByTwo,
                abilityNotes: "IMPLEMENTED (HighPriestessAbility). On play, heals your Soul for 1 + the number of rounds this copy sat in hand. A regular capped Heal, not an Over-Heal, since the GDD names Over-Heal as the only thing allowed past the Soul's cap (GDD 8). Cards in the opening hand count as having arrived on round 1 — that's what makes the GDD's own example (held from turn 1, played turn 8, heals 8) come out right."),

            new MajorArcanaStatBlock(
                "3_empress", "The Empress", cost: 3, startingHp: 4, footprint: FootprintShapeType.OneByThree,
                abilityNotes: "IMPLEMENTED (EmpressAbility). Condition: 2+ Queens played this match. On play she greens the tiles she occupies; a friendly card later played on green gains +3 HP, applied at placement so it's part of the unit's StartingHp. Two readings decided rather than guessed: the +3 is per unit, not per green tile covered (a 4x4 unit would otherwise gain +48), and greening outlives her, since she changes the terrain and nothing says the effect ends with her."),

            new MajorArcanaStatBlock(
                "4_emperor", "The Emperor", cost: 3, startingHp: 5, footprint: FootprintShapeType.OneByThree,
                attackPower: 1,
                abilityNotes: "FULLY IMPLEMENTED (EmperorAbility). Attack:1 (lane), condition (2+ Kings played), and on play a wall of TWO LINES: the column just left of the Emperor and the column just right of it, each from one end of the board to the other (user's design, revised 2026-09-15 — the first version walled its own 3 columns). The Emperor's own columns stay open between the lines. Wall tiles are solid for EVERYONE: nobody can place a unit on them, and no unit can be moved onto or off them (a unit already standing there when the wall rises stays, pinned — this is what stops the Chariot). The wall stands while the Emperor lives and comes down when it dies (my call, confirmed by the user)."),

            new MajorArcanaStatBlock(
                "5_hierophant", "The Hierophant", cost: 4, startingHp: 4, footprint: FootprintShapeType.TwoByTwo,
                abilityNotes: "IMPLEMENTED (HierophantAbility). Condition: 8+ units played this match (every card in this game spawns a unit, so that's the whole play history). Each Decay phase it grants +1 HP to every friendly unit in front of it — same columns, past its own front edge, toward the enemy, which is deliberately the identical geometry its units attack along (BoardQueries). It runs in the survivors-only pass, so a unit that decayed away this round correctly misses the buff."),

            new MajorArcanaStatBlock(
                "6_lovers", "The Lovers", cost: 4, startingHp: 3, footprint: FootprintShapeType.ThreeByOne,
                healPower: 1,
                abilityNotes: "FULLY IMPLEMENTED (LoversAbility). One card, two units: this card's unit is the LEFT Lover (Heal 1, 3 HP), and on play the RIGHT Lover (Attack 1, 3 HP) spawns 5 columns to its right in the same rows. GAP = 4 TILES (user decision; the GDD's 5 was judged too much). Each Lover is an UPRIGHT 3-tile column (3 rows x 1 col) — originally forced by the old 9-wide board, kept because the user liked it; on the new 18-wide board flat 1x3 strips would also fit if ever wanted. The gap is the 4x3 block between them. Any friendly unit placed (played or spawned) with a tile in the gap, while both Lovers live, permanently gains +1 Attack and +1 Heal. Room for the right Lover is checked before any mana is spent."),

            new MajorArcanaStatBlock(
                "7_chariot", "The Chariot", cost: 5, startingHp: 8, footprint: FootprintShapeType.FourByTwo,
                attackPower: 1,
                abilityNotes: "FULLY IMPLEMENTED (ChariotAbility). Attack:1 is still an ordinary lane attack; what's added is a step at the START of Combat: if an enemy stands in its lane, it relocates to a random spot on your side where one of its columns has a clear lane, and attacks from there. MY CALLS (user left the details to me): free spots are preferred; with none, it PUSHES — it takes a spot occupied only by friendly units that fit entirely inside it, and those slide into the space it just left (a swap, so nothing is ever shoved off the board). Destinations never overlap its current spot, which keeps that swap exact; 7-tile units can't be pushed. Units standing on any Emperor's wall can't be moved (the Chariot or anything it would push), and it never moves onto a wall. No legal spot = it attacks from where it stands."),

            new MajorArcanaStatBlock(
                "8_strength", "Strength", cost: 5, startingHp: 6, footprint: FootprintShapeType.TwoByTwo,
                healPower: 1, isImmuneToAttackDamage: true,
                abilityNotes: "Fully supported: Heal:1 to its owner's Soul, immune to attack damage, still takes normal decay. Its immunity also covers ability damage such as The Tower's explosion, since all damage goes through one funnel (UnitInstance.ApplyCombatDamage) — the card promises it survives 'regardless of enemy offense'."),

            new MajorArcanaStatBlock(
                "9_hermit", "The Hermit", cost: 6, startingHp: 3, footprint: FootprintShapeType.Single,
                abilityNotes: "IMPLEMENTED (HermitAbility), reworked per the user: instead of buffing every card in the deck (deck cards are immutable shared definitions, so there's nowhere to store that), every card you DRAW while the Hermit is isolated — no unit, friend or foe, within 2 tiles — gets +1 HP, +1 Attack and +1 Heal. The buff lives on that copy in hand (HandCard.Buff) and applies when it's played, even if the Hermit has died by then. Isolation is confirmed by the user: alone, it does its job; with anyone within 2 tiles, it doesn't — checked fresh at every draw. '+1 Attack' on a card with no attack gives it a normal lane attack of 1."),

            new MajorArcanaStatBlock(
                "10_wheel_of_fortune", "Wheel of Fortune", cost: 6, startingHp: 6, footprint: FootprintShapeType.ThreeByThree,
                pentaclePower: 1,
                abilityNotes: "FULLY IMPLEMENTED (WheelOfFortuneAbility). Pentacle:1, condition (Soul HP gap of 5+, either direction), and on play: discard your hand to the discard pile, draw that many (the Wheel itself has already left hand, so it doesn't count), regular capped Heal 5, and +2 Pentacles to THIS round's pool. DISCOUNT (user decision): only the cards the Wheel draws cost 1 less — not the round's normal draw — and each keeps the discount for as long as it's held. The discount lives on the copy in hand, never goes below 0, and doesn't apply to Aces (an Ace's cost is its HP, so a discount would be a free HP point). A Wheel-drawn The World costs 14, and that's intended (user decision): GDD 7.3's 'Over-Pentacles are the only way to reach 15' is NOT a rule the game enforces."),

            new MajorArcanaStatBlock(
                "11_justice", "Justice", cost: 7, startingHp: 7, footprint: FootprintShapeType.TwoByTwo,
                abilityNotes: "IMPLEMENTED (JusticeAbility). AttackPower stays 0 here ON PURPOSE and must not be 'fixed' to 2: the generic resolver only knows lane targeting, so a wired stat would add a wrong third attack down its column. Its Attack:2 lives in JusticeAbility, which strikes the highest-HP and lowest-HP unit each Combat phase, and reflects damage dealt to its owner's Soul back onto the attacking unit. TARGETING (user decision): ENEMY units only, per the design note's 'threats' — despite the card text's 'on the board'. With one enemy on the board, it takes both attacks."),

            new MajorArcanaStatBlock(
                "12_hanged_man", "The Hanged Man", cost: 8, startingHp: 2, footprint: FootprintShapeType.Single,
                healPower: 1, pentaclePower: 1,
                abilityNotes: "FULLY IMPLEMENTED (HangedManAbility). Heal:1/Pentacle:1, condition (your Soul below the opponent's), and an optional ally target chosen AT PLAY TIME. The target is sacrificed and a Tree (HP 10, 7-tile cluster, Heal 1, Pentacle 1, printed cost 0) grows centred on its anchor. Fit is checked before any mana is spent: all 7 tiles must be free (or the target's), on your side, and not where the Hanged Man itself will stand. The sacrifice is a REAL death (user decision) — a sacrificed Fool still burns mana, a sacrificed Tower still explodes — except that Death's 3-of-Swords passive stands down for it, since the Tree takes that ground."),

            new MajorArcanaStatBlock(
                "13_death", "Death", cost: 8, startingHp: 8, footprint: FootprintShapeType.TwoByOne,
                attackPower: 1, pentaclePower: 1,
                abilityNotes: "FULLY IMPLEMENTED (DeathAbility). Base Attack:1/Pentacle:1. ON PLAY (user decisions): optionally target one of YOUR units, chosen at play time; it's killed and a random deck card with a HIGHER printed cost whose footprint fits there spawns in its place. If no such card exists, that unit can't be targeted, so no mana is spent on a dud. The kill is a real death (Fool burns, Tower explodes) but Death's own passive stands down for it. The replacement is SPAWNED, not played: unpaid, not in match history, and its on-play ability doesn't fire (its ongoing abilities do). PASSIVE: the on-death 3 of Swords trigger works. ALLIES ONLY (user decision, overriding the GDD's 'ANY unit' / 'friend or foe'): only your own units become a 3 of Swords; an enemy unit dying does nothing. An allied 3 of Swords that dies spawns another, so Death's board self-replaces while Death lives; it can't loop within one death pass, since the spawn arrives at full HP. Death does not replace itself — it has already left the board when its own death is announced."),

            new MajorArcanaStatBlock(
                "14_temperance", "Temperance", cost: 9, startingHp: 10, footprint: FootprintShapeType.ThreeByThree,
                attackPower: 2, healPower: 2, pentaclePower: 2,
                abilityNotes: "FULLY IMPLEMENTED. Attack:2/Heal:2/Pentacle:2 with no extra ability beyond the stat block, and the play condition (one unit of each element alive) is now enforced. Read as YOUR OWN units of each element: the card's design note calls it a reward for generalist deckbuilding, which only holds if the board being asked for is one you built."),

            new MajorArcanaStatBlock(
                "15_devil", "The Devil", cost: 10, startingHp: 10, footprint: FootprintShapeType.TwoByThree,
                overAttackPower: 1,
                abilityNotes: "FULLY IMPLEMENTED (DevilAbility). Over-Attack:1 (pierces the lane + hits the Soul), condition (your Soul below 5 HP), Soul damage-immunity while it lives, 'whenever an enemy unit takes damage deal 1 to the enemy Soul' (fires for ANY source — its own attack, someone else's, a Tower explosion), and the doomsday clock. TIMER READING: played on round N, the 20 fatal damage lands at the end of round N+3, reading 'within 3 turns after playing' as the three turns following the play; DevilAbility.DoomsdayTurns is the one constant to change. The doomsday payment is the only damage in the game that bypasses Soul immunity — it has to, since the only Soul it can hit is the one the Devil is protecting, and respecting the immunity would make the card's drawback unreachable. It repeats each round after the deadline if the Soul somehow survives (only possible via Over-Heal)."),

            new MajorArcanaStatBlock(
                "16_tower", "The Tower", cost: 11, startingHp: 23, footprint: FootprintShapeType.FourByFour,
                decayMultiplier: 2, instantDeathHpThreshold: 5,
                abilityNotes: "IMPLEMENTED (TowerAbility + two unit stats). Double decay is DecayMultiplier=2; 'at 5 HP or below, any attack damage kills it outright' is InstantDeathHpThreshold=5, checked at the same funnel as Strength's immunity — note it keys off attack/ability damage only, so decay still walks it down through the threshold. On death it deals 1 damage in a 1-tile radius. Radius reading decided rather than guessed: the text says 'all friendly units in a 1-tile radius and all enemy units', which could mean every enemy on the board, but the card's design note says the explosion 'damages everything nearby', so the radius applies to both sides. Reversible by dropping the owner-agnostic radius check in TowerAbility."),

            new MajorArcanaStatBlock(
                "17_star", "The Star", cost: 12, startingHp: 5, footprint: FootprintShapeType.Single,
                healPower: 1, pentaclePower: 1,
                abilityNotes: "IMPLEMENTED (StarAbility). Base Heal:1/Pentacle:1 to its own Soul, plus +1 HP to every other unit on the board — enemies included, per its own design note — on play and every Decay phase. Condition: at least one unit on the board and none of them at full HP. The 'at least one unit' half is not in the printed text; an empty board vacuously satisfies 'no full-HP units', but the design note says plainly it can't be played on a fresh board. Full HP means CurrentHp >= StartingHp, since Wand buffs deliberately push units above their printed HP."),

            new MajorArcanaStatBlock(
                "18_moon", "The Moon", cost: 13, startingHp: 15, footprint: FootprintShapeType.ThreeByThree,
                healPower: 1,
                abilityNotes: "IMPLEMENTED as far as the simulation goes (MoonAbility). Heal:1 works normally, and the 50% miss chance now applies to every attack made by its owner's opponent. The roll is per ATTACK ACTION, not per unit hit, so a piercing Over-Attack either lands on the whole lane or misses all of it. A live Sun anywhere on the board cancels the miss chance entirely (GDD 11.XIX's dispel). Hiding HP values from the opponent is deliberately NOT implemented — it changes nothing about what happens, only about what one player may see, so it's a Presentation concern, and it can't be built honestly until the 'how do two humans play this' question is settled (one shared screen has no second viewer to hide from)."),

            new MajorArcanaStatBlock(
                "19_sun", "The Sun", cost: 13, startingHp: 6, footprint: FootprintShapeType.FourByFour,
                abilityNotes: "IMPLEMENTED (SunAbility). Correctly has no base Attack stat — its strike isn't lane-based, so wiring one would add a wrong second attack. Each Combat phase it deals damage equal to its CURRENT HP to the enemy unit with the highest HP, which produces the decaying nuke its design note describes (6, then 5, then 4) for free, with no stored power to keep in sync. If the enemy has no units it does nothing — the card gives it no Soul damage to fall back on. DISPEL SCOPE, flagged: 'cancels all illusions, stealth, and armor' is implemented as cancelling The Moon's miss chance only, that being the sole concealment effect the simulation has. Deliberately NOT extended to Strength's damage immunity or The Devil's Soul immunity, tempting as 'armor' sounds — those are the whole identity of their cards rather than a status layered on top, and cancelling them would rewrite two cards from one word in a third."),

            new MajorArcanaStatBlock(
                "20_judgement", "Judgement", cost: 14, startingHp: 14, footprint: FootprintShapeType.TwoByFour,
                overPentaclePower: 1,
                abilityNotes: "FULLY IMPLEMENTED (JudgementAbility). Over-Pentacle:1. On play: if the enemy Soul is higher than yours it's lowered to match (not damage — no triggers, ignores The Devil's immunity), then each side's empty tiles are filled with random units from that side's own graveyard (every unit that died this match, tokens included), each at most once, spawned rather than played. While Judgement lives, every death — friend or foe — queues a ruling for you (MatchState.PendingJudgements): RETURN puts that unit's card into YOUR hand (so judging a foe steals its card), DISCARD removes it from the graveyard for good. Tokens with no card (3 of Swords, Tree, right Lover) can only be discarded. MY CALLS: rulings have no deadline — an unanswered one just leaves the unit in the graveyard, so the match never has to stop and wait; a full hand burns a returned card like an overdraw."),

            new MajorArcanaStatBlock(
                "21_world", "The World", cost: 15, startingHp: 16, footprint: FootprintShapeType.EntireBoard,
                overAttackPower: 4, overHealPower: 4, overPentaclePower: 4, selfHpBuffAmount: 4,
                abilityNotes: "FULLY IMPLEMENTED. Over-Attack:4 (pierces every enemy in the lane + hits the Soul), Over-Heal:4 to its owner's Soul, Over-Pentacle:4, heals itself 4 HP/turn, and the play condition is enforced. CONDITION READING (user decision): 'the entire board must be completely empty' means YOUR OWN SIDE only. The literal whole-board reading was tried first and overruled — The World's footprint covers only your own half (see HexGrid's EntireBoard note), so that is the board it needs to move into, and requiring the opponent's half to be clear too would make the card depend on their play rather than yours. Checked against the TILES on your side rather than unit ownership, so it stays correct if a future card ever puts a unit on the far side."),
        };
    }
}
