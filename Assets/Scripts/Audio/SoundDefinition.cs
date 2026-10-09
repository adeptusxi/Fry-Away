using UnityEngine;

public enum VariantSelection
{
    Single,
    Random,
    IntensityBands,
    RandomNoRepeat = 3
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

    [System.NonSerialized] private AudioClip lastRandomClip;

    public SoundId Id => id;
    public float Volume => volume;

    public AudioClip GetClip(float intensity01 = 0f)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int index = SelectClipIndex(intensity01);
        if (index < 0)
        {
            return null;
        }

        AudioClip clip = clips[index];
        if (variantSelection == VariantSelection.RandomNoRepeat)
        {
            lastRandomClip = clip;
        }
        return clip;
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

            case VariantSelection.RandomNoRepeat:
                return SelectNonRepeatingIndex();

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

    // Shared by every UI element using this definition; empty slots are skipped.
    private int SelectNonRepeatingIndex()
    {
        int selectedIndex = -1;
        int fallbackIndex = -1;
        int candidateCount = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip candidate = clips[i];
            if (candidate == null)
            {
                continue;
            }

            fallbackIndex = i;
            if (candidate == lastRandomClip)
            {
                continue;
            }

            candidateCount++;
            if (Random.Range(0, candidateCount) == 0)
            {
                selectedIndex = i;
            }
        }

        // A group with just one distinct clip can still play that clip.
        return selectedIndex >= 0 ? selectedIndex : fallbackIndex;
    }
}
