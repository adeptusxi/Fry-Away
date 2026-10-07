using System.Collections;
using UnityEngine;

// slides an object vertically in and out of view, spinning it to a stop facing the player
public class SignSlideAnimation : MonoBehaviour
{
    [Header("Slide Animation")]
    [SerializeField, Min(0f), Tooltip("in seconds")] private float duration = 1.2f;
    [SerializeField, Tooltip("normalized 0-1 curve, 0 for hidden height, 1 for shown height")]
        private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float hiddenHeight = -3f;

    [Header("Spin")]
    [SerializeField, Tooltip("full turns during the slide. negative spins the other way")]
        private float spinTurns = 2f;
    [SerializeField, Tooltip("normalized 0-1. ease in out spins up, then slows to a stop")]
        private AnimationCurve spinCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Flip")]
    [SerializeField, Min(0f)] private float flipDuration = 0.5f;
    [SerializeField, Tooltip("normalized 0-1")] private AnimationCurve flipCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float shownHeight;
    private Quaternion shownRotation;
    private bool capturedShownPose;
    private Coroutine slide;
    private Coroutine flip;
    private float slideYaw;
    private float facingYaw; // 0 for front, 180 for back 

    private float SpinDegrees => spinTurns * 360f;

    public bool IsShown { get; private set; } // true during enter/exit anim and while up 
    public bool IsAnimating => slide != null && isActiveAndEnabled;

    private void Awake()
    {
        CaptureShownPose(); // "up" is whatever height it was at in the scene 
    }

    private void CaptureShownPose()
    {
        if (capturedShownPose)
            return;

        shownHeight = transform.position.y;
        shownRotation = transform.rotation;
        capturedShownPose = true;
    }

    public void Enter(bool immediate = false, bool flipped = false)
    {
        CaptureShownPose();
        IsShown = true;
        SetFacing(flipped);

        if (immediate)
        {
            StopSlide();
            gameObject.SetActive(true);
            SetHeight(shownHeight);
            SetSpin(0f);
        }
        else
        {
            gameObject.SetActive(true);
            SetHeight(hiddenHeight);
            SetSpin(-SpinDegrees);
            StartSlide(hiddenHeight, shownHeight, -SpinDegrees, 0f, false);
        }
    }

    public void Exit(bool immediate = false)
    {
        CaptureShownPose();
        IsShown = false;

        if (immediate)
        {
            StopSlide();
            SetFacing(false);
            SetHeight(shownHeight);
            SetSpin(0f);
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
            StartSlide(shownHeight, hiddenHeight, 0f, -SpinDegrees, true);
        }
    }
    
    public void Flip(bool flipped, bool immediate = false)
    {
        CaptureShownPose();

        float target = flipped ? 180f : 0f;

        if (immediate || !isActiveAndEnabled || flipDuration <= 0f)
        {
            SetFacing(flipped);
            SetSpin(slideYaw);
            return;
        }

        StopFlip();

        if (!Mathf.Approximately(facingYaw, target))
        {
            flip = StartCoroutine(FlipTo(target));
        }
    }

    private void SetFacing(bool flipped)
    {
        StopFlip();
        facingYaw = flipped ? 180f : 0f;
    }

    private void StopFlip()
    {
        if (flip != null)
        {
            StopCoroutine(flip);
            flip = null;
        }
    }

    private IEnumerator FlipTo(float target)
    {
        bool eases = flipCurve != null && flipCurve.length > 0;
        float from = facingYaw;
        float elapsed = 0f;

        while (elapsed < flipDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / flipDuration);

            facingYaw = Mathf.LerpUnclamped(from, target, eases ? flipCurve.Evaluate(progress) : progress);
            SetSpin(slideYaw);

            yield return null;
        }

        facingYaw = target;
        SetSpin(slideYaw);
        flip = null;
    }

    private void StartSlide(float from, float to, float fromYaw, float toYaw, bool deactivateWhenDone)
    {
        StopSlide();

        if (duration <= 0f || curve == null || curve.length == 0)
        {
            Debug.LogWarning($"[SignSlideAnimation] {name} has no usable duration or curve, snapping instead of animating", this);
            if (deactivateWhenDone)
            {
                SetFacing(false);
            }

            SetHeight(to);
            SetSpin(0f);

            if (deactivateWhenDone)
            {
                gameObject.SetActive(false);
            }

            return;
        }

        slide = StartCoroutine(Slide(from, to, fromYaw, toYaw, deactivateWhenDone));
    }

    private void StopSlide()
    {
        if (slide != null)
        {
            StopCoroutine(slide);
            slide = null;
        }
    }

    private IEnumerator Slide(float from, float to, float fromYaw, float toYaw, bool deactivateWhenDone)
    {
        bool spins = spinCurve != null && spinCurve.length > 0;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; 
            float progress = Mathf.Clamp01(elapsed / duration);

            SetHeight(Mathf.LerpUnclamped(from, to, curve.Evaluate(progress)));

            if (spins)
            {
                SetSpin(Mathf.LerpUnclamped(fromYaw, toYaw, spinCurve.Evaluate(progress)));
            }

            yield return null;
        }

        if (deactivateWhenDone)
        {
            SetFacing(false);
        }

        SetHeight(to);
        SetSpin(spins ? toYaw : 0f);
        slide = null;

        if (deactivateWhenDone)
        {
            gameObject.SetActive(false);
        }
    }

    private void SetHeight(float y)
    {
        Vector3 position = transform.position;
        position.y = y;
        transform.position = position;
    }

    private void SetSpin(float yawOffset)
    {
        slideYaw = yawOffset;
        transform.rotation = shownRotation * Quaternion.Euler(0f, slideYaw + facingYaw, 0f);
    }
}
