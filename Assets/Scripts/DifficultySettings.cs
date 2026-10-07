using System;
using UnityEngine;

[Serializable]
public sealed class DifficultyStats
{
    public string difficultyName;
    public int eventsPerDay;
    public float wbcHp;
    public float wbcAttack;
    public float bacteriaHp;
    public float virusHp;
    public float bacteriaAttack;
    public float virusAttack;
    public float attackIntervalSeconds;
    public float wbcDamageMultiplierVsBacteria;
    public float wbcDamageMultiplierVsVirus;

    public DifficultyStats()
    {
    }

    public DifficultyStats(string name, int events, float whiteBloodCellHp, float whiteBloodCellAttack,
        float bacterialHp, float viralHp, float bacterialAttack, float viralAttack,
        float attackInterval, float damageVsBacteria, float damageVsVirus)
    {
        difficultyName = name;
        eventsPerDay = events;
        wbcHp = whiteBloodCellHp;
        wbcAttack = whiteBloodCellAttack;
        bacteriaHp = bacterialHp;
        virusHp = viralHp;
        bacteriaAttack = bacterialAttack;
        virusAttack = viralAttack;
        attackIntervalSeconds = attackInterval;
        wbcDamageMultiplierVsBacteria = damageVsBacteria;
        wbcDamageMultiplierVsVirus = damageVsVirus;
    }
}

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
    private const string SelectedPresetKey = "Difficulty.SelectedPreset";
    private const int DifficultyPresetCount = 4;
    private static DifficultyStats[] configuredPresets;

    public static int SelectedDifficultyIndex => Mathf.Clamp(PlayerPrefs.GetInt(SelectedPresetKey, 1), 0, DifficultyPresetCount - 1);

    public static DifficultyStats CurrentStats
    {
        get
        {
            int index = SelectedDifficultyIndex;
            if (configuredPresets != null && configuredPresets.Length == DifficultyPresetCount && configuredPresets[index] != null)
                return configuredPresets[index];

            return CreateDefaultStats()[index];
        }
    }

    public static void ConfigureStats(DifficultyStats[] presets)
    {
        if (presets != null && presets.Length == DifficultyPresetCount)
            configuredPresets = presets;
    }

    public static void SaveSelectedDifficulty(int index)
    {
        PlayerPrefs.SetInt(SelectedPresetKey, Mathf.Clamp(index, 0, DifficultyPresetCount - 1));
        PlayerPrefs.Save();
    }

    public static DifficultyStats[] CreateDefaultStats()
    {
        return new[]
        {
            new DifficultyStats("Easy", 2, 150f, 15f, 80f, 100f, 3f, 3f, 1f, 1f, 0.5f),
            new DifficultyStats("Normal", 3, 100f, 10f, 90f, 100f, 5f, 4f, 1f, 1f, 0.5f),
            new DifficultyStats("Medium", 5, 100f, 10f, 120f, 150f, 6f, 5f, 1f, 1f, 0.5f),
            new DifficultyStats("Hard", 5, 100f, 10f, 130f, 170f, 7f, 6f, 1f, 1f, 0.5f)
        };
    }

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
