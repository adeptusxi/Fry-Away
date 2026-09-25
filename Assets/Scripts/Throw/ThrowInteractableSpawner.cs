using System;
using UnityEngine;

// basic spawner: spawns at a fixed worldspace position
public class ThrowInteractableSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject prefab;
    [SerializeField] protected bool verbose;

    private ThrowInteractable current;
    private GameObject currentInstance;
    private bool active = false;
    private bool needsSpawn = true; // to defer spawn to the next Update
                                    // (avoid messing up interactor's iteration list as it's still iterating)

    protected Transform SpawnPoint => spawnPoint;

    // fired when the spawned object is thrown, with the object that was thrown. listeners that care
    // about where that throw ends up can subscribe to its OnFlightStopped themselves
    public event Action<ThrowInteractable> OnThrowableThrown;

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

    // destroys whatever is currently waiting to be picked up, so nothing is left over across a reset.
    // does not touch objects that have already been thrown
    public void DespawnCurrent()
    {
        GameObject instance = currentInstance;

        Release();

        if (instance != null)
        {
            Destroy(instance);
        }

        needsSpawn = true;
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
        currentInstance = instance;
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
        currentInstance = null;
    }

    // instance is the instantiated prefab root
    protected virtual void OnSpawned(GameObject instance, ThrowInteractable spawned) { } 

    protected virtual void OnReleased(ThrowInteractable released) { }

    private void HandleThrown()
    {
        needsSpawn = true;

        // notify before the replacement spawns, so a listener can Activate(false) to stop the refill
        OnThrowableThrown?.Invoke(current);
    }
}
