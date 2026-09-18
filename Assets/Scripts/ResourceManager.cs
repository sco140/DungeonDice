using GameUtility;
using TMPro;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    private static ResourceManager Instance { get; set; }
    [SerializeField]
    TextMeshProUGUI m_redText;
    [SerializeField]
    TextMeshProUGUI m_blueText;
    [SerializeField]
    TextMeshProUGUI m_yellowText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Register event handlers for resource updates.
        EventManager.Register("ResourceUIUpdate", new EventHandler<int, int, int>(UpdateUI));

        if (m_redText == null || m_blueText == null || m_yellowText == null)
        {
            Debug.LogError("ResourceManager: One or more UI Text components are not assigned.");
            enabled = false; // Disable the script to prevent further errors.
        }
    }

    public void UpdateUI(int red, int blue, int yellow)
    {
        Instance.m_redText.text = "Red: " + red.ToString();
        Instance.m_blueText.text = "Blue: " + blue.ToString();
        Instance.m_yellowText.text = "Yellow: " + yellow.ToString();
    }
}
