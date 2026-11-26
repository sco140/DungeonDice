
namespace GameUtility
{
    public static class EventNames
    {
        // UI Events
        public const string ResourceUIUpdate = "ResourceUIUpdate";
        public const string UnitClicked = "UnitClicked";
        public const string CancelClicked = "CancelClicked";
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
        public const string BeforeResourceUpdate = "BeforeResourceUpdate";
        public const string AfterResourceUpdate = "AfterResourceUpdate";
        // Dice Events
        public const string SetUpDice = "SetUpDice";
        public const string LaunchDice = "LaunchDice";
        public const string BeforeDiceRoll = "BeforeDiceRoll";
        public const string BeforeResourceDiceRoll = "BeforeResourceDiceRoll";
        public const string AfterDiceRoll = "AfterDiceRoll";
        public const string AfterResourceDiceRoll = "AfterResourceDiceRoll";
        public const string EnableDiceLaunch = "EnableDiceLaunch";
        public const string DisableDiceLaunch = "DisableDiceLaunch";
    }
}
