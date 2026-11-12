
namespace GameUtility
{
    public static class EventNames
    {
        // Gameplay Events
        public const string BeforeDamageApplied = "BeforeDamageApplied";
        public const string AfterDamageApplied = "AfterDamageApplied";
        public const string BeforeHealingApplied = "BeforeHealingApplied";
        public const string AfterHealingApplied = "AfterHealingApplied";
        public const string BeforeStatusApplied = "BeforeStatusApplied";
        public const string AfterStatusApplied = "AfterStatusApplied";
        public const string BeforeUnitAttack = "BeforeUnitAttack";
        public const string AfterUnitAttack = "AfterUnitAttack";
        public const string BeforeUnitDeath = "BeforeUnitDeath";
        public const string AfterUnitDeath = "AfterUnitDeath";
        // Status Effects
        public const string BeforeBurnDamageApplied = "BeforeBurnDamageApplied";
        public const string AfterBurnDamageApplied = "AfterBurnDamageApplied";
        public const string BeforeBleedingDamageApplied = "BeforeBleedingDamageApplied";
        public const string AfterBleedingDamageApplied = "AfterBleedingDamageApplied";
        // Dice Events
        public const string SetUpDice = "SetUpDice";
        public const string LaunchDice = "LaunchDice";
        public const string BeforeDiceRoll = "BeforeDiceRoll";
        public const string AfterDiceRoll = "AfterDiceRoll";
        public const string EnableDiceLaunch = "EnableDiceLaunch";
        public const string DisableDiceLaunch = "DisableDiceLaunch";
    }
}
