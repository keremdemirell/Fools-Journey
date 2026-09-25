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
│    Play cards from hand onto the hex grid         │
│    Position units strategically                   │
│    Activate abilities                             │
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

---

## 9. Minor Arcana — Base Elements

The Minor Arcana form the core of every deck. Each element has cards valued from **2 to 10** (represented as **X**). The card's value directly determines its Cost and HP, creating a clean, predictable power curve.

### 9.1 Swords — Offensive Element

> **Role:** Consistent damage dealing. Swords are the primary way to reduce the enemy Soul.

| Stat | Value |
|---|---|
| **Cost** | X (2–10) |
| **HP** | X |
| **Size** | 1×1 |
| **Keyword** | Attack: 1 |

**Behavior:** Deals 1 damage every turn to the opposing unit or Soul. A "5 of Swords" costs 5, has 5 HP (lasts 5 turns), and deals 1 damage per turn for a total of 5 damage over its lifetime if uncontested.

---

### 9.2 Cups — Healing Element

> **Role:** Sustain and recovery. Cups restore Soul HP to outlast the opponent.

| Stat | Value |
|---|---|
| **Cost** | X (2–10) |
| **HP** | X |
| **Size** | 1×1 |
| **Keyword** | Heal: 1 |

**Behavior:** Heals your Nexus (Soul) for 1 HP every turn. Functionally the defensive counterpart to Swords.

---

### 9.3 Pentacles — Economy Element

> **Role:** Mana acceleration. Pentacles let you play bigger cards sooner or more cards per turn.

| Stat | Value |
|---|---|
| **Cost** | X (2–10) |
| **HP** | X × 2 |
| **Size** | 1×1 |
| **Keyword** | Pentacle: 1 |

**Behavior:** Grants +1 Pentacle (mana) per turn. Notably, Pentacle cards have **double HP** relative to their cost, making them exceptionally durable mana generators. A "4 of Pentacles" costs 4 but has 8 HP — lasting 8 turns and generating 8 bonus mana over its lifetime.

---

### 9.4 Wands — Support/Buffing Element

> **Role:** Unit longevity and synergy. Wands extend the lifespan of adjacent allies.

| Stat | Value |
|---|---|
| **Cost** | X (2–10) |
| **HP** | X |
| **Size** | 1×1 |
| **Ability** | Grants +1 HP every turn to the friendly unit positioned immediately to its left. |

**Behavior:** Each turn, the Wand adds +1 HP to the unit to its left. This directly counteracts natural decay, potentially making the buffed unit immortal (its HP neither rises nor falls). With multiple Wands or additional buffs, a unit can actually grow stronger over time.

---

### 9.5 Minor Arcana Summary Table

| Card | Cost | HP | Size | Effect |
|---|---|---|---|---|
| X of Swords | X | X | 1×1 | Attack: 1 |
| X of Cups | X | X | 1×1 | Heal: 1 |
| X of Pentacles | X | X×2 | 1×1 | Pentacle: 1 |
| X of Wands | X | X | 1×1 | +1 HP/turn to left unit |

---

## 10. Court Cards

Court Cards are elite versions of each element. They are the most powerful single-element cards, with escalating costs, unique abilities, and game-defining presence.

### 10.1 Ace of [Element]

> **The Flexible Foundation.** A card that scales with your commitment.

| Stat | Value |
|---|---|
| **Cost** | 1 **OR** All Current Mana |
| **HP** | 1 **OR** Equal to Mana Spent |
| **Size** | 1×1 |
| **Ability** | Performs its base element's trait |

**Element Traits:**
- **Ace of Swords:** Attack: 1
- **Ace of Cups:** Heal: 1
- **Ace of Pentacles:** Pentacle: 1
- **Ace of Wands:** +1 HP/turn to left unit

**Design Notes:** The Ace's dual-cost mechanic creates a fascinating decision point. Played for 1 mana, it's a cheap 1-HP disposable unit. Played for all your mana on Turn 5 (14 Pentacles), it becomes a 14 HP unit — rivaling Kings in durability. The Ace rewards reading the game state and committing resources at the right moment.

---

### 10.2 Knight of [Element]

> **The Guardian.** Absorbs all damage meant for its element's cards.

| Stat | Value |
|---|---|
| **Cost** | 11 |
| **HP** | 11 |
| **Size** | 1×1 |
| **Ability** | While on the board, all incoming damage to cards of its element is redirected to the Knight instead. |

**Element Variants:**
- **Knight of Swords:** Protects all Sword-element cards.
- **Knight of Cups:** Protects all Cup-element cards.
- **Knight of Pentacles:** Protects all Pentacle-element cards.
- **Knight of Wands:** Protects all Wand-element cards.

**Design Notes:** The Knight is a damage sponge for its element. At 1×1 size, it's space-efficient. However, redirected damage accelerates its HP decay. A Knight protecting many small element cards will die faster, creating a tension between breadth of protection and Knight longevity.

---

### 10.3 Prince of [Element]

> **The Ascended.** Performs the "Over" version of its element's ability.

| Stat | Value |
|---|---|
| **Cost** | 12 |
| **HP** | 12 |
| **Size** | 1×1 |
| **Ability** | Performs the enhanced "Over" version of its element's trait. |

**Element Variants:**
- **Prince of Swords:** Over-Attack: 1 (Pierces through blockers to hit Soul)
- **Prince of Cups:** Over-Heal: 1 (Heals Soul beyond max HP)
- **Prince of Pentacles:** Over-Pentacle: 1 (Mana exceeds the 14 cap)
- **Prince of Wands:** Grants +1 HP to the left unit **AND** +1 HP to itself each turn.

**Design Notes:** Princes are the bridge to endgame power. The Prince of Pentacles is particularly notable as it enables reaching 15 mana — the only way to play The World (XXI). The Prince of Wands is self-sustaining: its +1 HP to itself counteracts natural decay, making it theoretically immortal.

---

### 10.4 Queen of [Element]

> **The Scaling Monarch.** Grows stronger the more you've committed to her element.

| Stat | Value |
|---|---|
| **Cost** | 13 |
| **HP** | 13 |
| **Size** | 7 Tiles (1 center + 6 surrounding hexes) |
| **Ability** | Base element trait (Attack 1 / Heal 1 / Pentacle 1 / Wand buff). |
| **Special** | When played, permanently gains extra HP equal to the total number of cards of her specific element played so far in the match. |

**Element Variants:**
- **Queen of Swords:** Attack: 1 + bonus HP from Swords played.
- **Queen of Cups:** Heal: 1 + bonus HP from Cups played.
- **Queen of Pentacles:** Pentacle: 1 + bonus HP from Pentacles played.
- **Queen of Wands:** +1 HP to left unit + bonus HP from Wands played.

**Design Notes:** The Queen rewards element dedication. In a deck heavy with one element, she can enter the board with 20+ HP, making her an extremely durable late-game anchor. Her 7-tile size consumes significant board space, which limits additional deployments but also makes her hard to maneuver around.

---

### 10.5 King of [Element]

> **The Commander.** Empowers all units of his element currently on the board.

| Stat | Value |
|---|---|
| **Cost** | 14 |
| **HP** | 14 |
| **Size** | 7 Tiles (1 center + 6 surrounding hexes) |
| **Ability** | Base element trait (Attack 1 / Heal 1 / Pentacle 1 / Wand buff). |
| **Special** | When played, immediately **doubles** the current HP of all friendly units of his element on the board. |

**Element Variants:**
- **King of Swords:** Doubles HP of all friendly Sword units, extending their damage output window.
- **King of Cups:** Doubles HP of all friendly Cup units, extending healing duration.
- **King of Pentacles:** Doubles HP of all friendly Pentacle units, extending mana generation.
- **King of Wands:** Doubles HP of all friendly Wand units, extending buff duration.

**Design Notes:** The King is the ultimate payoff for building a wide board of one element. Playing the King of Swords when you have three Sword units on the board effectively doubles their remaining total damage output. At Cost 14 (the base mana cap), the King requires reaching Turn 5+ or having Pentacle support to play.

---

### 10.6 Court Card Summary Table

| Card | Cost | HP | Size | Core Ability |
|---|---|---|---|---|
| Ace | 1 or All Mana | 1 or Mana Spent | 1×1 | Base element trait (flexible cost) |
| Knight | 11 | 11 | 1×1 | Redirects all damage from element's cards to self |
| Prince | 12 | 12 | 1×1 | "Over" version of element trait |
| Queen | 13 | 13 + bonus | 7 tiles | Base trait + HP from total element cards played |
| King | 14 | 14 | 7 tiles | Base trait + doubles HP of element's units on board |

---

## 11. Major Arcana

The 22 Major Arcana are **unique, legendary cards** — only one copy of each may exist in a deck. They feature powerful abilities, many with specific **activation conditions** that must be met before they can be played. Major Arcana are the dramatic crescendos of any match.

---

### 0. The Fool

| Stat | Value |
|---|---|
| **Cost** | 0 |
| **HP** | Random (1–4) |
| **Size** | 1×1 |
| **Ability** | When it dies, randomly consumes/burns 1 to 6 of your current Pentacles (Mana). |
| **Condition** | None |

**Design Notes:** A free unit with a dangerous downside. The Fool is a gamble — useful as an early blocker or filler, but its death penalty can cripple your economy at a critical moment.

---

### I. The Magician

| Stat | Value |
|---|---|
| **Cost** | 1 |
| **HP** | 2 |
| **Size** | 1×1 |
| **Ability** | Upon play, choose an element. The Magician gains the effect of the "2 of [Element]" card of that chosen element. |
| **Condition** | Must have at least 3 different elements in hand. |

**Design Notes:** Versatile and cheap. The Magician rewards diverse hand composition and adapts to the current board state. Effectively a "2 of Anything" for 1 mana.

---

### II. The High Priestess

| Stat | Value |
|---|---|
| **Cost** | 2 |
| **HP** | 1 |
| **Size** | 1×2 |
| **Ability** | When played, heals your Nexus for (1 + the number of turns this card stayed in your hand). |
| **Condition** | None |

**Design Notes:** Rewards patience. Holding this card creates a tension — every turn it stays in hand increases its heal value, but it occupies a hand slot. A card held since Turn 1 and played on Turn 8 would heal for 8 HP.

---

### III. The Empress

| Stat | Value |
|---|---|
| **Cost** | 3 |
| **HP** | 4 |
| **Size** | 1×3 |
| **Ability** | "Greens" the tiles it occupies. Any friendly cards played on these green tiles gain +3 HP. |
| **Condition** | Must have played at least 2 Queens previously in the match. |

**Design Notes:** A terrain-modifier that creates premium deployment zones. Requires heavy Queen investment, tying it to element-dedication strategies.

---

### IV. The Emperor

| Stat | Value |
|---|---|
| **Cost** | 3 |
| **HP** | 5 |
| **Size** | 1×3 |
| **Attack** | 1 |
| **Ability** | Erects an unbreakable vertical wall across the board. Enemies cannot be played inside it, and units inside cannot be moved. |
| **Condition** | Must have played at least 2 Kings previously in the match. |

**Design Notes:** A board-control powerhouse. The wall reshapes the battlefield, potentially trapping enemy units or creating a safe zone. Requires King investment.

---

### V. The Hierophant

| Stat | Value |
|---|---|
| **Cost** | 4 |
| **HP** | 4 |
| **Size** | 2×2 |
| **Ability** | Grants +1 HP every turn to all friendly units positioned in front of it. |
| **Condition** | Must have played 8 units previously in the match. |

**Design Notes:** A mass-sustain engine. By buffing all units in front of it, the Hierophant counteracts natural decay for an entire frontline. Requires significant prior board activity.

---

### VI. The Lovers

| Stat | Value |
|---|---|
| **Cost** | 4 |
| **HP** | Two units: 3 HP each |
| **Size** | Two 3-tile shapes |
| **Ability** | Places 2 units with a 5-tile gap between them. Left unit: Heal 1. Right unit: Attack 1. Any unit placed in the gap gains +1 Attack and +1 Heal. |
| **Condition** | None |

**Design Notes:** A formation card that creates a powerful synergy zone. The 5-tile gap is a premium deployment area — any unit placed there becomes both an attacker and a healer.

---

### VII. The Chariot

| Stat | Value |
|---|---|
| **Cost** | 5 |
| **HP** | 8 |
| **Size** | 4×2 |
| **Attack** | 1 |
| **Ability** | Before attacking, checks for an enemy in front. If blocked, it randomly repositions to an unblocked tile (pushing friendly units if necessary) and then attacks. If it cannot reposition, it attacks from its current spot. |
| **Condition** | None |

**Design Notes:** An evasive attacker that's difficult to block. The Chariot forces opponents to commit extra resources to defense or accept unavoidable Soul damage. Its large size makes repositioning impactful.

---

### VIII. Strength

| Stat | Value |
|---|---|
| **Cost** | 5 |
| **HP** | 6 |
| **Size** | 2×2 |
| **Heal** | 1 |
| **Ability** | Immune to damage from enemy attacks. HP only decays naturally by 1 per turn. |
| **Condition** | None |

**Design Notes:** The ultimate wall. Strength cannot be killed by attacks — only by natural decay. At 6 HP, it will survive exactly 6 turns regardless of enemy offense, healing the Soul for 6 total.

---

### IX. The Hermit

| Stat | Value |
|---|---|
| **Cost** | 6 |
| **HP** | 3 |
| **Size** | 1×1 |
| **Ability** | As long as there are no units within a 2-tile radius, grants +1 HP, +1 Attack power, and +1 Heal power to all units in your deck every turn. |
| **Condition** | None |

**Design Notes:** Extremely powerful but requires isolation. The Hermit buffs your entire deck — making every future card drawn stronger. Protecting its isolation zone while keeping it alive (only 3 HP) is the challenge.

---

### X. Wheel of Fortune

| Stat | Value |
|---|---|
| **Cost** | 6 |
| **HP** | 6 |
| **Size** | 3×3 |
| **Pentacle** | 1 |
| **Ability** | Discard your entire hand, draw that many cards. Heal Nexus by 5, gain 2 Pentacles. All cards drawn this turn cost 1 Mana less. |
| **Condition** | Nexus HP difference between players must be at least ±5. |

**Design Notes:** A dramatic comeback or acceleration tool. It resets your hand, heals, grants mana, and discounts draws. The HP difference condition ensures it's used in volatile, high-stakes moments.

---

### XI. Justice

| Stat | Value |
|---|---|
| **Cost** | 7 |
| **HP** | 7 |
| **Size** | 2×2 |
| **Attack** | 2 |
| **Ability** | Reflects any damage taken by your Soul back to the attacker. Every turn, performs two attacks: one targeting the highest HP unit on the board, and one targeting the lowest HP unit. |
| **Condition** | None |

**Design Notes:** Justice punishes aggressive opponents with damage reflection and surgically removes both the strongest and weakest threats each turn. A powerful mid-to-late game control card.

---

### XII. The Hanged Man

| Stat | Value |
|---|---|
| **Cost** | 8 |
| **HP** | 2 |
| **Size** | 1×1 |
| **Heal** | 1 |
| **Pentacle** | 1 |
| **Ability** | Sacrifice a chosen friendly unit. It is replaced by a "Tree" (HP: 10, Size: 7 tiles, Heal: 1, Pentacle: 1). |
| **Condition** | Your Nexus HP must be lower than the opponent's. |

**Design Notes:** Transforms a dying or small unit into a massive, durable Tree structure. The Tree provides both healing and mana generation over a long period. Best used on low-HP units about to die, effectively recycling their board space.

---

### XIII. Death

| Stat | Value |
|---|---|
| **Cost** | 8 |
| **HP** | 8 |
| **Size** | 2×1 |
| **Attack** | 1 |
| **Pentacle** | 1 |
| **Ability** | Kills a chosen unit. A random unit from your deck with a higher cost spawns in its place. While Death is on the board, whenever ANY unit dies, a "3 of Swords" (HP: 3, Attack: 1) spawns on its tile. |
| **Condition** | None |

**Design Notes:** Death creates a snowball of board presence. Every dying unit — friend or foe — becomes a 3 of Swords. Combined with natural decay killing units every turn, Death can flood the board with attackers. The targeted kill + replacement effect also lets you upgrade a cheap unit into something powerful.

---

### XIV. Temperance

| Stat | Value |
|---|---|
| **Cost** | 9 |
| **HP** | 10 |
| **Size** | 3×3 |
| **Attack** | 2 |
| **Heal** | 2 |
| **Pentacle** | 2 |
| **Ability** | (Stat block is the ability — Temperance is a balanced powerhouse.) |
| **Condition** | Must have at least one card of each element (Sword, Cup, Pentacle, Wand) currently alive on the board. |

**Design Notes:** Temperance is the ultimate balanced card: 2 Attack, 2 Heal, 2 Mana per turn. Its condition requires a diverse board state with all four elements represented — rewarding generalist deckbuilding over element specialization.

---

### XV. The Devil

| Stat | Value |
|---|---|
| **Cost** | 10 |
| **HP** | 10 |
| **Size** | 2×3 |
| **Over-Attack** | 1 |
| **Ability** | Your Soul becomes immune to damage. Whenever an enemy unit takes damage, The Devil deals 1 damage to the enemy Soul. **Warning:** If the game does not end within 3 turns after playing The Devil, your Soul takes 20 fatal damage. |
| **Condition** | Your Nexus HP is below 5. |

**Design Notes:** The ultimate desperation play. The Devil makes you invincible but imposes a 3-turn hard deadline to win. The Over-Attack and damage-triggered Soul hits create enormous pressure, but failure means instant death. Only playable when already near defeat.

---

### XVI. The Tower

| Stat | Value |
|---|---|
| **Cost** | 11 |
| **HP** | 23 |
| **Size** | 4×4 |
| **Ability** | Takes 1 extra damage per turn (2 total decay). If HP is 5 or below and it takes attack damage, it dies instantly. Upon death, explodes dealing 1 damage to all friendly units in a 1-tile radius and all enemy units. |
| **Condition** | None |

**Design Notes:** The Tower is a ticking time bomb with massive HP. It occupies a huge 4×4 area and decays at double speed (2 HP/turn). When it inevitably falls, its explosion damages everything nearby. Can be used offensively (place near enemies) or as a massive blocker that punishes being destroyed.

---

### XVII. The Star

| Stat | Value |
|---|---|
| **Cost** | 12 |
| **HP** | 5 |
| **Size** | 1×1 |
| **Heal** | 1 |
| **Pentacle** | 1 |
| **Ability** | Upon play and every turn, heals all units on the board (except itself) for 1 HP. |
| **Condition** | There are no full-HP units on the board. |

**Design Notes:** A global healer that sustains the entire board — including enemy units. The Star's condition requires all units to be damaged, meaning it can't be played on a fresh board. Its self-exclusion from healing means it will always die to natural decay in 5 turns.

---

### XVIII. The Moon

| Stat | Value |
|---|---|
| **Cost** | 13 |
| **HP** | 15 |
| **Size** | 3×3 |
| **Heal** | 1 |
| **Ability** | Hides the HP values of all friendly units and your Soul from the opponent. Enemy attacks have a 50% chance to miss. |
| **Condition** | None |

**Design Notes:** Information warfare. The Moon creates uncertainty — the opponent can't see how close your units or Soul are to dying, and half their attacks miss entirely. At 15 HP and 3×3 size, The Moon is a durable, disruptive presence.

---

### XIX. The Sun

| Stat | Value |
|---|---|
| **Cost** | 13 |
| **HP** | 6 |
| **Size** | 4×4 |
| **Ability** | Cancels all illusions, stealth, and armor on the board. Every turn, deals damage equal to its current HP to the enemy unit with the highest HP. |
| **Condition** | None |

**Design Notes:** The Sun is the anti-Moon. It strips all hidden information and protective effects. Its damage scales down as it decays: Turn 1 it deals 6 damage, Turn 2 deals 5, etc. Best played as an immediate counter or to eliminate a single massive threat.

---

### XX. Judgement

| Stat | Value |
|---|---|
| **Cost** | 14 |
| **HP** | 14 |
| **Size** | 2×4 |
| **Over-Pentacle** | 1 |
| **Ability** | If the enemy Soul has more HP than yours, it matches your Soul HP. Then, spawns random previously died units on all empty tiles for both sides. If any friendly or foe unit dies, you can "judge" them — deciding to return it to your hand or discard it permanently. |
| **Condition** | None |

**Design Notes:** Judgement is a game-resetting bomb. It equalizes Soul HP (punishing the leader), floods the board with resurrected units, and gives you ongoing control over the death cycle. The Over-Pentacle also enables The World.

---

### XXI. The World

| Stat | Value |
|---|---|
| **Cost** | 15 |
| **HP** | 16 |
| **Size** | Entire Board |
| **Over-Attack** | 4 |
| **Over-Heal** | 4 |
| **Over-Pentacle** | 4 |
| **Ability** | Heals itself for 4 HP every turn. |
| **Condition** | The entire board must be completely empty. |

**Design Notes:** The ultimate card. The World occupies every tile on the board, preventing any other unit from being played. It deals 4 unblockable damage, heals for 4, generates 4 Over-Pentacles, and self-heals for 4 HP per turn — making it nearly immortal. Its conditions are extreme: Cost 15 (requires Over-Pentacles) and an empty board. Achieving both simultaneously is a monumental feat and a worthy win condition.

---

### Major Arcana Summary Table

| # | Name | Cost | HP | Size | Key Effect |
|---|---|---|---|---|---|
| 0 | The Fool | 0 | 1–4 | 1×1 | Free unit; burns mana on death |
| I | The Magician | 1 | 2 | 1×1 | Becomes any "2 of Element" |
| II | The High Priestess | 2 | 1 | 1×2 | Heals based on turns held in hand |
| III | The Empress | 3 | 4 | 1×3 | Greens tiles for +3 HP bonus |
| IV | The Emperor | 3 | 5 | 1×3 | Creates unbreakable wall |
| V | The Hierophant | 4 | 4 | 2×2 | +1 HP/turn to all units in front |
| VI | The Lovers | 4 | 3+3 | 2×3-tile | Dual unit with synergy gap |
| VII | The Chariot | 5 | 8 | 4×2 | Evasive repositioning attacker |
| VIII | Strength | 5 | 6 | 2×2 | Immune to attack damage |
| IX | The Hermit | 6 | 3 | 1×1 | Buffs entire deck if isolated |
| X | Wheel of Fortune | 6 | 6 | 3×3 | Hand reset + heal + discount |
| XI | Justice | 7 | 7 | 2×2 | Damage reflection + dual targeting |
| XII | The Hanged Man | 8 | 2 | 1×1 | Sacrifices unit → Tree (10 HP, 7 tiles) |
| XIII | Death | 8 | 8 | 2×1 | Kill + replace; spawns swords on death |
| XIV | Temperance | 9 | 10 | 3×3 | 2 Atk, 2 Heal, 2 Mana balanced |
| XV | The Devil | 10 | 10 | 2×3 | Soul immunity + 3-turn doomsday |
| XVI | The Tower | 11 | 23 | 4×4 | Massive HP; explodes on death |
| XVII | The Star | 12 | 5 | 1×1 | Global board heal every turn |
| XVIII | The Moon | 13 | 15 | 3×3 | Hides info + 50% miss chance |
| XIX | The Sun | 13 | 6 | 4×4 | Dispel + decaying nuke |
| XX | Judgement | 14 | 14 | 2×4 | Equalize HP + resurrect all dead |
| XXI | The World | 15 | 16 | Full Board | 4 Over-Atk/Heal/Mana + self-heal |

---

*End of Game Design Document — Version 1.0*
