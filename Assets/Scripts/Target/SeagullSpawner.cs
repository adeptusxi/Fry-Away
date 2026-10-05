using UnityEngine;
using Random = UnityEngine.Random;

public class SeagullSpawner : TargetSpawner
{
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverHeightMin = 2f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverHeightMax = 4f;
    [SerializeField, Min(0f), Tooltip("in meters. random spread applied on top of the height implied by the bird's HeightBias")] private float hoverHeightJitter = 0.5f;
    [SerializeField, Range(0f, 1f), Tooltip("how strongly HeightBias pulls hover height toward hoverHeightMax/Min. 1 = full spread, 0 = everyone hovers near the midpoint")] private float hoverHeightBiasStrength = 0.6f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverRadiusMin = 1f;
    [SerializeField, Min(0f), Tooltip("in meters")] private float hoverRadiusMax = 4f;
    [SerializeField, Range(0f, 360f)] private float hoverYawRange = 360f;

    [SerializeField, Min(0f), Tooltip("in meters. hovering seagulls try to space out at least this much")] private float minHoverSeparation = 1.2f;
    [SerializeField, Min(1)] private int hoverPlacementAttempts = 8;

    [Header("Bread Attraction")]
    [SerializeField, Range(0f, 360f), Tooltip("seagulls won't notice or chase bread outside this yaw sector (centered on this spawner's forward), so they never get lured behind the player")]
    private float attractionYawRange = 180f;

    [Header("Entry Waypoint")]
    [SerializeField, Min(0f), Tooltip("in meters from the player. seagulls first fly to a point in front of the player, then approach as usual. 0 max disables the waypoint")] private float entryDistanceMin = 50f;
    [SerializeField, Min(0f), Tooltip("in meters from the player")] private float entryDistanceMax = 60f;
    [SerializeField, Range(0f, 180f), Tooltip("in degrees, total spread of the waypoint around the player's front")] private float entryYawRange = 90f;

    public bool TryPickEntryPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (!ConeOrigin || entryDistanceMax <= 0f)
        {
            return false;
        }

        float yaw = Random.Range(-entryYawRange * 0.5f, entryYawRange * 0.5f);
        Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * SectorCenter();

        point = ConeOrigin.position + direction * Random.Range(entryDistanceMin, entryDistanceMax);
        return true;
    }

    public bool IsWithinAttractionSector(Vector3 worldPosition)
    {
        if (!ConeOrigin)
        {
            return true;
        }

        Vector3 toPosition = worldPosition - ConeOrigin.position;
        toPosition.y = 0f;

        if (toPosition.sqrMagnitude < Mathf.Epsilon)
        {
            return true;
        }

        return Vector3.Angle(SectorCenter(), toPosition) <= attractionYawRange * 0.5f;
    }

    public Vector3 PickHoverOffset(HittableSeagull asking)
    {
        Vector3 candidate = Vector3.zero;

        for (int attempt = 0; attempt < hoverPlacementAttempts; attempt++)
        {
            candidate = RandomHoverOffset(asking);

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

    private Vector3 RandomHoverOffset(HittableSeagull asking)
    {
        float yaw = Random.Range(-hoverYawRange * 0.5f, hoverYawRange * 0.5f);
        Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * SectorCenter();

        float biasedHeight = Mathf.Lerp(hoverHeightMin, hoverHeightMax, asking.HeightBias);
        float midHeight = (hoverHeightMin + hoverHeightMax) * 0.5f;
        float dampedHeight = Mathf.Lerp(midHeight, biasedHeight, hoverHeightBiasStrength);
        float height = Mathf.Clamp(
            dampedHeight + Random.Range(-hoverHeightJitter, hoverHeightJitter),
            hoverHeightMin, hoverHeightMax);

        return direction * Random.Range(hoverRadiusMin, hoverRadiusMax)
            + Vector3.up * height;
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
        entryDistanceMax = Mathf.Max(entryDistanceMin, entryDistanceMax);
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

        if (attractionYawRange < 360f)
        {
            Gizmos.color = Color.red;
            DrawAttractionSectorEdge(-attractionYawRange * 0.5f);
            DrawAttractionSectorEdge(attractionYawRange * 0.5f);
        }
    }

    // shows how far bread can be noticed/chased before it's considered "behind" and ignored
    private void DrawAttractionSectorEdge(float yaw)
    {
        Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * SectorCenter();
        Gizmos.DrawLine(ConeOrigin.position, ConeOrigin.position + direction * MaxDistance);
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
