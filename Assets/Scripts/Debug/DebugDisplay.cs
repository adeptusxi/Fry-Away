using UnityEngine;

// base for the visual debugging components. all debug visuals can be toggled centrally from a DebugDisplaySettings SO 
public abstract class DebugDisplay : MonoBehaviour
{
    private const string settingsPath = "DebugDisplaySettings";
    private static bool resolved;
    private static bool displaysEnabled;

    public static bool DisplaysEnabled
    {
        get
        {
            if (resolved)
            {
                return displaysEnabled;
            }

            resolved = true;

            DebugDisplaySettings settings = Resources.Load<DebugDisplaySettings>(settingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"[DebugDisplay] no {settingsPath} found in a Resources folder, debug displays are off");
                displaysEnabled = false;
                return displaysEnabled;
            }

            displaysEnabled = settings.ShowDebugDisplays;
            return displaysEnabled;
        }
    }

    private void Awake()
    {
        if (!DisplaysEnabled)
        {
            enabled = false;
            Cleanup();
            Destroy(this);
            return;
        }

        Initialize();
    }

    protected virtual void Initialize() { }
    
    protected virtual void Cleanup() { }
}
