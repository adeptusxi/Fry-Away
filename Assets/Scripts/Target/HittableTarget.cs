using UnityEngine;

public abstract class HittableTarget : MonoBehaviour
{
    [Header("Proximity")]
    [Tooltip("what this target moves toward and measures its distance against, such as the player. Can also be registered by a TargetSpawner")]
    [SerializeField] private Transform moveTo;

    [Tooltip("how close (in meters) this target can get to `moveTo` before OnTooClose() fires")]
    [SerializeField, Min(0f)] private float tooCloseDistance = 1.5f;

    [Header("Audio")]
    [SerializeField] private SoundId hitSoundId = SoundId.None;

    protected SoundId HitSoundId => hitSoundId;

    private bool reachedTarget;

    protected Transform MoveTo => moveTo;

    // callback for when this target is hit by something thrown. The thrown object will call this; the target is only responsible for implementing what happens after.
    // hitBy: the thing that hit this object 
    // hit: the hit data, which contains information such as the position and direction (see Unity RaycastHit definition) 
    public abstract void OnObjectHit(ThrowInteractable hitBy, RaycastHit hit);
    
    public void RegisterMoveTo(Transform target)
    {
        moveTo = target;
        reachedTarget = false;

        if (moveTo != null)
        {
            OnMoveToRegistered();
        }
    }

    private void Update()
    {
        Move();

        if (reachedTarget || moveTo == null)
        {
            return;
        }

        if ((transform.position - moveTo.position).sqrMagnitude <= tooCloseDistance * tooCloseDistance)
        {
            reachedTarget = true;
            OnTooClose();
        }
    }
    
    #region Subclass hooks
    
    protected virtual void OnMoveToRegistered() { }
    
    protected virtual void Move() { }
    
    protected virtual void OnTooClose() { }
    
    #endregion 

}