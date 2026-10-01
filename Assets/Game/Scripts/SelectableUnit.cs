using System;
using UnityEngine;

/// <summary>Marks a deployed unit as selectable and exposes its current display name and HP.</summary>
public sealed class SelectableUnit : MonoBehaviour
{
    private const int DefaultHitPoints = 100;

    [SerializeField] private string displayName;
    [SerializeField, Min(1)] private int currentHitPoints = DefaultHitPoints;

    /// <summary>Returns the UI name for this deployed unit.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? FormatDisplayName(gameObject.name) : displayName;

    /// <summary>Returns the unit's current hit points.</summary>
    public int CurrentHitPoints => currentHitPoints;

    /// <summary>Sets the UI display name from its source prefab name.</summary>
    public void Initialize(string sourceName)
    {
        displayName = FormatDisplayName(sourceName);
        currentHitPoints = DefaultHitPoints;
    }

    private static string FormatDisplayName(string sourceName)
    {
        string cleanName = sourceName ?? string.Empty;
        const string cloneSuffix = "(Clone)";
        if (cleanName.EndsWith(cloneSuffix, StringComparison.Ordinal))
            cleanName = cleanName.Substring(0, cleanName.Length - cloneSuffix.Length).TrimEnd();

        if (cleanName.StartsWith("Neutrophil", StringComparison.OrdinalIgnoreCase))
            return "WBC";
        if (cleanName.IndexOf("Macrophage", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Macrophage";
        if (cleanName.StartsWith("RBC", StringComparison.OrdinalIgnoreCase))
            return "RBC";
        if (cleanName.StartsWith("B Cell", StringComparison.OrdinalIgnoreCase))
            return "B Cell";
        if (cleanName.StartsWith("T Cell", StringComparison.OrdinalIgnoreCase))
            return "T Cell";

        int instanceSuffixIndex = cleanName.LastIndexOf('_');
        if (instanceSuffixIndex >= 0 && int.TryParse(cleanName.Substring(instanceSuffixIndex + 1), out _))
            cleanName = cleanName.Substring(0, instanceSuffixIndex);
        return cleanName.Trim();
    }
}
