using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Landing,   // handedness selection, no gameplay objects
        Tutorial,  // freebie seagull + diagetic guide sign, unlimited bread
        Playing,   // core loop, bread is limited
        GameOver,  // end sign is up
        Countdown  // waiting for the gameplay/music cue
    }

    public struct HitResult
    {
        public int points;
        public float pointsFraction; // 0-1 
        public bool isLongShot;
        public Vector3 position; // world space
    }

    [Header("Spawners")]
    [Tooltip("normally the BreadSpawnerBasket. a plain spawner also works for test scenes, it just can't attach to a hand")]
    [SerializeField] private ThrowInteractableSpawner breadSpawner;

    [SerializeField] private TargetSpawner seagullSpawner;

    [Header("Tutorial Seagull")]
    [SerializeField] private TutorialSeagull tutorialSeagullPrefab;

    [Tooltip("where the freebie seagull grows in. its rotation is used too, so it can face the player")]
    [SerializeField] private Transform tutorialSpawnPoint;

    [Header("Player")]
    [Tooltip("measured against for score distance. defaults to the seagull spawner's cone origin")]
    [SerializeField] private Transform playerTransform;

    [Header("Bread")]
    [Tooltip("how many breads the player gets once the tutorial is over. running out is the win condition. -1 means infinite")]
    [SerializeField, Min(-1)] private int breadCount = 20;

    [Tooltip("in seconds. safety net: if the last bread never reports landing, win anyway after this long")]
    [SerializeField, Min(0f)] private float lastThrowResolveTimeout = 10f;

    [Header("Score")]
    [Tooltip("points for a hit at maxScoreDistance, before any long shot bonus")]
    [SerializeField, Min(0)] private int maxPointsAtRange = 100;

    [Tooltip("in meters. a hit at or below this distance is worth the curve's value at 0")]
    [SerializeField, Min(0f)] private float minScoreDistance = 3f;

    [Tooltip("in meters. a hit at or beyond this distance is worth the curve's value at 1")]
    [SerializeField, Min(0f)] private float maxScoreDistance = 20f;

    [Tooltip("normalized 0-1 curve mapping distance to points. this is where the nonlinearity lives, so bend it to taste")]
    [SerializeField] private AnimationCurve scoreCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("points multiplier for a seagull that has already given up and started hovering")]
    [SerializeField, Range(0f, 1f)] private float hoveringPointsScalar = 0.25f;

    [Tooltip("in meters. a kill at or beyond this distance counts as a long shot")]
    [SerializeField, Min(0f)] private float longShotDistance = 10f;

    [SerializeField, Min(0)] private int longShotBonus = 50;

    [Header("Hit Feedback")] 
    [SerializeField] private ParticleSystem onHitVFX;

    [Header("Lose Condition")]
    [Tooltip("this many seagulls hovering overhead at once and the run is lost")]
    [SerializeField, Min(1)] private int maxHoveringSeagulls = 5;

    [Header("Hovering Crowd Audio")]
    [SerializeField, Range(1f, 1.25f), Tooltip("Highest BGM speed before the loss threshold. Also raises pitch.")]
    private float crowdBgmSpeedCeiling = 1.25f;
    [SerializeField, Min(0f), Tooltip("Seconds to smoothly adjust speed and warning cadence after the crowd changes.")]
    private float crowdAudioTransitionSeconds = 0.5f;
    [SerializeField, Tooltip("Your warning one-shot. Repeats while gulls hover and stops at game over.")]
    private AudioClip crowdWarningBeep;
    [SerializeField, Min(0f), Tooltip("Fixed loudness multiplier for every hovering-gull warning, including the first.")]
    private float crowdWarningVolumeScale = 2f;
    [SerializeField, Min(0.1f), Tooltip("Seconds between warning starts with one hovering gull.")]
    private float crowdBeepSlowInterval = 1.2f;
    [SerializeField, Min(0.1f), Tooltip("Warning starts per second just before the loss threshold.")]
    private float crowdBeepFastRate = 3f;
    [SerializeField, Min(1f), Tooltip("Warning playback speed just before the loss threshold. Raises its pitch too.")]
    private float crowdWarningFastPitch = 2.2f;

    private AudioSource crowdWarningSource;
    private int previousHoveringCount = -1;
    private float crowdPitch = 1f;
    private float crowdPitchFrom = 1f;
    private float crowdPitchTarget = 1f;
    private float crowdBeepInterval = 1.2f;
    private float crowdBeepIntervalFrom = 1.2f;
    private float crowdBeepIntervalTarget = 1.2f;
    private float crowdWarningPitch = 1f;
    private float crowdWarningPitchFrom = 1f;
    private float crowdWarningPitchTarget = 1f;
    private float crowdTransitionElapsed;
    private float crowdBeepPhase;

    [Header("Debug")]
    [Tooltip("Turn every DebugDisplay in the scene on or off")]
    [SerializeField] private bool showDebugDisplays = true;

    [Tooltip("Check this box for test scenes w/o UI: skips the landing page, start sign and freebie seagull, "
        + "and drops straight into the core loop with the bread limit already on")]
    [SerializeField] private bool spawnImmediately = false;

    [SerializeField] private bool verbose = true;

    private GameState state = GameState.Landing;
    private int breadRemaining;
    private int breadInFlight;
    private bool outOfBread;
    private float outOfBreadTime;
    private TutorialSeagull tutorialSeagull;
    private SoundId currentBgm = SoundId.None;
    private Coroutine countdownRoutine;
    private AudioSource countdownSource;
    private AudioSource gameEndSource;
    private bool countdownInProgress;

    public static GameManager Instance { get; private set; }

    public bool ShowDebugDisplays => showDebugDisplays;
    public GameState State => state;
    public int Score { get; private set; }
    public int BreadRemaining => breadRemaining;

    private bool UnlimitedBread => breadCount < 0;

    private BreadSpawnerBasket Basket => breadSpawner as BreadSpawnerBasket; // null if a general ThrowInteractableSpawner was referenced 

    private Transform PlayerAnchor =>
        playerTransform
            ? playerTransform
            : seagullSpawner && seagullSpawner.ConeOrigin ? seagullSpawner.ConeOrigin : transform;
    
    public int HoveringCount
    {
        get
        {
            if (!seagullSpawner)
            {
                return 0;
            }

            IReadOnlyList<HittableTarget> targets = seagullSpawner.CurrentTargets;
            int count = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                if (!targets[i])
                {
                    continue;
                }

                if (targets[i] is HittableSeagull seagull && seagull.IsHovering)
                {
                    count++;
                }
            }

            return count;
        }
    }
    
    #region Unity 

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (breadSpawner)
        {
            breadSpawner.OnThrowableThrown += HandleBreadThrown;
        }
        else
        {
            Debug.LogError("[GameManager] no breadSpawner assigned: no bread, no budget, no win condition", this);
        }

        if (!seagullSpawner)
        {
            Debug.LogError("[GameManager] no seagullSpawner assigned: no seagulls, no lose condition", this);
        }
    }

    private void Start()
    {
        AudioManager.Instance?.PlayLoop(
            SoundId.AmbientOcean,
            AudioManager.LoopTrack.Ambient
        );

        if (spawnImmediately)
        {
            EnterPlayingDirectly();
            return;
        }

        EnterLanding();
    }

    private void Update()
    {
        if (state != GameState.Playing || Time.timeScale <= 0f || AudioListener.pause ||
            (UIManager.Instance != null && UIManager.Instance.IsPaused))
        {
            return;
        }

        int hoveringCount = HoveringCount;
        if (hoveringCount >= maxHoveringSeagulls)
        {
            LoseGame();
            return;
        }

        UpdateCrowdAudio(hoveringCount);

        if (!outOfBread)
        {
            return;
        }

        if (breadInFlight <= 0 || Time.time - outOfBreadTime >= lastThrowResolveTimeout)
        {
            WinGame();
        }
    }

    private void UpdateCrowdAudio(int hoveringCount)
    {
        if (hoveringCount != previousHoveringCount)
        {
            // The warning cadence scales toward the last playable crowd size.
            int lastPlayableCount = Mathf.Max(1, maxHoveringSeagulls - 1);
            float beepFraction = Mathf.Clamp01(
                (float)(hoveringCount - 1) / Mathf.Max(1, lastPlayableCount - 1));
            crowdWarningPitchFrom = crowdWarningPitch;
            crowdWarningPitchTarget = Mathf.Lerp(1f, Mathf.Max(1f, crowdWarningFastPitch), beepFraction);
            // Keep a short gap between warnings at the faster playback speed.
            float minimumInterval = crowdWarningBeep != null
                ? crowdWarningBeep.length / crowdWarningPitchTarget + 0.05f : 0.1f;
            float slowInterval = Mathf.Max(crowdBeepSlowInterval, minimumInterval);

            crowdPitchFrom = crowdPitch;
            // Increase speed by 5% per hovering gull, with a 125% maximum.
            crowdPitchTarget = Mathf.Min(
                Mathf.Clamp(crowdBgmSpeedCeiling, 1f, 1.25f),
                1f + hoveringCount * 0.05f
            );
            crowdBeepIntervalFrom = crowdBeepInterval;
            // Increase beeps per second evenly for each additional hovering gull.
            float beepRate = Mathf.Lerp(1f / slowInterval, Mathf.Max(0.1f, crowdBeepFastRate), beepFraction);
            crowdBeepIntervalTarget = Mathf.Max(minimumInterval, 1f / beepRate);
            crowdTransitionElapsed = 0f;

            // Play the first warning immediately; preserve beat progress on later count changes.
            if (hoveringCount > 0 && previousHoveringCount <= 0)
                crowdBeepPhase = 1f;

            previousHoveringCount = hoveringCount;
        }

        crowdTransitionElapsed += Time.deltaTime;
        float progress = crowdAudioTransitionSeconds <= 0f ? 1f
            : Mathf.Clamp01(crowdTransitionElapsed / crowdAudioTransitionSeconds);
        float blend = Mathf.SmoothStep(0f, 1f, progress);
        crowdPitch = Mathf.Lerp(crowdPitchFrom, crowdPitchTarget, blend);
        crowdBeepInterval = Mathf.Lerp(crowdBeepIntervalFrom, crowdBeepIntervalTarget, blend);
        crowdWarningPitch = Mathf.Lerp(crowdWarningPitchFrom, crowdWarningPitchTarget, blend);
        AudioManager.Instance?.SetBgmPitch(crowdPitch);

        if (hoveringCount <= 0 || crowdWarningBeep == null)
        {
            StopCrowdWarning();
            return;
        }

        if (crowdWarningSource == null)
        {
            crowdWarningSource = AudioManager.Instance?.CreateGameplayOneShotSource();
            if (crowdWarningSource == null)
                return;
        }

        crowdWarningSource.pitch = crowdWarningPitch;
        crowdBeepPhase += Time.deltaTime / Mathf.Max(0.1f, crowdBeepInterval);
        if (crowdBeepPhase >= 1f)
        {
            if (crowdWarningSource.isPlaying)
            {
                crowdBeepPhase = 1f;
                return;
            }
            // At most one new warning per frame, even after a frame stall.
            crowdWarningSource.PlayOneShot(crowdWarningBeep, crowdWarningVolumeScale);
            crowdBeepPhase %= 1f;
        }
    }

    private void StopCrowdWarning()
    {
        crowdBeepPhase = 0f;
        if (crowdWarningSource != null)
        {
            crowdWarningSource.Stop();
            Destroy(crowdWarningSource.gameObject);
            crowdWarningSource = null;
        }
    }

    private void ResetCrowdAudio()
    {
        StopCrowdWarning();
        previousHoveringCount = -1;
        crowdPitch = crowdPitchFrom = crowdPitchTarget = 1f;
        crowdBeepInterval = crowdBeepIntervalFrom = crowdBeepIntervalTarget =
            Mathf.Max(0.1f, crowdBeepSlowInterval);
        crowdWarningPitch = crowdWarningPitchFrom = crowdWarningPitchTarget = 1f;
        crowdTransitionElapsed = 0f;
        AudioManager.Instance?.SetBgmPitch(1f);
    }

    private void OnDestroy()
    {
        if (breadSpawner)
        {
            breadSpawner.OnThrowableThrown -= HandleBreadThrown;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    #endregion 

    #region UI 

    // (hook) throw with the selected hand, basket attaches to other hand 
    public void SelectHandedness(bool right)
    {
        BreadSpawnerBasket basket = Basket;

        if (basket)
        {
            basket.SetHandedness(right);
        }

        if (SettingsManager.Instance)
        {
            if (right)
            {
                SettingsManager.Instance.SelectRightHand();
            }
            else
            {
                SettingsManager.Instance.SelectLeftHand();
            }
        }

        if (state == GameState.Landing)
        {
            StartRound();
        }
    }

    // (hook) basket, tutorial sign, and tutorial seagull appear 
    public void StartRound()
    {
        if (state != GameState.Landing)
        {
            return;
        }

        state = GameState.Tutorial;

        ShowBasket();

        if (UIManager.Instance)
        {
            UIManager.Instance.ShowTutorial();
        }

        // Keep the intro music until the tutorial hit starts the countdown.

        Log("tutorial started, bread is unlimited until the freebie seagull is hit");

        SpawnTutorialSeagull();
    }

    // (hook) replay the countdown without repeating the tutorial; keep the selected hand
    public void PlayAgain()
    {
        if (state != GameState.GameOver)
        {
            return;
        }

        ResetRound();

        if (UIManager.Instance)
        {
            UIManager.Instance.HideSigns();
        }

        state = GameState.Countdown;
        countdownInProgress = true;
        countdownRoutine = StartCoroutine(CountdownThenPlay(true));
    }

    // (hook) 
    public void ExitToLanding()
    {
        EnterLanding();
    }
    
    public void OnHitVisual(HitResult result)
    {
        if (UIManager.Instance)
        {
            UIManager.Instance.ShowScorePopup(result);
        }

        if (onHitVFX)
        {
            ParticleSystem vfx = Instantiate(onHitVFX, result.position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 4f);
        }

        if (result.isLongShot)
        {
            Log($"Long shot. Bonus +{longShotBonus} ({result.points} total for this hit)");
        }
    }

    private void OnLoseVisual()
    {
        // TODO: xiao/shiyu
    }

    #endregion

    #region Seagull hits 
    
    private float HitDistance(HittableSeagull seagull)
    {
        float distance = seagull.DistanceToMoveTo;

        if (distance <= 0f)
        {
            distance = Vector3.Distance(seagull.transform.position, PlayerAnchor.position);
        }

        return distance;
    }

    public HitResult ReportSeagullHit(HittableSeagull seagull, Vector3 hitPosition)
    {
        HitResult result = default;

        if (!seagull)
        {
            return result;
        }

        bool wasHovering = seagull.IsHovering;

        if (state != GameState.Playing)
        {
            return result;
        }

        float distance = HitDistance(seagull);
        float normalized = Mathf.InverseLerp(minScoreDistance, maxScoreDistance, distance);
        float shaped = scoreCurve != null && scoreCurve.length > 0
            ? scoreCurve.Evaluate(normalized)
            : normalized;

        float points = maxPointsAtRange * shaped;

        if (wasHovering)
        {
            points *= hoveringPointsScalar;
        }

        result.points = Mathf.Max(1, Mathf.RoundToInt(points));

        result.isLongShot = !wasHovering && distance >= longShotDistance;

        if (result.isLongShot)
        {
            result.points += longShotBonus;
        }

        result.pointsFraction = Mathf.InverseLerp(1f, maxPointsAtRange + longShotBonus, result.points);

        result.position = hitPosition;

        Score += result.points;

        OnHitVisual(result);

        Log($"hit at {distance:F1}m{(wasHovering ? " (hovering)" : "")} for {result.points} points, total {Score}");

        return result;
    }

    // start core gameplay. no points earned 
    public void ReportTutorialHit()
    {
        if (state != GameState.Tutorial || countdownInProgress)
        {
            return;
        }

        if (UIManager.Instance)
        {
            UIManager.Instance.HideSigns();
        }

        state = GameState.Countdown;
        countdownInProgress = true;
        countdownRoutine = StartCoroutine(CountdownThenPlay());
    }

    private IEnumerator CountdownThenPlay(bool showBasketAtCue = false)
    {
        AudioManager.Instance?.StopBgmImmediately();
        currentBgm = SoundId.None;
        AudioManager.Instance?.StopLoop(AudioManager.LoopTrack.Loading);
        countdownSource = AudioManager.Instance?.PlayTrackedOneShot2D(SoundId.Countdown);

        Log("countdown started, gameplay and combat music start at 4.55 seconds");
        // Start at the 4.55-second cue, rather than waiting for the clip to end.
        yield return new WaitForSecondsRealtime(4.55f);

        if (state != GameState.Countdown)
        {
            ReleaseCountdownSource();
            countdownRoutine = null;
            countdownInProgress = false;
            yield break;
        }

        AudioManager.Instance?.PlayBgmImmediately(SoundId.CombatBGM);
        currentBgm = SoundId.CombatBGM;
        if (showBasketAtCue)
        {
            ShowBasket();
        }
        EnterPlaying();
        countdownInProgress = false;

        // Let the countdown's remaining audio finish over gameplay and music.
        // Keep the routine handle so a reset can still cancel and clean it up.
        while (countdownSource != null && countdownSource.isPlaying)
        {
            yield return null;
        }

        ReleaseCountdownSource();
        countdownRoutine = null;
    }

    private void ReleaseCountdownSource()
    {
        if (countdownSource != null)
        {
            countdownSource.Stop();
            Destroy(countdownSource.gameObject);
            countdownSource = null;
        }
    }

    private void CancelCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
        ReleaseCountdownSource();
        countdownInProgress = false;
    }

    private void StopGameEndSound()
    {
        if (gameEndSource != null)
        {
            gameEndSource.Stop();
            Destroy(gameEndSource.gameObject);
            gameEndSource = null;
        }
    }

    private void OnDisable()
    {
        StopCrowdWarning();
        CancelCountdown();
        StopGameEndSound();
    }

    #endregion

    #region State transitions

    private void EnterLanding()
    {
        state = GameState.Landing;
        ResetRound();

        if (UIManager.Instance)
        {
            UIManager.Instance.ShowLanding();
        }

        PlayBgm(SoundId.IntroBGM);

        Log("on landing page");
    }

    private void EnterPlayingDirectly()
    {
        ResetRound();

        if (UIManager.Instance)
        {
            UIManager.Instance.HideSignsImmediate();
        }

        ShowBasket();
        PlayBgm(SoundId.CombatBGM);
        EnterPlaying();
    }

    private void EnterPlaying()
    {
        ResetCrowdAudio();
        state = GameState.Playing;

        breadRemaining = UnlimitedBread ? int.MaxValue : breadCount;
        breadInFlight = 0;
        outOfBread = false;

        if (seagullSpawner)
        {
            seagullSpawner.Activate(true);
        }

        Log($"core loop started with {(UnlimitedBread ? "unlimited" : breadRemaining.ToString())} bread");
    }

    private void WinGame()
    {
        if (state != GameState.Playing)
        {
            return;
        }

        state = GameState.GameOver;
        StopGameplay();
        AllSeagullsLeave();

        Log($"WIN: out of bread with {Score} points");
        EnterGameOverSign();
    }

    private void LoseGame()
    {
        if (state != GameState.Playing)
        {
            return;
        }

        state = GameState.GameOver;
        StopGameplay();

        OnLoseVisual();

        Log($"LOSE: {HoveringCount} seagulls hovering, {Score} points");

        AllSeagullsLeave();
        EnterGameOverSign();
    }

    private void AllSeagullsLeave()
    {
        if (!seagullSpawner)
        {
            return;
        }

        IReadOnlyList<HittableTarget> targets = seagullSpawner.CurrentTargets;

        for (int i = 0; i < targets.Count; i++)
        {
            if (!targets[i])
            {
                continue;
            }

            if (targets[i] is HittableSeagull seagull)
            {
                seagull.BeginLeave();
            }
        }
    }

    private void EnterGameOverSign()
    {
        CancelCountdown();
        StopGameEndSound();
        AudioManager.Instance?.FadeOutBgm(0.5f);
        currentBgm = SoundId.None;
        gameEndSource = AudioManager.Instance?.PlayTrackedOneShot2D(SoundId.GameEnd);

        if (UIManager.Instance)
        {
            UIManager.Instance.ShowEndScreen();
        }
    }
    
    private void StopGameplay()
    {
        StopCrowdWarning();
        if (seagullSpawner)
        {
            seagullSpawner.Activate(false);
        }

        if (breadSpawner)
        {
            breadSpawner.Activate(false);
        }
    }

    private void ResetRound()
    {
        ResetCrowdAudio();
        StopGameEndSound();
        CancelCountdown();
        Score = 0;
        breadRemaining = 0;
        breadInFlight = 0;
        outOfBread = false;

        if (seagullSpawner)
        {
            seagullSpawner.Activate(false);
            seagullSpawner.ClearTargets();
        }

        if (tutorialSeagull)
        {
            Destroy(tutorialSeagull.gameObject);
            tutorialSeagull = null;
        }

        if (breadSpawner)
        {
            breadSpawner.Activate(false);
            breadSpawner.DespawnCurrent();

            BreadSpawnerBasket basket = Basket;

            if (basket)
            {
                basket.DetachBasket();
            }

            breadSpawner.gameObject.SetActive(false);
        }
    }

    // attach and start spawning breads 
    private void ShowBasket()
    {
        if (!breadSpawner)
        {
            return;
        }

        breadSpawner.gameObject.SetActive(true);

        BreadSpawnerBasket basket = Basket;

        if (basket)
        {
            if (SettingsManager.Instance)
            {
                basket.SetHandedness(
                    SettingsManager.Instance.CurrentHand == SettingsManager.PreferredHand.Right);
            }

            basket.AttachBasket();
        }
        else
        {
            breadSpawner.Activate(true);
        }
    }

    private void SpawnTutorialSeagull()
    {
        if (!tutorialSeagullPrefab || !tutorialSpawnPoint)
        {
            Debug.LogError("[GameManager] no tutorial seagull prefab or spawn point assigned, skipping tutorial", this);

            if (UIManager.Instance)
            {
                UIManager.Instance.HideSigns();
            }

            PlayBgm(SoundId.CombatBGM);
            EnterPlaying();
            return;
        }

        tutorialSeagull = Instantiate(
            tutorialSeagullPrefab,
            tutorialSpawnPoint.position,
            tutorialSpawnPoint.rotation
        );
    }

    #endregion
    
    private void HandleBreadThrown(ThrowInteractable bread)
    {
        if (state != GameState.Playing || !bread)
        {
            return;
        }

        if (!UnlimitedBread)
        {
            breadRemaining = Mathf.Max(0, breadRemaining - 1);
        }

        breadInFlight++;

        Action onFlightStopped = null;
        onFlightStopped = () =>
        {
            bread.OnFlightStopped -= onFlightStopped;
            breadInFlight = Mathf.Max(0, breadInFlight - 1);
        };

        bread.OnFlightStopped += onFlightStopped;

        if (breadRemaining > 0)
        {
            return;
        }

        // wait for final throw to end, not just be released 
        outOfBread = true;
        outOfBreadTime = Time.time;

        if (breadSpawner)
        {
            breadSpawner.Activate(false);
        }

        Log("last bread thrown, waiting for it to land");
    }

    // PlayLoop restarts the track every call, so only switch when it changes 
    private void PlayBgm(SoundId id)
    {
        if (currentBgm == id)
        {
            return;
        }

        currentBgm = id;

        AudioManager.Instance?.PlayLoop(
            id,
            AudioManager.LoopTrack.BGM
        );
    }
    
    #region Util
    
    private void Log(string message)
    {
        if (verbose)
        {
            Debug.Log($"[GameManager] {message}", this);
        }
    }

    private void OnValidate()
    {
        maxScoreDistance = Mathf.Max(minScoreDistance, maxScoreDistance);
    }
    
    #endregion
}
