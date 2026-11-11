using UnityEngine;

/// <summary>Plays shared WBC voice lines for batch and combat lifecycle events.</summary>
[DisallowMultipleComponent]
public sealed class WbcVoiceLines : MonoBehaviour
{
    [SerializeField] private AudioClip[] deploymentAcceptedClips;
    [SerializeField] private AudioClip[] invalidDeploymentClips;
    [SerializeField] private AudioClip trainingReadyClip;
    [SerializeField] private AudioClip targetChangedClip;
    [SerializeField] private AudioClip targetAbortedClip;
    [SerializeField] private AudioClip bacteriaDestroyedClip;
    [SerializeField] private AudioClip unitDestroyedClip;
    [SerializeField] private AudioClip viralContainedClip;

    /// <summary>Plays a random line after a valid WBC deployment.</summary>
    public void PlayDeploymentAccepted()
    {
        PlayRandom(deploymentAcceptedClips);
    }

    /// <summary>Plays a random line when a WBC deployment is rejected as incompatible.</summary>
    public void PlayInvalidDeployment()
    {
        PlayRandom(invalidDeploymentClips);
    }

    /// <summary>Plays when WBC batch training completes.</summary>
    public void PlayTrainingReady()
    {
        VoiceLineAudioPlayer.Play(trainingReadyClip);
    }

    /// <summary>Plays when a WBC batch receives a new target after changing its previous target.</summary>
    public void PlayTargetChanged()
    {
        VoiceLineAudioPlayer.Play(targetChangedClip);
    }

    /// <summary>Plays when the current WBC target is aborted and the batch returns to idle.</summary>
    public void PlayTargetAborted()
    {
        VoiceLineAudioPlayer.Play(targetAbortedClip);
    }

    /// <summary>Plays when a WBC clears its assigned valid pathogen target.</summary>
    public void PlayPathogenCleared()
    {
        VoiceLineAudioPlayer.Play(bacteriaDestroyedClip);
    }

    /// <summary>Plays when an individual WBC unit or its batch is destroyed.</summary>
    public void PlayUnitDestroyed()
    {
        VoiceLineAudioPlayer.Play(unitDestroyedClip);
    }

    /// <summary>Plays when a Neutrophil contains a viral infection.</summary>
    public void PlayViralContained()
    {
        VoiceLineAudioPlayer.Play(viralContainedClip);
    }

    private static void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return;

        int clipIndex = Random.Range(0, clips.Length);
        VoiceLineAudioPlayer.Play(clips[clipIndex]);
    }
}
