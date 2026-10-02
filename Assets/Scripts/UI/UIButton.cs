using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// visual and audio hover/click feedback for a button, toggle or slider 
[RequireComponent(typeof(Selectable))]
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

    private Selectable selectable;

    private Vector3 baseScale;
    private bool hovered;
    private bool pressed;

    private bool Interactable => selectable && selectable.interactable;

    private void Reset()
    {
        selectable = GetComponent<Selectable>();
    }

    private void Awake()
    {
        if (!selectable)
        {
            selectable = GetComponent<Selectable>();
        }

        if (!scaleTarget)
        {
            scaleTarget = transform;
        }

        baseScale = scaleTarget.localScale;
    }

    private void OnEnable()
    {
        // a slider has no click event
        if (selectable is Button button)
        {
            button.onClick.AddListener(PlayClickSound);
        }
        else if (selectable is Toggle toggle)
        {
            toggle.onValueChanged.AddListener(OnToggleChanged);
        }
    }

    private void OnDisable()
    {
        if (selectable is Button button)
        {
            button.onClick.RemoveListener(PlayClickSound);
        }
        else if (selectable is Toggle toggle)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
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

        if (selectable is Slider && Interactable)
        {
            PlayClickSound();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    #endregion

    #region Sound

    private void OnToggleChanged(bool isOn)
    {
        PlayClickSound();
    }

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
