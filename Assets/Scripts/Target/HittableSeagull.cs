using UnityEngine;

public class HittableSeagull : HittableTarget
{
    [Header("Flight")]
    [SerializeField, Min(0f), Tooltip("in m/s")] private float minSpeed = 2f;
    [SerializeField, Min(0f), Tooltip("in m/s")] private float maxSpeed = 5f;

    [Header("Weave")]
    [Tooltip("how far the flight path swings off course, in degrees to either side (0 means fly in a straight line)")]
    [SerializeField, Range(0f, 90f)] private float maxYawOffset = 25f;
   
    [SerializeField, Min(0f)] private float weaveFrequency = 0.35f;
    
    [Tooltip("0 is a perfect sine wave, 1 is an uneven non-repeating \"wander\"")]
    [SerializeField, Range(0f, 1f)] private float irregularity = 0.5f;

    private float speed;
    private float phaseOffset;
    private float noiseSeed;
    private Vector3 fallbackHeading;

    private void Awake()
    {
        speed = Random.Range(minSpeed, maxSpeed);
        phaseOffset = Random.Range(0f, 100f);
        noiseSeed = Random.Range(0f, 100f);
        fallbackHeading = transform.forward; 
    }

    public override void OnObjectHit(ThrowInteractable hitBy, RaycastHit hit)
    {
        // TODO: Shiyu - sound effects placeholder code 
    }

    protected override void OnMoveToRegistered()
    {
        transform.rotation = Quaternion.LookRotation(BaseHeading(), Vector3.up);
    }

    protected override void Move()
    {
        float t = (Time.time + phaseOffset) * weaveFrequency;
        float sine = Mathf.Sin(t * 2f * Mathf.PI); 
        float noise = Mathf.PerlinNoise(t, noiseSeed) * 2f - 1f; 
        float waveform = Mathf.Lerp(sine, noise, irregularity);
        Vector3 heading = Quaternion.AngleAxis(maxYawOffset * waveform, Vector3.up) * BaseHeading();

        transform.position += heading * (speed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    protected override void OnTooClose()
    {
        // TODO: how do we want to handle this 
        Destroy(gameObject);
    }

    private Vector3 BaseHeading()
    {
        if (MoveTo == null)
        {
            return fallbackHeading;
        }

        Vector3 toTarget = MoveTo.position - transform.position;
        return toTarget.sqrMagnitude > Mathf.Epsilon ? toTarget.normalized : fallbackHeading;
    }
}
