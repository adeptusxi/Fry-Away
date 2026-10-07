using TMPro;
using UnityEngine;

// displays initialMessage, (stepCount-1), (stepCount-2) ..., 1, finalMessage 
public class CountdownUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    
    [SerializeField, Min(1), Tooltip("# beeps in the countdown audio")] private int stepCount = 4;
    [SerializeField] private string initialMessage = "Ready?";
    [SerializeField] private string finalMessage = "GO!";
    
    [Header("Timing")]
    [SerializeField, Min(0f), Tooltip("in seconds from the audio starting to the first number")] private float firstStepDelay = 0f;
    [SerializeField, Min(0.01f)] private float secondsBetweenSteps = 1.1f;
    
    [Header("Animation")]
    [SerializeField, Tooltip("normalized 0-1 time. multiplier on original scale")] 
        private AnimationCurve scaleCurve = new(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    [SerializeField, Min(0f), Tooltip("multiplier on original scale")] private float initialMessageScale = 1f;
    [SerializeField, Min(0f), Tooltip("multiplier on original scale")] private float finalMessageScale = 1f;
    [SerializeField] private Gradient colorOverStep;
    [SerializeField, Tooltip("normalized 0-1")] private AnimationCurve colorCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    
    private const int NoStep = -2;
    private const int InitialStep = -1;

    private Vector3 labelBaseScale = Vector3.one;
    private bool capturedBaseScale;
    private bool playing;
    private float startTime;
    private int currentStep = NoStep;
    
    public void Play()
    {
        playing = true;
        startTime = Time.realtimeSinceStartup;
        currentStep = NoStep;
        SetLabel(string.Empty);
    }

    private void OnEnable()
    {
        if (!playing) SetLabel(string.Empty);
    }

    private void OnDisable()
    {
        playing = false;
        SetLabel(string.Empty);
    }

    private void Update()
    {
        if (!playing || !label)
            return;

        UpdateStep(Time.realtimeSinceStartup - startTime);
    }
    
    private void UpdateStep(float elapsed)
    {
        int lastStep = stepCount - 1;
        int step = InitialStep;
        float stepStart = 0f;
        float stepDuration = firstStepDelay;

        if (elapsed >= firstStepDelay)
        {
            step = Mathf.Min(Mathf.FloorToInt((elapsed - firstStepDelay) / secondsBetweenSteps), lastStep);
            stepStart = firstStepDelay + step * secondsBetweenSteps;
            stepDuration = secondsBetweenSteps;
        }

        if (step != currentStep)
        {
            string text = step == InitialStep ? initialMessage
                : step < lastStep ? (lastStep - step).ToString()
                : finalMessage;

            currentStep = step;
            SetLabel(text);
        }

        float timeInStep = elapsed - stepStart;
        float progress = stepDuration > 0f ? Mathf.Clamp01(timeInStep / stepDuration) : 1f;
        
        // final message stays big
        float messageScale = step == InitialStep ? initialMessageScale
            : step < lastStep ? 1f
            : finalMessageScale;

        label.transform.localScale = labelBaseScale * (messageScale * scaleCurve.Evaluate(step < lastStep ? progress : 0f));
        label.color = colorOverStep.Evaluate(colorCurve.Evaluate(progress));
    }

    private void SetLabel(string text)
    {
        if (!label)
            return;

        // in case Play is called before Awake while the sign is still inactive
        if (!capturedBaseScale)
        {
            labelBaseScale = label.transform.localScale;
            capturedBaseScale = true;
        }

        label.text = text;
        label.transform.localScale = labelBaseScale;
    }
}
