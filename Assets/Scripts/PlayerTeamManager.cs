using GameUtility;
using UnityEngine;

public class Event_ResourceUpdate
{
    public int red = 0;
    public int blue = 0;
    public int yellow = 0;
}

public class PlayerTeamManager : MonoBehaviour
{
    private static PlayerTeamManager Instance { get; set; }

    // Skill resources
    [SerializeField]
    private int m_red = 0;
    [SerializeField]
    private int m_blue = 0;
    [SerializeField]
    private int m_yellow = 0;
    /// <summary>
    /// TODO: Consider creating a class to control the skill menu and its interactions with the player unit controllers,
    ///       instead of having the PlayerTeamManager directly manage it.
    /// </summary>
    [SerializeField]
    GameObject m_UiSkillMenu;
    [SerializeField]
    PlayerUnitController[] m_PlayerUnitController = new PlayerUnitController[3];

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

    private void Start()
    {
        // Register event handlers for resource updates.
        EventManager.Register("UpdateResources", new EventHandler<Event_ResourceUpdate>(UpdateResources));
    }
    /// <summary>
    /// TODO: Consider creating a class to control the skill menu and its interactions with the player unit controllers,
    ///       instead of having the PlayerTeamManager directly manage it.
    /// </summary>
    public static GameObject GetSkillMenu()
    {
        return Instance.m_UiSkillMenu;
    }
    public static PlayerUnitController GetPlayerUnitController(int index)
    {
        if (index < 0 || index >= Instance.m_PlayerUnitController.Length)
        {
            Debug.LogError($"Index {index} is out of bounds for PlayerUnitController array.");
            return null;
        }
        return Instance.m_PlayerUnitController[index];
    }
    public static PlayerUnitController[] GetTeam()
    {
        return Instance.m_PlayerUnitController;
    }
    public static void UpdateResources(Event_ResourceUpdate eventData)
    {
        // Send the event to all listeners to update affect this value.
        EventManager.TriggerEvent("BeforeResourceUpdate", eventData);

        // Applying all changes to the skill resources
        // after all listeners have had a chance to modify them.
        Instance.m_red = eventData.red;
        Instance.m_blue = eventData.blue;
        Instance.m_yellow = eventData.yellow;

        EventManager.TriggerEvent("ResourceUIUpdate", eventData);

        // Send the event to all listeners to update affect this value.
        EventManager.TriggerEvent("AfterResourceUpdate", eventData);
    }
}
