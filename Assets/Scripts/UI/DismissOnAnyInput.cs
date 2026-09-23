using UnityEngine;

public class DismissOnAnyInput : MonoBehaviour
{
    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
            OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            UIManager.Instance.OnGuideDismissed();
        }
    }
}