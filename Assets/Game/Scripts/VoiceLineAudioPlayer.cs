using UnityEngine;
using UnityEngine.Audio;

/// <summary>Plays voice lines through one persistent, non-positional audio source with a shared volume setting.</summary>
[DisallowMultipleComponent]
public sealed class VoiceLineAudioPlayer : MonoBehaviour
{
    private const string PlayerObjectName = "Voice Line Audio Player";
    private const string VoiceLineVolumePreferenceKey = "VoiceLineVolume";
    private const float DefaultVoiceLineVolume = 1f;

    private static VoiceLineAudioPlayer instance;
    private static AudioMixerGroup outputMixerGroup;

    private AudioSource audioSource;

    /// <summary>Returns the saved normalized volume for all voice lines.</summary>
    public static float GetVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(VoiceLineVolumePreferenceKey, DefaultVoiceLineVolume));
    }

    /// <summary>Sets and saves the normalized volume for all voice lines.</summary>
    /// <param name="volume">Normalized volume between zero and one.</param>
    public static void SetVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VoiceLineVolumePreferenceKey, clampedVolume);
        PlayerPrefs.Save();

        if (instance != null && instance.audioSource != null)
            instance.audioSource.volume = clampedVolume;
    }

    /// <summary>Routes the shared voice source through the game's master mixer group.</summary>
    /// <param name="mixerGroup">The mixer group used by the game's other audio sources.</param>
    public static void SetOutputMixerGroup(AudioMixerGroup mixerGroup)
    {
        outputMixerGroup = mixerGroup;
        if (instance != null && instance.audioSource != null)
            instance.audioSource.outputAudioMixerGroup = outputMixerGroup;
    }

    /// <summary>Plays a voice clip once at full 2D audibility, scaled by the saved voice volume.</summary>
    /// <param name="clip">The voice clip to play.</param>
    public static void Play(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsureInstance();
        if (instance != null && instance.audioSource != null)
            instance.audioSource.PlayOneShot(clip);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject playerObject = new GameObject(PlayerObjectName);
        instance = playerObject.AddComponent<VoiceLineAudioPlayer>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = GetVolume();
        audioSource.outputAudioMixerGroup = outputMixerGroup;
        DontDestroyOnLoad(gameObject);
    }
}
