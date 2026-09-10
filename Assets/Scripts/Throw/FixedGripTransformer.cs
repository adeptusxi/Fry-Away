using Oculus.Interaction;
using UnityEngine;

// holds a grabbed object at a fixed, specified pose relative to the grabbing hand. 
// assign this to the Grabbable's One Grab Transformer slot 
public class FixedGripTransformer : MonoBehaviour, ITransformer
{
    [Tooltip("Where the object sits relative to the grabbing hand, in the hand's local space.")]
    [SerializeField] private Vector3 heldPositionOffset;

    [Tooltip("How the object is oriented relative to the grabbing hand, in degrees.")]
    [SerializeField] private Vector3 heldRotationOffset;

    // this should be set by ThrowInteractable during a throw
    // (so while the interactor takes a few frames to "notice" the object has been released, the grip does not bring the object back to the hand) 
    public bool Suspended { get; set; }

    private IGrabbable grabbable;

    public void Initialize(IGrabbable grabbable)
    {
        this.grabbable = grabbable;
    }

    public void BeginTransform()
    {
        UpdateTransform();
    }

    public void UpdateTransform()
    {
        if (Suspended || grabbable == null || grabbable.GrabPoints.Count == 0)
        {
            return;
        }

        Pose grabPose = grabbable.GrabPoints[0];
        grabbable.Transform.SetPositionAndRotation(
            grabPose.position + grabPose.rotation * heldPositionOffset,
            grabPose.rotation * Quaternion.Euler(heldRotationOffset));
    }

    public void EndTransform() { }
}
