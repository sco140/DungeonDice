using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider))]
public class PlayerUnitController : MonoBehaviour , IPointerClickHandler
{
    public GameObject m_objSkillMenuPosition;
    public GameObject m_objCharacter;

    public void OnPointerClick(PointerEventData eventData)
    {
        var skillMenu = PlayerTeamManager.Instance.GetSkillMenu();

        if (skillMenu == null)
        {
            return;
        }

        skillMenu.SetActive(true);
        skillMenu.transform.position = m_objSkillMenuPosition.transform.position;
    }
}
