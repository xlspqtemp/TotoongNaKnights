using UnityEngine;

/// <summary>Plays B-cell voice lines for training, deployment, antigen tagging, and batch lifecycle events.</summary>
[DisallowMultipleComponent]
public sealed class BCellVoiceLines : MonoBehaviour
{
    [SerializeField] private AudioClip trainingCompletedClip;
    [SerializeField] private AudioClip deploymentAcceptedClip;
    [SerializeField] private AudioClip invalidDeploymentClip;
    [SerializeField] private AudioClip targetChangedOrAbortedClip;
    [SerializeField] private AudioClip pathogenTaggedClip;
    [SerializeField] private AudioClip batchDestroyedClip;

    /// <summary>Plays when B-cell batch training completes.</summary>
    public void PlayTrainingCompleted()
    {
        VoiceLineAudioPlayer.Play(trainingCompletedClip);
    }

    /// <summary>Plays when a B-cell batch is successfully dispatched to its matching infection.</summary>
    public void PlayDeploymentAccepted()
    {
        VoiceLineAudioPlayer.Play(deploymentAcceptedClip);
    }

    /// <summary>Plays when a B-cell deployment is rejected because its target does not match its antigen.</summary>
    public void PlayInvalidDeployment()
    {
        VoiceLineAudioPlayer.Play(invalidDeploymentClip);
    }

    /// <summary>Plays when a B-cell target is changed or aborted.</summary>
    public void PlayTargetChangedOrAborted()
    {
        VoiceLineAudioPlayer.Play(targetChangedOrAbortedClip);
    }

    /// <summary>Plays when a B-cell successfully tags its target pathogen.</summary>
    public void PlayPathogenTagged()
    {
        VoiceLineAudioPlayer.Play(pathogenTaggedClip);
    }

    /// <summary>Plays when the B-cell batch is destroyed.</summary>
    public void PlayBatchDestroyed()
    {
        VoiceLineAudioPlayer.Play(batchDestroyedClip);
    }
}
