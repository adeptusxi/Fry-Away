using UnityEngine;

public class BobAnimation : MonoBehaviour
{
    public enum AnimationTarget
    {
        X,
        Y,
        Z,
        Scale
    }

    [SerializeField] private AnimationTarget target = AnimationTarget.Y;
    [SerializeField, Min(0f), Tooltip("in meters, positive/negative delta from start value. for scale, a fraction of original scale.")]
        private float amplitude = 0.1f;
    [SerializeField, Min(0.01f), Tooltip("in seconds, for one full back-and-forth")]
        private float period = 3f;

    [SerializeField] private bool randomizeOffset = true;

    private float restAxisValue;
    private Vector3 restScale;
    private float phase;
    private float elapsed;

    private void Awake()
    {
        if (target == AnimationTarget.Scale)
            restScale = transform.localScale;
        else
            restAxisValue = transform.localPosition[(int)target];

        if (randomizeOffset) phase = Random.Range(0f, 2f * Mathf.PI);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float offset = Mathf.Sin(elapsed * 2f * Mathf.PI / period + phase) * amplitude;

        if (target == AnimationTarget.Scale)
        {
            transform.localScale = restScale * (1f + offset);
            return;
        }

        Vector3 position = transform.localPosition;
        position[(int)target] = restAxisValue + offset;
        transform.localPosition = position;
    }
}
