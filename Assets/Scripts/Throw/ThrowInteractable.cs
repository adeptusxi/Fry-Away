using System;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

// a grabbable object that is thrown by sustaining a fast hand motion for a specified duration,
// then letting go of grip. letting go without a windup just leaves it where it is (can regrab) 
public class ThrowInteractable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Grabbable grabbable;

    [Tooltip("All the GrabInteractable / HandGrabInteractable components on this object")]
    [SerializeField] private Behaviour[] interactables;

    [SerializeField] private Rigidbody body;
    [SerializeField] private FixedGripTransformer gripTransformer;

    [Tooltip("Defines the object's motion once released")]
    [SerializeField] private ThrowPhysics physicsProvider;

    [Header("Throw")]
    [Tooltip("Hand speed in m/s at which a throw starts being tracked")]
    [SerializeField] private float throwVelocityThreshold = 2f;

    [Tooltip("How long in seconds the hand must stay at/above the threshold for the throw to fire")]
    [SerializeField] private float throwDuration = 0.25f;
    
    [Tooltip("How many recent poses to average velocity over")]
    [SerializeField, Min(2)] private int sampleCount = 15;

    [Tooltip("Rotates the sampled controller pose so its forward axis is the throw aim direction")]
    [SerializeField] private Vector3 aimRotationOffset;

    [Header("Haptics")]
    [SerializeField, Range(0f, 1f)] private float hapticFrequency = 0.5f;
    [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.5f;

    [Header("Debug")]
    [Tooltip("Fallback hand used for haptics if the controller handedness can't be identified")]
    [SerializeField] private bool fallbackToRightHand = true;

    [SerializeField] private bool verbose;
    
    private PoseBuffer handPoses;
    private PoseBuffer heldPoses;
    
    public event Action OnThrown; // fired when the object is handed off to physics

    private bool isHeld;
    private int selectorId;
    private bool isThrowing;
    private bool isArmed;
    private float throwTimer;
    private float handSpeed;
    private Kinematics peakHand;
    private Kinematics peakHeld;
    private bool hasPeak;

    // running sums over the throw window 
    private Vector3 handVelocitySum;
    private Vector3 heldVelocitySum;
    private int windowSamples;
    
    private Transform heldTransform; // the transform the Grabbable actually moves 
    private bool isVibrating;
    private OVRInput.Controller hapticController = OVRInput.Controller.None;
    private IController grabController; // the controller holding the object, to sample pose from
    private Transform grabAnchor; // scene anchor for the grabbing hand. see TryGetHandPose
    private bool warnedNoController;
    private bool subscribedToPhysics;

    private void Awake()
    {
        if (grabbable == null || gripTransformer == null || body == null || physicsProvider == null)
        {
            Debug.LogError("[ThrowInteractable] Missing required grabbable, gripTransformer, body or physicsProvider", this);
            enabled = false;
            return;
        }

        handPoses = new PoseBuffer(sampleCount);
        heldPoses = new PoseBuffer(sampleCount);

        grabbable.InjectOptionalOneGrabTransformer(gripTransformer);
        grabbable.InjectOptionalThrowWhenUnselected(false);
        grabbable.InjectOptionalKinematicWhileSelected(true);
        grabbable.MaxGrabPoints = 1;

        EnsureInert();
    }

    // don't let it move by itself 
    private void EnsureInert()
    {
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnEnable()
    {
        grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }

        StopHaptics();
    }

    private void OnDestroy()
    {
        if (subscribedToPhysics && physicsProvider != null)
        {
            physicsProvider.OnStopped -= HandlePhysicsStopped;
            subscribedToPhysics = false;
        }
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
        ClearWindow();
        handPoses.Clear();
        heldPoses.Clear();
        warnedNoController = false;

        bool resolved = TryResolveGrab(evt, out Handedness handedness, out grabController);
        grabAnchor = resolved ? ControllerAnchor.Get(handedness) : null;
        hapticController = ToController(handedness, resolved);

        gripTransformer.Suspended = false;
        
        heldTransform = grabbable.Transform != null ? grabbable.Transform : transform;

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

        if (!TryGetHandPose(out Pose handPose))
        {
            return;
        }

        handPoses.Add(handPose, Time.time);
        heldPoses.Add(new Pose(heldTransform.position, heldTransform.rotation), Time.time);

        if (!handPoses.TryGetKinematics(out Kinematics hand))
        {
            return;
        }

        handSpeed = hand.velocity.magnitude;

        if (handSpeed < throwVelocityThreshold || !heldPoses.TryGetKinematics(out Kinematics held))
        {
            return;
        }

        handVelocitySum += hand.velocity;
        heldVelocitySum += held.velocity;
        windowSamples++;

        if (!hasPeak || handSpeed > peakHand.velocity.magnitude)
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

        bool throwing = isArmed;

        isArmed = false;
        throwTimer = 0f;
        handSpeed = 0f;

        EnsureInert();

        if (throwing)
        {
            FireThrow();
        }

        // after the buffers have been read, so we don't hold a reference to the hand rig
        grabController = null;
        grabAnchor = null;
    }

    private void Update()
    {
        if (!isHeld)
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
                ClearWindow();
            }
        }
    }

    private void FireThrow()
    {
        ThrowData data = BuildThrowData();

        StopHaptics();
        BeginHandOff();

        if (verbose)
        {
            Debug.Log($"[ThrowInteractable] throw fired at {data.hand.velocity.magnitude:F2} m/s (peak {data.peakHand.velocity.magnitude:F2})");
        }

        physicsProvider.Initialize(data, this);
    }

    private ThrowData BuildThrowData()
    {
        ThrowData data = default;
        
        handPoses.TryGetKinematics(out Kinematics hand);
        heldPoses.TryGetKinematics(out Kinematics heldObject);

        data.hand = hand;
        data.heldObject = heldObject;

        // a throw too brief to build a window has no distinct peak or average
        data.peakHand = hasPeak ? peakHand : hand;
        data.peakHeld = hasPeak ? peakHeld : heldObject;

        return data;
    }

    private void ClearWindow()
    {
        handVelocitySum = Vector3.zero;
        heldVelocitySum = Vector3.zero;
        windowSamples = 0;
    }

    private void BeginHandOff()
    {
        isThrowing = true;
        throwTimer = 0f;

        gripTransformer.Suspended = true;

        if (!subscribedToPhysics)
        {
            physicsProvider.OnStopped += HandlePhysicsStopped;
            subscribedToPhysics = true;
        }
        
        OnThrown?.Invoke();

        SetInteractablesEnabled(false);
    }

    private void HandlePhysicsStopped()
    {
        isThrowing = false;
        gripTransformer.Suspended = false;
        EnsureInert();
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
    
    private bool TryResolveGrab(PointerEvent evt, out Handedness handedness, out IController controller)
    {
        handedness = default;
        controller = null;

        if (evt.Data is Component source)
        {
            controller = source.GetComponentInParent<IController>();
            if (controller != null)
            {
                handedness = controller.Handedness;
                return true;
            }

            IHand hand = source.GetComponentInParent<IHand>();
            if (hand != null)
            {
                handedness = hand.Handedness;
                return true;
            }
        }

        return false;
    }

    // PLACEHOLDER: reads pose from scene anchors 
    // TODO: Xiao - figure out why SDK lookup isn't working 
    private bool TryGetHandPose(out Pose pose)
    {
        if (grabAnchor != null)
        {
            pose = new Pose(grabAnchor.position, grabAnchor.rotation * Quaternion.Euler(aimRotationOffset));
            return true;
        }

        if (grabController != null && grabController.TryGetPose(out Pose controllerPose))
        {
            pose = new Pose(controllerPose.position, controllerPose.rotation * Quaternion.Euler(aimRotationOffset));
            return true;
        }

        if (!warnedNoController)
        {
            warnedNoController = true;
            Debug.LogWarning(
                "[ThrowInteractable] no hand pose source: assign a DebugHandAnchor to each controller. the throw can't arm without one",
                this);
        }

        pose = default;
        return false;
    }

    private OVRInput.Controller ToController(Handedness handedness, bool resolved)
    {
        if (!resolved)
        {
            return fallbackToRightHand ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
        }

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

        // returns false when there aren't enough samples to measure velocity
        // (the pose is still filled in from the newest sample)
        public bool TryGetKinematics(out Kinematics kinematics)
        {
            kinematics = default;

            if (count < 1)
            {
                return false;
            }

            int newest = (head - 1 + poses.Length) % poses.Length;
            kinematics.position = poses[newest].position;
            kinematics.rotation = poses[newest].rotation;

            if (count < 2)
            {
                return false;
            }

            int oldest = (head - count + poses.Length) % poses.Length;

            float dt = times[newest] - times[oldest];
            if (dt <= Mathf.Epsilon)
            {
                return false;
            }

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
