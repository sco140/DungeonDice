using GameUtility;
using UnityEngine;

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
}
