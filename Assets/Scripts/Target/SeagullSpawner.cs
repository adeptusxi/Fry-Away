using UnityEngine;
using Random = UnityEngine.Random;

public class SeagullSpawner : TargetSpawner
{
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverHeightMin = 2f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverHeightMax = 4f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverRadiusMin = 1f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverRadiusMax = 4f;
    [SerializeField, Range(0f, 360f)] private float hoverYawRange = 360f;

    [SerializeField, Min(0f), Tooltip("in meters. hovering seagulls try to space out at least this much")] private float minHoverSeparation = 1.2f;
    [SerializeField, Min(1)] private int hoverPlacementAttempts = 8;
    
    public Vector3 PickHoverOffset(HittableSeagull asking)
    {
        Vector3 candidate = Vector3.zero;

        for (int attempt = 0; attempt < hoverPlacementAttempts; attempt++)
        {
            candidate = RandomHoverOffset();

            if (IsClearOfOtherSeagulls(candidate, asking))
            {
                return candidate;
            }
        }

        return candidate;
    }

    protected override void ConfigureTarget(HittableTarget target)
    {
        if (target is HittableSeagull seagull)
        {
            seagull.RegisterHoverSpace(this);
        }
    }

    private Vector3 RandomHoverOffset()
    {
        float yaw = Random.Range(-hoverYawRange * 0.5f, hoverYawRange * 0.5f);
        Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * SectorCenter();

        return direction * Random.Range(hoverRadiusMin, hoverRadiusMax)
            + Vector3.up * Random.Range(hoverHeightMin, hoverHeightMax);
    }
    
    private Vector3 SectorCenter()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;

        return forward.sqrMagnitude > Mathf.Epsilon ? forward.normalized : Vector3.forward;
    }
    
    private bool IsClearOfOtherSeagulls(Vector3 candidate, HittableSeagull asking)
    {
        if (minHoverSeparation <= 0f || !ConeOrigin)
        {
            return true;
        }

        Vector3 candidateWorld = ConeOrigin.position + candidate;
        float minSeparationSqr = minHoverSeparation * minHoverSeparation;

        for (int i = 0; i < CurrentTargets.Count; i++)
        {
            if (!CurrentTargets[i])
            {
                continue;
            }

            if (CurrentTargets[i] is not HittableSeagull other || other == asking || !other.IsHovering)
            {
                continue;
            }

            if ((other.HoverAnchor - candidateWorld).sqrMagnitude < minSeparationSqr)
            {
                return false;
            }
        }

        return true;
    }

    private void OnValidate()
    {
        hoverHeightMax = Mathf.Max(hoverHeightMin, hoverHeightMax);
        hoverRadiusMax = Mathf.Max(hoverRadiusMin, hoverRadiusMax);
    }

#if UNITY_EDITOR
    // debug gizmos draws the hovering area 
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (ConeOrigin == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        DrawHoverRing(hoverHeightMin, hoverRadiusMin);
        DrawHoverRing(hoverHeightMin, hoverRadiusMax);
        DrawHoverRing(hoverHeightMax, hoverRadiusMin);
        DrawHoverRing(hoverHeightMax, hoverRadiusMax);

        if (hoverYawRange < 360f)
        {
            DrawSectorEdge(-hoverYawRange * 0.5f);
            DrawSectorEdge(hoverYawRange * 0.5f);
        }
    }

    private void DrawHoverRing(float height, float radius)
    {
        const int segments = 24;

        Vector3 center = ConeOrigin.position + Vector3.up * height;
        Vector3 first = Vector3.zero;
        Vector3 previous = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float yaw = Mathf.Lerp(-hoverYawRange * 0.5f, hoverYawRange * 0.5f, i / (float)segments);
            Vector3 point = center + Quaternion.AngleAxis(yaw, Vector3.up) * SectorCenter() * radius;

            if (i == 0)
            {
                first = point;
            }
            else
            {
                Gizmos.DrawLine(previous, point);
            }

            previous = point;
        }

        if (hoverYawRange >= 360f)
        {
            Gizmos.DrawLine(previous, first);
        }
    }

    private void DrawSectorEdge(float yaw)
    {
        Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * SectorCenter();

        Gizmos.DrawLine(
            ConeOrigin.position + Vector3.up * hoverHeightMin + direction * hoverRadiusMin,
            ConeOrigin.position + Vector3.up * hoverHeightMax + direction * hoverRadiusMax
        );
    }
#endif
}
