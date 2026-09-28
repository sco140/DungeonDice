using GameUtility;
using UnityEngine;
using UnityEngine.EventSystems;

public class LaunchDice :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    Vector2 m_initialMousePosition;
    public void OnPointerDown(PointerEventData eventData)
    {
        m_initialMousePosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        var toEndPoint = eventData.position - m_initialMousePosition;
        DiceManager.LaunchDice(new Vector3(toEndPoint.x, 0f, toEndPoint.y));
    }
}
