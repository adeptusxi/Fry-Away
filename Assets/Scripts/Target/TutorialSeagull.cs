using UnityEngine;

// freebie seagull with no movement or behavior. hit it to start the game 
public class TutorialSeagull : HittableTarget
{
    [SerializeField] private Transform beak;

    [Header("Animation / Motion")]
    [SerializeField, Min(0f), Tooltip("in seconds")] private float growDuration = 0.8f;
    [SerializeField, Tooltip("normalized 0-1")] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Range(0f, 90f)] private float flyAwayAngle = 65f; // 90 is straight up 
    [SerializeField, Min(0f), Tooltip("in m/s")] private float flyAwaySpeed = 9f;
    [SerializeField, Min(0f), Tooltip("in seconds")] private float flyAwayDuration = 1.2f;
    [SerializeField, Tooltip("normalized 0-1")] private AnimationCurve flyAwayScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // for smoothly disappearing 

    private Vector3 originalScale;
    private float growElapsed;

    private bool flyingAway;
    private float flyAwayElapsed;
    private Vector3 flyAwayHeading;

    private void Awake()
    {
        originalScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    public override void OnObjectHit(ThrowInteractable hitBy, RaycastHit hit)
    {
        if (flyingAway)
            return;

        if (HitSoundId != SoundId.None)
        {
            AudioManager.Instance?.PlayOneShotAtPosition(
                HitSoundId,
                hit.point
            );
        }

        TryCatch(hitBy);

        GameManager.Instance?.ReportTutorialHit();

        if (TryGetComponent(out Collider hitbox))
            hitbox.enabled = false;

        BeginFlyAway();
    }

    protected override void Move()
    {
        if (flyingAway)
        {
            MoveFlyAway();
            return;
        }

        if (growElapsed >= growDuration)
            return;

        growElapsed += Time.deltaTime;
        float progress = growDuration > 0f ? Mathf.Clamp01(growElapsed / growDuration) : 1f;

        transform.localScale = originalScale * growCurve.Evaluate(progress);

        if (progress >= 1f)
        {
            transform.localScale = originalScale;
        }
    }
    
    private void BeginFlyAway()
    {
        flyingAway = true;
        flyAwayElapsed = 0f;

        Vector3 horizontal = transform.forward;
        horizontal.y = 0f;

        if (horizontal.sqrMagnitude < Mathf.Epsilon)
        {
            horizontal = Vector3.forward;
        }

        horizontal.Normalize();

        float climb = flyAwayAngle * Mathf.Deg2Rad;
        flyAwayHeading = (horizontal * Mathf.Cos(climb) + Vector3.up * Mathf.Sin(climb)).normalized;
    }

    private void MoveFlyAway()
    {
        flyAwayElapsed += Time.deltaTime;
        float progress = flyAwayDuration > 0f ? Mathf.Clamp01(flyAwayElapsed / flyAwayDuration) : 1f;

        transform.position += flyAwayHeading * (flyAwaySpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(flyAwayHeading, Vector3.up);

        float scale = flyAwayScaleCurve != null && flyAwayScaleCurve.length > 0
            ? flyAwayScaleCurve.Evaluate(progress)
            : 1f - progress;

        transform.localScale = originalScale * Mathf.Max(0f, scale);

        if (progress >= 1f)
            Destroy(gameObject);
    }

    private void TryCatch(ThrowInteractable hitBy)
    {
        if (hitBy == null || beak == null)
        {
            return;
        }

        Catchable catchable = hitBy.GetComponentInParent<Catchable>();

        if (catchable != null)
        {
            catchable.TryGiveTo(beak);
        }
    }
}
