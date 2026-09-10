using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;

// marks a hand or controller anchor in the scene for prefabs to reference
public class ControllerAnchor : MonoBehaviour
{
    [SerializeField] private Handedness handedness;

    private static readonly List<ControllerAnchor> anchors = new();

    public static Transform Get(Handedness handedness)
    {
        for (int i = 0; i < anchors.Count; i++)
        {
            if (anchors[i].handedness == handedness)
            {
                return anchors[i].transform;
            }
        }

        return null;
    }

    // handedness-blind, only for readouts. anything gameplay-facing should use Get()
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
