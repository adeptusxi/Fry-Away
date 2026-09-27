using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// visual and audio hover/click feedback for button
[RequireComponent(typeof(Button))]
public class UIButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale")]
    [SerializeField] private bool scaleEnabled = true;
    [SerializeField] private Transform scaleTarget;
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField, Min(0.01f)] private float transitionSpeed = 15f;

    [Header("Sound")]
    [SerializeField] private bool soundEnabled = true;
    [SerializeField] private SoundId hoverSound = SoundId.UIButtonHover;
    [SerializeField] private SoundId clickSound = SoundId.UIButtonClick;

    [Tooltip("Enable for the Start button: loops until StopSustained() is called.")]
    [SerializeField] private bool sustainClickUntilStopped;

    private Button button;

    private Vector3 baseScale;
    private bool hovered;
    private bool pressed;

    private bool Interactable => button && button.interactable;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        if (!button)
        {
            button = GetComponent<Button>();
        }

        if (!scaleTarget)
        {
            scaleTarget = transform;
        }

        baseScale = scaleTarget.localScale;
    }

    private void OnEnable()
    {
        if (button)
        {
            button.onClick.AddListener(PlayClickSound);
        }
    }

    private void OnDisable()
    {
        if (button)
        {
            button.onClick.RemoveListener(PlayClickSound);
        }

        hovered = false;
        pressed = false;

        if (scaleEnabled && scaleTarget)
        {
            scaleTarget.localScale = TargetScale();
        }
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);

        if (scaleEnabled && scaleTarget)
        {
            scaleTarget.localScale = Vector3.Lerp(scaleTarget.localScale, TargetScale(), t);
        }
    }

    #region Pointer events

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;

        if (soundEnabled && hoverSound != SoundId.None && Interactable)
        {
            AudioManager.Instance?.PlayOneShot2D(hoverSound);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    #endregion

    #region Sound

    private void PlayClickSound()
    {
        if (!soundEnabled || clickSound == SoundId.None)
        {
            return;
        }

        if (sustainClickUntilStopped)
        {
            AudioManager.Instance?.PlayLoop(
                clickSound,
                AudioManager.LoopTrack.Loading
            );
        }
        else
        {
            AudioManager.Instance?.PlayOneShot2D(clickSound);
        }
    }

    public void StopSustained()
    {
        AudioManager.Instance?.StopLoop(
            AudioManager.LoopTrack.Loading
        );
    }

    #endregion

    private Vector3 TargetScale()
    {
        if (!Interactable)
        {
            return baseScale;
        }

        if (pressed)
        {
            return baseScale * pressScale;
        }

        return hovered ? baseScale * hoverScale : baseScale;
    }
}
