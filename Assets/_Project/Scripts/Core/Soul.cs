using System;

namespace ArcanaWars.Core
{
    /// <summary>
    /// A player's life total — the "Main Nexus Health Pool" (GDD section 3). Reduce the
    /// opponent's to 0 to win. Unlike UnitInstance.ApplyHeal, this one DOES cap at MaxHp for
    /// a regular heal — GDD 8 names "Over-Heal" as specifically "the only way" past that cap,
    /// which only makes sense if the ordinary Heal keyword can't do it.
    /// </summary>
    public class Soul
    {
        /// <summary>The standard starting/max Soul HP for a match (not stated numerically in the GDD's text — confirmed as 25).</summary>
        public const int StandardStartingHp = 25;

        public PlayerSide Owner { get; }
        public int MaxHp { get; }
        public int CurrentHp { get; private set; }
        public bool IsDefeated => CurrentHp <= 0;

        public Soul(PlayerSide owner, int maxHp)
        {
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            Owner = owner;
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        /// <summary>Unblocked Attack damage, or Over-Attack that pierced a blocker (GDD 5).</summary>
        public void ApplyDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            CurrentHp = Math.Max(0, CurrentHp - amount);
        }

        /// <summary>Regular Heal (Cups keyword, GDD 9.2). Capped at MaxHp.</summary>
        public void ApplyHeal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
        }

        /// <summary>Over-Heal (GDD 8) — the only thing allowed to push CurrentHp past MaxHp.</summary>
        public void ApplyOverHeal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            CurrentHp += amount;
        }

        /// <summary>
        /// Brings the Soul DOWN to `hp` if it's above it — Judgement's "the enemy Soul ... matches your
        /// Soul HP" (GDD 11.XX). Deliberately not damage: it fires no damage triggers and ignores The
        /// Devil's immunity, because a Soul being set equal to another isn't a hit. Never raises HP.
        /// </summary>
        public void LowerTo(int hp)
        {
            if (hp < 0) hp = 0;
            if (CurrentHp > hp) CurrentHp = hp;
        }
    }
}
