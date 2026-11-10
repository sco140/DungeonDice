
namespace GameUtility
{
    public class EventNames
    {
        // Gameplay Events
        public string BeforeDamageApplied = "BeforeDamageApplied";
        public string AfterDamageApplied = "AfterDamageApplied";
        public string BeforeHealingApplied = "BeforeHealingApplied";
        public string AfterHealingApplied = "AfterHealingApplied";
        public string BeforeStatusApplied = "BeforeStatusApplied";
        public string AfterStatusApplied = "AfterStatusApplied";
        public string BeforeUnitAttack = "BeforeUnitAttack";
        public string AfterUnitAttack = "AfterUnitAttack";
        public string BeforeUnitDeath = "BeforeUnitDeath";
        public string AfterUnitDeath = "AfterUnitDeath";
        // Status Effects
        public string BeforeBurnDamageApplied = "BeforeBurnDamageApplied";
        public string AfterBurnDamageApplied = "AfterBurnDamageApplied";
        public string BeforeBleedingDamageApplied = "BeforeBleedingDamageApplied";
        public string AfterBleedingDamageApplied = "AfterBleedingDamageApplied";
        // Dice Events
        public string SetUpDice = "SetUpDice";
        public string LaunchDice = "LaunchDice";
        public string BeforeDiceRoll = "BeforeDiceRoll";
        public string AfterDiceRoll = "AfterDiceRoll";
        public string EnableDiceLaunch = "EnableDiceLaunch";
        public string DisableDiceLaunch = "DisableDiceLaunch";
    }
}
