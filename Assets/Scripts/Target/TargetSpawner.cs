using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// spawns hittable targets at random intervals inside a cone whose tip is at the coneOrigin 
public class TargetSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HittableTarget hittableTarget;
    [SerializeField] private Transform coneOrigin;

    [Header("Timing")]
    [SerializeField, Tooltip("in seconds")] private float initialSpawnDelay = 3f;
    [SerializeField, Min(0f), Tooltip("in seconds")] private float minSpawnInterval = 1.5f;
    [SerializeField, Min(0f), Tooltip("in seconds")] private float maxSpawnInterval = 3.5f;

    [Header("Cone")]
    [SerializeField, Min(0f), Tooltip("in meters")] private float minDistance = 8f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float maxDistance = 20f;
    [SerializeField, Range(0f, 180f), Tooltip("in meters")] private float coneAngle = 45f;

    [Tooltip("targets never spawn below this transform's height. leave empty for no floor")]
    [SerializeField] private Transform floor;

    [Tooltip("how far above the floor (meters) the lowest target can spawn, so it isn't half-buried")]
    [SerializeField, Min(0f)] private float floorClearance = 1f;

    [Header("Limits")]
    [SerializeField, Min(1)] private int maxTargets = 15;

    [Header("Debug")]
    [SerializeField] protected bool verbose;

    private bool active = false;
    private readonly List<HittableTarget> currTargets = new();
    private float nextSpawnTime;

    public static TargetSpawner Instance { get; private set; }
    public event Action<Vector3> OnTargetSpawned;

    public Transform ConeOrigin => coneOrigin;

    public IReadOnlyList<HittableTarget> CurrentTargets => currTargets; // may contain nulls between prunes. reader should check anything read from here

    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public bool HasFloor => floor != null;
    public float MinSpawnHeight => floor != null ? floor.position.y + floorClearance : float.NegativeInfinity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;

        if (hittableTarget == null)
        {
            Debug.LogError("[TargetSpawner] no target prefab assigned, disabling", this);
            enabled = false;
            return;
        }

        if (coneOrigin == null)
        {
            Debug.LogError("[TargetSpawner] no coneOrigin transform assigned, disabling", this);
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (!active || Time.time < nextSpawnTime) 
            return;

        nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
        
        PruneCurrTargets();

        if (currTargets.Count >= maxTargets)
        {
            if (verbose)
            {
                Debug.Log($"[TargetSpawner] at cap ({maxTargets} targets), skipping spawn", this);
            }

            return;
        }

        Spawn();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Activate(bool activate)
    {
        active = activate;
        if (activate)
            nextSpawnTime = Time.time + initialSpawnDelay;
    }

    public void ClearTargets()
    {
        for (int i = currTargets.Count - 1; i >= 0; i--)
        {
            if (currTargets[i] != null)
            {
                Destroy(currTargets[i].gameObject);
            }
        }

        currTargets.Clear();
    }

    private void Spawn()
    {
        Vector3 dir = RandomDirectionInCone();
        float dist = Random.Range(minDistance, maxDistance);
        Vector3 pos = coneOrigin.position + dir * dist;

        if (floor != null && pos.y < MinSpawnHeight)
        {
            // mirror vertically 
            dir.y = -dir.y;
            pos = coneOrigin.position + dir * dist;
            pos.y = Mathf.Max(pos.y, MinSpawnHeight);
        }

        Quaternion rot = Quaternion.LookRotation(-dir, Vector3.up);

        HittableTarget target = Instantiate(hittableTarget, pos, rot);
        target.RegisterMoveTo(coneOrigin);
        ConfigureTarget(target);
        currTargets.Add(target);

        OnTargetSpawned?.Invoke(pos);

        if (verbose)
        {
            Debug.Log($"[TargetSpawner] spawned {target.name} at {pos}", this);
        }
    }

    // called after RegisterMoveTo, before the target is tracked
    protected virtual void ConfigureTarget(HittableTarget target) { }

    private Vector3 RandomDirectionInCone()
    {
        float cosMax = Mathf.Cos(coneAngle * Mathf.Deg2Rad);
        float cosTheta = Random.Range(cosMax, 1f);
        float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));
        float phi = Random.Range(0f, 2f * Mathf.PI);

        Vector3 local = new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), cosTheta);

        return transform.rotation * local;
    }

    // unit direction on the cone's rim, phi radians around the axis
    public Vector3 RimDirection(float phi)
    {
        float sinMax = Mathf.Sin(coneAngle * Mathf.Deg2Rad);
        float cosMax = Mathf.Cos(coneAngle * Mathf.Deg2Rad);

        return transform.rotation * new Vector3(sinMax * Mathf.Cos(phi), sinMax * Mathf.Sin(phi), cosMax);
    }

    // the target closest to the line of a throw, or null if nothing qualifies 
    public HittableTarget FindAssistTarget(Vector3 origin, Vector3 direction, float maxAngle, float maxRange)
    {
        HittableTarget best = null;
        float bestAngle = maxAngle; 
        float maxRangeSqr = maxRange * maxRange;

        for (int i = 0; i < currTargets.Count; i++)
        {
            HittableTarget candidate = currTargets[i];

            if (candidate == null)
                continue;

            Vector3 toCandidate = candidate.transform.position - origin;
            float distanceSqr = toCandidate.sqrMagnitude;

            if (distanceSqr > maxRangeSqr || distanceSqr < Mathf.Epsilon)
                continue;

            float angle = Vector3.Angle(direction, toCandidate);
            if (angle > bestAngle)
                continue;

            bestAngle = angle;
            best = candidate;
        }

        return best;
    }

    // unregister targets that were destroyed
    private void PruneCurrTargets()
    {
        for (int i = currTargets.Count - 1; i >= 0; i--)
        {
            if (currTargets[i] == null)
            {
                currTargets.RemoveAt(i);
            }
        }
    }

    private void OnValidate()
    {
        maxSpawnInterval = Mathf.Max(minSpawnInterval, maxSpawnInterval);
        maxDistance = Mathf.Max(minDistance, maxDistance);
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (coneOrigin == null)
        {
            return;
        }

        Vector3 tip = coneOrigin.position;
        Vector3 axis = transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(tip + axis * minDistance, tip + axis * maxDistance);

        Vector3 firstNear = Vector3.zero;
        Vector3 firstFar = Vector3.zero;
        Vector3 previousNear = Vector3.zero;
        Vector3 previousFar = Vector3.zero;

        int ringSegments = 24;

        for (int i = 0; i < ringSegments; i++)
        {
            float phi = (2f * Mathf.PI * i) / ringSegments;
            Vector3 rim = RimDirection(phi);

            Vector3 near = tip + rim * minDistance;
            Vector3 far = tip + rim * maxDistance;

            if (i % (ringSegments / 4) == 0)
            {
                Gizmos.DrawLine(tip, far);
            }

            if (i == 0)
            {
                firstNear = near;
                firstFar = far;
            }
            else
            {
                Gizmos.DrawLine(previousNear, near);
                Gizmos.DrawLine(previousFar, far);
            }

            previousNear = near;
            previousFar = far;
        }

        Gizmos.DrawLine(previousNear, firstNear);
        Gizmos.DrawLine(previousFar, firstFar);
    }
#endif 
}
