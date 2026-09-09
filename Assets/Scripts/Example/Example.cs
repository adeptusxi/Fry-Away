using UnityEngine;

public class Example : MonoBehaviour
{
    [SerializeField] private bool verbose;
    [SerializeField] private int exampleVariable;
    
    /*
     * Unity functions (Awake, Start, OnEnable, OnDisable, OnDestroy, Update) are called automatically by Unity.
     * 
     * Execution order of these when a scene opens in the game:
     * 1) Awake() runs once on all MonoBehaviours, regardless of whether they're enabled or disabled. 
     * 2) Start() runs once on all enabled MonoBehaviours. 
     * 3) Every frame, Update() runs on all enabled MonoBehaviours. 
     * 
     * Note: Ordering between different MonoBehaviours is not guaranteed within any of these steps.
     *      You can assume  : A's Awake() finishes before B's Start()
     *      You can't assume: A's Awake() finishes before B's Awake()
     */

    // Awake() runs when this component is loaded (i.e. the first time the scene loads, before anything happens). 
    void Awake()
    {
        // Put one-time setup here, especially setup that other components depend on 
        // e.g. initialize variables, cache references, create internal state  
    }

    // Start() runs once before the first Update() call, after all Awake() calls are done. 
    void Start()
    {
        // Put setup here that assumes other objects have finished their Awake() setup 
        // e.g. begin gameplay logic, find other objects, subscribe to events 
    }

    // OnEnable() runs every time this component/GameObject becomes enabled.
    // note: a component/GameObject can be enabled/disabled multiple times during its life 
    void OnEnable()
    {
        // Put logic here that needs to happen each time the object is activated 
        // e.g. subscribe to events, reset temporary state, start listeners 
    }

    // OnDisable() runs every time this component/GameObject becomes disabled. 
    // note: a component/GameObject can be enabled/disabled multiple times.
    // disabling is not the same as destroying; it just turns the component/script off temporarily 
    void OnDisable()
    {
        // Undo anything started in OnEnable(), especially event subscriptions 
        // e.g. unsubscribe from events, stop listeners, clean up temporary state 
    }

    // OnDestroy() runs when this component or its GameObject is destroyed. 
    // no code in this class will ever run for the attached GameObject after OnDestroy() returns 
    void OnDestroy()
    {
        // Put final cleanup here for things that shouldn't outlive this object.
        // e.g. release resources, unregister from other systems, save final state 
    }

    // Update() runs once per frame while this component is enabled 
    void Update()
    {
        // Put per-frame gameplay logic here 
        // e.g. read input, update positions of moving objects, check timers, update gameplay state 
        // this runs every frame !! so avoid putting expensive/slow code here 
    }

    // Example of a regular non-Unity function 
    void ExampleFunction()
    {
        if (verbose)
        {
            // ideally, gate regular logging behind a toggleable boolean 
            Debug.Log("blahblahblah");
        }
    }
}