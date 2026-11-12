using GameUtility;
using UnityEngine;
public enum UnitType
{
    PLAYER = 0,
    ENEMY = 1
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
        // Turn on the dice roller UI when the game starts.
        EventManager.TriggerEvent("EnableDiceLaunch", PlayerTeamManager.DiceCount);
    }

    public void EndTurn()
    {
        // TODO: This should go to the next player or AI turn, but for now, it just starts the turn again.
        StartTurn();
    }

    public void StartTurn()
    {
        // Turn on the dice roller UI when the turn starts.
        EventManager.TriggerEvent("EnableDiceLaunch", PlayerTeamManager.DiceCount);
    }

    public bool TryGetUnit(int unitType, int unitId, out GameObject unit)
    {
        unit = null;
        switch ((UnitType)unitType)
        {
            case UnitType.PLAYER:
                return PlayerTeamManager.Instance.TryGetUnit(unitId, out unit);
            case UnitType.ENEMY:
                Debug.Log("Trying to get enemy unit with ID: " + unitId);
                Debug.Log("Not Implemented");
                //return EnemyTeamManager.Instance.TryGetUnit(unitId, out unit);
                break;
        }
        return false;
    }
}
