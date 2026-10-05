using System.Collections.Generic;
using UnityEngine;

// a thrown object whose model can be taken off it 
// e.g. a bread to be caught by seagulls 
public class Catchable : MonoBehaviour
{
    [SerializeField] private Transform modelRoot;
    [SerializeField] private ThrowInteractable throwable;
    
    private bool taken;

    public bool Taken => taken;
    public bool IsInFlight { get; private set; }
    public bool IsGrounded { get; private set; } // lying on the ground after a throw 
    public bool IsAvailable => IsInFlight || IsGrounded;
    
    private static readonly List<Catchable> available = new(); 
    
    public static Catchable FindNearestAvailable(Vector3 position, float flightRadius, float groundedRadius)
    {
        Catchable best = null;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < available.Count; i++)
        {
            Catchable candidate = available[i];

            if (!candidate || candidate.taken)
            {
                continue;
            }

            float radius = candidate.IsGrounded ? groundedRadius : flightRadius;
            float distanceSqr = (candidate.transform.position - position).sqrMagnitude;

            if (distanceSqr > radius * radius || distanceSqr > bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            best = candidate;
        }

        return best;
    }

    public static void DestroyAllGrounded()
    {
        for (int i = available.Count - 1; i >= 0; i--)
        {
            Catchable candidate = available[i];

            if (candidate && candidate.IsGrounded)
            {
                Destroy(candidate.gameObject);
            }
        }
    }
    
    #region Unity
    
    private void Awake()
    {
        if (throwable == null)
        {
            throwable = GetComponentInChildren<ThrowInteractable>(true);
        }

        if (throwable == null)
        {
            Debug.LogError("[Catchable] no ThrowInteractable found, this can never be caught", this);
            enabled = false;
            return;
        }

        if (modelRoot == null)
        {
            Debug.LogError("[Catchable] no modelRoot assigned, there is nothing to hand over", this);
        }

        throwable.OnThrown += EnterInFlight;
        throwable.OnFlightStopped += LeaveInFlight;
        throwable.OnLanded += EnterGrounded;
    }

    private void OnDestroy()
    {
        if (throwable != null)
        {
            throwable.OnThrown -= EnterInFlight;
            throwable.OnFlightStopped -= LeaveInFlight;
            throwable.OnLanded -= EnterGrounded;
        }

        LeaveInFlight();
        LeaveGrounded();
    }
    
    #endregion
    
    // returns false if someone already caught this Catchable 
    public bool TryGiveTo(Transform attachTo)
    {
        if (taken || !attachTo)
        {
            return false;
        }

        taken = true;

        Transform model = modelRoot;
        modelRoot = null;

        LeaveInFlight();
        LeaveGrounded();

        if (model)
        {
            model.SetParent(attachTo, true);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
        }

        // may have been "snatched" mid-air instead of hitting hitbox 
        if (throwable)
            throwable.StopFlight();

        Destroy(gameObject);

        return true;
    }

    private void EnterInFlight()
    {
        if (IsInFlight || taken)
        {
            return;
        }

        IsInFlight = true;
        available.Add(this);
    }

    private void LeaveInFlight()
    {
        if (!IsInFlight)
        {
            return;
        }

        IsInFlight = false;
        available.Remove(this);
    }

    private void EnterGrounded()
    {
        if (IsGrounded || taken)
        {
            return;
        }

        IsGrounded = true;
        available.Add(this);
    }

    private void LeaveGrounded()
    {
        if (!IsGrounded)
        {
            return;
        }

        IsGrounded = false;
        available.Remove(this);
    }
}
