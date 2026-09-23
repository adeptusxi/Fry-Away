using Oculus.Interaction;
using UnityEngine;

// holds a grabbed object at a fixed, specified pose relative to the grabbing hand. 
// assign this to the Grabbable's One Grab Transformer slot 
public class FixedGripTransformer : MonoBehaviour, ITransformer
{
    [SerializeField] private float h = 0.05f; // gizmo scale
    [Tooltip("Where the object sits relative to the grabbing hand, in the hand's local space.")]
    [SerializeField] private Transform heldPositionOffsetTransform;
    [SerializeField] private Vector3 heldPositionOffset;

    [Tooltip("How the object is oriented relative to the grabbing hand, in degrees.")]
    [SerializeField] private Quaternion heldRotationOffset;

    // this should be set by ThrowInteractable during a throw
    // (so while the interactor takes a few frames to "notice" the object has been released, the grip does not bring the object back to the hand) 
    public bool Suspended { get; set; }

    private IGrabbable grabbable;

    public void Initialize(IGrabbable grabbable)
    {
        this.grabbable = grabbable;
        heldRotationOffset = heldPositionOffsetTransform.localRotation;
        heldPositionOffset = heldPositionOffsetTransform.localPosition;
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
        heldRotationOffset = heldPositionOffsetTransform.localRotation;
        heldPositionOffset = heldPositionOffsetTransform.localPosition;

        Pose grabPose = grabbable.GrabPoints[0];
        grabbable.Transform.SetPositionAndRotation(
            grabPose.position + grabPose.rotation * heldPositionOffset,
            grabPose.rotation * heldRotationOffset);
    }

    public void EndTransform() { }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(heldPositionOffset, new Vector3(3*h, h, 3*h));
        Gizmos.color = Color.red;
        Matrix4x4 old = Gizmos.matrix;
        Matrix4x4 handMatrix = grabbable == null ? heldPositionOffsetTransform.localToWorldMatrix : grabbable.Transform.localToWorldMatrix;
        Gizmos.matrix = handMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(3*h, h, 3*h));
        Gizmos.matrix = old;
        //Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 1, 1));
    }
}
