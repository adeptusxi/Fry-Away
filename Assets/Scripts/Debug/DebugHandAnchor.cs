using System.Collections.Generic;
using UnityEngine;

// marks a hand or controller anchor in the scene for prefabs to referencev
public class DebugHandAnchor : DebugDisplay
{
    private static readonly List<DebugHandAnchor> anchors = new();

    public static Transform GetNearest(Vector3 position)
    {
        Transform nearest = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < anchors.Count; i++)
        {
            float distance = (anchors[i].transform.position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = anchors[i].transform;
            }
        }

        return nearest;
    }

    private void OnEnable()
    {
        anchors.Add(this);
    }

    private void OnDisable()
    {
        anchors.Remove(this);
    }
}
