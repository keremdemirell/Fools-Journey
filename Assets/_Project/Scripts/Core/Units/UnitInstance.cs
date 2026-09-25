using System;
using ArcanaWars.Core.Board;

namespace ArcanaWars.Core.Units
{
    /// <summary>
    /// A unit actually on the board — the mutable "physical copy," as opposed to the card
    /// definition (the immutable "printed template") it was spawned from. See UnitSpawnStats
    /// for how the two connect.
    ///
    /// CurrentHp only has a private setter: every legal way to change it is one of the three
    /// methods below, so decay, combat damage, and healing can never quietly disagree about
    /// what "0 HP" means or forget to respect DecayMultiplier / IsImmuneToAttackDamage.
    /// </summary>
    public class UnitInstance
    {
        public int Id { get; }
        public string DefinitionId { get; }
        public PlayerSide Owner { get; }
        /// <summary>Where the unit stands. Changes only through UnitRegistry.TryMoveUnits (The Chariot), never by assignment from outside.</summary>
        public HexCoord AnchorCoord { get; private set; }
        public FootprintShapeType Footprint { get; }

        /// <summary>
        /// Which of the footprint's columns this unit attacks/defends from, chosen explicitly at
        /// placement (not part of UnitSpawnStats — it's a per-placement decision, not a card stat,
        /// same as AnchorCoord). For a 1-column-wide footprint there's only one legal choice; wider
        /// footprints (which is most of them — even a 1x2 spans 2 columns) genuinely need this.
        /// </summary>
        public int AttackColumn { get; private set; }

        /// <summary>
        /// The round this unit reached the board. Like AnchorCoord and AttackColumn it's a fact about
        /// this placement rather than a card stat, so it's a constructor argument, not part of
        /// UnitSpawnStats.
        ///
        /// It exists because The Devil (GDD 11.XV) has to know when its own 3-turn doomsday clock
        /// started. AbilityRegistry's note explains why that couldn't be remembered on the ability
        /// object itself (abilities are shared singletons); deriving the deadline from the round the
        /// unit was played is better than per-unit scratch state anyway, since nothing has to be kept
        /// in sync as rounds advance.
        /// </summary>
        public int RoundPlayed { get; }

        /// <summary>
        /// The exact stats this unit was created from, bonuses included. Kept so a unit can be brought
        /// back as it was — Judgement "spawns random previously died units" (GDD 11.XX), and a dead
        /// unit's live fields (CurrentHp 0, buffs from the Lovers' gap) are not what should return.
        /// </summary>
        public UnitSpawnStats OriginStats { get; }

        public int CurrentHp { get; private set; }

        /// <summary>
        /// The HP this unit entered the board with, including any placement bonus (the Empress's
        /// greened tiles). Kept because "is this unit at full HP?" is a real question two cards ask —
        /// The Star's play condition (GDD 11.XVII) needs it, and there was previously nothing to
        /// compare CurrentHp against. Note the comparison must be CurrentHp >= StartingHp, not ==:
        /// Wand buffs deliberately push units ABOVE their starting HP (GDD 9.4), and a unit that has
        /// grown past its printed HP is certainly not "damaged".
        /// </summary>
        public int StartingHp { get; }

        /// <summary>The unit's element, or null for the Major Arcana (which have none). Set at spawn from the card; a unit's element never changes.</summary>
        public Element? Element { get; }

        /// <summary>The printed cost of the card it came from — see UnitSpawnStats.PrintedCost. Death compares against this.</summary>
        public int PrintedCost { get; }

        public int DecayMultiplier { get; }
        public bool IsImmuneToAttackDamage { get; }

        /// <summary>The Tower's fragility rule (GDD 16), -1 for everything else. See ApplyCombatDamage.</summary>
        public int InstantDeathHpThreshold { get; }

        public int AttackPower { get; set; }
        public int OverAttackPower { get; set; }
        public int HealPower { get; set; }
        public int OverHealPower { get; set; }
        public int PentaclePower { get; set; }
        public int OverPentaclePower { get; set; }
        public int LeftNeighborHpBuffAmount { get; }
        public int SelfHpBuffAmount { get; }

        public bool IsDead => CurrentHp <= 0;

        /// <summary>At or above the HP it started with — i.e. undamaged. See StartingHp for why this isn't an equality check.</summary>
        public bool IsAtFullHp => CurrentHp >= StartingHp;

        public UnitInstance(int id, PlayerSide owner, HexCoord anchor, int attackColumn, UnitSpawnStats stats, int roundPlayed = 0)
        {
            if (stats.StartingHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(stats), "Units must spawn with positive HP.");

            Id = id;
            DefinitionId = stats.DefinitionId;
            Owner = owner;
            AnchorCoord = anchor;
            AttackColumn = attackColumn;
            RoundPlayed = roundPlayed;
            OriginStats = stats;
            Footprint = stats.Footprint;
            CurrentHp = stats.StartingHp;
            StartingHp = stats.StartingHp;
            Element = stats.Element;
            PrintedCost = stats.PrintedCost;
            DecayMultiplier = stats.DecayMultiplier;
            IsImmuneToAttackDamage = stats.IsImmuneToAttackDamage;
            InstantDeathHpThreshold = stats.InstantDeathHpThreshold;
            AttackPower = stats.AttackPower;
            OverAttackPower = stats.OverAttackPower;
            HealPower = stats.HealPower;
            OverHealPower = stats.OverHealPower;
            PentaclePower = stats.PentaclePower;
            OverPentaclePower = stats.OverPentaclePower;
            LeftNeighborHpBuffAmount = stats.LeftNeighborHpBuffAmount;
            SelfHpBuffAmount = stats.SelfHpBuffAmount;
        }

        /// <summary>
        /// Sets HP to 0 outright — a sacrifice (The Hanged Man) or an execution (Death's on-play kill),
        /// neither of which is damage. That's why this bypasses IsImmuneToAttackDamage: Strength is
        /// immune to being HIT, and nothing in its card text says it can't be given up. The death
        /// itself still runs through the normal pipeline, so on-death effects fire as usual.
        /// </summary>
        public void Kill()
        {
            CurrentHp = 0;
        }

        /// <summary>
        /// Relocates the unit. Internal on purpose: the tiles have to move with it, and only
        /// UnitRegistry.TryMoveUnits updates both together — calling this from anywhere else would leave
        /// the board pointing at where the unit used to be.
        /// </summary>
        internal void MoveTo(HexCoord anchor, int attackColumn)
        {
            AnchorCoord = anchor;
            AttackColumn = attackColumn;
        }

        /// <summary>Natural end-of-round decay (GDD 6.2). The Tower's double decay is just DecayMultiplier == 2.</summary>
        public void ApplyDecay()
        {
            CurrentHp = Math.Max(0, CurrentHp - DecayMultiplier);
        }

        /// <summary>
        /// Combat damage (GDD 6.3). Strength-style immunity is checked here, once, so nothing else
        /// has to remember it — and for the same reason, so is The Tower's opposite rule: "if HP is
        /// 5 or below and it takes attack damage, it dies instantly" (GDD 16). Both are properties
        /// of how this unit receives damage, so both belong at the single funnel that applies it,
        /// not in AttackResolver (which would then have to special-case two cards) or in an ability
        /// hook (which would only see the damage after it had already been applied wrongly).
        ///
        /// Note the instant-death rule keys off ATTACK damage specifically: decay still takes The
        /// Tower down 2 at a time through the threshold without killing it outright, which is what
        /// makes "23 HP but fragile once low" read the way the card intends.
        /// </summary>
        public void ApplyCombatDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsImmuneToAttackDamage) return;
            if (amount == 0) return;

            if (InstantDeathHpThreshold >= 0 && CurrentHp <= InstantDeathHpThreshold)
            {
                CurrentHp = 0;
                return;
            }

            CurrentHp = Math.Max(0, CurrentHp - amount);
        }

        /// <summary>
        /// Direct HP restoration (Wand/Hierophant-style buffs, Star's board heal). Deliberately
        /// uncapped, unlike Soul.ApplyHeal — GDD 9.4: "a unit can actually grow stronger over
        /// time." Because HP is a unit's remaining lifespan (GDD 6.1), a heal here isn't
        /// "restoring lost health" the way a potion would be — it's handing the unit turns of
        /// life it never started with, which is exactly what a Wand or Hierophant is for. The
        /// Soul is the thing with a real cap (GDD 8, "Over-Heal": normal Heal caps at max,
        /// Over-Heal is the only way past it) — units were never given that cap to begin with.
        /// </summary>
        public void ApplyHeal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            CurrentHp += amount;
        }
    }
}
