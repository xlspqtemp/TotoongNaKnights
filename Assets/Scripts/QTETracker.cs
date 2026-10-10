using System.Collections.Generic;

/// <summary>Tracks active QTE occurrences by the layer displayed in the Layers panel.</summary>
internal static class QTETracker
{
    private const int LayerCount = 4;
    public const int RespiratoryLayerIndex = 0;
    public const int DigestiveLayerIndex = 1;
    public const int CirculatoryLayerIndex = 2;
    public const int LymphaticLayerIndex = 3;
    private static readonly HashSet<string>[] ActiveQteIds =
    {
        new HashSet<string>(),
        new HashSet<string>(),
        new HashSet<string>(),
        new HashSet<string>()
    };

    /// <summary>Adds one active QTE occurrence to the specified layer.</summary>
    public static void Register(int layerIndex, string qteId)
    {
        if (!IsValidLayer(layerIndex) || string.IsNullOrWhiteSpace(qteId))
            return;

        ActiveQteIds[layerIndex].Add(qteId);
    }

    /// <summary>Removes one resolved QTE occurrence from the specified layer.</summary>
    public static void Unregister(int layerIndex, string qteId)
    {
        if (!IsValidLayer(layerIndex) || string.IsNullOrWhiteSpace(qteId))
            return;

        ActiveQteIds[layerIndex].Remove(qteId);
    }

    /// <summary>Returns whether the specified layer has any active QTE occurrences.</summary>
    public static bool HasActiveQTE(int layerIndex)
    {
        return IsValidLayer(layerIndex) && ActiveQteIds[layerIndex].Count > 0;
    }

    /// <summary>Clears all tracked QTEs when changing scenes.</summary>
    public static void ClearAll()
    {
        for (int index = 0; index < LayerCount; index++)
            ActiveQteIds[index].Clear();
    }

    private static bool IsValidLayer(int layerIndex)
    {
        return layerIndex >= 0 && layerIndex < LayerCount;
    }
}
