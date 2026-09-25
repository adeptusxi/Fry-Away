using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Landing,   // handedness selection, no gameplay objects
        Tutorial,  // freebie seagull + diagetic guide sign, unlimited bread
        Playing,   // core loop, bread is limited
        GameOver   // end sign is up
    }

    public struct HitResult
    {
        public int points;
        public bool isLongShot;
    }

    [Header("Spawners")]
    [Tooltip("normally the BreadSpawnerBasket. a plain spawner also works for test scenes, it just can't attach to a hand")]
    [SerializeField] private ThrowInteractableSpawner breadSpawner;

    [SerializeField] private TargetSpawner seagullSpawner;

    [Header("Diagetic Signs")]
    [SerializeField] private SignSlideAnimation tutorialSign;
    [SerializeField] private SignSlideAnimation endSign;

    [Header("Tutorial Seagull")]
    [SerializeField] private TutorialSeagull tutorialSeagullPrefab;

    [Tooltip("where the freebie seagull grows in. its rotation is used too, so it can face the player")]
    [SerializeField] private Transform tutorialSpawnPoint;

    [Header("Player")]
    [Tooltip("measured against for score distance. defaults to the seagull spawner's cone origin")]
    [SerializeField] private Transform playerTransform;

    [Header("Bread")]
    [Tooltip("how many breads the player gets once the tutorial is over. running out is the win condition")]
    [SerializeField, Min(1)] private int breadCount = 20;

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
    [SerializeField, Min(0f)] private float longShotDistance = 15f;

    [SerializeField, Min(0)] private int longShotBonus = 50;

    [Header("Lose Condition")]
    [Tooltip("this many seagulls hovering overhead at once and the run is lost")]
    [SerializeField, Min(1)] private int maxHoveringSeagulls = 5;

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

    public static GameManager Instance { get; private set; }

    public bool ShowDebugDisplays => showDebugDisplays;
    public GameState State => state;
    public int Score { get; private set; }
    public int BreadRemaining => breadRemaining;

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
        if (state != GameState.Playing)
        {
            return;
        }

        if (HoveringCount >= maxHoveringSeagulls)
        {
            LoseGame();
            return;
        }

        if (!outOfBread)
        {
            return;
        }

        if (breadInFlight <= 0 || Time.time - outOfBreadTime >= lastThrowResolveTimeout)
        {
            WinGame();
        }
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

        if (tutorialSign)
        {
            tutorialSign.Enter();
        }

        PlayBgm(SoundId.CombatBGM);

        Log("tutorial started, bread is unlimited until the freebie seagull is hit");

        SpawnTutorialSeagull();
    }

    // (hook) round begins immediately (no tutorial; same handedness as previously selected)
    public void PlayAgain()
    {
        if (state != GameState.GameOver)
        {
            return;
        }

        EnterPlayingDirectly();
    }

    // (hook) 
    public void ExitToLanding()
    {
        EnterLanding();
    }
    
    // TODO: xiao/shiyu - UI popup (e.g. "Long shot! Bonus +N" or something)
    public void ShowScorePopup(HitResult result, Vector3 worldPosition)
    {
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

    public HitResult ReportSeagullHit(HittableSeagull seagull, RaycastHit hit)
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

        result.points = Mathf.Max(0, Mathf.RoundToInt(points));

        result.isLongShot = !wasHovering && distance >= longShotDistance;

        if (result.isLongShot)
        {
            result.points += longShotBonus;
        }

        Score += result.points;

        ShowScorePopup(result, hit.point);

        Log($"hit at {distance:F1}m{(wasHovering ? " (hovering)" : "")} for {result.points} points, total {Score}");

        return result;
    }

    // start core gameplay. no points earned 
    public void ReportTutorialHit()
    {
        if (state != GameState.Tutorial)
        {
            return;
        }

        if (tutorialSign)
        {
            tutorialSign.Exit();
        }

        AudioManager.Instance?.PlayOneShot2D(SoundId.Countdown);

        Log("freebie seagull hit, core game loop starting");
        EnterPlaying();
    }

    #endregion

    #region State transitions

    private void EnterLanding()
    {
        state = GameState.Landing;
        ResetRound();

        PlayBgm(SoundId.IntroBGM);

        Log("on landing page");
    }

    private void EnterPlayingDirectly()
    {
        ResetRound();
        ShowBasket();
        PlayBgm(SoundId.CombatBGM);
        EnterPlaying();
    }

    private void EnterPlaying()
    {
        state = GameState.Playing;

        breadRemaining = breadCount;
        breadInFlight = 0;
        outOfBread = false;

        if (seagullSpawner)
        {
            seagullSpawner.Activate(true);
        }

        Log($"core loop started with {breadRemaining} bread");
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
        if (endSign)
        {
            endSign.Enter();
        }
        
        if (UIManager.Instance)
        {
            UIManager.Instance.ShowEndScreen();
        }
    }
    
    private void StopGameplay()
    {
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

        if (tutorialSign)
        {
            tutorialSign.Exit(true);
        }

        if (endSign)
        {
            endSign.Exit(true);
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

        breadRemaining = Mathf.Max(0, breadRemaining - 1);
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
