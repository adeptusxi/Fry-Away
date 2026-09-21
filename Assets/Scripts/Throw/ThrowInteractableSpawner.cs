using UnityEngine;

// basic spawner: spawns at a fixed worldspace position 
public class ThrowInteractableSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject prefab;
    [SerializeField] protected bool verbose;

    private ThrowInteractable current;
    private bool active = false;
    private bool needsSpawn = true; // to defer spawn to the next Update
                                    // (avoid messing up interactor's iteration list as it's still iterating)

    protected Transform SpawnPoint => spawnPoint;

    private void Awake()
    {
        if (prefab == null)
        {
            Debug.LogError("[ThrowInteractableSpawner] no prefab assigned, disabling", this);
            enabled = false;
            return;
        }

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        if (current == null)
        {
            needsSpawn = true;
        }

        if (needsSpawn)
        {
            Spawn();
        }
    }
    
    private void OnDestroy()
    {
        Release();
    }
    
    public void Activate(bool activate)
    {
        active = activate;
    }
    
    private void Spawn()
    {
        needsSpawn = false;

        Release();

        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        ThrowInteractable spawned = instance.GetComponentInChildren<ThrowInteractable>(true);
        
        if (spawned == null || !spawned.isActiveAndEnabled)
        {
            Debug.LogError(
                $"[ThrowInteractableSpawner] {prefab.name} has no usable ThrowInteractable (missing, inactive, or disabled), disabling", this);
            Destroy(instance);
            enabled = false;
            return;
        }

        current = spawned;
        current.OnThrown += HandleThrown;

        if (verbose)
        {
            Debug.Log($"[ThrowInteractableSpawner] spawned {instance.name}");
        }

        OnSpawned(instance, current);
    }

    private void Release()
    {
        if (current != null)
        {
            current.OnThrown -= HandleThrown;
            OnReleased(current);
        }

        current = null;
    }

    // instance is the instantiated prefab root
    protected virtual void OnSpawned(GameObject instance, ThrowInteractable spawned) { } 

    protected virtual void OnReleased(ThrowInteractable released) { }

    private void HandleThrown()
    {
        needsSpawn = true;
    }
}
