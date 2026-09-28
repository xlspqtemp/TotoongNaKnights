using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates difficulty-weighted health and lifestyle events on each in-game hour.
/// This prototype only rolls, logs, and broadcasts event data; it does not resolve QTEs
/// or apply health changes directly.
/// </summary>
public class RandomEventSystem : MonoBehaviour
{
    private const float EASY_TRIGGER_CHANCE = 0.12f;
    private const float NORMAL_TRIGGER_CHANCE = 0.22f;
    private const float MEDIUM_TRIGGER_CHANCE = 0.32f;
    private const float HARD_TRIGGER_CHANCE = 0.48f;
    private const float MINIMUM_WEIGHT = 0.0001f;
    private const string RANDOM_EVENT_PREFIX = "<color=#FF5555><b>[RANDOM EVENT]</b></color>";
    private const string RANDOM_EVENT_SYSTEM_PREFIX = "<color=#55D6FF><b>[RANDOM EVENT SYSTEM]</b></color>";

    public static RandomEventSystem Instance { get; private set; }

    /// <summary>Raised after an event is generated, before any future resolver handles it.</summary>
    public static event Action<RandomEventData> OnRandomEventTriggered;

    [Header("References")]
    [SerializeField] private DayCounterUI dayCounter;

    [Header("Runtime Difficulty")]
    [SerializeField] private RandomEventDifficulty activeDifficulty = RandomEventDifficulty.Normal;

    [Header("Difficulty Presets")]
    [SerializeField] private List<RandomEventDifficultyPreset> difficultyPresets = new List<RandomEventDifficultyPreset>();

    [Header("Debug Controls")]
    [SerializeField] private bool enableDebugKeyboardShortcuts = true;
    [SerializeField] private KeyCode forceEventKey = KeyCode.F9;
    [SerializeField] private KeyCode cycleDifficultyKey = KeyCode.F10;
    [SerializeField] private bool logHourlyRolls;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureDifficultyPresets();
    }

    private void OnValidate()
    {
        EnsureDifficultyPresets();
    }

    private void OnEnable()
    {
        if (dayCounter != null)
            dayCounter.OnHourAdvanced += HandleHourAdvanced;
    }

    private void Start()
    {
        if (dayCounter == null)
            Debug.LogWarning("[RandomEventSystem] DayCounterUI reference is missing; hourly events are disabled.");
    }

    private void OnDisable()
    {
        if (dayCounter != null)
            dayCounter.OnHourAdvanced -= HandleHourAdvanced;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!enableDebugKeyboardShortcuts)
            return;

        if (Input.GetKeyDown(forceEventKey))
            ForceTriggerEvent();

        if (Input.GetKeyDown(cycleDifficultyKey))
            CycleDifficulty();
    }

    private void HandleHourAdvanced(int currentDay, int currentHour)
    {
        RollForEvent(currentDay, currentHour, false);
    }

    /// <summary>Force-generates an event immediately using the active preset.</summary>
    [ContextMenu("Random Event/Force Trigger")]
    public void ForceTriggerEvent()
    {
        int day = dayCounter != null ? dayCounter.CurrentDay : 1;
        int hour = dayCounter != null ? dayCounter.CurrentHour : 0;
        RollForEvent(day, hour, true);
    }

    /// <summary>Changes the active preset used by future hourly rolls.</summary>
    public void SetDifficulty(RandomEventDifficulty difficulty)
    {
        activeDifficulty = difficulty;
        string message = $"{RANDOM_EVENT_SYSTEM_PREFIX} Difficulty set to <b>{activeDifficulty}</b>.";
        Debug.Log(message);
        LogToConsole(message, ConsoleLogUI.LogType.System);
    }

    /// <summary>Cycles Easy, Normal, Medium, and Hard for runtime weighting tests.</summary>
    [ContextMenu("Random Event/Cycle Difficulty")]
    public void CycleDifficulty()
    {
        int nextDifficulty = ((int)activeDifficulty + 1) % Enum.GetValues(typeof(RandomEventDifficulty)).Length;
        SetDifficulty((RandomEventDifficulty)nextDifficulty);
    }

    /// <summary>Rebuilds only missing or empty preset data using the prototype defaults.</summary>
    [ContextMenu("Random Event/Reset Missing Presets")]
    public void ResetMissingPresets()
    {
        EnsureDifficultyPresets();
        Debug.Log($"{RANDOM_EVENT_SYSTEM_PREFIX} Missing preset data restored.");
    }

    private void RollForEvent(int day, int hour, bool forceTrigger)
    {
        RoutineSystem routineSystem = RoutineSystem.Instance;
        if (routineSystem != null && routineSystem.IsSleepingAtHour(hour))
            return;

        RandomEventDifficultyPreset preset = GetActivePreset();
        if (preset == null)
            return;

        float triggerChance = Mathf.Clamp01(preset.hourlyTriggerChance);
        float triggerRoll = UnityEngine.Random.value;
        bool shouldTrigger = forceTrigger || triggerRoll <= triggerChance;

        if (logHourlyRolls)
        {
            string rollMessage = $"{RANDOM_EVENT_SYSTEM_PREFIX} Day {day}, Hour {hour:00}:00 rolled " +
                                 $"{triggerRoll:0.000} against {triggerChance:0.000} ({activeDifficulty}).";
            Debug.Log(rollMessage);
            LogToConsole(rollMessage, ConsoleLogUI.LogType.System);
        }

        if (!shouldTrigger)
            return;

        RandomEventSeverity severity = RollSeverity(preset);
        RandomEventId eventId = RollEventId(preset);
        if (activeDifficulty == RandomEventDifficulty.Easy &&
            (eventId == RandomEventId.AteExpiredFood ||
             eventId == RandomEventId.JunkFoodBinge ||
             eventId == RandomEventId.ColdFromSickPerson ||
             eventId == RandomEventId.NickedOrScraped))
        {
            severity = ClampEasyEventSeverity(severity);
        }

        RandomEventData eventData = CreateEventData(eventId, severity, day, hour);
        string logMessage = $"{RANDOM_EVENT_PREFIX} Day {day}, Hour {hour:00}:00: " +
                            $"{eventData.eventName} ({eventData.severity}) — {eventData.eventDescription}";
        Debug.Log(logMessage);
        LogToConsole(logMessage, GetConsoleLogType(eventData));
        OnRandomEventTriggered?.Invoke(eventData);
    }

    private RandomEventDifficultyPreset GetActivePreset()
    {
        EnsureDifficultyPresets();
        return difficultyPresets.Find(preset => preset.difficulty == activeDifficulty);
    }

    private RandomEventSeverity RollSeverity(RandomEventDifficultyPreset preset)
    {
        float minorWeight = Mathf.Max(0f, preset.minorSeverityWeight);
        float moderateWeight = Mathf.Max(0f, preset.moderateSeverityWeight);
        float severeWeight = Mathf.Max(0f, preset.severeSeverityWeight);
        float totalWeight = minorWeight + moderateWeight + severeWeight;

        if (totalWeight <= MINIMUM_WEIGHT)
            return RandomEventSeverity.Minor;

        float roll = UnityEngine.Random.value * totalWeight;
        if (roll < minorWeight)
            return RandomEventSeverity.Minor;
        if (roll < minorWeight + moderateWeight)
            return RandomEventSeverity.Moderate;
        return RandomEventSeverity.Severe;
    }

    private RandomEventId RollEventId(RandomEventDifficultyPreset preset)
    {
        if (preset.eventPoolWeights == null || preset.eventPoolWeights.Length == 0)
            return RandomEventId.SkippedMeal;

        float totalWeight = 0f;
        foreach (RandomEventPoolWeight entry in preset.eventPoolWeights)
            totalWeight += Mathf.Max(0f, entry.weight);

        if (totalWeight <= MINIMUM_WEIGHT)
            return RandomEventId.SkippedMeal;

        float roll = UnityEngine.Random.value * totalWeight;
        foreach (RandomEventPoolWeight entry in preset.eventPoolWeights)
        {
            roll -= Mathf.Max(0f, entry.weight);
            if (roll <= 0f)
                return entry.eventId;
        }

        return preset.eventPoolWeights[preset.eventPoolWeights.Length - 1].eventId;
    }

    private RandomEventSeverity ClampEasyEventSeverity(RandomEventSeverity severity)
    {
        return severity == RandomEventSeverity.Severe
            ? RandomEventSeverity.Moderate
            : severity;
    }

    private RandomEventData CreateEventData(RandomEventId eventId, RandomEventSeverity severity, int day, int hour)
    {
        switch (eventId)
        {
            case RandomEventId.AteExpiredFood:
                return new RandomEventData("Ate expired/spoiled food", "Digestive discomfort detected; vomit QTE if unresolved.", RandomEventType.Digestive, severity, true, false, -8f, activeDifficulty, day, hour);
            case RandomEventId.InhaledDustOrAllergen:
                return new RandomEventData("Inhaled dust/allergen", "Respiratory irritation detected; cough QTE if unresolved.", RandomEventType.Respiratory, severity, true, false, -4f, activeDifficulty, day, hour);
            case RandomEventId.SkippedMeal:
                return new RandomEventData("Skipped a meal", "Energy intake was insufficient; minor health drain if unresolved.", RandomEventType.GeneralLifestyle, severity, false, false, -2f, activeDifficulty, day, hour);
            case RandomEventId.Overexertion:
                return new RandomEventData("Overexertion/fatigue from exercise", "Physical exertion exceeded the current recovery baseline.", RandomEventType.Physical, severity, false, false, -3f, activeDifficulty, day, hour);
            case RandomEventId.StressOrPoorHydration:
                return new RandomEventData("Mild stress/poor hydration", "Stress or hydration deficit detected; follow-up event chance may rise.", RandomEventType.Psychological, severity, false, false, -1f, activeDifficulty, day, hour);
            case RandomEventId.JunkFoodBinge:
                return new RandomEventData("Junk food binge", "Digestive and lifestyle strain detected.", RandomEventType.Digestive, severity, false, false, -5f, activeDifficulty, day, hour);
            case RandomEventId.ArgumentOrConflict:
                return new RandomEventData("Got into an argument / stressful conflict", "Psychological stress from a conflict caused a minor-to-moderate health drain.", RandomEventType.Psychological, severity, false, false, -3f, activeDifficulty, day, hour);
            case RandomEventId.ColdFromSickPerson:
                return new RandomEventData("Caught a cold from a sick classmate/coworker", "A bacteria breach is suspected; light cough QTE if unresolved.", RandomEventType.Respiratory, severity, true, false, -4f, activeDifficulty, day, hour);
            case RandomEventId.SunOrFreshAir:
                return new RandomEventData("Sat in the sun / got fresh air", "Fresh air and sunlight provided a minor health benefit.", RandomEventType.PsychologicalPhysical, severity, false, false, 2f, activeDifficulty, day, hour);
            case RandomEventId.NickedOrScraped:
                return new RandomEventData("Nicked by a sharp object or tripped and scraped knees", "A direct physical breach needs wound-clean QTE resolution; unresolved infection may escalate later.", RandomEventType.Physical, severity, true, true, -6f, activeDifficulty, day, hour);
            default:
                throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "Unknown random event id.");
        }
    }

    private ConsoleLogUI.LogType GetConsoleLogType(RandomEventData eventData)
    {
        if (eventData.healthImpact > 0f)
            return ConsoleLogUI.LogType.Success;
        if (eventData.severity == RandomEventSeverity.Severe)
            return ConsoleLogUI.LogType.Danger;
        if (eventData.severity == RandomEventSeverity.Moderate)
            return ConsoleLogUI.LogType.Warning;
        return ConsoleLogUI.LogType.Info;
    }

    private void LogToConsole(string message, ConsoleLogUI.LogType logType)
    {
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log(message, logType);
    }

    private void EnsureDifficultyPresets()
    {
        if (difficultyPresets == null)
            difficultyPresets = new List<RandomEventDifficultyPreset>();

        RandomEventDifficulty[] difficulties = (RandomEventDifficulty[])Enum.GetValues(typeof(RandomEventDifficulty));
        foreach (RandomEventDifficulty difficulty in difficulties)
        {
            RandomEventDifficultyPreset preset = difficultyPresets.Find(item => item != null && item.difficulty == difficulty);
            if (preset == null)
            {
                difficultyPresets.Add(CreateDefaultPreset(difficulty));
            }
            else if (preset.eventPoolWeights == null || preset.eventPoolWeights.Length != 10)
            {
                preset.eventPoolWeights = CreateDefaultEventPool(difficulty);
            }
        }
    }

    private RandomEventDifficultyPreset CreateDefaultPreset(RandomEventDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RandomEventDifficulty.Easy:
                return new RandomEventDifficultyPreset(difficulty, EASY_TRIGGER_CHANCE, 70f, 25f, 5f, CreateDefaultEventPool(difficulty));
            case RandomEventDifficulty.Medium:
                return new RandomEventDifficultyPreset(difficulty, MEDIUM_TRIGGER_CHANCE, 35f, 45f, 20f, CreateDefaultEventPool(difficulty));
            case RandomEventDifficulty.Hard:
                return new RandomEventDifficultyPreset(difficulty, HARD_TRIGGER_CHANCE, 20f, 45f, 35f, CreateDefaultEventPool(difficulty));
            default:
                return new RandomEventDifficultyPreset(difficulty, NORMAL_TRIGGER_CHANCE, 50f, 40f, 10f, CreateDefaultEventPool(difficulty));
        }
    }

    private RandomEventPoolWeight[] CreateDefaultEventPool(RandomEventDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RandomEventDifficulty.Easy:
                return CreatePool(5f, 10f, 5f, 10f, 10f, 5f, 8f, 8f, 35f, 4f);
            case RandomEventDifficulty.Medium:
                return CreatePool(15f, 15f, 15f, 10f, 15f, 20f, 12f, 8f, 5f, 12f);
            case RandomEventDifficulty.Hard:
                return CreatePool(20f, 15f, 15f, 7f, 15f, 25f, 10f, 10f, 2f, 13f);
            default:
                return CreatePool(10f, 12f, 10f, 12f, 15f, 11f, 12f, 8f, 15f, 8f);
        }
    }

    private RandomEventPoolWeight[] CreatePool(
        float expiredFood,
        float allergen,
        float skippedMeal,
        float overexertion,
        float stress,
        float junkFood,
        float argument,
        float cold,
        float sunOrFreshAir,
        float nickedOrScraped)
    {
        return new[]
        {
            new RandomEventPoolWeight(RandomEventId.AteExpiredFood, expiredFood),
            new RandomEventPoolWeight(RandomEventId.InhaledDustOrAllergen, allergen),
            new RandomEventPoolWeight(RandomEventId.SkippedMeal, skippedMeal),
            new RandomEventPoolWeight(RandomEventId.Overexertion, overexertion),
            new RandomEventPoolWeight(RandomEventId.StressOrPoorHydration, stress),
            new RandomEventPoolWeight(RandomEventId.JunkFoodBinge, junkFood),
            new RandomEventPoolWeight(RandomEventId.ArgumentOrConflict, argument),
            new RandomEventPoolWeight(RandomEventId.ColdFromSickPerson, cold),
            new RandomEventPoolWeight(RandomEventId.SunOrFreshAir, sunOrFreshAir),
            new RandomEventPoolWeight(RandomEventId.NickedOrScraped, nickedOrScraped)
        };
    }

    /// <summary>Returns the active difficulty used by future hourly rolls.</summary>
    public RandomEventDifficulty ActiveDifficulty => activeDifficulty;
}
