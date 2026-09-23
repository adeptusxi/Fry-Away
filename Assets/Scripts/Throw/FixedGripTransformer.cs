using Oculus.Interaction;
using Oculus.Interaction.Input;
using Unity.VisualScripting;
using UnityEngine;

// holds a grabbed object at a fixed, specified pose relative to the grabbing hand. 
// assign this to the Grabbable's One Grab Transformer slot 
public class FixedGripTransformer : MonoBehaviour, ITransformer
{
    [SerializeField] private float h = 0.05f; // gizmo scale
    [Tooltip("Where and what orientation the object sits relative to the grabbing hand, in the hand's local space. Right hand for reference.")]
    [SerializeField] private Transform heldOffsetTransform;
    private Vector3 heldPositionOffset;

    //[Tooltip("How the object is oriented relative to the grabbing hand, in degrees.")]
    private Quaternion heldRotationOffset;

    // this should be set by ThrowInteractable during a throw
    // (so while the interactor takes a few frames to "notice" the object has been released, the grip does not bring the object back to the hand) 
    public bool Suspended { get; set; }

    private IGrabbable grabbable;
    private Handedness handedness = Handedness.Right;
    [SerializeField] private bool leftHand = false; // for testing only

    public void Initialize(IGrabbable grabbable)
    {
        this.grabbable = grabbable;
        // for testing only
        if (leftHand) ChangeHandedness(Handedness.Left);
        handedness = leftHand ? Handedness.Left : Handedness.Right;
        //
        //heldRotationOffset = heldOffsetTransformR.localRotation;
        //heldPositionOffset = heldOffsetTransformR.localPosition;
    }

    public void ChangeHandedness(Handedness h)
    {
        if (handedness != h)
        {
            Vector3 pos = heldOffsetTransform.localPosition;
            Vector3 mirroredPos = new Vector3(-pos.x, pos.y, pos.z);
            Vector3 forward = heldOffsetTransform.localRotation * Vector3.forward;
            Vector3 up = heldOffsetTransform.localRotation * Vector3.up;
            forward.x *= -1;
            up.x *= -1;
            Quaternion mirroredRot = Quaternion.LookRotation(forward, up);
            heldOffsetTransform.localPosition = mirroredPos;
            heldOffsetTransform.localRotation = mirroredRot;
            handedness = h;
        }
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

        heldRotationOffset = heldOffsetTransform.localRotation;
        heldPositionOffset = heldOffsetTransform.localPosition;

        Pose grabPose = grabbable.GrabPoints[0];
        grabbable.Transform.SetPositionAndRotation(
            grabPose.position + grabPose.rotation * heldPositionOffset,
            grabPose.rotation * heldRotationOffset);
    }

    public void EndTransform() { }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(Vector3.zero, h);
        Gizmos.color = Color.red;
        Matrix4x4 old = Gizmos.matrix;
        Matrix4x4 handMatrix = grabbable == null ? heldOffsetTransform.localToWorldMatrix : grabbable.Transform.localToWorldMatrix;
        Gizmos.matrix = handMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(3*h, h, 3*h));
        Gizmos.matrix = old;
        //Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 1, 1));
    }
}
