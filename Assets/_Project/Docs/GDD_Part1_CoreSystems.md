# ARCANA WARS — Game Design Document

**Version:** 1.0  
**Date:** September 1, 2026  
**Status:** Pre-Production  
**Genre:** 1v1 Tactical Grid-Based Card Game  
**Platform:** TBD  

---

## Table of Contents

### Part 1 — Core Systems
1. Executive Summary
2. Core Game Loop
3. Win Condition
4. The Board — Hexagonal Grid
5. Combat & Blocking
6. The HP/Duration System
7. Economy — Pentacle (Mana) System
8. Keywords Glossary

### Part 2 — Card Database
9. Minor Arcana (Base Elements)
10. Court Cards
11. Major Arcana

---

## 1. Executive Summary

**Arcana Wars** is a 1v1 competitive tactical card game set in a world of Tarot mysticism and elemental warfare. Players build custom 45-card decks from a pool of Minor Arcana, Court Cards, and Major Arcana, then deploy units onto a shared hexagonal grid battlefield.

The game's defining innovation is its **HP = Duration** system: every unit's Health Points simultaneously represent its remaining lifespan. All units naturally decay by 1 HP each turn, creating a constant tension between offense, defense, and resource management. Combat damage accelerates this decay, making every point of damage a meaningful reduction in a unit's operational window.

Four classical Tarot elements — **Swords** (Offense), **Cups** (Healing), **Pentacles** (Economy), and **Wands** (Buffing) — form the strategic backbone. Layered on top are 22 unique Major Arcana cards, each a powerful game-changer with specific conditions and dramatic effects.

**Core Pillars:**
- **Tactical Depth:** Hex-grid positioning, unit sizing, and blocking create spatial puzzles every turn.
- **Strategic Deckbuilding:** 45-card decks from four elements plus Major Arcana allow diverse archetypes.
- **Temporal Pressure:** The HP decay mechanic ensures no game stalls — every unit is on a countdown.
- **Dramatic Moments:** Major Arcana provide cinematic, match-defining plays.

---

## 2. Core Game Loop

```
┌─────────────────────────────────────────────────┐
│                  TURN START                      │
│         Receive Pentacles (Mana)                 │
│         Draw Card(s)                             │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│               ACTION PHASE                       │
│    Players ALTERNATE turns (see 2.1)              │
│    On your turn: play ONE card, or pass           │
│    Phase ends when BOTH players pass              │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│              COMBAT PHASE                        │
│    Units attack enemies / Nexus across from them  │
│    Blocking is resolved                           │
│    Damage reduces target HP                       │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│            UPKEEP / DECAY PHASE                  │
│    ALL units lose 1 HP (natural decay)            │
│    Units at 0 HP are removed from the board       │
│    Passive effects trigger (Heal, Mana, Buffs)    │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│    Check Win Condition: Is either Soul at 0?      │
│    YES → Game Over  |  NO → Next Turn             │
└─────────────────────────────────────────────────┘
```

### 2.1 The Action Phase — Alternating Turns

**Revised September 16, 2026.** The Action Phase was originally simultaneous: both players planned
at once, hidden from each other, and their plays resolved together. It is now strictly **alternating**.

- On your turn you may **play one card, or pass**. Either way, the turn then passes to your opponent.
- **Passing does not end your Action Phase.** It yields the turn only — if your opponent then plays a
  card, the turn comes back to you and you may act again.
- The Action Phase ends only when **both players pass in succession**. Any card played resets that
  count.
- The player who **moves first alternates each round**: Player 1 opens round 1, Player 2 opens round
  2, and so on.

The other four phases are unchanged. RoundStart, Combat, Decay and WinCheck involve no player
decisions, so they still resolve for both players at once exactly as the diagram above describes.

> **Design consequence — blocking.** Under the original simultaneous design, blocking (section 5.2)
> was a guess: the defender had to predict which column an attacker had committed to. Under
> alternating turns the defender simply watches the placement and answers it, so blocking becomes a
> matter of having a spare body and spare Pentacles rather than a read. This makes **acting last in a
> round an advantage**, which is precisely why the two rules above exist: priority passing means
> nobody is handed a free unanswered final placement, and the alternating first player splits the
> remaining edge evenly instead of letting it compound all match.

> **Design consequence — passing is a move.** Because Pentacles do not carry over (section 7.1), a
> player who passes early has not given up their mana — they can still spend it later in the same
> phase. "You commit first, I'll answer" is therefore a legitimate line rather than a concession.

---

## 3. Win Condition

Reduce the opponent's **Soul** (Main Nexus Health Pool) to **0 HP**. The Soul is each player's life total and the primary target for offensive strategies. All Attack damage that is unblocked by units hits the Soul directly.

---

## 4. The Board — Hexagonal Grid

### 4.1 Grid Structure

The battlefield is a **shared hexagonal grid**. Both players deploy units onto this single grid from their respective sides.

### 4.2 Unit Sizes

Units occupy varying numbers of hex tiles, creating spatial strategy around placement and coverage:

| Size Category | Tiles Occupied | Examples |
|---|---|---|
| **1×1** | 1 tile | Minor Arcana, Aces, Knights, Princes, The Fool, The Hermit |
| **1×2** | 2 tiles | The High Priestess |
| **2×1** | 2 tiles | Death |
| **1×3** | 3 tiles | The Empress, The Emperor |
| **2×2** | 4 tiles | The Hierophant, Strength, Justice |
| **2×3** | 6 tiles | The Devil |
| **7-Tile Cluster** | 7 tiles (1 center + 6 surrounding) | Queens, Kings, The Hanged Man's Tree |
| **3×3** | 9 tiles | Wheel of Fortune, Temperance, The Moon |
| **4×2** | 8 tiles | The Chariot |
| **2×4** | 8 tiles | Judgement |
| **4×4** | 16 tiles | The Tower, The Sun |
| **Entire Board** | All tiles | The World |

### 4.3 Placement Rules

- Units are placed from the player's side of the board.
- A unit cannot be placed on tiles already occupied by another unit (unless a specific card ability allows it).
- Larger units require contiguous free tiles of the appropriate shape.
- Board space is a finite resource — large units trade flexibility for power.

---

## 5. Combat & Blocking

### 5.1 Attack Resolution

Units placed on the grid **attack the enemy Nexus (Soul) or enemy units directly across from them** each combat phase.

### 5.2 Blocking (Interception)

If a player places a unit **in front of** an attacking enemy unit, the newly placed unit **intercepts the damage**. This is the primary defensive mechanic:

- The blocking unit absorbs the incoming attack damage.
- Damage reduces the blocker's HP (and therefore its remaining lifespan).
- Strategic blocking can protect the Soul while the blocker contributes its own abilities.

> Since the Action Phase became alternating (section 2.1), a defender sees an attacker placed before
> deciding whether to block it. Blocking is no longer a prediction — it is a question of whether you
> have a spare body and the Pentacles to spend on it.

### 5.3 Combat Implications

- **Positioning is paramount.** Where you place units determines what they attack and what they block.
- **Trading HP for time.** Blocking sacrifices a unit's longevity to protect your Soul.
- **Over-Attack bypasses blockers**, piercing through to damage the Soul even when blocked.

---

## 6. The HP/Duration System — Critical Core Mechanic

This is the game's signature system and must be understood by all players.

### 6.1 HP = Turns Remaining

A unit's **Health Points (HP)** serve a dual purpose:

1. **Survivability:** HP is the amount of damage a unit can absorb before dying.
2. **Lifespan:** HP dictates exactly how many turns the unit will remain on the board, assuming no combat damage.

> A unit played with 5 HP will naturally survive for exactly 5 turns if undamaged.

### 6.2 Natural Decay

At the end of **every turn**, **every unit on the board** loses **1 HP** automatically. This is non-negotiable and cannot be prevented by standard means.

- This creates a "ticking clock" on every unit.
- Higher-cost units with more HP inherently last longer.
- Healing effects (Cups, Wands) can counteract or slow decay.

### 6.3 Damage Interaction

When a unit takes **combat damage**, it loses HP equal to the damage dealt. This **shortens its remaining lifespan** in addition to bringing it closer to death.

> A 5 HP unit that takes 2 damage now has 3 HP — it will only survive 3 more turns (down from the original 5).

### 6.4 Death

When a unit's HP reaches **0** (whether from decay, damage, or a combination), it **dies** and is **removed from the board**.

### 6.5 Strategic Implications

| Scenario | Implication |
|---|---|
| High HP Unit | Long-lasting board presence; expensive to play |
| Low HP Unit | Cheap and disposable; useful for blocking or burst effects |
| Healing (Cups) | Extends unit lifespan by counteracting decay |
| Buffing (Wands) | Directly adds turns of life to adjacent units |
| Aggressive Damage | Removes enemy units faster by accelerating their countdown |

---

## 7. Economy — Pentacle (Mana) System

### 7.1 Mana Curve

Players receive Pentacles (Mana) at the start of each turn according to the following fixed curve:

| Turn | Pentacles Available |
|---|---|
| 1 | 3 |
| 2 | 6 |
| 3 | 9 |
| 4 | 12 |
| 5+ | 14 (Maximum Base Cap) |

- Mana **does not** carry over between turns (unspent Pentacles are lost).
- The curve accelerates quickly, reaching the cap by Turn 5.
- Early turns require efficient low-cost plays; late turns allow powerful combinations.

### 7.2 Mana Generation (Pentacle Keyword)

Units with the **Pentacle** keyword generate **+1 Pentacle per turn** while they remain on the board. This bonus stacks with multiple Pentacle-generating units.

### 7.3 Over-Pentacles

Certain special cards grant **Over-Pentacles**, which allow a player's mana capacity to **exceed the hard cap of 14**. This is the **only way** to reach 15 Pentacles, which is required to play the ultimate card: **XXI. The World** (Cost: 15).

> **Design Note:** Over-Pentacles create a specific late-game pursuit. Players who invest in Pentacle-element strategies or play Judgement (Over-Pentacle: 1) can unlock The World as a win condition.

---

## 8. Keywords Glossary

| Keyword | Effect | Element Association |
|---|---|---|
| **Attack** | Deals damage to the opposing unit or Soul each turn. | Swords |
| **Heal** | Restores HP to the friendly Soul each turn. | Cups |
| **Pentacles** | Grants +1 Pentacle (mana) per turn while unit is on the board. | Pentacles |
| **Over-Attack** | Pierces through enemy blocking units to also deal damage directly to the enemy Nexus (Soul). | Swords (Enhanced) |
| **Over-Heal** | Can heal the Nexus beyond its maximum starting health, exceeding the HP cap. | Cups (Enhanced) |
| **Over-Pentacle** | Grants bonus mana that can exceed the hard cap of 14, enabling 15-cost cards. | Pentacles (Enhanced) |

### Keyword Interactions

- **Attack + Blocking:** Attack damage hits the blocker first. Only Over-Attack bypasses.
- **Heal + Decay:** Heal restores Soul HP; it does not prevent unit decay.
- **Pentacles + Mana Curve:** Pentacle mana stacks on top of the turn's base mana.
- **Wand Buff + Decay:** The +1 HP from Wands can counteract the -1 HP natural decay, effectively freezing a unit's lifespan.
