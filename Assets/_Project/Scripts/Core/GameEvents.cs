namespace DungeonDice.Core
{
    /// <summary>
    /// Every <c>EventManager</c> event name, with its payload type in the comment.
    /// Register and trigger with these constants; never with raw string literals.
    /// </summary>
    public static class GameEvents
    {
        // --- Dice box -------------------------------------------------------------
        /// <summary>int diceCount. Show the roll overlay and set up that many dice.</summary>
        public const string EnableDiceLaunch = "EnableDiceLaunch";
        /// <summary>No payload. Hide the roll overlay.</summary>
        public const string DisableDiceLaunch = "DisableDiceLaunch";
        /// <summary>int diceCount. Spawn or re-pool dice in the box.</summary>
        public const string SetUpDice = "SetUpDice";
        /// <summary>UnityEngine.Vector3 drag vector (x, 0, y). Throw the active dice.</summary>
        public const string LaunchDice = "LaunchDice";
        /// <summary>DungeonDice.Dice.RelaunchRequest. Re-throw a subset of the settled dice (reroll).</summary>
        public const string RelaunchDice = "RelaunchDice";
        /// <summary>int diceCount. Dice have just left the player's hand (throw or re-throw). Presentation only.</summary>
        public const string DiceThrown = "DiceThrown";
        /// <summary>DungeonDice.Dice.DiceBoxProfile. Swap the box's look and sound (combat vs. traversal).</summary>
        public const string SetDiceBoxProfile = "SetDiceBoxProfile";
        /// <summary>DungeonDice.Dice.RollCompletedEvent. Every thrown die has settled and been read.</summary>
        public const string RollCompleted = "RollCompleted";
        /// <summary>DungeonDice.Dice.RollResult. A modifier changed one settled result; the die was re-oriented.</summary>
        public const string RollResultModified = "RollResultModified";

        // --- Run ------------------------------------------------------------------------
        /// <summary>DungeonDice.Run.RunPhase. The run moved to a new screen.</summary>
        public const string RunPhaseChanged = "RunPhaseChanged";

        // --- Legacy mana loop (SampleScene) -----------------------------------------
        /// <summary>Event_ResourceUpdate. Request a change to the team mana pool.</summary>
        public const string UpdateResources = "UpdateResources";
        /// <summary>Event_ResourceUpdate (mutable). Listeners may alter the pending values.</summary>
        public const string BeforeResourceUpdate = "BeforeResourceUpdate";
        /// <summary>Event_ResourceUpdate. Pool was applied; refresh the HUD.</summary>
        public const string ResourceUIUpdate = "ResourceUIUpdate";
        /// <summary>Event_ResourceUpdate. Pool was applied; react to it.</summary>
        public const string AfterResourceUpdate = "AfterResourceUpdate";
    }
}
