using UnityEngine;
using System.Collections;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [SerializeField, Tooltip("sounds which should play even while game is paused")] private AudioSource[] pausedSFX;

    [Header("Menu Sign")]
    [SerializeField] private SignSlideAnimation menuSign;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject handPreferencePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject endScreenPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("End Screen")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text seagullsHitText;

    [Header("Tutorial Sign")]
    [SerializeField] private SignSlideAnimation tutorialSign;
    [SerializeField] private CountdownUI countdownUI;
    [SerializeField, Min(0f)] private float countdownLingerSeconds = 0.75f;

    [Header("Hovering Warning Sign")]
    [SerializeField] private SignSlideAnimation warningSign;
    [SerializeField] private HoveringWarningUI hoveringWarningUI;
    [SerializeField, Min(1), Tooltip("how many seagulls hovering causes the sign to appear. should be < GameManager's MaxHoveringSeagulls")]
        private int hoveringSeagullThreshold = 2;
    [SerializeField, Min(0f), Tooltip("in seconds. duration that seagull count needs to stay below the threshold before the sign goes away")]
        private float warningHideDelay = 0.75f;

    [Header("On Hit")]
    [SerializeField] private HitPointsPopup hitPointPopupPrefab;

    // A, B, X, Y, and Menu buttons open the pause menu 
    private const OVRInput.Button PauseButtons = OVRInput.Button.One | OVRInput.Button.Two | OVRInput.Button.Three | OVRInput.Button.Four | OVRInput.Button.Start;

    private SignSlideAnimation[] signs;
    private GameObject[] panels;
    private Coroutine swapRoutine;
    private SignSlideAnimation signBeforePause; 

    private const string BestScoreKey = "score.best";

    private int seagullsHit;
    private bool wasPlaying;
    private float warningBelowElapsed;

    public bool IsPaused { get; private set; }

    public bool TutorialSignInView => tutorialSign == null || (tutorialSign.IsShown && !tutorialSign.IsAnimating);
    
    // can only pause mid-round (not during countdown or while another menu is active) 
    private bool CanPause
    {
        get
        {
            if (IsPaused || GameManager.Instance == null || menuSign == null)
            {
                return false;
            }

            GameManager.GameState state = GameManager.Instance.State;

            if (state != GameManager.GameState.Tutorial && state != GameManager.GameState.Playing)
            {
                return false;
            }

            return !menuSign.IsShown && !menuSign.IsAnimating;
        }
    }

    private void Awake()
    {
        Instance = this;

        signs = new[] { menuSign, tutorialSign };
        panels = new[] { mainMenuPanel, handPreferencePanel, settingsPanel, endScreenPanel, pausePanel, creditsPanel };

        if (pausedSFX != null)
        {
            foreach (AudioSource source in pausedSFX)
            {
                if (source != null)
                {
                    source.ignoreListenerPause = true;
                }
            }
        }

        HideSignsImmediate();

        if (warningSign != null)
        {
            warningSign.Exit(true);
        }
    }

    private void Start()
    {
        if (warningSign != null && GameManager.Instance != null
            && hoveringSeagullThreshold >= GameManager.Instance.MaxHoveringSeagulls)
        {
            Debug.LogWarning("[UIManager] hoveringSeagullThreshold is not below the lose count, the warning sign will never show", this);
        }
    }

    private void Update()
    {
        if (OVRInput.GetDown(PauseButtons, OVRInput.Controller.Touch))
        {
            ShowPause();
        }

        bool playing = GameManager.Instance != null
            && GameManager.Instance.State == GameManager.GameState.Playing;

        if (playing && !wasPlaying)
        {
            // new round 
            seagullsHit = 0;

            if (tutorialSign != null && tutorialSign.IsShown)
            {
                ShowSign(null, null, false, countdownLingerSeconds);
            }
        }

        wasPlaying = playing;

        UpdateHoveringWarning(playing);
    }

    private void UpdateHoveringWarning(bool playing)
    {
        if (warningSign == null)
            return;

        if (!playing)
        {
            HideHoveringWarning();
            return;
        }

        int count = GameManager.Instance.HoveringCount;

        if (count >= hoveringSeagullThreshold)
        {
            warningBelowElapsed = 0f;

            if (!warningSign.IsShown)
            {
                warningSign.Enter();
            }
        }
        else if (warningSign.IsShown)
        {
            warningBelowElapsed += Time.deltaTime;

            if (warningBelowElapsed >= warningHideDelay)
            {
                HideHoveringWarning();
            }
        }

        if (warningSign.IsShown && hoveringWarningUI != null)
        {
            hoveringWarningUI.SetCount(warningSign.IsAnimating ? 0 : count);
        }
    }

    private void HideHoveringWarning()
    {
        warningBelowElapsed = 0f;

        if (warningSign.IsShown)
        {
            warningSign.Exit();
        }
    }

    private void OnDestroy()
    {
        SetPaused(false);
    }

    // ---------- Signs ----------
    
    public void ShowLanding()
    {
        SetPaused(false);
        ShowSign(menuSign, mainMenuPanel);
    }

    public void ShowTutorial()
    {
        SetPaused(false);
        ShowSign(tutorialSign);
    }

    public void ShowEndScreen()
    {
        SetPaused(false);
        PopulateEndScreen();
        ShowSign(menuSign, endScreenPanel);
    }

    public void ShowCountdown()
    {
        SetPaused(false);

        if (countdownUI != null)
        {
            countdownUI.Play();
        }

        ShowSign(tutorialSign, null, true);
    }

    public void HideSigns()
    {
        SetPaused(false);
        ShowSign(null);
    }

    public void HideSignsImmediate()
    {
        SetPaused(false);
        StopSwap();

        foreach (SignSlideAnimation sign in signs)
        {
            if (sign != null)
            {
                sign.Exit(true);
            }
        }
    }

    private void ShowSign(SignSlideAnimation next, GameObject panel = null, bool flipped = false, float delay = 0f)
    {
        StopSwap();
        swapRoutine = StartCoroutine(SwapTo(next, panel, flipped, delay));
    }

    private IEnumerator SwapTo(SignSlideAnimation next, GameObject panel, bool flipped, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (next != null && next.IsShown)
        {
            ShowPanel(panel);
            next.Flip(flipped);
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
            next.Enter(false, flipped);
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

    public void OnCreditsPressed()
    {
        ShowSign(menuSign, creditsPanel);
    }

    // ---------- Credits ----------

    public void OnCreditsBackPressed()
    {
        ShowSign(menuSign, mainMenuPanel);
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
        if (!IsPaused)
        {
            ShowSign(menuSign, mainMenuPanel);
            return;
        }

        ApplyHandPreference();
        ShowSign(menuSign, pausePanel);
    }

    private void ApplyHandPreference()
    {
        GameManager game = GameManager.Instance;

        if (game == null || SettingsManager.Instance == null)
            return;

        if (game.State == GameManager.GameState.Playing && game.BreadRemaining <= 0)
            return;

        game.SelectHandedness(
            SettingsManager.Instance.CurrentHand == SettingsManager.PreferredHand.Right
        );
    }

    // ---------- Pause ----------

    // (hook) brings up the pause menu and freezes the round. no-op if game is not in a pausable state
    public void ShowPause()
    {
        if (!CanPause)
            return;

        signBeforePause = GameManager.Instance.State == GameManager.GameState.Tutorial && tutorialSign != null && tutorialSign.IsShown 
            ? tutorialSign 
            : null;

        SetPaused(true);
        ShowSign(menuSign, pausePanel);
    }

    // (hook) unpauses and put back whatever sign was up before
    public void OnContinuePressed()
    {
        if (!IsPaused)
            return;

        SetPaused(false);
        ShowSign(signBeforePause);
    }
    
    private void SetPaused(bool paused)
    {
        if (IsPaused == paused)
        {
            return;
        }

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
    }

    // ---------- Score Popup ----------

    // (hook) called when a seagull is hit 
    public void ShowScorePopup(GameManager.HitResult result)
    {
        seagullsHit++;

        if (hitPointPopupPrefab == null)
        {
            return;
        }

        HitPointsPopup popup = Instantiate(hitPointPopupPrefab, result.position, Quaternion.identity);
        popup.Show(result.points, result.pointsFraction, result.isLongShot);
    }

    // ---------- End Screen ----------

    private void PopulateEndScreen()
    {
        int score = GameManager.Instance != null ? GameManager.Instance.Score : 0;
        int best = PlayerPrefs.GetInt(BestScoreKey, 0);

        if (score > best)
        {
            best = score;

            PlayerPrefs.SetInt(
                BestScoreKey,
                best
            );

            PlayerPrefs.Save(); 
        }

        SetText(scoreText, score);
        SetText(bestScoreText, best);
        SetText(seagullsHitText, seagullsHit);
    }

    private void SetText(TMP_Text text, int value)
    {
        if (text != null)
        {
            text.text = value.ToString();
        }
    }
    
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
