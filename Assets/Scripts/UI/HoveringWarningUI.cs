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
        
    private Vector3[] baseScales;
    private float[] progress; // 0 = gone, 1 = fully in 
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
        progress = new float[icons.Length];

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i])
            {
                icons[i].gameObject.SetActive(true);
                baseScales[i] = icons[i].transform.localScale;
            }
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            progress[i] = 0f;
            Apply(i);
        }
    }

    private void Update()
    {
        int usable = GameManager.Instance ? GameManager.Instance.MaxHoveringSeagulls : icons.Length;
        float step = scaleDuration > 0f ? Time.deltaTime / scaleDuration : 1f;

        for (int i = 0; i < icons.Length; i++)
        {
            float target = i < count && i < usable ? 1f : 0f;

            if (Mathf.Approximately(progress[i], target))
            {
                continue;
            }

            progress[i] = Mathf.MoveTowards(progress[i], target, step);
            Apply(i);
        }
    }

    private void Apply(int i)
    {
        if (!icons[i])
            return;

        icons[i].enabled = progress[i] > 0f;
        icons[i].transform.localScale = baseScales[i] * scaleCurve.Evaluate(progress[i]);
    }
}
