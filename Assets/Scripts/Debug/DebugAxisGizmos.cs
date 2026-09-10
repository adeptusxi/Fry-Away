using UnityEngine;

// Draws all XYZ axes on the object 
public class DebugAxisGizmos : DebugDisplay
{
    [SerializeField] private float length = 0.25f;
    [SerializeField] private float width = 0.005f;

    private LineRenderer xLine;
    private LineRenderer yLine;
    private LineRenderer zLine;

    protected override void Initialize()
    {
        xLine = CreateLineRenderer("X Axis");
        yLine = CreateLineRenderer("Y Axis");
        zLine = CreateLineRenderer("Z Axis");

        SetupLine(xLine, Color.red);
        SetupLine(yLine, Color.green);
        SetupLine(zLine, Color.blue);
    }

    private LineRenderer CreateLineRenderer(string objectName)
    {
        GameObject axisObject = new GameObject(objectName);
        axisObject.transform.SetParent(transform, false);

        return axisObject.AddComponent<LineRenderer>();
    }

    private void SetupLine(LineRenderer renderer, Color color)
    {
        renderer.useWorldSpace = true;
        renderer.positionCount = 2;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = color;
        renderer.endColor = color;

        Shader fallback = Shader.Find("Sprites/Default");
        if (fallback == null)
        {
            Debug.LogWarning(
                "[DebugAxisGizmos] no material assigned and Sprites/Default not found",
                this
            );
            return;
        }

        renderer.material = new Material(fallback);
    }

    private void LateUpdate()
    {
        Vector3 position = transform.position;

        xLine.SetPosition(0, position);
        xLine.SetPosition(1, position + transform.right * length);

        yLine.SetPosition(0, position);
        yLine.SetPosition(1, position + transform.up * length);

        zLine.SetPosition(0, position);
        zLine.SetPosition(1, position + transform.forward * length);
    }

    protected override void Cleanup()
    {
        if (xLine != null) Destroy(xLine.gameObject);
        if (yLine != null) Destroy(yLine.gameObject);
        if (zLine != null) Destroy(zLine.gameObject);
    }
}