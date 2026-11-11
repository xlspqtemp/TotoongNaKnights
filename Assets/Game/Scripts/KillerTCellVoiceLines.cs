using UnityEngine;

/// <summary>Plays Killer T-cell voice lines for training, deployment, viral clearance, and batch lifecycle events.</summary>
[DisallowMultipleComponent]
public sealed class KillerTCellVoiceLines : MonoBehaviour
{
    [SerializeField] private AudioClip trainingCompletedClip;
    [SerializeField] private AudioClip deploymentAcceptedClip;
    [SerializeField] private AudioClip invalidDeploymentClip;
    [SerializeField] private AudioClip targetChangedOrAbortedClip;
    [SerializeField] private AudioClip viralDestroyedClip;
    [SerializeField] private AudioClip batchDestroyedClip;

    /// <summary>Plays when Killer T-cell batch training completes.</summary>
    public void PlayTrainingCompleted()
    {
        VoiceLineAudioPlayer.Play(trainingCompletedClip);
    }

    /// <summary>Plays when a Killer T-cell batch is successfully dispatched.</summary>
    public void PlayDeploymentAccepted()
    {
        VoiceLineAudioPlayer.Play(deploymentAcceptedClip);
    }

    /// <summary>Plays when a Killer T-cell deployment target is incompatible.</summary>
    public void PlayInvalidDeployment()
    {
        VoiceLineAudioPlayer.Play(invalidDeploymentClip);
    }

    /// <summary>Plays when a Killer T-cell target is changed or aborted.</summary>
    public void PlayTargetChangedOrAborted()
    {
        VoiceLineAudioPlayer.Play(targetChangedOrAbortedClip);
    }

    /// <summary>Plays when the Killer T-cell successfully clears its matching viral infection.</summary>
    public void PlayViralDestroyed()
    {
        VoiceLineAudioPlayer.Play(viralDestroyedClip);
    }

    /// <summary>Plays when the Killer T-cell batch is destroyed.</summary>
    public void PlayBatchDestroyed()
    {
        VoiceLineAudioPlayer.Play(batchDestroyedClip);
    }
}
