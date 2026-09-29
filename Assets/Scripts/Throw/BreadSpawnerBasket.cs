using Oculus.Interaction.Input;
using Oculus.Platform.Models;
using UnityEngine;
using UnityEngine.UIElements;



// spawns bread in a basket attached to the non-dominant controller. handedness selected from UI.

public class BreadSpawnerBasket : ThrowInteractableSpawner

{

    [SerializeField, Tooltip("Point on the basket where it attaches to the controller")]
    private Transform attachPoint;
    private Transform attachPosition;
    [SerializeField, Tooltip("For debugging scenes")]
    private bool activateOnStart = false;

    private Handedness dominant = Handedness.Right;

    private bool attached;



    private GameObject currentBread; 

    private ThrowInteractable currentInteractable;

    

    private void Start() {

        AttachBasket();
        if (activateOnStart) { Activate(true);  }

    }



    public void SetHandedness(bool right)

    {

        Handedness previous = dominant;

        dominant = right ? Handedness.Right : Handedness.Left;



        if (verbose)

        {

            Debug.Log($"[BreadSpawnerBasket] dominant hand set to {dominant}");

        }



        if (attached && previous != dominant)

        {

            AttachBasket();

        }

    }



    public void AttachBasket()

    {
        if (!attachPoint)
        {
            Debug.LogError($"[BreadSpawnerBasket] no attachPoint found, can't attach", this);
            return;
        }
        if (attachPosition != null) { Destroy(attachPosition.gameObject); }

        Handedness target = dominant == Handedness.Right ? Handedness.Left : Handedness.Right;

        Transform anchor = ControllerAnchor.Get(target);



        if (anchor == null)

        {

            Debug.LogError($"[BreadSpawnerBasket] no ControllerAnchor found for {target}, can't attach", this);

            return;

        }
        attachPosition = new GameObject("AttachPositionReference").transform;
        attachPosition.SetParent(attachPoint.parent, false);
        Vector3 position = attachPoint.localPosition;
        Quaternion rotation = attachPoint.localRotation;
        if (target == Handedness.Left) // TODO clean up (reusable code in FixedGripTransformer, also is hardcoded for starting on right hand and only one switch
        {
            // Mirror
            position.x = -position.x;
            
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            forward.x = -forward.x;
            up.x = -up.x;

            rotation = Quaternion.LookRotation(forward, up);
        }

        attachPosition.localPosition = position;
        attachPosition.localRotation = rotation;

        Quaternion relativeRot = attachPosition != null

            ? Quaternion.Inverse(transform.rotation) * attachPosition.rotation

            : Quaternion.identity;


        transform.SetParent(anchor, false);

        transform.rotation = anchor.rotation * Quaternion.Inverse(relativeRot);

        transform.localPosition = Vector3.zero;



        if (attachPoint != null)

        {

            transform.position += anchor.position - attachPosition.position;

        }



        attached = true;

        Activate(true);



        if (verbose)

        {

            Debug.Log($"[BreadSpawnerBasket] attached to {target} controller ({anchor.name})");

        }
    }



    public void DetachBasket()

    {

        transform.SetParent(null, true);

        attached = false;



        if (verbose)

        {

            Debug.Log("[BreadSpawnerBasket] detached");

        }

    }



    protected override void OnSpawned(GameObject instance, ThrowInteractable spawned)

    {

        currentBread = instance;

        currentInteractable = spawned;

        instance.transform.SetParent(SpawnPoint, true);

        spawned.OnGrabbed += HandleBreadGrabbed;

    }



    protected override void OnReleased(ThrowInteractable released)

    {

        released.OnGrabbed -= HandleBreadGrabbed;



        if (released == currentInteractable)

        {

            currentBread = null;

            currentInteractable = null;

        }

    }



    private void HandleBreadGrabbed()

    {

        if (currentBread == null)

        {

            return;

        }



        currentBread.transform.SetParent(null, true);



        if (verbose)

        {

            Debug.Log($"[BreadSpawnerBasket] {currentBread.name} taken out of basket");

        }

    }

}

