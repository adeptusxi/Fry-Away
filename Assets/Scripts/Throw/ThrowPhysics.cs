using System;
using UnityEngine;

// snapshot of how something was moving at the moment of release
public struct ThrowKinematics
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
}

// everything known about the moment a throw was released 
public struct ThrowData
{
    public ThrowKinematics hand;
    public ThrowKinematics heldObject;
    public ThrowKinematics peakHand;
    public ThrowKinematics peakHeld;
}

// "interface" for anything that moves a thrown object after it leaves the player's hand.
// defaults to moving the GameObject it is attached to, but can drive a separate one if specified 
public abstract class ThrowPhysics : MonoBehaviour
{
    [Tooltip("What to move")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("What to sweep for collisions. Needs a Rigidbody on it")]
    [SerializeField] private Collider targetCollider;

    [Tooltip("Layers which the object collides with while in flight")]
    [SerializeField] private LayerMask collisionLayerMask;

    [Tooltip("Max seconds to simulate before stopping automatically. <0 means no limit")]
    [SerializeField] private float timeout = 60f;
    
    private const float velocityEpsilon = 0.01f; // below this speed, the object counts as no longer moving
    private float startTime;
    
    protected ThrowData Data { get; private set; } // everything about the release that produced this throw. see ThrowData and ThrowKinematics at the top of this file 
    protected Vector3 CurrentVelocity { get; set; }
    protected Transform Target => targetTransform; // the object being moved 
    protected Collider TargetCollider => targetCollider; // ^ its collider 
    
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

    // begins simulation after a successful throw
    public void Initialize(ThrowData data)
    {
        Data = data;
        CurrentVelocity = data.heldObject.velocity;
        Begin();
        BeginSimulating();
    }

    // a release that happened before the throw timer completed 
    public void InitializeFailed(ThrowData data)
    {
        Data = data;
        CurrentVelocity = data.heldObject.velocity;

        if (OnThrowFailed())
        {
            BeginSimulating();
            return;
        }

        StopSimulating();
    }

    // optional hook for a subclass to derive its own starting state from Data (before simulation begins) 
    protected virtual void Begin() { }

    // override to handle a failed throw.
    // return true and set CurrentVelocity to continue moving. 
    // return false to route to Stop() 
    protected virtual bool OnThrowFailed()
    {
        return false;
    }

    private void BeginSimulating()
    {
        startTime = Time.time;
        IsSimulating = true;
    }

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
    
    // returns whether the move succeeded (i.e. was not blocked by a hittable object) 
    protected bool TryMove(Vector3 delta, Quaternion newRotation)
    {
        float distance = delta.magnitude;

        // check if it hit something 
        if (distance > Mathf.Epsilon && TryGetBlockingHit(delta / distance, distance, out RaycastHit hit))
        {
            // stop at the surface hit point 
            targetTransform.SetPositionAndRotation(targetTransform.position + delta.normalized * hit.distance, newRotation);
            StopSimulating();
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
