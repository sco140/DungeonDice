using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider))]
public class UnitController : MonoBehaviour, IPointerClickHandler
{
    virtual public void OnPointerClick(PointerEventData eventData)
    {
        throw new System.NotImplementedException();
    }
}

