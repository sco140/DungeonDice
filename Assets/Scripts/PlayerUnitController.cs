using GameUtility;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider))]
public class PlayerUnitController : UnitController
{
    public GameObject m_objSkillMenuPosition;
    public GameObject m_objCharacter;

    override public void OnPointerClick(PointerEventData eventData)
    {
        int unitId = GameManager.Instance.GetUnitId(this);
        
        EventManager.TriggerEvent(
            EventNames.UnitClicked, 
            new UnitClickedEventData { unitType = UnitType.PLAYER, unitId = unitId });
    }
}
