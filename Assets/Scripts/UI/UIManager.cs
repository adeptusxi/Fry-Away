using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private GameObject settingsPanel;

    private void Awake()
    {
        Instance = this;
    }

    public void OnStartPressed()
    {
        mainMenuPanel.SetActive(false);
        guidePanel.SetActive(true);
    }

    public void OnSettingsPressed()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void OnSettingsBackPressed()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void OnGuideDismissed()
    {
        guidePanel.SetActive(false);
        GameManager.Instance?.StartRound();
    }

    public void OnExitPressed()
    {
        Application.Quit();
    }
}