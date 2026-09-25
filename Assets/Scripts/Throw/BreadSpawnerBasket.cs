using Oculus.Interaction.Input;

using UnityEngine;



// spawns bread in a basket attached to the non-dominant controller. handedness selected from UI.

public class BreadSpawnerBasket : ThrowInteractableSpawner

{

    [SerializeField, Tooltip("Point on the basket where it attaches to the controller")]

    private Transform attachPoint;



    private Handedness dominant = Handedness.Right;

    private bool attached;



    private GameObject currentBread; 

    private ThrowInteractable currentInteractable;

    

    private void Start() {

        AttachBasket();

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

        Handedness target = dominant == Handedness.Right ? Handedness.Left : Handedness.Right;

        Transform anchor = ControllerAnchor.Get(target);



        if (anchor == null)

        {

            Debug.LogError($"[BreadSpawnerBasket] no ControllerAnchor found for {target}, can't attach", this);

            return;

        }



        Quaternion relativeRot = attachPoint != null

            ? Quaternion.Inverse(transform.rotation) * attachPoint.rotation

            : Quaternion.identity;



        transform.SetParent(anchor, false);

        transform.rotation = anchor.rotation * Quaternion.Inverse(relativeRot);

        transform.localPosition = Vector3.zero;



        if (attachPoint != null)

        {

            transform.position += anchor.position - attachPoint.position;

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

