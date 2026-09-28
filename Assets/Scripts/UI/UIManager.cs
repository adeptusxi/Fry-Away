using UnityEngine;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Menu Sign")]
    [SerializeField] private SignSlideAnimation menuSign;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject handPreferencePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject endScreenPanel;

    [Header("Tutorial Sign")]
    [SerializeField] private SignSlideAnimation tutorialSign;

    private SignSlideAnimation[] signs;
    private GameObject[] panels;
    private Coroutine swapRoutine;

    private void Awake()
    {
        Instance = this;

        signs = new[] { menuSign, tutorialSign };
        panels = new[] { mainMenuPanel, handPreferencePanel, settingsPanel, endScreenPanel };

        HideSignsImmediate();
    }

    // ---------- Signs ----------

    public void ShowLanding()
    {
        ShowSign(menuSign, mainMenuPanel);
    }

    public void ShowTutorial()
    {
        ShowSign(tutorialSign);
    }

    public void ShowEndScreen()
    {
        ShowSign(menuSign, endScreenPanel);
    }

    public void HideSigns()
    {
        ShowSign(null);
    }

    public void HideSignsImmediate()
    {
        StopSwap();

        foreach (SignSlideAnimation sign in signs)
        {
            if (sign != null)
            {
                sign.Exit(true);
            }
        }
    }

    private void ShowSign(SignSlideAnimation next, GameObject panel = null)
    {
        StopSwap();
        swapRoutine = StartCoroutine(SwapTo(next, panel));
    }

    private IEnumerator SwapTo(SignSlideAnimation next, GameObject panel)
    {
        if (next != null && next.IsShown)
        {
            ShowPanel(panel);
        }

        foreach (SignSlideAnimation sign in signs)
        {
            if (sign != null && sign != next && sign.IsShown)
            {
                sign.Exit();
            }
        }

        while (AnyExiting())
        {
            yield return null;
        }

        if (next != null && !next.IsShown)
        {
            ShowPanel(panel);
            next.Enter();
        }

        swapRoutine = null;
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        foreach (GameObject p in panels)
        {
            if (p != null)
            {
                p.SetActive(p == panel);
            }
        }
    }

    private bool AnyExiting()
    {
        foreach (SignSlideAnimation sign in signs)
        {
            if (sign != null && !sign.IsShown && sign.IsAnimating)
            {
                return true;
            }
        }

        return false;
    }

    private void StopSwap()
    {
        if (swapRoutine != null)
        {
            StopCoroutine(swapRoutine);
            swapRoutine = null;
        }
    }

    // ---------- Main Menu ----------

    public void OnStartPressed()
    {
        // Start the sustained loading/start sound
        AudioManager.Instance?.PlayLoop(
            SoundId.UIStartButton,
            AudioManager.LoopTrack.Loading
        );

        ShowSign(menuSign, handPreferencePanel);
    }

    public void OnSettingsPressed()
    {
        ShowSign(menuSign, settingsPanel);
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

        AudioManager.Instance?.StopLoop(
            AudioManager.LoopTrack.Loading
        );

        GameManager.Instance?.SelectHandedness(right); // start round 
    }

    // ---------- Settings ----------

    public void OnSettingsBackPressed()
    {
        ShowSign(menuSign, mainMenuPanel);
    }

    // ---------- End Screen ----------

    public void OnTryAgainPressed()
    {
        GameManager.Instance?.PlayAgain();
    }

    public void OnReturnToMenuPressed()
    {
        GameManager.Instance?.ExitToLanding();
    }

    // ---------- Exit ----------

    public void OnExitPressed()
    {
        Application.Quit();
    }
}
