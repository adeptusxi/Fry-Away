using UnityEngine;

// prototype for flight: constant velocity in a straight line until the object hits something, then it's destroyed 
public class ThrowPhysicsLinear : ThrowPhysics
{
    [Tooltip("The object travels at its release velocity multiplied by this. Basically \"throw power\"")]
    [SerializeField] private float velocityScalar = 1f;

    protected override void Begin()
    {
        CurrentVelocity = Data.heldObject.velocity * velocityScalar;
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
