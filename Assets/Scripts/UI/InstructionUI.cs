using UnityEngine;

public class InstructionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject instructionPanel;

    private bool _isPanelOpen;

    private void Start()
    {
        if (instructionPanel != null)
            instructionPanel.SetActive(false);
    }

    private void Update()
    {
        if (_isPanelOpen && Input.GetKeyDown(KeyCode.Escape))
            ClosePanel();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (instructionPanel != null)
        {
            instructionPanel.SetActive(true);
            Time.timeScale = 0f;
            _isPanelOpen = true;
        }
    }

    public void ClosePanel()
    {
        if (instructionPanel != null)
            instructionPanel.SetActive(false);

        Time.timeScale = 1f;
        Destroy(gameObject);
    }
}