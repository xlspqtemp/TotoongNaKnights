using System;
using UnityEngine;

[Serializable]
public sealed class DifficultyStats
{
    public string difficultyName;
    public int maxActiveSquads = 2;
    public int displayMaxActiveSquads = 2;
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
    public int startingSupply;
    public int maxSupply;
    public int deployCost;
    public float regenSecondsPerPoint;
    [Range(0f, 100f)] public float commonPathogenWeightPercent;
    [Range(0f, 100f)] public float uncommonPathogenWeightPercent;
    [Range(0f, 100f)] public float rarePathogenWeightPercent;
    [Range(0f, 100f)] public float veryRarePathogenWeightPercent;
    [Range(0f, 100f)] public float namedInfectionChance;
    [Min(0f)] public float barrierDamageMultiplier = 1f;
    [Min(1f)] public float innateVariantSpecialistDamageMultiplier = 3f;

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

    /// <summary>Returns this preset's configured weight percentage for a pathogen rarity tier.</summary>
    public float GetPathogenTierWeightPercent(InfectionRarity rarity)
    {
        switch (rarity)
        {
            case InfectionRarity.Common:
                return Mathf.Max(0f, commonPathogenWeightPercent);
            case InfectionRarity.Uncommon:
                return Mathf.Max(0f, uncommonPathogenWeightPercent);
            case InfectionRarity.Rare:
                return Mathf.Max(0f, rarePathogenWeightPercent);
            case InfectionRarity.VeryRare:
                return Mathf.Max(0f, veryRarePathogenWeightPercent);
            default:
                return 0f;
        }
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
    private const int HardDifficultyIndex = 3;
    private const float HardDifficultyWbcHp = 80f;
    private const float HardDifficultyWbcAttack = 8f;
    private const float DefaultInnateVariantSpecialistDamageMultiplier = 3f;
    private static readonly float[] DefaultNamedInfectionChances = { 20f, 40f, 50f, 60f };
    private static readonly float[] DefaultBarrierDamageMultipliers = { 0.5f, 1f, 1.5f, 2f };
    private static readonly float[][] DefaultPathogenTierWeights =
    {
        new[] { 80f, 20f, 0f, 0f },
        new[] { 55f, 35f, 10f, 0f },
        new[] { 20f, 30f, 30f, 20f },
        new[] { 5f, 5f, 10f, 80f }
    };
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
        if (presets == null || presets.Length != DifficultyPresetCount)
            return;

        for (int index = 0; index < presets.Length; index++)
        {
            DifficultyStats preset = presets[index];
            if (preset == null)
                continue;

            float[] tierWeights = DefaultPathogenTierWeights[index];
            preset.commonPathogenWeightPercent = tierWeights[0];
            preset.uncommonPathogenWeightPercent = tierWeights[1];
            preset.rarePathogenWeightPercent = tierWeights[2];
            preset.veryRarePathogenWeightPercent = tierWeights[3];
            preset.barrierDamageMultiplier = DefaultBarrierDamageMultipliers[index];
            if (preset.innateVariantSpecialistDamageMultiplier <= 0f)
                preset.innateVariantSpecialistDamageMultiplier = DefaultInnateVariantSpecialistDamageMultiplier;
            if (index == HardDifficultyIndex)
            {
                preset.wbcHp = HardDifficultyWbcHp;
                preset.wbcAttack = HardDifficultyWbcAttack;
            }
            if (preset.namedInfectionChance <= 0f)
                preset.namedInfectionChance = DefaultNamedInfectionChances[index];
            preset.barrierDamageMultiplier = DefaultBarrierDamageMultipliers[index];
        }

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
            new DifficultyStats("Easy", 2, 150f, 15f, 80f, 100f, 3f, 3f, 1f, 1f, 0.5f) { maxActiveSquads = 3, startingSupply = 100, maxSupply = 100, deployCost = 15, regenSecondsPerPoint = 2.0f, commonPathogenWeightPercent = 80f, uncommonPathogenWeightPercent = 20f, rarePathogenWeightPercent = 0f, veryRarePathogenWeightPercent = 0f, namedInfectionChance = 20f, barrierDamageMultiplier = 0.5f },
            new DifficultyStats("Normal", 3, 100f, 10f, 90f, 100f, 5f, 4f, 1f, 1f, 0.5f) { maxActiveSquads = 2, startingSupply = 100, maxSupply = 100, deployCost = 20, regenSecondsPerPoint = 3.0f, commonPathogenWeightPercent = 55f, uncommonPathogenWeightPercent = 35f, rarePathogenWeightPercent = 10f, veryRarePathogenWeightPercent = 0f, namedInfectionChance = 40f, barrierDamageMultiplier = 1f },
            new DifficultyStats("Medium", 5, 100f, 10f, 120f, 150f, 6f, 5f, 1f, 1f, 0.5f) { maxActiveSquads = 2, startingSupply = 100, maxSupply = 100, deployCost = 25, regenSecondsPerPoint = 4.0f, commonPathogenWeightPercent = 20f, uncommonPathogenWeightPercent = 30f, rarePathogenWeightPercent = 30f, veryRarePathogenWeightPercent = 20f, namedInfectionChance = 50f, barrierDamageMultiplier = 1.5f },
            new DifficultyStats("Hard", 5, 80f, 8f, 130f, 170f, 7f, 6f, 1f, 1f, 0.5f) { maxActiveSquads = 2, startingSupply = 100, maxSupply = 100, deployCost = 30, regenSecondsPerPoint = 5.0f, commonPathogenWeightPercent = 5f, uncommonPathogenWeightPercent = 5f, rarePathogenWeightPercent = 10f, veryRarePathogenWeightPercent = 80f, namedInfectionChance = 60f, barrierDamageMultiplier = 2f }
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
