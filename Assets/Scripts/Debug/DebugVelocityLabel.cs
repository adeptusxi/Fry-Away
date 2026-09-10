using TMPro;
using UnityEngine;

// writes this object's current speed to a text label
public class DebugVelocityLabel : DebugDisplay
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private bool faceCamera = true;
    [SerializeField, Range(0f, 0.99f)] private float smoothing = 0.2f;

    private Vector3 lastPosition;
    private Vector3 velocity;
    private Camera playerCamera;

    protected override void Initialize()
    {
        if (label == null)
        {
            Debug.LogWarning("[DebugVelocityLabel] no label assigned, disabling", this);
            enabled = false;
            return;
        }

        lastPosition = transform.position;
        playerCamera = Camera.main;
    }
    
    private void LateUpdate()
    {
        if (Time.deltaTime > 0f)
        {
            Vector3 sample = (transform.position - lastPosition) / Time.deltaTime;
            velocity = Vector3.Lerp(sample, velocity, smoothing);
        }

        lastPosition = transform.position;
        label.text = $"{velocity.magnitude:F2} m/s";

        if (faceCamera && playerCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - playerCamera.transform.position);
        }
    }

    protected override void Cleanup()
    {
        Destroy(gameObject);
    }
}
