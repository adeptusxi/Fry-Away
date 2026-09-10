using Oculus.Interaction;
using TMPro;
using UnityEngine;

// shows where the held object sits relative to the grab
// (for finding values for FixedGripTransformer's fields)
public class DebugGripPose : DebugDisplay
{
    [SerializeField] private Grabbable grabbable;
    [SerializeField] private TMP_Text label;

    protected override void Initialize()
    {
        if (grabbable == null || label == null)
        {
            Debug.LogError("[DebugGripPose] missing required grabbable and label", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        Transform target = grabbable.Transform;
        Transform hand = ControllerAnchor.GetNearest(target.position);

        if (hand == null)
        {
            label.text = "no hand found";
            return;
        }

        Pose grabPose = new Pose(hand.position, hand.rotation);
        
        Quaternion inverse = Quaternion.Inverse(grabPose.rotation);
        Vector3 localPosition = inverse * (target.position - grabPose.position);
        Vector3 localEuler = (inverse * target.rotation).eulerAngles;

        label.text =
            $"pos ({localPosition.x:F2}, {localPosition.y:F2}, {localPosition.z:F2})\n" +
            $"rot ({localEuler.x:F1}, {localEuler.y:F1}, {localEuler.z:F1})";
    }

    protected override void Cleanup()
    {
        Destroy(gameObject);
    }
}
