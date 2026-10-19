using DungeonDice.Dice;
using GameUtility;
using UnityEngine;

namespace DungeonDice.Core
{
    /// <summary>
    /// Legacy SampleScene driver: starts the mana roll and turns a completed roll into mana.
    /// Replaced by the run/combat controllers as the prototype is built.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Registered in Start rather than Awake: EventManager shares this GameObject and
            // component Awake order is not guaranteed.
            EventManager.Register(GameEvents.RollCompleted, new EventHandler<RollCompletedEvent>(OnRollCompleted));
            EventManager.TriggerEvent(GameEvents.EnableDiceLaunch, PlayerTeamManager.DiceCount);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            EventManager.Unregister<RollCompletedEvent>(GameEvents.RollCompleted, OnRollCompleted);
        }

        /// <summary>
        /// Previews the "bank it" rule: every die yields 1 mana of its color.
        /// </summary>
        private void OnRollCompleted(RollCompletedEvent roll)
        {
            var update = new Event_ResourceUpdate();
            foreach (var result in roll.Results)
            {
                switch (result.Color)
                {
                    case DieColor.Red: update.red++; break;
                    case DieColor.Blue: update.blue++; break;
                    case DieColor.Yellow: update.yellow++; break;
                }
            }
            EventManager.TriggerEvent(GameEvents.UpdateResources, update);
        }

        public void EndTurn()
        {
            // TODO: advance to the next player or AI turn; for now the turn simply restarts.
            StartTurn();
        }

        public void StartTurn()
        {
            EventManager.TriggerEvent(GameEvents.EnableDiceLaunch, PlayerTeamManager.DiceCount);
        }
    }
}
