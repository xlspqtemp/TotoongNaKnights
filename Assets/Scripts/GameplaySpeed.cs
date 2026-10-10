using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Provides gameplay-only acceleration without changing Unity's global time scale.</summary>
public static class GameplaySpeed
{
    private static readonly float[] SpeedModes = { 1f, 2f, 4f, 6f };
    private static readonly HashSet<string> activeQteIds = new HashSet<string>(StringComparer.Ordinal);
    private static int selectedModeIndex;

    /// <summary>Raised when the selected or effective gameplay speed changes.</summary>
    public static event Action OnSpeedChanged;

    /// <summary>Returns the player's selected speed, independent of any active QTE override.</summary>
    public static float SelectedMultiplier => SpeedModes[selectedModeIndex];

    /// <summary>Returns the player's selected gameplay speed multiplier.</summary>
    public static float Multiplier => SelectedMultiplier;

    /// <summary>Returns whether at least one QTE is currently unresolved.</summary>
    public static bool IsQTEActive => activeQteIds.Count > 0;

    /// <summary>Returns Unity's frame delta scaled for gameplay only.</summary>
    public static float DeltaTime => Time.deltaTime * Multiplier;

    /// <summary>Returns the selected mode index (0=1x, 1=2x, 2=4x, 3=6x).</summary>
    public static int SelectedModeIndex => selectedModeIndex;

    /// <summary>Cycles through 1x, 2x, 4x, 6x, and back to 1x.</summary>
    public static void Cycle()
    {
        selectedModeIndex = (selectedModeIndex + 1) % SpeedModes.Length;
        OnSpeedChanged?.Invoke();
    }

    /// <summary>Sets the selected speed mode by index, clamped to the supported modes.</summary>
    public static void SetMode(int modeIndex)
    {
        int clampedIndex = Mathf.Clamp(modeIndex, 0, SpeedModes.Length - 1);
        if (selectedModeIndex == clampedIndex)
            return;

        selectedModeIndex = clampedIndex;
        OnSpeedChanged?.Invoke();
    }

    /// <summary>Forces normal effective speed until the matching QTE is resolved.</summary>
    public static void BeginQTE(string qteId)
    {
        if (string.IsNullOrWhiteSpace(qteId) || !activeQteIds.Add(qteId))
            return;

        OnSpeedChanged?.Invoke();
    }

    /// <summary>Resolves the matching QTE and restores the selected speed when none remain active.</summary>
    public static void ResolveQTE(string qteId)
    {
        if (string.IsNullOrWhiteSpace(qteId) || !activeQteIds.Remove(qteId))
            return;

        OnSpeedChanged?.Invoke();
    }

    /// <summary>Resets speed and unresolved QTE state when a new run begins.</summary>
    public static void ResetForNewRun()
    {
        ResetState();
    }

    /// <summary>Resets speed and unresolved QTE state at the end of a run.</summary>
    public static void ResetForRunEnd()
    {
        ResetState();
    }

    private static void ResetState()
    {
        bool changed = selectedModeIndex != 0 || activeQteIds.Count > 0;
        selectedModeIndex = 0;
        activeQteIds.Clear();
        if (changed)
            OnSpeedChanged?.Invoke();
    }

    /// <summary>Waits for a duration measured in gameplay seconds, respecting the current speed and pause state.</summary>
    public static IEnumerator WaitForGameplaySeconds(float duration)
    {
        float elapsed = 0f;
        float targetDuration = Mathf.Max(0f, duration);
        while (elapsed < targetDuration)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }
}
