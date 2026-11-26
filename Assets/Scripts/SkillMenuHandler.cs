using GameUtility;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SkillMenuHandler : MonoBehaviour
{
    public Button[] m_SkillButton = new Button[4];
    Canvas m_Canvas;
    public void Start()
    {
        m_Canvas = GetComponent<Canvas>();
        if (m_Canvas == null)
        {
            Debug.LogError("SkillMenuHandler: Canvas component is missing.");
            gameObject.SetActive(false);
        }
        EventManager.Register(
            EventNames.UnitClicked,
            new EventHandler<UnitClickedEventData>(OnUnitClicked));
        EventManager.Register(
            EventNames.CancelClicked,
            new EventHandler(OnCancelClicked));
    }
    private void OnUnitClicked(UnitClickedEventData eventData)
    {
        if(!GameManager.Instance.TryGetUnit(
            eventData.unitType, 
            eventData.unitId, 
            out GameObject unit))
        {
            Debug.LogError("Failed to get unit.");
            return;
        }

        if (unit.TryGetComponent(out PlayerUnitController playerUnitController))
        {
            SetPosition(playerUnitController.m_objSkillMenuPosition.transform.position);
            DisplaySkillMenu();
        }
        else
        {
            Debug.LogError("The clicked unit is not a player unit.");
        }
    }
    private void OnCancelClicked()
    {
        EventManager.Unregister<UnitClickedEventData>(
            EventNames.UnitClicked,
            OnUnitClicked);
    }
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }
    public void DisplaySkillMenu()
    {
        m_Canvas.enabled = true;
    }

    public void HideSkillMenu()
    {
        m_Canvas.enabled = false;
    }
}
