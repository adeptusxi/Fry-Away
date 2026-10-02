using UnityEngine;

public class BobAnimation : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("in meters, above and below the starting height")]
        private float amplitude = 0.1f;
    [SerializeField, Min(0.01f), Tooltip("in seconds, for one full up-down")]
        private float period = 3f;

    private float restHeight;
    private float phase;
    private float elapsed;

    private void Awake()
    {
        restHeight = transform.localPosition.y;
        phase = Random.Range(0f, 2f * Mathf.PI);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        Vector3 position = transform.localPosition;
        position.y = restHeight + Mathf.Sin(elapsed * 2f * Mathf.PI / period + phase) * amplitude;
        transform.localPosition = position;
    }
}
