using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Music and Sound effects sliders in the pause menu.
/// Each slider goes from 0 (silent) to 1 (full). AudioManager does the real work and saves the values.
/// Put this on the same always-active object as PauseMenu (like MouseSensitivity).
/// </summary>
public class VolumeSliders : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private float nextSample;   // real time when the next sample click may play (so dragging doesn't machine-gun)

    private void Start()
    {
        // Show the saved values without firing the change event, then start listening
        musicSlider.SetValueWithoutNotify(AudioManager.MusicSetting);
        sfxSlider.SetValueWithoutNotify(AudioManager.SfxSetting);

        musicSlider.onValueChanged.AddListener(AudioManager.SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnDestroy()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(AudioManager.SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
    }

    private void OnSfxChanged(float value)
    {
        AudioManager.SetSfxVolume(value);

        // A sample click so you hear the new level, at most every 0.15 s while dragging
        if (Time.unscaledTime >= nextSample)
        {
            nextSample = Time.unscaledTime + 0.15f;
            AudioManager.Play(Sfx.ButtonClick);
        }
    }
}