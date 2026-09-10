using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

public enum ThrowReleaseStyle
{
    Duration, // detaches when the timer completes, while grip is still held
    Release, // detaches when the player lets go of grip, provided the timer had completed by then
}

// a grabbable object that is thrown by sustaining a fast hand motion for a specified duration 
public class ThrowInteractable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Grabbable grabbable;

    [Tooltip("The GrabInteractable / HandGrabInteractable components on this object")]
    [SerializeField] private Behaviour[] interactables;

    [SerializeField] private Rigidbody body;
    [SerializeField] private FixedGripTransformer gripTransformer;

    [Tooltip("Defines the object's motion once released")]
    [SerializeField] private ThrowPhysics physicsProvider;

    [Header("Throw")]
    [Tooltip("Duration = release as soon as the timer completes. Release = wait for the player to let go of grip")]
    [SerializeField] private ThrowReleaseStyle releaseStyle = ThrowReleaseStyle.Duration;

    [Tooltip("Hand speed in m/s at which a throw starts being tracked")]
    [SerializeField] private float throwVelocityThreshold = 2f;

    [Tooltip("How long in seconds the hand must stay at/above the threshold for the throw to fire")]
    [SerializeField] private float throwDuration = 0.25f;

    [Header("Haptics")]
    [SerializeField, Range(0f, 1f)] private float hapticFrequency = 0.5f;
    [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.5f;

    [Header("Debug")]
    [Tooltip("Fallback hand used for haptics if the controller handedness can't be identified")]
    [SerializeField] private bool fallbackToRightHand = true;

    [SerializeField] private bool verbose;

    // how many recent poses to average velocity over, to prevent one frame of noisy input from dropping the object 
    private const int sampleCount = 5;

    private readonly PoseBuffer handPoses = new PoseBuffer(sampleCount);
    private readonly PoseBuffer heldPoses = new PoseBuffer(sampleCount);

    private bool isHeld;
    private int selectorId;
    private bool isThrowing;
    private bool isArmed;
    private float throwTimer;
    private float handSpeed;
    private ThrowKinematics peakHand;
    private ThrowKinematics peakHeld;
    private bool hasPeak;
    private bool isVibrating;
    private OVRInput.Controller hapticController = OVRInput.Controller.None;
    private bool subscribedToPhysics;

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }

        StopHaptics();
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                HandleSelect(evt);
                break;
            case PointerEventType.Move:
                HandleMove(evt);
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                HandleRelease(evt);
                break;
        }
    }

    private void HandleSelect(PointerEvent evt)
    {
        if (isHeld || isThrowing)
        {
            return;
        }

        isHeld = true;
        selectorId = evt.Identifier;
        isArmed = false;
        throwTimer = 0f;
        handSpeed = 0f;
        hasPeak = false;
        handPoses.Clear();
        heldPoses.Clear();
        hapticController = ResolveController(evt);

        if (verbose)
        {
            Debug.Log($"[ThrowInteractable] grabbed by selector {selectorId} ({hapticController})");
        }
    }

    private void HandleMove(PointerEvent evt)
    {
        if (!isHeld || evt.Identifier != selectorId)
        {
            return;
        }

        handPoses.Add(evt.Pose, Time.time);
        heldPoses.Add(new Pose(transform.position, transform.rotation), Time.time);

        if (!handPoses.TryGetKinematics(out ThrowKinematics hand))
        {
            return;
        }

        handSpeed = hand.velocity.magnitude;

        if (handSpeed >= throwVelocityThreshold
            && (!hasPeak || handSpeed > peakHand.velocity.magnitude)
            && heldPoses.TryGetKinematics(out ThrowKinematics held))
        {
            peakHand = hand;
            peakHeld = held;
            hasPeak = true;
        }
    }

    private void HandleRelease(PointerEvent evt)
    {
        if (!isHeld || evt.Identifier != selectorId)
        {
            return;
        }

        isHeld = false;
        StopHaptics();

        bool wasArmed = isArmed;

        isArmed = false;
        throwTimer = 0f;
        handSpeed = 0f;

        // isThrowing means a Duration throw already fired and caused this unselect
        if (!isThrowing)
        {
            if (wasArmed && releaseStyle == ThrowReleaseStyle.Release)
            {
                FireThrow();
            }
            else
            {
                FireFailedThrow();
            }
        }

        if (body != null)
        {
            body.isKinematic = isThrowing;
        }
    }

    private void Update()
    {
        if (!isHeld || isThrowing)
        {
            return;
        }

        if (handSpeed >= throwVelocityThreshold)
        {
            if (!isVibrating)
            {
                StartHaptics();
            }

            throwTimer += Time.deltaTime;

            if (throwTimer >= throwDuration)
            {
                isArmed = true;

                if (releaseStyle == ThrowReleaseStyle.Duration)
                {
                    FireThrow();
                }
            }
        }
        else
        {
            StopHaptics();

            // an armed throw latches until the player lets go. only an unarmed timer restarts
            if (!isArmed && throwTimer > 0f)
            {
                throwTimer = 0f;
                hasPeak = false;
            }
        }
    }

    private void FireThrow()
    {
        if (physicsProvider == null)
        {
            Debug.LogWarning("[ThrowInteractable] no physicsProvider assigned, cannot throw", this);
            return;
        }

        if (!TryBuildThrowData(out ThrowData data))
        {
            return;
        }

        StopHaptics();
        BeginHandOff();

        if (verbose)
        {
            Debug.Log($"[ThrowInteractable] throw fired at {data.hand.velocity.magnitude:F2} m/s (peak {data.peakHand.velocity.magnitude:F2})");
        }

        physicsProvider.Initialize(data);
    }

    // released before the timer completed. the provider may decline 
    private void FireFailedThrow()
    {
        if (!TryBuildThrowData(out ThrowData data))
        {
            return;
        }
        
        BeginHandOff();

        if (verbose)
        {
            Debug.Log("[ThrowInteractable] failed throw");
        }

        physicsProvider.InitializeFailed(data);
    }

    private bool TryBuildThrowData(out ThrowData data)
    {
        data = default;

        if (physicsProvider == null)
        {
            return false;
        }

        if (!handPoses.TryGetKinematics(out ThrowKinematics hand)
            || !heldPoses.TryGetKinematics(out ThrowKinematics heldObject))
        {
            return false;
        }

        data.hand = hand;
        data.heldObject = heldObject;

        // a release that never crossed the threshold has no distinct peak
        data.peakHand = hasPeak ? peakHand : hand;
        data.peakHeld = hasPeak ? peakHeld : heldObject;
        return true;
    }

    private void BeginHandOff()
    {
        isThrowing = true;
        throwTimer = 0f;

        if (gripTransformer != null)
        {
            gripTransformer.Suspended = true;
        }

        SetInteractablesEnabled(false);

        if (body != null)
        {
            body.isKinematic = true;
        }

        if (!subscribedToPhysics)
        {
            physicsProvider.OnStopped += HandlePhysicsStopped;
            subscribedToPhysics = true;
        }
    }

    private void HandlePhysicsStopped()
    {
        isThrowing = false;

        if (gripTransformer != null)
        {
            gripTransformer.Suspended = false;
        }

        if (body != null)
        {
            body.isKinematic = false;
        }

        SetInteractablesEnabled(true);
    }

    private void SetInteractablesEnabled(bool value)
    {
        if (interactables == null)
        {
            return;
        }

        for (int i = 0; i < interactables.Length; i++)
        {
            if (interactables[i] != null)
            {
                interactables[i].enabled = value;
            }
        }
    }

    private void StartHaptics()
    {
        isVibrating = true;

        if (hapticController != OVRInput.Controller.None)
        {
            OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, hapticController);
        }
    }

    private void StopHaptics()
    {
        if (!isVibrating)
        {
            return;
        }

        isVibrating = false;

        if (hapticController != OVRInput.Controller.None)
        {
            OVRInput.SetControllerVibration(0f, 0f, hapticController);
        }
    }
    
    private OVRInput.Controller ResolveController(PointerEvent evt)
    {
        if (evt.Data is Component source)
        {
            IController controller = source.GetComponentInParent<IController>();
            if (controller != null)
            {
                return ToController(controller.Handedness);
            }

            IHand hand = source.GetComponentInParent<IHand>();
            if (hand != null)
            {
                return ToController(hand.Handedness);
            }
        }

        if (verbose)
        {
            Debug.Log("[ThrowInteractable] could not resolve grabbing hand, using fallback");
        }

        return fallbackToRightHand ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
    }

    private OVRInput.Controller ToController(Handedness handedness)
    {
        return handedness == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
    }

    // ring buffer of recent pose. for estimating velocity across several frames
    private class PoseBuffer
    {
        private readonly Pose[] poses;
        private readonly float[] times;
        private int count;
        private int head;

        public PoseBuffer(int capacity)
        {
            poses = new Pose[capacity];
            times = new float[capacity];
        }

        public void Clear()
        {
            count = 0;
            head = 0;
        }

        public void Add(Pose pose, float time)
        {
            poses[head] = pose;
            times[head] = time;
            head = (head + 1) % poses.Length;
            count = Mathf.Min(count + 1, poses.Length);
        }

        public bool TryGetKinematics(out ThrowKinematics kinematics)
        {
            kinematics = default;

            if (count < 2)
            {
                return false;
            }

            int newest = (head - 1 + poses.Length) % poses.Length;
            int oldest = (head - count + poses.Length) % poses.Length;

            float dt = times[newest] - times[oldest];
            if (dt <= Mathf.Epsilon)
            {
                return false;
            }

            kinematics.position = poses[newest].position;
            kinematics.rotation = poses[newest].rotation;
            kinematics.velocity = (poses[newest].position - poses[oldest].position) / dt;
            kinematics.angularVelocity = AngularVelocity(poses[oldest].rotation, poses[newest].rotation, dt);
            return true;
        }

        private static Vector3 AngularVelocity(Quaternion from, Quaternion to, float dt)
        {
            (to * Quaternion.Inverse(from)).ToAngleAxis(out float angle, out Vector3 axis);

            if (float.IsInfinity(axis.x) || angle < Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            if (angle > 180f)
            {
                angle -= 360f;
            }

            return axis.normalized * (angle * Mathf.Deg2Rad / dt);
        }
    }
}
