using UnityEngine;

public class PlayerTeamManager : MonoBehaviour
{
    /// <summary>
    /// TODO: Consider creating a class to control the skill menu and its interactions with the player unit controllers,
    ///       instead of having the PlayerTeamManager directly manage it.
    /// </summary>
    [SerializeField]
    GameObject m_UiSkillMenu;

    [SerializeField]
    PlayerUnitController[] m_PlayerUnitController = new PlayerUnitController[3];

    public static PlayerTeamManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// TODO: Consider creating a class to control the skill menu and its interactions with the player unit controllers,
    ///       instead of having the PlayerTeamManager directly manage it.
    /// </summary>
    public GameObject GetSkillMenu()
    {
        return m_UiSkillMenu;
    }

    public PlayerUnitController GetPlayerUnitController(int index)
    {
        if (index < 0 || index >= m_PlayerUnitController.Length)
        {
            Debug.LogError($"Index {index} is out of bounds for PlayerUnitController array.");
            return null;
        }
        return m_PlayerUnitController[index];
    }

    public PlayerUnitController[] GetTeam()
    {
        return m_PlayerUnitController;
    }
}
