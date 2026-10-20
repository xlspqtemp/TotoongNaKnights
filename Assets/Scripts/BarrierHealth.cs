using UnityEngine;

/// <summary>Tracks the remaining protective barrier for a body system.</summary>
public sealed class BarrierHealth : MonoBehaviour
{
    private const float MaximumBarrier = 100f;

    public float CurrentBarrier = MaximumBarrier;

    private bool depletionWarningLogged;

    private void Awake()
    {
        CurrentBarrier = Mathf.Clamp(CurrentBarrier, 0f, MaximumBarrier);
    }

    /// <summary>Reduces the barrier by the requested amount and reports depletion once.</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f)
            return;

        CurrentBarrier = Mathf.Clamp(CurrentBarrier - amount, 0f, MaximumBarrier);
        if (CurrentBarrier <= 0f && !depletionWarningLogged)
        {
            depletionWarningLogged = true;
            Debug.LogWarning($"{name} barrier depleted; breach handling is not configured yet.", this);
        }
    }

    /// <summary>Restores the barrier without exceeding its maximum value.</summary>
    public void RestoreBarrier(float amount)
    {
        if (amount <= 0f)
            return;

        CurrentBarrier = Mathf.Clamp(CurrentBarrier + amount, 0f, MaximumBarrier);
        if (CurrentBarrier > 0f)
            depletionWarningLogged = false;
    }
}
