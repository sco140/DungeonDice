using UnityEngine;
using UnityEngine.EventSystems;

public class LaunchDice :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    private void Start()
    {
        DiceManager.Instance.SpawnDice();
    }
    public Vector2 m_initialMousePosition;
    public void OnPointerDown(PointerEventData eventData)
    {
        m_initialMousePosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        var toEndPoint = eventData.position - m_initialMousePosition;
        DiceManager.Instance.LaunchDice(new Vector3(toEndPoint.x, 0f, toEndPoint.y));
    }
}
