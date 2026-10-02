using TMPro;
using UnityEngine;

public class HitPointsPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    
    [Header("Text Format")]
    [SerializeField] private string format = "+{0}";
    [SerializeField, TextArea] private string longShotFormat = "LONG SHOT!\n+{0}";
    [SerializeField] private Color longShotColor = new(1f, 0.8f, 0.2f);

    [Header("Motion")]
    [SerializeField, Min(0.01f), Tooltip("in seconds")] private float lifetime = 1.2f;
    [SerializeField, Min(0f), Tooltip("world scale per meter of distance from the player")] 
        private float sizePerMeter = 0.04f;
    [SerializeField, Tooltip("relative to hit point")] 
        private float startHeight = 0.5f;
    [SerializeField] private float riseHeight = 1f;
    [SerializeField] private AnimationCurve scaleOverLife = new(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 1f));
    [SerializeField] private AnimationCurve alphaOverLife = new(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

    private Vector3 origin;
    private float elapsed;
    private Camera playerCamera;

    public void Show(int points, bool isLongShot)
    {
        origin = transform.position;
        elapsed = 0f;
        playerCamera = Camera.main;

        if (label != null)
        {
            label.text = string.Format(isLongShot ? longShotFormat : format, points);

            if (isLongShot)
            {
                label.color = longShotColor;
            }
        }

        Apply(0f);
    }

    private void LateUpdate()
    {
        elapsed += Time.deltaTime; 

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        Apply(elapsed / lifetime);
    }

    private void Apply(float progress)
    {
        float scale = 1f;

        if (playerCamera != null)
        {
            Vector3 fromPlayer = origin - playerCamera.transform.position;

            scale = fromPlayer.magnitude * sizePerMeter;

            if (fromPlayer.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(fromPlayer);
            }
        }

        transform.localScale = Vector3.one * (scale * scaleOverLife.Evaluate(progress));
        transform.position = origin + Vector3.up * ((startHeight + riseHeight * progress) * scale);

        if (label != null)
        {
            label.alpha = alphaOverLife.Evaluate(progress);
        }
    }
}
