using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SoundLibrary",
    menuName = "Audio/Sound Library"
)]
public class SoundLibrary : ScriptableObject
{
    [SerializeField]
    private List<SoundDefinition> definitions = new List<SoundDefinition>();

    public bool TryGet(SoundId id, out SoundDefinition definition)
    {
        foreach (SoundDefinition item in definitions)
        {
            if (item != null && item.Id == id)
            {
                definition = item;
                return true;
            }
        }

        definition = null;
        return false;
    }
}