using UnityEngine;

// prototype for flight: constant velocity in a straight line until the object hits something, then it's destroyed. 
// travel direction is the controller forward 
public class ThrowPhysicsLinear : ThrowPhysics
{
    [Tooltip("The object travels at its release velocity multiplied by this. Basically \"throw power\"")]
    [SerializeField] private float velocityScalar = 1f;

    protected override void Begin()
    {
        Vector3 direction = Data.hand.rotation * Vector3.forward;
        if (direction.sqrMagnitude < 0.0001f)
        {
            Stop();
            return;
        }
        
        CurrentVelocity = direction.normalized * Data.peakHand.velocity.magnitude * velocityScalar;
    }

    protected override void Step()
    {
        TryMove(CurrentVelocity * Time.deltaTime, Target.rotation);
    }

    protected override void Stop()
    {
        Destroy(Target.gameObject);
    }
}
