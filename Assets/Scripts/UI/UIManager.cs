using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject handPreferencePanel;
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject endScreenPanel;

    private void Awake()
    {
        Instance = this;
    }

    // ---------- Main Menu ----------

    public void OnStartPressed()
    {
        // Start the sustained loading/start sound
        AudioManager.Instance?.PlayLoop(
            SoundId.UIStartButton,
            AudioManager.LoopTrack.Loading
        );

        // Hide main menu
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }

        // Show hand preference
        if (handPreferencePanel != null)
        {
            handPreferencePanel.SetActive(true);
        }
    }

    public void OnSettingsPressed()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    // ---------- Hand Preference ----------

    public void OnHandPreferenceSelected(bool right)
    {
        if (right)
        {
            SettingsManager.Instance?.SelectRightHand();
        }
        else
        {
            SettingsManager.Instance?.SelectLeftHand();
        }

        // Hide hand preference panel
        if (handPreferencePanel != null)
        {
            handPreferencePanel.SetActive(false);
        }

        // Show guide
        if (guidePanel != null)
        {
            guidePanel.SetActive(true);
        }

    }

    // ---------- Settings ----------

    public void OnSettingsBackPressed()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
    }

    // ---------- Guide / Start Game ----------

    public void OnGuideDismissed()
    {
        StartCoroutine(StartGameSequence());
    }

    private IEnumerator StartGameSequence()
    {
        // Hide guide
        if (guidePanel != null)
        {
            guidePanel.SetActive(false);
        }

        // Stop loading/start sound if it is currently playing
        AudioManager.Instance?.StopLoop(
            AudioManager.LoopTrack.Loading
        );

        // Play countdown
        AudioManager.Instance?.PlayOneShot2D(
            SoundId.Countdown
        );

        // Find countdown length
        float countdownLength = 0f;

        if (AudioManager.Instance != null)
        {
            countdownLength =
                AudioManager.Instance.GetClipLength(
                    SoundId.Countdown
                );
        }

        // Wait until countdown finishes
        if (countdownLength > 0f)
        {
            yield return new WaitForSeconds(
                countdownLength
            );
        }

        // Make sure countdown sound has stopped
        AudioManager.Instance?.StopOneShot2D();

        // Start actual gameplay
        GameManager.Instance?.StartRound();
    }

    // ---------- End Screen ----------

    public void ShowEndScreen()
    {
        if (endScreenPanel != null)
        {
            endScreenPanel.SetActive(true);
        }
    }

    public void OnTryAgainPressed()
    {
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    // ---------- Exit ----------

    public void OnExitPressed()
    {
        Application.Quit();
    }
}