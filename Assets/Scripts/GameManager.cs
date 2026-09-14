using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Spawners")]
    [SerializeField] private ThrowInteractableSpawner frisbeeSpawner;
    [SerializeField] private TargetSpawner seagullSpawner;

    [Header("Debug")]
    [Tooltip("Turn every DebugDisplay in the scene on or off")]
    [SerializeField] private bool showDebugDisplays = true;

    public static GameManager Instance { get; private set; }

    public bool ShowDebugDisplays => showDebugDisplays;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;

        frisbeeSpawner.Activate(true);
        seagullSpawner.Activate(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // TODO: intro UI, score tracking (+ leaderboard?), game over, etc.
}
