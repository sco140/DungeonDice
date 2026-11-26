using GameUtility;
using System.Collections.Generic;
using UnityEngine;

public enum UnitType
{
    PLAYER = 0,
    ENEMY = 1,
    MAX
}
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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartTurn();
    }

    public void EndTurn()
    {
        // TODO: This should go to the next player or AI turn, but for now, it just starts the turn again.
        StartTurn();
    }

    public void StartTurn()
    {
        var diceEntries = new List<DiceRollEntry>();
        foreach (var unit in PlayerTeamManager.GetTeam())
        {
            if (unit == null)
            {
                Debug.LogError("The player team contains an unassigned unit controller.");
                continue;
            }

            var character = unit.Character;
            if (character == null)
            {
                Debug.LogError($"Unit {unit.name} has no BaseCharacters asset assigned.");
                continue;
            }

            if (character.m_lstDiceValues == null)
            {
                Debug.LogError($"Character {character.name} has no dice list assigned.");
                continue;
            }

            foreach (var dice in character.m_lstDiceValues)
            {
                if (dice is ResourceDice resourceDie)
                {
                    diceEntries.Add(new DiceRollEntry(resourceDie, unit));
                }
            }

            if (StatusManager.Instance != null)
            {
                int burnStacks = StatusManager.Instance.GetStackCount(StatusTypes.Burn, unit.gameObject);
                for (int i = 0; i < burnStacks; i++)
                {
                    diceEntries.Add(new DiceRollEntry(ScriptableObject.CreateInstance<BurnDice>(), unit));
                }
            }
        }

        if (diceEntries.Count == 0)
        {
            Debug.LogError("The player team has no resource or burn dice to roll.");
            return;
        }

        var rollRequest = new DiceRollRequest(diceEntries);
        EventManager.TriggerEvent(EventNames.EnableDiceLaunch, rollRequest);
    }

    public bool TryGetUnit(UnitType unitType, int unitId, out GameObject unit)
    {
        unit = null;
        switch (unitType)
        {
            case UnitType.PLAYER:
                return PlayerTeamManager.Instance.TryGetUnit(unitId, out unit);
            case UnitType.ENEMY:
                Debug.Log("Trying to get enemy unit with ID: " + unitId);
                Debug.Log("Not Implemented");
                //return EnemyTeamManager.Instance.TryGetUnit(unitId, out unit);
                break;
            default:
                Debug.LogError($"TryGetUnit: Unrecognized unit type {unitType}");
                break;
        }
        return false;
    }
    public int GetUnitId(UnitController unit)
    {
        if (unit == null)
        {
            Debug.LogError("GetUnitId called with a null unit.");
            return -1;
        }
        var playerController = unit as PlayerUnitController;
        if (playerController != null)
        {
            return PlayerTeamManager.Instance.GetUnitId(playerController);
        }
        // If you have an EnemyUnitController, you can add similar logic here.
        // var enemyController = unit.GetComponent<EnemyUnitController>();
        // if (enemyController != null)
        // {
        //     return EnemyTeamManager.Instance.GetUnitId(enemyController);
        // }
        Debug.LogError($"GetUnitId: The unit {unit.name} does not have a recognized controller.");
        return -1;
    }
}
