using UnityEngine;

public enum VariantSelection
{
    Single,
    Random,
    IntensityBands
}

[CreateAssetMenu(
    fileName = "SoundDefinition",
    menuName = "Audio/Sound Definition"
)]
public class SoundDefinition : ScriptableObject
{
    [SerializeField] private SoundId id = SoundId.None;

    [SerializeField] private AudioClip[] clips;

    [SerializeField, Range(0f, 1f)]
    private float volume = 1f;

    [SerializeField]
    private VariantSelection variantSelection = VariantSelection.Single;

    public SoundId Id => id;
    public float Volume => volume;

    public AudioClip GetClip(float intensity01 = 0f)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int index = SelectClipIndex(intensity01);
        return clips[index];
    }

    public int SelectClipIndex(float intensity01 = 0f)
    {
        if (clips == null || clips.Length == 0)
        {
            return 0;
        }

        switch (variantSelection)
        {
            case VariantSelection.Random:
                return Random.Range(0, clips.Length);

            case VariantSelection.IntensityBands:
                int index = Mathf.FloorToInt(
                    Mathf.Clamp01(intensity01) * clips.Length
                );
                return Mathf.Clamp(index, 0, clips.Length - 1);

            case VariantSelection.Single:
            default:
                return 0;
        }
    }
}