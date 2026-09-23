using UnityEngine;

public class DismissOnAnyInput : MonoBehaviour
{
    private bool canDismiss = false;

    private void OnEnable()
    {
        // When the Guide first appears, do not immediately accept
        // the trigger press that selected LEFT / RIGHT.
        canDismiss = false;
    }

    private void Update()
    {
        // First wait until both triggers have been released.
        if (!canDismiss)
        {
            bool primaryHeld =
                OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger);

            bool secondaryHeld =
                OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);

            if (!primaryHeld && !secondaryHeld)
            {
                canDismiss = true;
            }

            return;
        }

        // After release, require a NEW trigger press to dismiss Guide.
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
            OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            UIManager.Instance?.OnGuideDismissed();
        }
    }
}