using Oculus.Interaction;
using UnityEngine.EventSystems;

public class XRPointableCanvasModule : PointableCanvasModule
{
    protected override void ProcessMove(PointerEventData pointerEvent)
    {
        HandlePointerExitAndEnter(pointerEvent, pointerEvent.pointerCurrentRaycast.gameObject);
    }
}
