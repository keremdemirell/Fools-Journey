# Arcana Wars

A 1v1 tactical card game on a hexagonal grid, built in Unity 6. Players build 45-card decks from the Tarot's Minor Arcana, Court Cards, and Major Arcana, then deploy units onto a shared battlefield.

> **Status:** Playable prototype. The full rules simulation, all 78 cards, deckbuilding, and LAN multiplayer are implemented, with UI Toolkit screens front to back. What's missing is art and audio — everything renders with placeholder visuals.

---

## The core idea: HP is duration

Every unit's Health Points double as its remaining lifespan. All units lose 1 HP at the start of each turn, so a 5 HP unit lives five turns if nothing touches it. Combat damage doesn't just weaken a unit — it shortens its operational window.

This one rule drives the whole design:

- **No stalling.** Every board state is on a countdown, so games resolve.
- **Every point of damage matters.** There is no "chip damage" that gets healed away for free.
- **Cost curves become time curves.** A 5-cost Sword deals 1 damage per turn for 5 turns: you are buying 5 damage spread over 5 turns, not 5 damage now.

The four suits split cleanly along that axis:

| Suit | Role | Keyword | Notes |
|---|---|---|---|
| **Swords** | Offense | Attack 1 | Deals 1 damage per turn |
| **Cups** | Sustain | Heal 1 | Restores 1 Soul HP per turn |
| **Pentacles** | Economy | Pentacle 1 | +1 mana per turn, and **double HP** for its cost |
| **Wands** | Support | — | Extends the lifespan of adjacent allies |

On top sit 22 Major Arcana, each a one-off card with a bespoke play condition and a match-defining effect.

---

## Architecture

The part of this project I'd point at first is the assembly split. The rules simulation has **no Unity dependency at all**:

```
ArcanaWars.Core          ← "noEngineReferences": true — pure C#, no UnityEngine
      ▲
      │
ArcanaWars.Cards         ← ScriptableObject card definitions, the Unity-side data layer
      ▲
      │
ArcanaWars.Presentation  ← rendering, input, UI
```

Dependencies only ever point downward. `Core` doesn't know `Cards` exists; `Cards` doesn't know `Presentation` exists.

**Why it's built this way:**

- The entire game can be simulated and tested without opening the Unity editor or entering play mode.
- A headless server can run `Core` as-is — it links against nothing but the standard library.
- Networking is lockstep: both clients run the same deterministic simulation and exchange commands, not state. `MatchFingerprint` hashes the full match state each turn so desyncs surface immediately instead of silently corrupting the game.

### The Definition / Rules / Instance pattern

The codebase uses one recurring shape for anything with both static data and runtime state:

- **Definition** — immutable authored data (a `ScriptableObject`: this card costs 5 and has 5 HP).
- **Rules** — pure static functions over that data (`DeckRules`, `HandRules`, `ManaCurve`). No state, trivially testable.
- **Instance** — mutable runtime state (`UnitInstance`: *this particular* 5 of Swords, currently at 3 HP, at hex (2,-1), owned by South).

Ability code is a closed hierarchy under `UnitAbility` with one file per Major Arcana card, registered through `AbilityRegistry` and resolved against an `IAbilityContext` — so abilities read the board through a narrow interface rather than reaching into match state directly.

---

## Project layout

```
Assets/_Project/
├── Docs/         Game Design Document (the rules are the spec)
├── Data/Cards/   All 78 cards as generated ScriptableObject assets
├── Scripts/
│   ├── Core/         Engine-agnostic simulation (~77 files)
│   │   ├── Board/        Hex coordinates, grid, tiles, footprint shapes
│   │   ├── Cards/        Deck, hand, play resolution, validation
│   │   ├── Combat/       Lane-based attack resolution, blocking
│   │   ├── Economy/      Pentacle mana curve and ledger
│   │   ├── GameLoop/     Phases, rounds, alternating action turns
│   │   ├── Abilities/    One file per Major Arcana card
│   │   ├── Session/      Command codec, executor, snapshots, fingerprinting
│   │   └── Net/          Lockstep driver and transport
│   ├── Cards/        ScriptableObject definitions + editor generators
│   └── Presentation/ Rendering and UI
```

## What's implemented

**Simulation**
- Hex board with side-based placement rules and multi-tile unit footprints
- The HP-as-duration decay system
- Pentacle mana curve with stacking generators
- Lane-based combat with per-unit attack-column choice, blocking, and Over-Attack lane piercing
- Card play as an ordered transaction — mana is spent only after placement is accepted, so a rejected placement never costs resources
- Win and draw detection

**Content**
- All 78 cards generated as data: 36 Minor Arcana, 20 Court Cards, 22 Major Arcana
- Every Major Arcana and Court Card ability, each with its own play condition

**Deckbuilding**
- Deck lists, copy limits, and validation that reports every problem at once rather than the first one
- Decks saved as JSON at runtime, plus authored decks as ScriptableObjects

**Interface**
- UI Toolkit screens (UXML/USS): lobby, match HUD, and overlays
- Hand row with affordability and play-condition feedback, placement preview on hover, two-click ally targeting, Judgement rulings panel, hot-seat handover curtain, end-of-match screen

**Multiplayer**
- Deterministic lockstep over LAN by direct IP — clients exchange commands, not state
- `MatchFingerprint` hashes full match state each turn so desyncs surface immediately
- Determinism proven by record/replay: full matches replay bit-for-bit

**Testing**
- ~300-assertion test harness that runs the simulation headlessly, without opening Unity or entering play mode — a direct payoff of the engine-agnostic `Core` assembly

## Not yet built

- Art, animation, and audio — everything currently renders with placeholder visuals
- The deck builder screen is still on the older IMGUI implementation; the UI Toolkit version is the next piece of UI work
- Matchmaking — connection is direct IP only
- AI opponent
- Balance passes

---

## Running it

Requires **Unity 6000.0.55f1** (URP). Git LFS is used for binary assets.

```bash
git lfs install
git clone https://github.com/keremdemirell/Fools-Journey.git
```

Open the folder in Unity Hub, then open `Assets/_Project/Scenes/Main.unity` and enter play mode. The lobby lets you pick a deck per side and start a local hot-seat match, or host/join over LAN by IP.

The design document in `Assets/_Project/Docs/` is the authoritative spec — the code follows it, and where the GDD left a number unspecified the choice is recorded there.

---

## License

Not currently licensed for reuse. All rights reserved.
