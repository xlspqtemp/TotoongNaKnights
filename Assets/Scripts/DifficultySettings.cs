using UnityEngine;

/// <summary>
/// Stores the player's selected difficulty levels between scene loads.
/// </summary>
public static class DifficultySettings
{
    public const int MinimumLevel = 1;
    public const int MaximumLevel = 5;
    public const int DefaultLevel = 3;

    private const string EnemyLevelKey = "Difficulty.Enemy";
    private const string DefenseLevelKey = "Difficulty.Defense";
    private const string RandomEventsLevelKey = "Difficulty.RandomEvents";

    /// <summary>
    /// Gets the selected enemy difficulty level.
    /// </summary>
    public static int EnemyLevel => PlayerPrefs.GetInt(EnemyLevelKey, DefaultLevel);

    /// <summary>
    /// Gets the selected defense difficulty level.
    /// </summary>
    public static int DefenseLevel => PlayerPrefs.GetInt(DefenseLevelKey, DefaultLevel);

    /// <summary>
    /// Gets the selected random events difficulty level.
    /// </summary>
    public static int RandomEventsLevel => PlayerPrefs.GetInt(RandomEventsLevelKey, DefaultLevel);

    /// <summary>
    /// Saves all selected difficulty levels after clamping them to the supported range.
    /// </summary>
    public static void SaveLevels(int enemyLevel, int defenseLevel, int randomEventsLevel)
    {
        PlayerPrefs.SetInt(EnemyLevelKey, ClampLevel(enemyLevel));
        PlayerPrefs.SetInt(DefenseLevelKey, ClampLevel(defenseLevel));
        PlayerPrefs.SetInt(RandomEventsLevelKey, ClampLevel(randomEventsLevel));
        PlayerPrefs.Save();
    }

    private static int ClampLevel(int level)
    {
        return Mathf.Clamp(level, MinimumLevel, MaximumLevel);
    }
}
