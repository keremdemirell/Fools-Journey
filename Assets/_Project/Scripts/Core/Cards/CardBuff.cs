using ArcanaWars.Core.Units;

namespace ArcanaWars.Core.Cards
{
    /// <summary>
    /// A stat bonus carried by one copy of a card in hand, applied when that copy is played — The
    /// Hermit's "every card you draw while it's isolated gets +1 HP, +1 Attack, +1 Heal".
    ///
    /// Why on the copy in hand and not on the card: the GDD's version buffed "all units in your deck",
    /// but a deck is a list of shared, immutable card definitions (the same ScriptableObject sits in
    /// both players' decks), so there is nowhere per-match to put that buff. Buffing each card as it's
    /// DRAWN (user's rework) needs only per-copy state, which HandCard already has for the Wheel's
    /// discount.
    /// </summary>
    public readonly struct CardBuff
    {
        public readonly int Hp;
        public readonly int Attack;
        public readonly int Heal;

        public CardBuff(int hp, int attack, int heal)
        {
            Hp = hp;
            Attack = attack;
            Heal = heal;
        }

        public static readonly CardBuff None = default;

        public bool IsNone => Hp == 0 && Attack == 0 && Heal == 0;

        public static CardBuff operator +(CardBuff a, CardBuff b) => new CardBuff(a.Hp + b.Hp, a.Attack + b.Attack, a.Heal + b.Heal);

        /// <summary>
        /// The buffed stats. "+1 Attack" on a card with no attack gives it a normal lane attack of 1:
        /// the Hermit grants Attack power, and a lane attack is what Attack power means.
        /// </summary>
        public UnitSpawnStats ApplyTo(UnitSpawnStats stats) => IsNone ? stats : stats.With(
            startingHp: stats.StartingHp + Hp,
            attackPower: stats.AttackPower + Attack,
            healPower: stats.HealPower + Heal);
    }
}
