using UnityEngine;
using UnityEngine.UI;

public class SkillMenuHandler : MonoBehaviour
{
    public Button[] m_SkillButton = new Button[4];
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }
    public void OpenSkillMenu()
    {
        gameObject.SetActive(true);
    }

    public void CloseSkillMenu()
    {
        gameObject.SetActive(false);
    }
}
