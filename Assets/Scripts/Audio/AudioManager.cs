using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public enum LoopTrack
    {
        BGM,
        Ambient
    }

    public static AudioManager Instance { get; private set; }

    [Header("Audio Data")]
    [SerializeField] private SoundLibrary library;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource ambientSource;

    private readonly HashSet<SoundId> missingClipWarnings = new HashSet<SoundId>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayLoop(SoundId id, LoopTrack track)
    {
        if (!TryGetClip(id, out AudioClip clip, out float volume))
        {
            return;
        }

        AudioSource source =
            track == LoopTrack.BGM ? bgmSource : ambientSource;

        if (source == null)
        {
            Debug.LogWarning(
                $"[AudioManager] No AudioSource assigned for {track}."
            );
            return;
        }

        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.loop = true;
        source.Play();
    }

    public void StopLoop(LoopTrack track)
    {
        AudioSource source =
            track == LoopTrack.BGM ? bgmSource : ambientSource;

        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }
    }

    public void PlayOneShotAtPosition(
        SoundId id,
        Vector3 position,
        float intensity01 = 0f
    )
    {
        if (!TryGetClip(id, out AudioClip clip, out float volume, intensity01))
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    public AudioSource StartAttachedLoop(
        SoundId id,
        Transform target,
        float intensity01 = 0f
    )
    {
        if (target == null)
        {
            return null;
        }

        if (!TryGetClip(id, out AudioClip clip, out float volume, intensity01))
        {
            return null;
        }

        AudioSource source = target.gameObject.AddComponent<AudioSource>();

        source.clip = clip;
        source.volume = volume;
        source.loop = true;
        source.playOnAwake = false;

        // This sound comes from the moving object in 3D space.
        source.spatialBlend = 1f;

        source.Play();

        return source;
    }

    public void StopAttachedLoop(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        Destroy(source);
    }

    private bool TryGetClip(
    SoundId id,
    out AudioClip clip,
    out float volume,
    float intensity01 = 0f
    )
    {
        clip = null;
        volume = 1f;

        if (library == null || !library.TryGet(id, out SoundDefinition definition))
        {
            LogMissingOnce(id);
            return false;
        }

        clip = definition.GetClip(intensity01);
        volume = definition.Volume;

        if (clip == null)
        {
            LogMissingOnce(id);
            return false;
        }

        return true;
    }

    private void LogMissingOnce(SoundId id)
    {
        if (missingClipWarnings.Add(id))
        {
            Debug.Log(
                $"[AudioManager] No clip assigned for {id} yet."
            );
        }
    }
}