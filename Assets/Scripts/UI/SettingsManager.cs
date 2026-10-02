using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    public enum PreferredHand
    {
        Left,
        Right
    }

    [Header("Toggles")]
    [SerializeField] private Toggle hapticsToggle;
    [SerializeField] private Toggle handToggle;

    private const string MasterKey = "settings.masterVolume";
    private const string MusicKey = "settings.musicVolume";
    private const string SfxKey = "settings.sfxVolume";
    private const string HapticsKey = "settings.hapticsEnabled";
    private const string HandKey = "settings.preferredHand";

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    public bool HapticsEnabled { get; private set; } = true;

    public PreferredHand CurrentHand { get; private set; }
        = PreferredHand.Right;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        MasterVolume = PlayerPrefs.GetFloat(
            MasterKey,
            1f
        );

        MusicVolume = PlayerPrefs.GetFloat(
            MusicKey,
            1f
        );

        SfxVolume = PlayerPrefs.GetFloat(
            SfxKey,
            1f
        );

        HapticsEnabled =
            PlayerPrefs.GetInt(HapticsKey, 1) == 1;

        CurrentHand =
            (PreferredHand)PlayerPrefs.GetInt(
                HandKey,
                (int)PreferredHand.Right
            );

        AudioManager.Instance?.SetMasterVolume(
            MasterVolume
        );

        AudioManager.Instance?.SetMusicVolume(
            MusicVolume
        );

        AudioManager.Instance?.SetSfxVolume(
            SfxVolume
        );

        RefreshToggles();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = value;

        PlayerPrefs.SetFloat(
            MasterKey,
            value
        );

        AudioManager.Instance?.SetMasterVolume(
            value
        );
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = value;

        PlayerPrefs.SetFloat(
            MusicKey,
            value
        );

        AudioManager.Instance?.SetMusicVolume(
            value
        );
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = value;

        PlayerPrefs.SetFloat(
            SfxKey,
            value
        );

        AudioManager.Instance?.SetSfxVolume(
            value
        );
    }

    public void SetHapticsEnabled(bool value)
    {
        HapticsEnabled = value;

        PlayerPrefs.SetInt(
            HapticsKey,
            value ? 1 : 0
        );

        RefreshToggles();
    }

    public void SelectLeftHand()
    {
        CurrentHand = PreferredHand.Left;

        PlayerPrefs.SetInt(
            HandKey,
            (int)PreferredHand.Left
        );

        RefreshToggles();
    }

    public void SelectRightHand()
    {
        CurrentHand = PreferredHand.Right;

        PlayerPrefs.SetInt(
            HandKey,
            (int)PreferredHand.Right
        );

        RefreshToggles();
    }

    public void SetRightHand(bool right)
    {
        if (right)
            SelectRightHand();
        else
            SelectLeftHand();
    }

    private void RefreshToggles()
    {
        if (hapticsToggle != null)
            hapticsToggle.SetIsOnWithoutNotify(HapticsEnabled);
        if (handToggle != null)
            handToggle.SetIsOnWithoutNotify(CurrentHand == PreferredHand.Right);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}