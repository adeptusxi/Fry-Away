// using NUnit.Framework;
using UnityEngine;

public class ThrowPhysicsFrisbee : ThrowPhysics
{
    [Tooltip("The object travels at its release velocity multiplied by this. Basically \"throw power\"")]
    [SerializeField] private float mHand = 1f;
    [SerializeField] private float rho = 1.2f; // air density in kg/m^3;usually ~1.2 kg/m^3 for dry air
    [Tooltip("Initial lift coefficient (try 0.13)")]
    [SerializeField] private float cli;
    // TODO: add c_lift_aoa once changing aoa is implemented
    [Tooltip("Initial drag coefficient (try 0.085)")]
    [SerializeField] private float cdi;
    [Tooltip("Frisbee radius (m) to approximate wing area")]
    [SerializeField] private float r;
    [Tooltip("Frisbee mass (kg)")]
    [SerializeField] private float mFrisbee;

    private float liftdragConstant; // rho * a/2 --> |L| = cl * v^2 * liftdragConstant, |D| = cd * v^2 * liftdragConstant
    private float aoa; // angle of attack (rad)
    // TODO: Clytie - implement changing aoa affecting lift & drag
    //private float liftMagnitude;
    //private float dragMagnitude;
    //private Vector3 vi; // (m/s)
    private Vector3 lift; // (N)
    private Vector3 drag; // (N)
    private Vector3 fgrav; // (N)
    private Vector3 angVelocity; // rad/s

    protected override void Begin()
    {
        // Assert.IsTrue(mFrisbee > 0);
        if (mFrisbee <= 0f)
        {
            Debug.LogError("[ThrowPhysicsFrisbee] Frisbee mass must be greater than 0.", this);
            return;
        }
        liftdragConstant = rho * Mathf.PI * Mathf.Pow(r, 2f) / 2f;
        // conservation of (angular) momentum
        // TODO: possibly use more data pts than just the last one?
        Vector3 dv = Data.hand.dv * mHand / mFrisbee;
        CurrentVelocity = Data.hand.dv * mHand / mFrisbee; // (CurrentVelocity + dv).magnitude * ???(angVelocity).normalized; // quaternions use RHR
        angVelocity = Data.hand.angularVelocity * mHand / mFrisbee;
        // gravity
        fgrav = mFrisbee * 9.81f * Vector3.down;

        CalculatePhysics();
    }

    protected override void Step()
    {
        /* TODO: Clytie - implement frisbee physics (gravity, lift, drag, tilt, etc.)
         * 
         * the Data variable from the parent ThrowPhysics.cs class holds all the information about the frisbee and the hand. 
         * a dummy example is in ThrowPhysicsLinear.cs 
         */
        CalculatePhysics();
        float dt = Time.deltaTime;
        Vector3 dv = (lift + drag + fgrav) * dt / mFrisbee;
        CurrentVelocity += dv;
        TryMove(CurrentVelocity * dt, Quaternion.AngleAxis(angVelocity.magnitude*dt*Mathf.Rad2Deg, angVelocity.normalized) * Target.rotation);
    }

    protected private void CalculatePhysics()
    {
        // aerodynamics
        aoa = -transform.rotation.eulerAngles.x * Mathf.Deg2Rad; // neg sign from Unity using LHR
        lift = cli * Mathf.Pow(CurrentVelocity.magnitude, 2f) * liftdragConstant * transform.up;
        drag = cdi * Mathf.Pow(CurrentVelocity.magnitude, 2f) * liftdragConstant * (-CurrentVelocity.normalized);
        // TODO: update angular velocity
    }

    protected override void Stop()
    {
        // TODO: Xiao - make this look better 
        Destroy(Target.gameObject);
    }
}
