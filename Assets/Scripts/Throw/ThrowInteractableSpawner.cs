using UnityEngine;

public class ThrowInteractableSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject prefab;
    [SerializeField] private bool verbose;

    private ThrowInteractable current;
    private bool needsSpawn = true; // to defer spawn to the next Update
                                    // (avoid messing up interactor's iteration list as it's still iterating) 

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
        if (current == null)
        {
            needsSpawn = true;
        }

        if (needsSpawn)
        {
            Spawn();
        }
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
    }
    
    private void Release()
    {
        if (current != null)
        {
            current.OnThrown -= HandleThrown;
        }

        current = null;
    }

    private void HandleThrown()
    {
        needsSpawn = true;
    }

    private void OnDestroy()
    {
        Release();
    }
}
