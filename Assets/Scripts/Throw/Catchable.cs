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
    
    private static readonly List<Catchable> inFlight = new(); // currently available Catchables 
    
    public static Catchable FindNearestInFlight(Vector3 position, float radius)
    {
        Catchable best = null;
        float bestDistanceSqr = radius * radius;

        for (int i = 0; i < inFlight.Count; i++)
        {
            Catchable candidate = inFlight[i];

            if (!candidate || candidate.taken)
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - position).sqrMagnitude;

            if (distanceSqr > bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            best = candidate;
        }

        return best;
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
    }

    private void OnDestroy()
    {
        if (throwable != null)
        {
            throwable.OnThrown -= EnterInFlight;
            throwable.OnFlightStopped -= LeaveInFlight;
        }

        LeaveInFlight();
    }
    
    #endregion
    
    // returns false if someone already caught this Catchable 
    public bool TryGiveTo(Transform attachTo)
    {
        if (taken || !modelRoot || !attachTo)
        {
            return false;
        }

        taken = true;

        Transform model = modelRoot;
        modelRoot = null;

        LeaveInFlight();

        model.SetParent(attachTo, true);
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        
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
        inFlight.Add(this);
    }

    private void LeaveInFlight()
    {
        if (!IsInFlight)
        {
            return;
        }

        IsInFlight = false;
        inFlight.Remove(this);
    }
}
