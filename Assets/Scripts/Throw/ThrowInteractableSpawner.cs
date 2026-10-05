using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// basic spawner: keeps a queue of objects at a fixed worldspace position. the front one is grabbable  
public class ThrowInteractableSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject prefab;
    [SerializeField] private GameObject[] prefabs;
    [SerializeField, Min(1)] private int queueSize = 1;
    [SerializeField] protected bool verbose;

    private class Entry
    {
        public GameObject instance;
        public ThrowInteractable throwable;
        public int slot = -1; // queue idx it was last placed at
    }

    private readonly List<Entry> queue = new(); 
    private Entry front;
    private bool active = false;
    private int stock = -1; // <0 means infinite 
    private bool dirty = true; // to defer queue changes to the next Update
                               // (avoid messing up interactor's iteration list as it's still iterating)

    protected Transform SpawnPoint => spawnPoint;
    protected GameObject FrontInstance => front?.instance; // prefab instance root 

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
        bool changed = dirty;
        dirty = false;

        if (queue.RemoveAll(entry => entry.throwable == null) > 0)
        {
            changed = true;
        }

        if (active)
        {
            int target = stock < 0 ? queueSize : Mathf.Min(queueSize, stock);

            while (queue.Count < target && Spawn())
            {
                changed = true;
            }
        }

        if (changed)
        {
            RefreshQueue();
        }
    }

    private void OnDestroy()
    {
        ClearFront();

        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].throwable != null)
            {
                OnReleased(queue[i].throwable);
            }
        }

        queue.Clear();
    }

    public void Activate(bool activate)
    {
        active = activate;
    }

    // <0 means no limit
    public void SetStock(int total)
    {
        stock = total;

        while (stock >= 0 && queue.Count > stock)
        {
            Remove(queue.Count - 1, true);
        }

        dirty = true;
    }

    // does not touch objects that have already been thrown
    public void DespawnCurrent()
    {
        while (queue.Count > 0)
        {
            Remove(queue.Count - 1, true);
        }

        dirty = true;
    }

    private bool Spawn()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("[ThrowInteractableSpawner] has empty prefabs list, disabling", this);
            enabled = false;
            return false;
        }

        prefab = prefabs[Random.Range(0, prefabs.Length)];
        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        ThrowInteractable spawned = instance.GetComponentInChildren<ThrowInteractable>(true);

        if (spawned == null || !spawned.isActiveAndEnabled)
        {
            Debug.LogError(
                $"[ThrowInteractableSpawner] {prefab.name} has no usable ThrowInteractable (missing, inactive, or disabled), disabling", this);
            Destroy(instance);
            enabled = false;
            return false;
        }

        queue.Add(new Entry { instance = instance, throwable = spawned });

        if (verbose)
        {
            Debug.Log($"[ThrowInteractableSpawner] spawned {instance.name}");
        }

        OnSpawned(instance, spawned);
        return true;
    }

    private void RefreshQueue()
    {
        for (int i = 0; i < queue.Count; i++)
        {
            Entry entry = queue[i];
            entry.throwable.SetGrabEnabled(i == 0);

            if (entry.slot != i)
            {
                entry.slot = i;
                PlaceInQueue(entry.instance.transform, i);
            }
        }

        Entry next = queue.Count > 0 ? queue[0] : null;

        if (next != front)
        {
            ClearFront();
            front = next;

            if (front != null)
            {
                front.throwable.OnThrown += HandleThrown;
            }
        }
    }

    private void ClearFront()
    {
        if (front == null)
        {
            return;
        }

        if (front.throwable != null)
        {
            front.throwable.OnThrown -= HandleThrown;
        }

        front = null;
    }

    private void Remove(int index, bool destroy)
    {
        Entry entry = queue[index];
        queue.RemoveAt(index);

        if (entry == front)
        {
            ClearFront();
        }

        if (entry.throwable != null)
        {
            OnReleased(entry.throwable);
        }

        if (destroy && entry.instance != null)
        {
            Destroy(entry.instance);
        }
    }

    // instance is the instantiated prefab root
    protected virtual void OnSpawned(GameObject instance, ThrowInteractable spawned) { }

    // called when an object leaves the queue for any reason
    protected virtual void OnReleased(ThrowInteractable released) { }

    // index 0 is the front
    protected virtual void PlaceInQueue(Transform instance, int index)
    {
        instance.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
    }

    private void HandleThrown()
    {
        ThrowInteractable thrown = front.throwable;

        thrown.OnThrown -= HandleThrown;
        queue.Remove(front);
        OnReleased(thrown);

        if (stock > 0)
        {
            stock--;
        }

        dirty = true;

        // notify before the replacement spawns, so a listener can Activate(false) to stop the refill
        OnThrowableThrown?.Invoke(thrown);
    }
}
