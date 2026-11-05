using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider))]
public class PlayerUnitController : UnitController
{
    public GameObject m_objSkillMenuPosition;
    public GameObject m_objCharacter;

    override public void OnPointerClick(PointerEventData eventData)
    {
        var skillMenu = PlayerTeamManager.GetSkillMenu();

        if (skillMenu == null)
        {
            return;
        }

        skillMenu.SetActive(true);
        skillMenu.transform.position = m_objSkillMenuPosition.transform.position;
    }
}
