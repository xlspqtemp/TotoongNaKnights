using UnityEngine;

/// <summary>Plays Macrophage voice lines for training, orders, combat, and removal events.</summary>
[DisallowMultipleComponent]
public sealed class MacrophageVoiceLines : MonoBehaviour
{
    [SerializeField] private AudioClip trainingCompletedClip;
    [SerializeField] private AudioClip deploymentAcceptedClip;
    [SerializeField] private AudioClip invalidDeploymentClip;
    [SerializeField] private AudioClip targetChangedOrAbortedClip;
    [SerializeField] private AudioClip pathogenClearedClip;
    [SerializeField] private AudioClip batchDestroyedClip;

    /// <summary>Plays once when Macrophage batch training completes successfully.</summary>
    public void PlayTrainingCompleted()
    {
        PlayVoiceLine(trainingCompletedClip);
    }

    /// <summary>Plays once when a Macrophage batch is successfully assigned to a compatible target.</summary>
    public void PlayDeploymentAccepted()
    {
        PlayVoiceLine(deploymentAcceptedClip);
    }

    /// <summary>Plays when a Macrophage batch is assigned to an incompatible target.</summary>
    public void PlayInvalidDeployment()
    {
        PlayVoiceLine(invalidDeploymentClip);
    }

    /// <summary>Plays when an active Macrophage target is aborted or changed.</summary>
    public void PlayTargetChangedOrAborted()
    {
        PlayVoiceLine(targetChangedOrAbortedClip);
    }

    /// <summary>Plays when a Macrophage successfully clears a bacteria target.</summary>
    public void PlayPathogenCleared()
    {
        PlayVoiceLine(pathogenClearedClip);
    }

    /// <summary>Plays when the Macrophage batch is destroyed.</summary>
    public void PlayBatchDestroyed()
    {
        PlayVoiceLine(batchDestroyedClip);
    }

    private void PlayVoiceLine(AudioClip clip)
    {
        VoiceLineAudioPlayer.Play(clip);
    }
}
