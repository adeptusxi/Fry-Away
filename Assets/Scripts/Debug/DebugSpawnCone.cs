using UnityEngine;

// draws a TargetSpawner's spawn cone. put on the same GameObject as the spawner 
public class DebugSpawnCone : DebugDisplay
{
    [SerializeField] private float width = 0.02f;
    [SerializeField, Min(4)] private int ringSegments = 24;
    [SerializeField] private Color coneColor = Color.cyan;
    [SerializeField] private Color floorColor = Color.cyan;
    
    [SerializeField] private Color spawnPointColor = Color.cyan;
    [SerializeField] private float spawnPointSize = 0.3f;
    [SerializeField, Min(0f)] private float spawnPointDuration = 1f;

    private const int rimRayCount = 4;

    private TargetSpawner spawner;
    private LineRenderer axisLine;
    private LineRenderer nearRing;
    private LineRenderer farRing;
    private LineRenderer floorRing;
    private LineRenderer[] rimRays;

    protected override void Initialize()
    {
        spawner = GetComponent<TargetSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[DebugSpawnCone] no TargetSpawner on this object, disabling", this);
            enabled = false;
            return;
        }

        axisLine = CreateLine("Cone Axis", 2, false, coneColor);
        nearRing = CreateLine("Near Ring", ringSegments, true, coneColor);
        farRing = CreateLine("Far Ring", ringSegments, true, coneColor);
        floorRing = CreateLine("Floor Ring", ringSegments, true, floorColor);

        rimRays = new LineRenderer[rimRayCount];
        for (int i = 0; i < rimRayCount; i++)
        {
            rimRays[i] = CreateLine($"Rim Ray {i}", 2, false, coneColor);
        }

        spawner.OnTargetSpawned += HandleTargetSpawned;
    }

    private void LateUpdate()
    {
        if (spawner.ConeOrigin == null)
        {
            return;
        }

        Vector3 tip = spawner.ConeOrigin.position;
        float minDistance = spawner.MinDistance;
        float maxDistance = spawner.MaxDistance;

        axisLine.SetPosition(0, tip + transform.forward * minDistance);
        axisLine.SetPosition(1, tip + transform.forward * maxDistance);

        for (int i = 0; i < ringSegments; i++)
        {
            float phi = (2f * Mathf.PI * i) / ringSegments;
            Vector3 rim = spawner.RimDirection(phi);

            nearRing.SetPosition(i, tip + rim * minDistance);
            farRing.SetPosition(i, tip + rim * maxDistance);
        }

        for (int i = 0; i < rimRayCount; i++)
        {
            Vector3 rim = spawner.RimDirection((2f * Mathf.PI * i) / rimRayCount);

            rimRays[i].SetPosition(0, tip);
            rimRays[i].SetPosition(1, tip + rim * maxDistance);
        }

        floorRing.enabled = spawner.HasFloor;
        if (!floorRing.enabled)
        {
            return;
        }

        float floorHeight = spawner.MinSpawnHeight;
        for (int i = 0; i < ringSegments; i++)
        {
            float phi = (2f * Mathf.PI * i) / ringSegments;
            Vector3 offset = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi)) * maxDistance;

            floorRing.SetPosition(i, new Vector3(tip.x + offset.x, floorHeight, tip.z + offset.z));
        }
    }

    private void HandleTargetSpawned(Vector3 position)
    {
        GameObject marker = new GameObject("Spawn Marker");
        marker.transform.SetPositionAndRotation(position, Quaternion.identity);

        DrawMarkerAxis(marker.transform, Vector3.right);
        DrawMarkerAxis(marker.transform, Vector3.up);
        DrawMarkerAxis(marker.transform, Vector3.forward);

        Destroy(marker, spawnPointDuration);
    }

    private void DrawMarkerAxis(Transform parent, Vector3 axis)
    {
        GameObject lineObject = new GameObject("Marker Axis");
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        SetupLine(line, 2, false, spawnPointColor);

        line.SetPosition(0, parent.position - axis * (spawnPointSize * 0.5f));
        line.SetPosition(1, parent.position + axis * (spawnPointSize * 0.5f));
    }

    private LineRenderer CreateLine(string objectName, int positionCount, bool loop, Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        SetupLine(line, positionCount, loop, color);

        return line;
    }

    private void SetupLine(LineRenderer renderer, int positionCount, bool loop, Color color)
    {
        renderer.useWorldSpace = true;
        renderer.positionCount = positionCount;
        renderer.loop = loop;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = color;
        renderer.endColor = color;

        Shader fallback = Shader.Find("Sprites/Default");
        if (fallback == null)
        {
            Debug.LogWarning("[DebugSpawnCone] no material assigned and Sprites/Default not found", this);
            return;
        }

        renderer.material = new Material(fallback);
    }

    protected override void Cleanup()
    {
        if (axisLine != null) Destroy(axisLine.gameObject);
        if (nearRing != null) Destroy(nearRing.gameObject);
        if (farRing != null) Destroy(farRing.gameObject);
        if (floorRing != null) Destroy(floorRing.gameObject);

        if (rimRays != null)
        {
            for (int i = 0; i < rimRays.Length; i++)
            {
                if (rimRays[i] != null) Destroy(rimRays[i].gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.OnTargetSpawned -= HandleTargetSpawned;
        }
    }
}
