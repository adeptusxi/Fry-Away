using UnityEngine;

public abstract class HittableTarget : MonoBehaviour
{
    // callback for when this target is hit by something thrown. The thrown object will call this; the target is only responsible for implementing what happens after.
    // hitBy: the thing that hit this object 
    // hit: the hit data, which contains information such as the position and direction (see Unity RaycastHit definition) 
    public abstract void OnObjectHit(ThrowInteractable hitBy, RaycastHit hit);
}
