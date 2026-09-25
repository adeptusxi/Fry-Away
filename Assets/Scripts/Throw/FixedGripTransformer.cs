using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

// holds a grabbed object at a fixed, specified pose relative to the grabbing hand. 
// assign this to the Grabbable's One Grab Transformer slot 
public class FixedGripTransformer : MonoBehaviour, ITransformer
{
    [Tooltip("Where and what orientation the object sits relative to the grabbing hand, in the hand's local space. Right hand for reference.")]
    [SerializeField] private Transform heldOffsetTransform;

    [SerializeField] private Handedness fallbackHand = Handedness.Right;

    [SerializeField] private float h = 0.05f; // gizmo scale
    
    // this should be set by ThrowInteractable during a throw
    // (so while the interactor takes a few frames to "notice" the object has been released, the grip does not bring the object back to the hand) 
    public bool Suspended { get; set; }

    private IGrabbable grabbable;
    private Handedness handedness;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;

    public void Initialize(IGrabbable grabbable)
    {
        this.grabbable = grabbable;
        handedness = fallbackHand;

        if (heldOffsetTransform)
        {
            baseLocalPosition = heldOffsetTransform.localPosition;
            baseLocalRotation = heldOffsetTransform.localRotation;
        }
    }

    public void SetHandedness(Handedness hand)
    {
        handedness = hand;
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

        Vector3 positionOffset = baseLocalPosition;
        Quaternion rotationOffset = baseLocalRotation;

        if (handedness == Handedness.Left)
        {
            Mirror(ref positionOffset, ref rotationOffset);
        }

        Pose grabPose = grabbable.GrabPoints[0];
        grabbable.Transform.SetPositionAndRotation(
            grabPose.position + grabPose.rotation * positionOffset,
            grabPose.rotation * rotationOffset);
    }

    public void EndTransform() { }

    private static void Mirror(ref Vector3 position, ref Quaternion rotation)
    {
        position.x = -position.x;

        Vector3 forward = rotation * Vector3.forward;
        Vector3 up = rotation * Vector3.up;

        forward.x = -forward.x;
        up.x = -up.x;

        rotation = Quaternion.LookRotation(forward, up);
    }

    private void OnDrawGizmos()
    {
        if (!heldOffsetTransform)
        {
            return;
        }

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(Vector3.zero, h);

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.color = Color.red;
        Gizmos.matrix = grabbable == null
            ? heldOffsetTransform.localToWorldMatrix
            : grabbable.Transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(3 * h, h, 3 * h));
        Gizmos.matrix = old;
    }
}
