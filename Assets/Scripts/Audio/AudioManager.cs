using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public enum LoopTrack
    {
        BGM,
        Ambient,
        Loading
    }

    public static AudioManager Instance { get; private set; }

    [Header("BGM Fades")]
    [Tooltip("Seconds for background music to fade in.")]
    [SerializeField, Min(0f)] private float bgmFadeInSeconds = 1f;

    [Tooltip("Seconds for background music to fade out.")]
    [SerializeField, Min(0f)] private float bgmFadeOutSeconds = 1f;

    private Coroutine bgmTransition;

    [Header("Audio Data")]
    [SerializeField] private SoundLibrary library;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource loadingSource;
    [SerializeField] private AudioSource sfx2DSource;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    private readonly HashSet<SoundId> missingClipWarnings =
        new HashSet<SoundId>();

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

    // ---------- Loop Sources ----------

    private AudioSource GetLoopSource(LoopTrack track)
    {
        return track switch
        {
            LoopTrack.BGM => bgmSource,
            LoopTrack.Ambient => ambientSource,
            LoopTrack.Loading => loadingSource,
            _ => null
        };
    }
    public void PlayLoop(SoundId id, LoopTrack track)
    {
        if (!TryGetClip(id, out AudioClip clip, out float volume))
        {
            return;
        }

        AudioSource source = GetLoopSource(track);

        if (source == null)
        {
            Debug.LogWarning(
                $"[AudioManager] No AudioSource assigned for {track}."
            );
            return;
        }

        if (track == LoopTrack.BGM)
        {
            CancelBgmTransition();
            bgmTransition = StartCoroutine(
                TransitionBgm(source, clip, volume)
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
        AudioSource source = GetLoopSource(track);

        if (source == null)
        {
            return;
        }

        if (track == LoopTrack.BGM)
        {
            CancelBgmTransition();
            bgmTransition = StartCoroutine(
                TransitionBgm(source, null, 0f)
            );
            return;
        }

        source.Stop();
        source.clip = null;
    }

    // Used when a cue must start or stop without the normal music fades.
    public void PlayBgmImmediately(SoundId id)
    {
        if (bgmSource == null || !TryGetClip(id, out AudioClip clip, out float volume))
        {
            return;
        }

        CancelBgmTransition();
        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.volume = volume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBgmImmediately()
    {
        CancelBgmTransition();
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    // A separate source lets the game wait for or cancel this cue alone.
    public AudioSource PlayTrackedOneShot2D(SoundId id)
    {
        if (sfx2DSource == null || !TryGetClip(id, out AudioClip clip, out float volume))
        {
            return null;
        }

        GameObject cue = new GameObject($"TrackedOneShot_{id}");
        cue.transform.SetParent(transform, false);
        AudioSource source = cue.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = sfx2DSource.outputAudioMixerGroup;
        source.volume = volume * sfx2DSource.volume;
        source.mute = sfx2DSource.mute;
        source.clip = clip;
        source.Play();
        return source;
    }

    private void CancelBgmTransition()
    {
        if (bgmTransition != null)
        {
            StopCoroutine(bgmTransition);
            bgmTransition = null;
        }
    }

    private IEnumerator TransitionBgm(
        AudioSource source, AudioClip nextClip, float volume)
    {
        if (source.isPlaying)
        {
            yield return FadeBgm(source, 0f, bgmFadeOutSeconds);
        }

        if (source == null)
        {
            yield break;
        }

        source.Stop();
        source.clip = nextClip;

        if (nextClip != null)
        {
            source.loop = true;
            source.volume = 0f;
            source.Play();

            yield return FadeBgm(source, volume, bgmFadeInSeconds);
        }

        bgmTransition = null;
    }

    private IEnumerator FadeBgm(
        AudioSource source, float targetVolume, float seconds)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            if (source == null)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                Mathf.Clamp01(elapsed / seconds)
            );

            yield return null;
        }

        if (source != null)
        {
            source.volume = targetVolume;
        }
    }


    // ---------- Volume Controls ----------


    public void SetMasterVolume(float value)
    {
        SetMixerVolume("MasterVol", value);
    }

    public void SetMusicVolume(float value)
    {
        SetMixerVolume("MusicVol", value);
    }

    public void SetSfxVolume(float value)
    {
        SetMixerVolume("SfxVol", value);
    }

    private void SetMixerVolume(string parameter, float value)
    {
        if (mixer == null)
        {
            return;
        }

        float dB =
            value > 0.0001f
                ? Mathf.Log10(value) * 20f
                : -80f;

        mixer.SetFloat(parameter, dB);
    }

    // ---------- 3D One-Shot SFX ----------

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

        GameObject temp = new GameObject($"OneShot_{id}");
        temp.transform.position = position;

        AudioSource source = temp.AddComponent<AudioSource>();

        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.outputAudioMixerGroup = sfxMixerGroup;

        source.Play();

        Destroy(temp, clip.length);
    }

    // ---------- 2D One-Shot SFX ----------

    public void PlayOneShot2D(
        SoundId id,
        float intensity01 = 0f
    )
    {
        if (!TryGetClip(id, out AudioClip clip, out float volume, intensity01))
        {
            return;
        }

        if (sfx2DSource == null)
        {
            Debug.LogWarning(
                "[AudioManager] No AudioSource assigned for 2D one-shots."
            );
            return;
        }

        sfx2DSource.PlayOneShot(clip, volume);
    }

    // Get the length of a sound clip.
    public float GetClipLength(
        SoundId id,
        float intensity01 = 0f
    )
    {
        if (library == null ||
            !library.TryGet(id, out SoundDefinition definition))
        {
            return 0f;
        }

        AudioClip clip = definition.GetClip(intensity01);

        if (clip == null)
        {
            return 0f;
        }

        return clip.length;
    }

    // Stop the current 2D one-shot sound.
    public void StopOneShot2D()
    {
        if (sfx2DSource != null)
        {
            sfx2DSource.Stop();
        }
    }

    // ---------- Attached / Moving SFX ----------

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
        source.spatialBlend = 1f;

        // Route moving-object sounds through SFX volume.
        source.outputAudioMixerGroup = sfxMixerGroup;

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

    // ---------- Sound Lookup ----------

    private bool TryGetClip(
        SoundId id,
        out AudioClip clip,
        out float volume,
        float intensity01 = 0f
    )
    {
        clip = null;
        volume = 1f;

        if (library == null ||
            !library.TryGet(id, out SoundDefinition definition))
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