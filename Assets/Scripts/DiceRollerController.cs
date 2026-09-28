using GameUtility;
using UnityEngine;
using UnityEngine.EventSystems;

public class DiceRollerController :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    public int m_diceCount = 6;

    Vector2 m_initialMousePosition;
    
    private void Awake()
    {
        EventManager.Register("DieLaunchSetUp", new EventHandler());
    }
    private void Start()
    {

    }
    public void OnPointerDown(PointerEventData eventData)
    {
        m_initialMousePosition = eventData.position;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        var toEndPoint = eventData.position - m_initialMousePosition;

        EventManager.TriggerEvent("DiceLaunched", new Vector3(toEndPoint.x, 0f, toEndPoint.y));
    }
}
