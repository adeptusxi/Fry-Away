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
        Landing, // gliding down to a ground spot
        Idle, // sitting on the ground
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
    [SerializeField, Range(-45f, 45f), Tooltip("degrees; steady pitch bias for a 'low flier' (HeightBias near 0)")] private float lowHeightPitchBias = -8f;
    [SerializeField, Range(-45f, 45f), Tooltip("degrees; steady pitch bias for a 'high flier' (HeightBias near 1)")] private float highHeightPitchBias = 8f;

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
    [SerializeField, Range(0f, 90f)] private float flyAwayAngle = 25f; // 90 is straight up. shallow, since this now sustains over a longer flyAwayDuration - a steep angle held that long looks like a rocket launch
    [SerializeField, Min(0f), Tooltip("in seconds, length of the turn onto the climb")] private float flyAwayTurnDuration = 0.35f;
    [SerializeField, Min(0f), Tooltip("in m/s")] private float flyAwaySpeed = 9f;
    [SerializeField, Min(0f), Tooltip("in seconds")] private float flyAwayDuration = 3f;
    [SerializeField, Range(0f, 1f), Tooltip("fraction of the flight (0-1) before it starts shrinking away. keeps it full-size while it's still visibly departing, instead of fading out right next to the player")]
    private float flyAwayShrinkStart = 0.6f;
    [SerializeField, Tooltip("normalized 0-1, evaluated only over the shrink portion of the flight (see flyAwayShrinkStart)")] private AnimationCurve flyAwayScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // for smoothly disappearing

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
    private float heightBias;
    private Vector3 fallbackHeading;
    private Vector3 originalScale;

    // 0 = low flier, 1 = high flier. randomized once per bird so it flies (and later hovers) at a consistent altitude relative to others
    public float HeightBias => heightBias;

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
        heightBias = Random.Range(0f, 1f);
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
        hoverTimeSinceLastLandingCheck = 0f;
    }

    public override void OnObjectHit(ThrowInteractable hitBy, RaycastHit hit)
    {
        if (state == State.FlyingAway)
            return;

        ResolveHit(hitBy ? hitBy.GetComponentInParent<Catchable>() : null, hit.point);
    }
    
    private void ResolveHit(Catchable bread, Vector3 point)
    {
        PlayHitSound(point);

        TryCatch(bread);

        GameManager.HitResult result = GameManager.Instance
            ? GameManager.Instance.ReportSeagullHit(this, point)
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
            case State.Landing:
                MoveLanding();
                break;
            case State.Idle:
                MoveIdle();
                break;
            case State.Leaving:
                MoveLeave();
                break;
            default:
                MoveApproach();
                break;
        }

        // Landing/Idle deliberately target the ground (via the floor reference), which can sit
        // lower than sea level on dry land - the flight-safety clamp would otherwise fight them
        // and drag a landed bird back up to sea level
        if (state != State.Landing && state != State.Idle)
        {
            ClampAboveFloor();
        }
    }

    // the higher of the floor's spawn-clearance height and the sea level, whichever is more restrictive.
    // in the beach scene the floor reference (a mostly-buried sand block) sits well below the
    // actual water surface, so sea level is normally the one that matters here.
    private float EffectiveMinHeight()
    {
        float minHeight = float.NegativeInfinity;

        if (hoverSpace)
        {
            if (hoverSpace.HasFloor)
            {
                minHeight = hoverSpace.MinSpawnHeight;
            }

            if (hoverSpace.HasSeaLevel)
            {
                minHeight = Mathf.Max(minHeight, hoverSpace.SeaLevelHeight);
            }
        }

        return minHeight;
    }

    // hard safety net: whatever a state's movement did this frame, never let the bird end up
    // below the floor/sea level (the soft pitch-weave damping only reduces how hard it dives, it doesn't guarantee this)
    private void ClampAboveFloor()
    {
        float minHeight = EffectiveMinHeight();

        if (float.IsNegativeInfinity(minHeight))
        {
            return;
        }

        if (transform.position.y < minHeight)
        {
            Vector3 position = transform.position;
            position.y = minHeight;
            transform.position = position;
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
        float pitchBias = Mathf.Lerp(lowHeightPitchBias, highHeightPitchBias, heightBias);
        heading = PitchWeave(heading, maxPitchOffset, irregularity, pitchBias);

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

    // swings a heading up/down by up to pitchOffset degrees around a steady biasDegrees center,
    // phase-shifted off the yaw weave so they don't move in lockstep
    private Vector3 PitchWeave(Vector3 heading, float pitchOffset, float waveIrregularity, float biasDegrees)
    {
        Vector3 pitchAxis = Vector3.Cross(heading, Vector3.up);

        if (pitchAxis.sqrMagnitude < 0.0001f)
        {
            return heading;
        }

        pitchAxis.Normalize();

        float appliedOffset = biasDegrees;

        if (pitchOffset > 0f)
        {
            float t = (Time.time + phaseOffset) * weaveFrequency + 0.25f;
            float sine = Mathf.Sin(t * 2f * Mathf.PI);
            float noise = Mathf.PerlinNoise(t, noiseSeed + 50f) * 2f - 1f;
            float waveform = Mathf.Lerp(sine, noise, waveIrregularity);

            appliedOffset += pitchOffset * waveform;
        }

        // don't let a downward pitch dive the bird into the ground/water
        if (appliedOffset < 0f && pitchGroundDamping > 0f)
        {
            float minHeight = EffectiveMinHeight();

            if (!float.IsNegativeInfinity(minHeight))
            {
                float clearance = transform.position.y - minHeight;
                appliedOffset *= Mathf.Clamp01(clearance / pitchGroundDamping);
            }
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

        // close enough 
        if (beak && catchRadius > 0f &&
            (NoticedBreadPosition - beak.position).sqrMagnitude <= catchRadius * catchRadius)
        {
            ResolveHit(noticedBread, NoticedBreadPosition);
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

        if (animator)
        {
            animator.speed = 1f;
        }

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

        if (animator)
        {
            animator.speed = 1f;
        }
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

        float shrinkProgress = flyAwayShrinkStart < 1f
            ? Mathf.Clamp01((progress - flyAwayShrinkStart) / (1f - flyAwayShrinkStart))
            : (progress >= 1f ? 1f : 0f);

        float scale = flyAwayScaleCurve != null && flyAwayScaleCurve.length > 0
            ? flyAwayScaleCurve.Evaluate(shrinkProgress)
            : 1f - shrinkProgress;

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
    [SerializeField, Min(0f), Tooltip("in meters. beyond this distance from its hover spot, it faces the way it's actually flying instead of the player, so it doesn't look like it's sliding in sideways")]
    private float hoverSettleDistance = 1.5f;

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
        Vector3 toDesired = desired - transform.position;
        float distanceToAnchor = toDesired.magnitude;

        transform.position = Vector3.MoveTowards(transform.position, desired, hoverSpeed * Time.deltaTime);

        // while still closing in on its hover spot, face the way it's actually flying rather than
        // the player, so it reads as directed flight instead of sliding in sideways/backwards
        Vector3? faceDirection = null;

        if (distanceToAnchor > hoverSettleDistance && toDesired.sqrMagnitude > Mathf.Epsilon)
        {
            faceDirection = toDesired.normalized;
        }
        else if (MoveTo)
        {
            Vector3 toPlayer = MoveTo.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > Mathf.Epsilon)
            {
                faceDirection = toPlayer.normalized;
            }
        }

        if (faceDirection.HasValue)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(faceDirection.Value, Vector3.up),
                hoverTurnRate * Time.deltaTime
            );
        }

        // only roll the chance to land once it's actually settled at its hover spot,
        // not while it's still gliding in
        if (distanceToAnchor > hoverSettleDistance)
        {
            return;
        }

        hoverTimeSinceLastLandingCheck += Time.deltaTime;

        if (hoverTimeSinceLastLandingCheck < landingCheckInterval)
        {
            return;
        }

        hoverTimeSinceLastLandingCheck = 0f;

        if (Random.value < landingChance && TryPickLandingSpot(out Vector3 spot))
        {
            BeginLanding(spot);
        }
    }

    #endregion

    #region Landing

    [Header("Landing")]
    [SerializeField, Min(0f), Tooltip("in meters from the player. landing spots are picked at least this far away so they stay visible, not underfoot")]
    private float landingDistanceMin = 4f;
    [SerializeField, Min(0f), Tooltip("in meters from the player. landing spots are picked at most this far away")]
    private float landingDistanceMax = 7f;
    [SerializeField, Min(0f), Tooltip("in seconds, how often it rolls the chance to land while settled in hovering")]
    private float landingCheckInterval = 2f;
    [SerializeField, Range(0f, 1f), Tooltip("chance per check to start landing")] private float landingChance = 0.4f;
    [SerializeField, Min(0f), Tooltip("in seconds, length of the glide down")] private float landingDuration = 1.5f;
    [SerializeField, Min(0f), Tooltip("in seconds, length of the turn onto the glide")] private float landingTurnDuration = 0.4f;
    [SerializeField, Min(0f), Tooltip("in seconds, length of the turn to face the player at the end of the glide")]
    private float landingSettleDuration = 0.4f;
    [SerializeField, Min(0f), Tooltip("in seconds, minimum time spent idle on the ground")] private float idleDurationMin = 3f;
    [SerializeField, Min(0f), Tooltip("in seconds, maximum time spent idle on the ground")] private float idleDurationMax = 8f;
    [SerializeField, Tooltip("optional. its playback speed is slowed while idle so the flap reads as 'settled' instead of full-speed flying, and restored on takeoff/leave/fly-away")]
    private Animator animator;
    [SerializeField, Range(0f, 1f), Tooltip("Animator.speed multiplier while idle on the ground")]
    private float idleAnimationSpeed = 0.3f;

    private float hoverTimeSinceLastLandingCheck;

    private float landingElapsed;
    private Vector3 landingStartPosition;
    private Vector3 landingTargetPosition;
    private Vector3 landingStartHeading;
    private Vector3 landingHeading;
    private Vector3 landingTurnAxis;

    private float idleElapsed;
    private float idleDuration;
    private Vector3 idleFaceDirection;

    // picks a ground point near where this bird is currently hovering. returns false if there's
    // no floor configured to land on. deliberately uses the floor (sand) height, not
    // EffectiveMinHeight() - that one leans on sea level to keep flight above the water, but a
    // landing spot needs the actual ground it's supposed to stand on
    private bool TryPickLandingSpot(out Vector3 spot)
    {
        if (!hoverSpace || !hoverSpace.HasFloor || !MoveTo)
        {
            spot = transform.position;
            return false;
        }

        // land out along the same direction it was already hovering in (so it stays within the
        // front sector, same as hovering), just farther out - a spot picked as a small jitter
        // around the hover anchor could end up right underfoot and go unnoticed
        Vector3 fromPlayer = HoverAnchor - MoveTo.position;
        fromPlayer.y = 0f;

        Vector3 direction = fromPlayer.sqrMagnitude > Mathf.Epsilon ? fromPlayer.normalized : Vector3.zero;

        if (direction.sqrMagnitude < Mathf.Epsilon)
        {
            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;
            direction = forwardFlat.sqrMagnitude > Mathf.Epsilon ? forwardFlat.normalized : Vector3.forward;
        }

        float distance = Random.Range(landingDistanceMin, landingDistanceMax);
        Vector3 groundPoint = MoveTo.position + direction * distance;

        spot = new Vector3(groundPoint.x, hoverSpace.MinSpawnHeight, groundPoint.z);
        return true;
    }

    private void BeginLanding(Vector3 spot)
    {
        state = State.Landing;
        landingElapsed = 0f;
        landingStartPosition = transform.position;
        landingTargetPosition = spot;
        landingStartHeading = transform.forward;

        // horizontal-only: otherwise it points straight down at the ground for the whole glide,
        // which reads as diving in head-first instead of a level approach that sinks in altitude
        Vector3 toSpotFlat = spot - transform.position;
        toSpotFlat.y = 0f;
        landingHeading = toSpotFlat.sqrMagnitude > Mathf.Epsilon ? toSpotFlat.normalized : landingStartHeading;
        landingTurnAxis = TurnAxis(landingStartHeading, landingHeading);
    }

    private void MoveLanding()
    {
        landingElapsed += Time.deltaTime;

        float progress = landingDuration > 0f ? Mathf.Clamp01(landingElapsed / landingDuration) : 1f;
        float turnProgress = landingTurnDuration > 0f ? Mathf.Clamp01(landingElapsed / landingTurnDuration) : 1f;

        Vector3 heading = TurnToward(landingStartHeading, landingHeading, landingTurnAxis, turnProgress);

        // the landing spot is picked further out from the player than the bird already was, so the
        // glide heading points away from the player - ease into facing the player over the last
        // stretch, so it doesn't touch down with its back turned
        float settleStart = Mathf.Max(0f, landingDuration - landingSettleDuration);

        if (landingElapsed > settleStart && MoveTo)
        {
            Vector3 toPlayer = MoveTo.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > Mathf.Epsilon)
            {
                float settleProgress = landingSettleDuration > 0f
                    ? Mathf.Clamp01((landingElapsed - settleStart) / landingSettleDuration)
                    : 1f;
                heading = Vector3.Slerp(landingHeading, toPlayer.normalized, settleProgress).normalized;
            }
        }

        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        transform.position = Vector3.Lerp(landingStartPosition, landingTargetPosition, progress);

        if (progress >= 1f)
        {
            BeginIdle();
        }
    }

    private void BeginIdle()
    {
        state = State.Idle;
        idleElapsed = 0f;
        idleDuration = Random.Range(idleDurationMin, idleDurationMax);

        Vector3 toPlayer = MoveTo ? MoveTo.position - transform.position : landingHeading;
        toPlayer.y = 0f;
        idleFaceDirection = toPlayer.sqrMagnitude > Mathf.Epsilon ? toPlayer.normalized : landingHeading;

        if (animator)
        {
            animator.speed = idleAnimationSpeed;
        }
    }

    private void MoveIdle()
    {
        idleElapsed += Time.deltaTime;

        if (idleElapsed >= idleDuration)
        {
            BeginTakeOff();
            return;
        }

        // small cosmetic look-around, rotation only, no translation - centered on facing the player
        float t = (Time.time + phaseOffset) * 0.2f;
        float yaw = (Mathf.PerlinNoise(t, noiseSeed) * 2f - 1f) * 20f;
        transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(yaw, Vector3.up) * idleFaceDirection, Vector3.up);
    }

    private void BeginTakeOff()
    {
        // re-picks a hover offset and hands off to MoveHover(), which already knows how to glide
        // smoothly from wherever it currently is up to a hover anchor (see hoverSettleDistance)
        if (hoverSpace)
        {
            hoverOffset = hoverSpace.PickHoverOffset(this);
        }

        hoverTimeSinceLastLandingCheck = 0f;
        state = State.Hovering;

        if (animator)
        {
            animator.speed = 1f;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !TryPickLandingSpot(out Vector3 spot))
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(spot, 0.25f);
        Gizmos.DrawLine(transform.position, spot);
    }
#endif

    #endregion

    #region Catch Bread

    [Header("Beak")]
    [SerializeField, Tooltip("put it on a head bone so the bread follows the animation")] private Transform beak;
    [SerializeField, Min(0f), Tooltip("in meters, how close a thrown bread has to be to chase it")] private float noticeRadius = 6f;
    [SerializeField, Min(0f), Tooltip("in meters from the beak. 0 means just use hitbox")] private float catchRadius = 0.25f;

    private Catchable noticedBread;

    public bool HasBread { get; private set; }

    // stops mid-chase too: if the bread it's already after sinks below sea level, this goes false
    // and MoveSwoop()'s guard ends the chase, same as if the bread had been caught by someone else
    private bool NoticedBreadAvailable => noticedBread && noticedBread.IsInFlight && !noticedBread.Taken &&
        (!hoverSpace || !hoverSpace.HasSeaLevel || noticedBread.transform.position.y > hoverSpace.SeaLevelHeight);

    private Vector3 NoticedBreadPosition => noticedBread.transform.position;

    private bool TryNoticeBread()
    {
        if (HasBread || noticeRadius <= 0f)
        {
            return false;
        }

        noticedBread = Catchable.FindNearestInFlight(transform.position, noticeRadius);

        // never chase bread behind the player, and never chase bread that's already underwater
        bool valid = NoticedBreadAvailable &&
            (!hoverSpace || hoverSpace.IsWithinAttractionSector(noticedBread.transform.position));

        if (!valid)
        {
            noticedBread = null;
            return false;
        }

        return true;
    }

    // may fail if another seagull got it first 
    private bool TryCatch(Catchable catchable)
    {
       noticedBread = null;

        if (!catchable || !beak || !catchable.TryGiveTo(beak))
        {
            return false;
        }

        HasBread = true;
        return true;
    }

    #endregion
}
