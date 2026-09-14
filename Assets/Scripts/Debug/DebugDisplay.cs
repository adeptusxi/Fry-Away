using UnityEngine;

// base for the visual debugging components. all debug visuals can be toggled centrally from the GameManager
public abstract class DebugDisplay : MonoBehaviour
{
    private static bool warned;

    public static bool DisplaysEnabled
    {
        get
        {
            if (GameManager.Instance != null)
            {
                return GameManager.Instance.ShowDebugDisplays;
            }

            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[DebugDisplay] no GameManager in the scene, debug displays are off by default");
            }

            return false;
        }
    }

    private void Start()
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
