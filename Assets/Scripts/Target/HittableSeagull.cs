using UnityEngine;

/*
 * seagull behavior:
 * - fly towards player after spawning
 * - if a bread comes near, tries to catch it and fly away. if it fails to catch, continues towards player
 * - if gets too close to player, hovers nearby. still catches breads that come near it
 */
public class HittableSeagull : HittableTarget
{
    private enum State
    {
        Approaching,
        Hovering, // got too close to player
        Swooping, // going towards a bread
        Leaving, // game over, lost interest
        FlyingAway // caught a bread
    }

    [Header("General Flight")]
    [SerializeField, Min(0f), Tooltip("in m/s")] private float minSpeed = 2f;
    [SerializeField, Min(0f), Tooltip("in m/s")] private float maxSpeed = 5f;

    [Header("Weave Towards Player")]
    [SerializeField, Range(0f, 90f), Tooltip("in degrees to either side. 0 flies straight")] private float maxYawOffset = 25f;
    [SerializeField, Range(0f, 90f), Tooltip("in degrees up/down. 0 adds no vertical weave")] private float maxPitchOffset = 15f;
    [SerializeField, Min(0f)] private float weaveFrequency = 0.35f;
    [SerializeField, Range(0f, 1f), Tooltip("0 is a sine wave, 1 is an uneven wander")] private float irregularity = 0.5f;
    [SerializeField, Min(0f), Tooltip("in meters above the floor. pitch weave dampens toward level flight within this height, so it can't dive into the ground")]
    private float pitchGroundDamping = 3f;

    [Header("Swoop Towards Bread")]
    [SerializeField, Min(0f), Tooltip("in m/s")] private float swoopSpeed = 7f;
    [SerializeField, Min(0f)] private float swoopTurnRate = 4f;

    [Header("Leaving (Game Over)")]
    [SerializeField, Min(0f), Tooltip("in seconds, stays in place before turning around")] private float leavePauseDuration = 0.5f;
    [SerializeField, Min(0f), Tooltip("in seconds, length of the U turn")] private float leaveTurnDuration = 1.5f;
    [SerializeField, Min(0f), Tooltip("in m/s")] private float leaveSpeed = 1.5f;
    [SerializeField, Range(0f, 90f)] private float leaveYawOffset = 45f;
    [SerializeField, Range(0f, 1f)] private float leaveIrregularity = 0.85f;
    [SerializeField, Range(0f, 1f), Tooltip("0 is level flight")] private float leaveClimb = 0.2f;
    [SerializeField, Min(0f), Tooltip("in seconds, until it despawns")] private float leaveLifetime = 8f;

    [Header("Fly Away (after caught bread)")]
    [SerializeField, Range(0f, 90f)] private float flyAwayAngle = 65f; // 90 is straight up
    [SerializeField, Min(0f), Tooltip("in seconds, length of the turn onto the climb")] private float flyAwayTurnDuration = 0.35f;
    [SerializeField, Min(0f), Tooltip("in m/s")] private float flyAwaySpeed = 9f;
    [SerializeField, Min(0f), Tooltip("in seconds")] private float flyAwayDuration = 1.2f;
    [SerializeField, Tooltip("normalized 0-1")] private AnimationCurve flyAwayScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // for smoothly disappearing

    [Header("On Hit")]
    [SerializeField] private GameObject longShotVfx;

    [Header("Approach Sound")]
    [SerializeField] private SoundId approachSoundId = SoundId.SeagullApproach;
    [SerializeField, Min(0f), Tooltip("Play one squawk when this bird gets within this many meters of the player. Zero disables it.")]
    private float approachSoundDistance = 8f;
    private bool approachSoundPlayed;
    [SerializeField, Min(0f), Tooltip("Repeat the call inside this distance. Zero disables repetition.")]
    private float closeSoundDistance = 3f;
    private AudioSource approachOneShot;
    private AudioSource closeSoundLoop;
    [SerializeField, Min(0f), Tooltip("Silent pause in seconds after each nearby call finishes.")]
    private float closeSoundPause = 1f;
    private bool closeCallWasPlaying;
    private float nextCloseCallTime;

    private State state = State.Approaching;
    private State failedCatchState = State.Approaching;

    private float speed;
    private float phaseOffset;
    private float noiseSeed;
    private Vector3 fallbackHeading;
    private Vector3 originalScale;

    private SeagullSpawner hoverSpace;
    private Vector3 hoverOffset;

    private float leaveElapsed;
    private Vector3 leaveStartHeading;
    private Vector3 leaveHeading;
    private Vector3 leaveTurnAxis;

    private float flyAwayElapsed;
    private Vector3 flyAwayStartHeading;
    private Vector3 flyAwayHeading;
    private Vector3 flyAwayTurnAxis;
    
    #region Unity & Callbacks
    
    private void Awake()
    {
        speed = Random.Range(minSpeed, maxSpeed);
        phaseOffset = Random.Range(0f, 100f);
        noiseSeed = Random.Range(0f, 100f);
        fallbackHeading = transform.forward;
        originalScale = transform.localScale;
    }
    
    protected override void OnMoveToRegistered()
    {
        transform.rotation = Quaternion.LookRotation(BaseHeading(), Vector3.up);
    }

    protected override void OnTooClose()
    {
        if (state != State.Approaching)
            return;

        if (!hoverSpace)
        {
            Destroy(gameObject);
            return;
        }

        hoverOffset = hoverSpace.PickHoverOffset(this);
        state = State.Hovering;
    }
    
    public override void OnObjectHit(ThrowInteractable hitBy, RaycastHit hit)
    {
        if (state == State.FlyingAway)
            return;

        if (HitSoundId != SoundId.None)
        {
            AudioManager.Instance?.PlayOneShotAtPosition(
                HitSoundId,
                hit.point
            );
        }

        TryCatch(hitBy);

        GameManager.HitResult result = GameManager.Instance
            ? GameManager.Instance.ReportSeagullHit(this, hit)
            : default;

        if (result.isLongShot && longShotVfx)
        {
            Instantiate(longShotVfx, transform.position, Quaternion.identity);
        }

        if (TryGetComponent(out Collider hitbox))
            hitbox.enabled = false;

        BeginFlyAway();
    }
    
    #endregion
    
    protected override void Move()
    {
        UpdateCloseSoundLoop();
        TryPlayApproachSound();
        if ((state == State.Approaching || state == State.Hovering) && TryNoticeBread())
        {
            failedCatchState = state;
            state = State.Swooping;
        }

        switch (state)
        {
            case State.FlyingAway:
                MoveFlyAway();
                break;
            case State.Swooping:
                MoveSwoop();
                break;
            case State.Hovering:
                MoveHover();
                break;
            case State.Leaving:
                MoveLeave();
                break;
            default:
                MoveApproach();
                break;
        }
    }
    
    private void TryPlayApproachSound()
    {
        if (approachSoundPlayed || approachSoundId == SoundId.None ||
            approachSoundDistance <= 0f || !MoveTo ||
            (state != State.Approaching && state != State.Swooping))
        {
            return;
        }

        if ((transform.position - MoveTo.position).sqrMagnitude >
            approachSoundDistance * approachSoundDistance)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            // Attempt once per bird, including when clips are not assigned yet.
            approachSoundPlayed = true;
            approachOneShot = AudioManager.Instance.PlayAttachedOneShot(
                approachSoundId, transform);
        }
    }

    private void UpdateCloseSoundLoop()
    {
        // A half-meter margin prevents rapid restarting at the boundary.
        float range = closeSoundDistance + (closeSoundLoop != null ? 0.5f : 0f);
        bool shouldLoop = closeSoundDistance > 0f && MoveTo &&
            approachSoundId != SoundId.None &&
            state != State.Leaving && state != State.FlyingAway &&
            (transform.position - MoveTo.position).sqrMagnitude <= range * range;

        if (!shouldLoop)
        {
            StopCloseSoundLoop();
            return;
        }

        if (closeSoundLoop == null && AudioManager.Instance != null)
        {
            // Let the initial approach call finish before repeating.
            if (approachOneShot != null && approachOneShot.isPlaying)
                return;

            closeSoundLoop = AudioManager.Instance.StartAttachedLoop(approachSoundId, transform);
            if (closeSoundLoop != null)
            {
                approachSoundPlayed = true;
                closeSoundLoop.loop = false;
                closeCallWasPlaying = true;
            }
        }

        if (closeSoundLoop == null || closeSoundLoop.isPlaying)
            return;

        // Measure silence from the end of the call, not from its start.
        if (closeCallWasPlaying)
        {
            closeCallWasPlaying = false;
            nextCloseCallTime = Time.time + closeSoundPause;
        }

        if (Time.time >= nextCloseCallTime)
        {
            closeSoundLoop.Play();
            closeCallWasPlaying = true;
        }
    }

    private void StopCloseSoundLoop()
    {
        closeCallWasPlaying = false;
        nextCloseCallTime = 0f;
        if (closeSoundLoop != null)
        {
            closeSoundLoop.Stop();
            Destroy(closeSoundLoop);
            closeSoundLoop = null;
        }
    }

    private void OnDisable()
    {
        StopCloseSoundLoop();
    }

    private Vector3 BaseHeading()
    {
        if (!MoveTo)
        {
            return fallbackHeading;
        }

        Vector3 toTarget = MoveTo.position - transform.position;
        return toTarget.sqrMagnitude > Mathf.Epsilon ? toTarget.normalized : fallbackHeading;
    }
    
    private static Vector3 TurnToward(Vector3 from, Vector3 to, Vector3 axis, float progress)
    {
        return Quaternion.AngleAxis(Vector3.Angle(from, to) * progress, axis) * from;
    }
    
    private static Vector3 TurnAxis(Vector3 from, Vector3 to)
    {
        Vector3 axis = Vector3.Cross(from, to);

        if (axis.sqrMagnitude < 0.0001f)
        {
            axis = Vector3.up * (Random.value < 0.5f ? -1f : 1f);
        }

        return axis.normalized;
    }
    
    #region Regular Motion 

    private void MoveApproach()
    {
        Vector3 heading = Weave(BaseHeading(), maxYawOffset, irregularity);
        heading = PitchWeave(heading, maxPitchOffset, irregularity);

        transform.position += heading * (speed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    // swings a heading off course by up to yawOffset degrees to either side
    private Vector3 Weave(Vector3 heading, float yawOffset, float waveIrregularity)
    {
        float t = (Time.time + phaseOffset) * weaveFrequency;
        float sine = Mathf.Sin(t * 2f * Mathf.PI);
        float noise = Mathf.PerlinNoise(t, noiseSeed) * 2f - 1f;
        float waveform = Mathf.Lerp(sine, noise, waveIrregularity);

        return Quaternion.AngleAxis(yawOffset * waveform, Vector3.up) * heading;
    }

    // swings a heading up/down by up to pitchOffset degrees, phase-shifted off the yaw weave so they don't move in lockstep
    private Vector3 PitchWeave(Vector3 heading, float pitchOffset, float waveIrregularity)
    {
        if (pitchOffset <= 0f)
        {
            return heading;
        }

        Vector3 pitchAxis = Vector3.Cross(heading, Vector3.up);

        if (pitchAxis.sqrMagnitude < 0.0001f)
        {
            return heading;
        }

        pitchAxis.Normalize();

        float t = (Time.time + phaseOffset) * weaveFrequency + 0.25f;
        float sine = Mathf.Sin(t * 2f * Mathf.PI);
        float noise = Mathf.PerlinNoise(t, noiseSeed + 50f) * 2f - 1f;
        float waveform = Mathf.Lerp(sine, noise, waveIrregularity);

        float appliedOffset = pitchOffset * waveform;

        // don't let a downward pitch dive the bird into the ground
        if (appliedOffset < 0f && pitchGroundDamping > 0f && hoverSpace && hoverSpace.HasFloor)
        {
            float clearance = transform.position.y - hoverSpace.MinSpawnHeight;
            appliedOffset *= Mathf.Clamp01(clearance / pitchGroundDamping);
        }

        return Quaternion.AngleAxis(appliedOffset, pitchAxis) * heading;
    }

    #endregion
    
    #region Swooping 

    private void MoveSwoop()
    {
        if (!NoticedBreadAvailable)
        {
            EndSwoop();
            return;
        }

        Vector3 toBread = NoticedBreadPosition - transform.position;

        if (toBread.sqrMagnitude < Mathf.Epsilon)
        {
            return;
        }

        Vector3 heading = Vector3.Slerp(
            transform.forward,
            toBread.normalized,
            swoopTurnRate * Time.deltaTime
        ).normalized;

        transform.position += heading * (swoopSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    private void EndSwoop()
    {
       noticedBread = null;
       state = failedCatchState;

        // swoop may have carried it inside tooCloseDistance 
        if (state == State.Approaching)
        {
            ResetProximity();
        }
    }
    
    #endregion 
    
    #region Leave 
    
    public void BeginLeave()
    {
        if (state == State.FlyingAway || state == State.Leaving)
            return;

        StopCloseSoundLoop();
        state = State.Leaving;
        noticedBread = null;

        leaveElapsed = 0f;
        leaveStartHeading = transform.forward;

        Vector3 away = MoveTo ? transform.position - MoveTo.position : -leaveStartHeading;
        away.y = 0f;

        if (away.sqrMagnitude < Mathf.Epsilon)
        {
            away = -leaveStartHeading;
            away.y = 0f;
        }

        if (away.sqrMagnitude < Mathf.Epsilon)
        {
            away = Vector3.forward;
        }

        away.Normalize();
        leaveHeading = (away + Vector3.up * leaveClimb).normalized;
        leaveTurnAxis = TurnAxis(leaveStartHeading, leaveHeading);
    }

    private void MoveLeave()
    {
        leaveElapsed += Time.deltaTime;

        if (leaveElapsed >= leavePauseDuration + leaveLifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (leaveElapsed < leavePauseDuration)
            return;

        float turnElapsed = leaveElapsed - leavePauseDuration;
        float turnProgress = leaveTurnDuration > 0f ? Mathf.Clamp01(turnElapsed / leaveTurnDuration) : 1f;

        Vector3 heading;
        float currentSpeed;

        if (turnProgress < 1f)
        {
            heading = TurnToward(leaveStartHeading, leaveHeading, leaveTurnAxis, turnProgress);
            currentSpeed = Mathf.Lerp(0f, leaveSpeed, turnProgress);
        }
        else
        {
            heading = Weave(leaveHeading, leaveYawOffset, leaveIrregularity);
            currentSpeed = leaveSpeed;
        }

        transform.position += heading * (currentSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }
    
    #endregion 
    
    #region Fly Away 

    private void BeginFlyAway()
    {
        StopCloseSoundLoop();
        state = State.FlyingAway;
        flyAwayElapsed = 0f;
        flyAwayStartHeading = transform.forward;

        Vector3 horizontal = flyAwayStartHeading;
        horizontal.y = 0f;

        if (horizontal.sqrMagnitude < Mathf.Epsilon && MoveTo)
        {
            horizontal = transform.position - MoveTo.position;
            horizontal.y = 0f;
        }

        if (horizontal.sqrMagnitude < Mathf.Epsilon)
        {
            horizontal = Vector3.forward;
        }

        horizontal.Normalize();

        float climb = flyAwayAngle * Mathf.Deg2Rad;
        flyAwayHeading = (horizontal * Mathf.Cos(climb) + Vector3.up * Mathf.Sin(climb)).normalized;

        flyAwayTurnAxis = TurnAxis(flyAwayStartHeading, flyAwayHeading);
    }

    private void MoveFlyAway()
    {
        flyAwayElapsed += Time.deltaTime;

        float progress = flyAwayDuration > 0f ? Mathf.Clamp01(flyAwayElapsed / flyAwayDuration) : 1f;
        float turnProgress = flyAwayTurnDuration > 0f
            ? Mathf.Clamp01(flyAwayElapsed / flyAwayTurnDuration)
            : 1f;

        Vector3 heading = TurnToward(flyAwayStartHeading, flyAwayHeading, flyAwayTurnAxis, turnProgress);

        transform.position += heading * (Mathf.Lerp(speed, flyAwaySpeed, turnProgress) * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);

        float scale = flyAwayScaleCurve != null && flyAwayScaleCurve.length > 0
            ? flyAwayScaleCurve.Evaluate(progress)
            : 1f - progress;

        transform.localScale = originalScale * Mathf.Max(0f, scale);

        if (progress >= 1f)
            Destroy(gameObject);
    }
    
    #endregion 

    #region Hover

    [Header("Hover")]
    [SerializeField, Min(0f), Tooltip("in m/s, drift towards its hover spot")] private float hoverSpeed = 1.5f;
    [SerializeField, Min(0f), Tooltip("in meters, wobble around its hover spot")] private float hoverBobAmplitude = 0.4f;
    [SerializeField, Min(0f)] private float hoverBobFrequency = 0.5f;
    [SerializeField, Min(0f), Tooltip("how fast it turns to face the player")] private float hoverTurnRate = 2f;

    public bool IsHovering => state == State.Hovering || (state == State.Swooping && failedCatchState == State.Hovering);

    public Vector3 HoverAnchor => MoveTo ? MoveTo.position + hoverOffset : transform.position;

    public void RegisterHoverSpace(SeagullSpawner space)
    {
        hoverSpace = space;
    }

    private void MoveHover()
    {
        float t = (Time.time + phaseOffset) * hoverBobFrequency;

        Vector3 bob = new Vector3(
            Mathf.Sin(t * 2f * Mathf.PI),
            Mathf.PerlinNoise(t, noiseSeed) * 2f - 1f,
            Mathf.Cos(t * 2f * Mathf.PI)
        ) * hoverBobAmplitude;

        Vector3 desired = HoverAnchor + bob;
        transform.position = Vector3.MoveTowards(transform.position, desired, hoverSpeed * Time.deltaTime);

        if (!MoveTo)
        {
            return;
        }

        Vector3 toPlayer = MoveTo.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude > Mathf.Epsilon)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toPlayer.normalized, Vector3.up),
                hoverTurnRate * Time.deltaTime
            );
        }
    }

    #endregion

    #region Catch Bread

    [Header("Beak")]
    [SerializeField, Tooltip("put it on a head bone so the bread follows the animation")] private Transform beak;
    [SerializeField, Min(0f), Tooltip("in meters, how close a thrown bread has to be to chase it")] private float noticeRadius = 6f;

    private Catchable noticedBread;

    public bool HasBread { get; private set; }

    private bool NoticedBreadAvailable => noticedBread && noticedBread.IsInFlight && !noticedBread.Taken;

    private Vector3 NoticedBreadPosition => noticedBread.transform.position;

    private bool TryNoticeBread()
    {
        if (HasBread || noticeRadius <= 0f)
        {
            return false;
        }

        noticedBread = Catchable.FindNearestInFlight(transform.position, noticeRadius);

        return noticedBread;
    }
    
    // may fail if another seagull got it first 
    private bool TryCatch(ThrowInteractable hitBy)
    {
       noticedBread = null;

        if (!hitBy || !beak)
        {
            return false;
        }

        Catchable catchable = hitBy.GetComponentInParent<Catchable>();

        if (!catchable || !catchable.TryGiveTo(beak))
        {
            return false;
        }

        HasBread = true;
        return true;
    }

    #endregion
}
