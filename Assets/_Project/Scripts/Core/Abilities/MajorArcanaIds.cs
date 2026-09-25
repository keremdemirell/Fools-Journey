namespace ArcanaWars.Core.Abilities
{
    /// <summary>
    /// The 22 Major Arcana card ids, as authored in the Cards assembly's MajorArcanaData table.
    ///
    /// This is the one place Core names specific cards, and it exists so that coupling is explicit
    /// and auditable in a single file rather than scattered as string literals across ability code.
    /// If an id is ever renamed on the data side, this file is the compile-time-ish tripwire: the
    /// ability simply stops being found, which is why AbilityRegistrySelfTest exists in the harness.
    /// </summary>
    public static class MajorArcanaIds
    {
        public const string Fool = "0_fool";
        public const string Magician = "1_magician";
        public const string HighPriestess = "2_high_priestess";
        public const string Empress = "3_empress";
        public const string Emperor = "4_emperor";
        public const string Hierophant = "5_hierophant";
        public const string Lovers = "6_lovers";
        public const string Chariot = "7_chariot";
        public const string Strength = "8_strength";
        public const string Hermit = "9_hermit";
        public const string WheelOfFortune = "10_wheel_of_fortune";
        public const string Justice = "11_justice";
        public const string HangedMan = "12_hanged_man";
        public const string Death = "13_death";
        public const string Temperance = "14_temperance";
        public const string Devil = "15_devil";
        public const string Tower = "16_tower";
        public const string Star = "17_star";
        public const string Moon = "18_moon";
        public const string Sun = "19_sun";
        public const string Judgement = "20_judgement";
        public const string World = "21_world";
    }
}
