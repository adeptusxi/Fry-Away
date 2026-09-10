using UnityEngine;

// project-wide switch for every DebugDisplay 
[CreateAssetMenu(fileName = "DebugDisplaySettings", menuName = "Debug/Debug Display Settings")]
public class DebugDisplaySettings : ScriptableObject
{
    [Tooltip("Turn every DebugDisplay in the project on or off")]
    [SerializeField] private bool showDebugDisplays = true;

    public bool ShowDebugDisplays => showDebugDisplays;
}
