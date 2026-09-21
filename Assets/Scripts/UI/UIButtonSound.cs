using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    [SerializeField] private SoundId clickSound = SoundId.UIButtonClick;

    [Tooltip("Enable for the Start button: loops until StopSustained() is called.")]
    [SerializeField] private bool sustainUntilStopped;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(PlaySound);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(PlaySound);
    }

    private void PlaySound()
    {
        if (sustainUntilStopped)
        {
            AudioManager.Instance?.PlayLoop(
                clickSound,
                AudioManager.LoopTrack.Loading
            );
        }
        else
        {
            AudioManager.Instance?.PlayOneShot2D(clickSound);
        }
    }

    public void StopSustained()
    {
        AudioManager.Instance?.StopLoop(
            AudioManager.LoopTrack.Loading
        );
    }
}