using System;
using UnityEngine;

// snapshot of how something was moving at a single moment
public struct Kinematics
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
}

// everything known about the moment a throw was released 
public struct ThrowData
{
    public Kinematics hand;
    public Kinematics heldObject;
    public Kinematics peakHand;
    public Kinematics peakHeld;
}

// "interface" for anything that moves a thrown object after it leaves the player's hand.
// defaults to moving the GameObject it is attached to, but can drive a separate one if specified 
public abstract class ThrowPhysics : MonoBehaviour
{
    private ThrowInteractable parentInteractable; // the ThrowInteractable that this ThrowPhysics is providing physics for 
    
    [Tooltip("What to move")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("What to sweep for collisions. Needs a Rigidbody on it")]
    [SerializeField] private Collider targetCollider;

    [Tooltip("Layers which the object collides with while in flight")]
    [SerializeField] private LayerMask collisionLayerMask;

    [Tooltip("Max seconds to simulate before stopping automatically. <0 means no limit")]
    [SerializeField] private float timeout = 60f;

    [Header("Aim Assist")]
    [Tooltip("0 is pure physics, 1 flies straight at the nearest HittableTarget, 0.5 mixes both")]
    [SerializeField, Range(0f, 1f)] private float aimAssist;
    [SerializeField, Range(0f, 180f), Tooltip("in degrees")] private float maxAssistAngle = 30f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float maxAssistRange = 30f;
    private HittableTarget assistTarget; 

    private const float velocityEpsilon = 0.01f; // below this speed, the object counts as no longer moving
    private float startTime;
    
    protected ThrowData Data { get; private set; } // everything about the release that produced this throw. see ThrowData and ThrowKinematics at the top of this file 
    protected Vector3 CurrentVelocity { get; set; }
    protected Transform Target => targetTransform; // the object being moved 
    protected Collider TargetCollider => targetCollider; // ^ its collider
    protected HittableTarget AssistTarget => assistTarget; // what aim assist is steering toward, if anything

    public event Action OnStopped; 
    public bool IsSimulating { get; private set; }

    protected virtual void Awake()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }

        if (targetCollider == null)
        {
            targetCollider = GetComponent<Collider>();
        }

        if (targetCollider == null || targetCollider.attachedRigidbody == null)
        {
            Debug.LogWarning("[ThrowPhysics] no collider with a Rigidbody, collisions will be ignored", this);
        }
    }

    // begins simulation once the object has been thrown
    public void Initialize(ThrowData data, ThrowInteractable _parentInteractable)
    {
        parentInteractable = _parentInteractable;
        
        Data = data;
        CurrentVelocity = data.heldObject.velocity;
        Begin();

        FindAssistTarget();

        startTime = Time.time;
        IsSimulating = true;
    }

    // optional hook for a subclass to derive its own starting state from Data (before simulation begins)
    protected virtual void Begin() { }

    // moves the object one frame. call TryMove() to actually apply the movement 
    protected abstract void Step();

    // cleanup after the object has stopped moving 
    protected abstract void Stop();

    private void Update()
    {
        if (!IsSimulating)
        {
            return;
        }

        if (timeout >= 0f && Time.time - startTime >= timeout)
        {
            StopSimulating();
            return;
        }

        if (CurrentVelocity.sqrMagnitude < velocityEpsilon * velocityEpsilon)
        {
            StopSimulating();
            return;
        }

        Step();
    }
    
    // once per throw (no rehoming mid-flight). checks the nearest target within the cone around the throw direction 
    private void FindAssistTarget()
    {
        assistTarget = null;

        if (aimAssist <= 0f 
            || TargetSpawner.Instance == null 
            || CurrentVelocity.sqrMagnitude < velocityEpsilon * velocityEpsilon)
            return;

        assistTarget = TargetSpawner.Instance.FindAssistTarget(
            targetTransform.position, CurrentVelocity.normalized, maxAssistAngle, maxAssistRange);
    }
    
    private Vector3 ApplyAimAssist(Vector3 delta)
    {
        if (aimAssist <= 0f || assistTarget == null)
            return delta;

        float distance = delta.magnitude;
        Vector3 toTarget = assistTarget.transform.position - targetTransform.position;

        if (distance < Mathf.Epsilon || toTarget.sqrMagnitude < Mathf.Epsilon)
            return delta;

        return Vector3.Slerp(delta / distance, toTarget.normalized, aimAssist) * distance;
    }

    // returns whether the move succeeded (i.e. was not blocked by a hittable object)
    protected bool TryMove(Vector3 delta, Quaternion newRotation)
    {
        delta = ApplyAimAssist(delta);
        float distance = delta.magnitude;

        // check if it hit something 
        if (distance > Mathf.Epsilon && TryGetBlockingHit(delta / distance, distance, out RaycastHit hit))
        {
            // stop at the surface hit point 
            targetTransform.SetPositionAndRotation(targetTransform.position + delta.normalized * hit.distance, newRotation);
            StopSimulating();
            
            // notify the object that it was hit by me 
            if (hit.collider.gameObject.TryGetComponent(out HittableTarget hittableTarget))
            {
                hittableTarget.OnObjectHit(parentInteractable, hit);
            }
            
            return false;
        }

        // move 
        targetTransform.SetPositionAndRotation(targetTransform.position + delta, newRotation);
        return true;
    }
    
    private bool TryGetBlockingHit(Vector3 direction, float distance, out RaycastHit blockingHit)
    {
        blockingHit = default;
        bool found = false;
        
        Rigidbody body = targetCollider != null ? targetCollider.attachedRigidbody : null;
        if (body == null)
        {
            return false;
        }

        RaycastHit[] hits = body.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if ((collisionLayerMask.value & (1 << hits[i].collider.gameObject.layer)) == 0)
            {
                continue;
            }

            if (!found || hits[i].distance < blockingHit.distance)
            {
                blockingHit = hits[i];
                found = true;
            }
        }

        return found;
    }

    private void StopSimulating()
    {
        IsSimulating = false;
        CurrentVelocity = Vector3.zero;
        Stop();
        OnStopped?.Invoke();
    }
}
