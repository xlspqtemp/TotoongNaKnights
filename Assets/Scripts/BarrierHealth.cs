using UnityEngine;

/// <summary>Tracks the remaining protective barrier for a body system.</summary>
public sealed class BarrierHealth : MonoBehaviour
{
    private const float MaximumBarrier = 100f;

    public float CurrentBarrier = MaximumBarrier;
    public bool IsBreached { get; private set; }

    private void Awake()
    {
        CurrentBarrier = Mathf.Clamp(CurrentBarrier, 0f, MaximumBarrier);
        RefreshBreachState();
    }

    private void Update()
    {
        RefreshBreachState();
    }

    /// <summary>Reduces the barrier by the requested amount and updates its breach state.</summary>
    public void TakeDamage(float amount)
    {
        if (amount > 0f)
            CurrentBarrier = Mathf.Clamp(CurrentBarrier - amount, 0f, MaximumBarrier);

        RefreshBreachState();
    }

    /// <summary>Restores the barrier without exceeding its maximum value.</summary>
    public void RestoreBarrier(float amount)
    {
        if (amount > 0f)
            CurrentBarrier = Mathf.Clamp(CurrentBarrier + amount, 0f, MaximumBarrier);

        RefreshBreachState();
    }

    private void RefreshBreachState()
    {
        bool isCurrentlyBreached = CurrentBarrier <= 0f;
        if (isCurrentlyBreached && !IsBreached)
        {
            IsBreached = true;
            Debug.LogWarning($"[Barrier] {name} Barrier has been breached! Next day pathogens will be severely mutated.", this);
        }
        else if (!isCurrentlyBreached)
        {
            IsBreached = false;
        }
    }
}
