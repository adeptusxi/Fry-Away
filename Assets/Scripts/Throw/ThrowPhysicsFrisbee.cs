using UnityEngine;

public class ThrowPhysicsFrisbee : ThrowPhysics
{
    protected override void Step()
    {
        /* TODO: Clytie - implement frisbee physics (gravity, lift, drag, tilt, etc.)
         * 
         * the Data variable from the parent ThrowPhysics.cs class holds all the information about the frisbee and the hand. 
         * a dummy example is in ThrowPhysicsLinear.cs 
         */
    }

    protected override void Stop()
    {
        // TODO: Xiao - make this look better 
        Destroy(Target.gameObject);
    }
}
