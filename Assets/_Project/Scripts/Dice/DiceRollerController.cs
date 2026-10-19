using GameUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
/// <summary>
/// This class handles the user input for rolling dice in the game. 
/// It listens for pointer down and up events to determine the direction and strength of the dice launch. 
/// It also manages the activation of the dice camera panel and triggers events to spawn and launch dice based on user input.
/// </summary>
public class DiceRollerController :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    public GameObject m_objDiceCameraPanel;
    public Image m_cBackgroundPanel;
    Vector2 m_initialMousePosition;
    private void Awake()
    {
        EventManager.Register("EnableDiceLaunch", new EventHandler<int>(EnableDiceLaunch));
        EventManager.Register("DisableDiceLaunch", new EventHandler(DisableDiceLaunch));
    }
    private void Start()
    {
        if (m_objDiceCameraPanel == null)
        {
            Debug.LogError("DiceRollerController: Dice camera is not assigned.");
            enabled = false; // Disable the script to prevent further errors.
        }
    }
    public void EnableDiceLaunch(int diceAmount)
    {
        EventManager.TriggerEvent("SetUpDice", diceAmount);
        m_objDiceCameraPanel.SetActive(true);
        m_cBackgroundPanel.enabled = true;
    }
    public void DisableDiceLaunch()
    {
        m_objDiceCameraPanel.SetActive(false);
        m_cBackgroundPanel.enabled = false;
    }
    private void OnDestroy()
    {
        EventManager.Unregister<int>("EnableDiceLaunch", EnableDiceLaunch);
        EventManager.Unregister("DisableDiceLaunch", DisableDiceLaunch);
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        m_initialMousePosition = eventData.position;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        var toEndPoint = eventData.position - m_initialMousePosition;
        EventManager.TriggerEvent("LaunchDice", new Vector3(toEndPoint.x, 0f, toEndPoint.y));
    }
}
