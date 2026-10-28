using System;
using System.Collections.Generic;

/// <summary>Stores antigens discovered during gameplay.</summary>
public static class Databank
{
    public static List<string> UnlockedAntigens = new List<string>();

    /// <summary>Adds an antigen once, ignoring duplicate names regardless of case.</summary>
    public static void AddAntigen(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        foreach (string unlockedAntigen in UnlockedAntigens)
        {
            if (string.Equals(unlockedAntigen, name, StringComparison.OrdinalIgnoreCase))
                return;
        }

        UnlockedAntigens.Add(name);
    }
}
