using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Controls persistent music and SFX mixer volumes and toggles the pause audio settings panel.
/// </summary>
public class AudioSettingsController : MonoBehaviour
{
    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SFXVolume";
    private const string MusicParameterName = "MusicVolume";
    private const string SfxParameterName = "SFXVolume";
    private const float DefaultVolume = 1f;
    private const float MinimumVolume = 0.0001f;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Volume Sliders")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Pause Audio Settings UI")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject audioSettingsPanel;

    private void Start()
    {
        float musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);
        float sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);

        SetSliderWithoutNotification(musicVolumeSlider, musicVolume);
        SetSliderWithoutNotification(sfxVolumeSlider, sfxVolume);
        ApplyMusicVolume(musicVolume);
        ApplySfxVolume(sfxVolume);

        HideAudioSettingsPanel();
    }

    private void OnEnable()
    {
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
    }

    private void OnDisable()
    {
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }
    }

    /// <summary>
    /// Opens the audio settings sub-panel from the pause panel.
    /// </summary>
    public void OpenAudioSettings()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.SetActive(true);
        }
    }

    private void HideAudioSettingsPanel()
    {
        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Closes the audio settings sub-panel and returns to the pause panel.
    /// </summary>
    public void CloseAudioSettings()
    {
        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.SetActive(false);
        }

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Applies and persists the music slider value.
    /// </summary>
    public void OnMusicVolumeChanged(float value)
    {
        ApplyMusicVolume(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, value);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Applies and persists the SFX slider value.
    /// </summary>
    public void OnSfxVolumeChanged(float value)
    {
        ApplySfxVolume(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, value);
        PlayerPrefs.Save();
    }

    private void ApplyMusicVolume(float value)
    {
        SetMixerVolume(MusicParameterName, value);
    }

    private void ApplySfxVolume(float value)
    {
        SetMixerVolume(SfxParameterName, value);
    }

    private void SetMixerVolume(string parameterName, float value)
    {
        if (audioMixer == null)
        {
            return;
        }

        float clampedValue = Mathf.Clamp01(value);
        float decibels = Mathf.Log10(Mathf.Max(clampedValue, MinimumVolume)) * 20f;
        audioMixer.SetFloat(parameterName, decibels);
    }

    private static void SetSliderWithoutNotification(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }
    }
}
