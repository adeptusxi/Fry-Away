using TMPro;
using UnityEngine;
using UnityEngine.UI;

// visual indicator for game-almost-over. shows how many seagulls are hovering 
public class HoveringWarningUI : MonoBehaviour
{
    [SerializeField, Tooltip("first is the first icon that appears")] private Graphic[] icons; 

    [Header("Animation")]
    [SerializeField, Min(0f), Tooltip("in seconds, icon enter/exit")] private float scaleDuration = 0.25f;
    [SerializeField, Tooltip("normalized 0-1. multiplier on original scale")] 
        private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Danger")]
    [SerializeField, Min(1), Tooltip("# hovering seagulls. should be >= UIManager's hoveringSeagullThreshold")]
        private int dangerThreshold = 3;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color noDangerColor = Color.black;
    [SerializeField] private Color maxDangerColor = Color.red;
    [SerializeField, Min(0f), Tooltip("in degrees")] private float maxTwitchAngle = 15f;
    [SerializeField, Tooltip("in seconds")] private Vector2 twitchInterval = new(0.04f, 0.12f);

    private Vector3[] baseScales;
    private Quaternion[] baseRotations;
    private float[] progress; // 0 = gone, 1 = fully in
    private float[] twitchTimers; 
    private int count;

    public void SetCount(int count)
    {
        this.count = count;
    }

    private void Awake()
    {
        if (icons == null)
        {
            icons = new Graphic[0];
        }

        baseScales = new Vector3[icons.Length];
        baseRotations = new Quaternion[icons.Length];
        progress = new float[icons.Length];
        twitchTimers = new float[icons.Length];

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i])
            {
                icons[i].gameObject.SetActive(true);
                baseScales[i] = icons[i].transform.localScale;
                baseRotations[i] = icons[i].transform.localRotation;
            }
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            progress[i] = 0f;
            UpdateIcon(i);
            ResetIconTwitch(i);
        }

        if (label)
        {
            label.color = noDangerColor;
        }
    }

    private void Update()
    {
        int usable = GameManager.Instance ? GameManager.Instance.MaxHoveringSeagulls : icons.Length;
        float step = scaleDuration > 0f ? Time.deltaTime / scaleDuration : 1f;

        float danger = count >= dangerThreshold
            ? Mathf.InverseLerp(dangerThreshold - 1, Mathf.Max(dangerThreshold, usable - 1), count)
            : 0f;

        if (label)
        {
            label.color = Color.Lerp(noDangerColor, maxDangerColor, danger);
        }

        for (int i = 0; i < icons.Length; i++)
        {
            float target = i < count && i < usable ? 1f : 0f;

            if (!Mathf.Approximately(progress[i], target))
            {
                progress[i] = Mathf.MoveTowards(progress[i], target, step);
                UpdateIcon(i);
            }

            if (danger > 0f && progress[i] > 0f)
            {
                TwitchIcon(i, danger);
            }
            else
            {
                ResetIconTwitch(i);
            }
        }
    }

    private void TwitchIcon(int idx, float intensity)
    {
        if (!icons[idx])
            return;

        twitchTimers[idx] -= Time.deltaTime;

        if (twitchTimers[idx] > 0f)
            return;

        twitchTimers[idx] = Random.Range(twitchInterval.x, twitchInterval.y);
        float angle = Random.Range(-1f, 1f) * maxTwitchAngle * intensity;
        icons[idx].transform.localRotation = baseRotations[idx] * Quaternion.Euler(0f, 0f, angle);
    }

    private void ResetIconTwitch(int idx)
    {
        twitchTimers[idx] = 0f;

        if (icons[idx])
            icons[idx].transform.localRotation = baseRotations[idx];
    }

    private void UpdateIcon(int idx)
    {
        if (!icons[idx])
            return;

        icons[idx].enabled = progress[idx] > 0f;
        icons[idx].transform.localScale = baseScales[idx] * scaleCurve.Evaluate(progress[idx]);
    }
}
