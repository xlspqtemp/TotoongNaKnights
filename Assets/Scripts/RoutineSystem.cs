using System;
using System.Collections.Generic;
using UnityEngine;
public class RoutineSystem : MonoBehaviour
{
    private const int HOURS_PER_DAY = 24;
    private const string ROUTINE_LOG_PREFIX = "<color=#55AAFF><b>[ROUTINE]</b></color>";
    private const string ROUTINE_SYSTEM_LOG_PREFIX = "<color=#55AAFF><b>[ROUTINE SYSTEM]</b></color>";

    public static RoutineSystem Instance { get; private set; }

    public static event Action<RoutineActivity> OnRoutineActivityChanged;

    [Header("References")]
    [SerializeField] private DayCounterUI dayCounter;

    [Header("Runtime Difficulty")]
    [SerializeField] private RoutineDifficulty activeDifficulty = RoutineDifficulty.Normal;

    [Header("Difficulty Schedules")]
    [SerializeField] private List<RoutineDifficultySchedule> schedules = new List<RoutineDifficultySchedule>();

    [Header("Debug Controls")]
    [SerializeField] private bool enableDebugKeyboardShortcuts = true;
    [SerializeField] private KeyCode forceAdvanceHourKey = KeyCode.F7;
    [SerializeField] private KeyCode cycleDifficultyKey = KeyCode.F8;
    [SerializeField] private KeyCode jumpToHourKey = KeyCode.F6;
    [Range(0, 23)]
    [SerializeField] private int debugJumpHour = 12;

    private RoutineActivity currentActivity;
    private bool hasCurrentActivity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureSchedules();
    }

    private void OnValidate()
    {
        EnsureSchedules();
    }

    private void OnEnable()
    {
        if (dayCounter != null)
            dayCounter.OnHourAdvanced += HandleHourAdvanced;
    }

    private void Start()
    {
        if (dayCounter == null)
        {
            Debug.LogWarning("[RoutineSystem] DayCounterUI reference is missing; routine updates are disabled.");
            return;
        }

        UpdateCurrentActivity(dayCounter.CurrentDay, dayCounter.CurrentHour);
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

        if (Input.GetKeyDown(forceAdvanceHourKey))
            ForceAdvanceHour();

        if (Input.GetKeyDown(cycleDifficultyKey))
            CycleDifficulty();

        if (Input.GetKeyDown(jumpToHourKey))
            JumpToConfiguredHour();
    }

    private void HandleHourAdvanced(int currentDay, int currentHour)
    {
        UpdateCurrentActivity(currentDay, currentHour);
    }

    [ContextMenu("Routine/Force Advance One Hour")]
    public void ForceAdvanceHour()
    {
        if (dayCounter == null)
        {
            Debug.LogWarning("[RoutineSystem] Cannot advance an hour without a DayCounterUI reference.");
            return;
        }

        dayCounter.TickMinutes(60);
    }

    public void JumpToHour(int hour)
    {
        if (dayCounter == null)
        {
            Debug.LogWarning("[RoutineSystem] Cannot jump hours without a DayCounterUI reference.");
            return;
        }

        int clampedHour = Mathf.Clamp(hour, 0, HOURS_PER_DAY - 1);
        dayCounter.SetTime(clampedHour, 0);
        UpdateCurrentActivity(dayCounter.CurrentDay, clampedHour);
    }

    [ContextMenu("Routine/Jump To Configured Hour")]
    public void JumpToConfiguredHour()
    {
        JumpToHour(debugJumpHour);
    }

    public void SetDifficulty(RoutineDifficulty difficulty)
    {
        if (activeDifficulty == difficulty)
            return;

        activeDifficulty = difficulty;
        LogSystemMessage($"Difficulty set to <b>{activeDifficulty}</b>.");

        if (dayCounter != null)
            UpdateCurrentActivity(dayCounter.CurrentDay, dayCounter.CurrentHour);
    }

    [ContextMenu("Routine/Cycle Difficulty")]
    public void CycleDifficulty()
    {
        int nextDifficulty = ((int)activeDifficulty + 1) % Enum.GetValues(typeof(RoutineDifficulty)).Length;
        SetDifficulty((RoutineDifficulty)nextDifficulty);
    }

    [ContextMenu("Routine/Reset Missing Schedules")]
    public void ResetMissingSchedules()
    {
        EnsureSchedules();
        LogSystemMessage("Missing schedule data restored.");
    }

    private void UpdateCurrentActivity(int day, int hour)
    {
        RoutineActivity nextActivity = GetActivityForHour(activeDifficulty, hour);
        if (hasCurrentActivity && currentActivity == nextActivity)
            return;

        currentActivity = nextActivity;
        hasCurrentActivity = true;
        string message = $"{ROUTINE_LOG_PREFIX} Day {day}, Hour {hour:00}: {currentActivity}";
        Debug.Log(message);
        LogToConsole(message);
        OnRoutineActivityChanged?.Invoke(currentActivity);
    }

    private RoutineActivity GetActivityForHour(RoutineDifficulty difficulty, int hour)
    {
        RoutineDifficultySchedule schedule = FindSchedule(difficulty);
        if (schedule == null || schedule.entries == null || schedule.entries.Length == 0)
            return RoutineActivity.Idle;

        int normalizedHour = Mathf.Clamp(hour, 0, HOURS_PER_DAY - 1);
        RoutineScheduleEntry selectedEntry = null;
        int selectedHour = -1;
        RoutineScheduleEntry latestEntry = null;
        int latestHour = -1;

        foreach (RoutineScheduleEntry entry in schedule.entries)
        {
            if (entry == null || entry.hour < 0 || entry.hour >= HOURS_PER_DAY)
                continue;

            if (entry.hour > latestHour)
            {
                latestHour = entry.hour;
                latestEntry = entry;
            }

            if (entry.hour <= normalizedHour && entry.hour > selectedHour)
            {
                selectedHour = entry.hour;
                selectedEntry = entry;
            }
        }

        return (selectedEntry ?? latestEntry)?.activity ?? RoutineActivity.Idle;
    }

    private RoutineDifficultySchedule FindSchedule(RoutineDifficulty difficulty)
    {
        foreach (RoutineDifficultySchedule schedule in schedules)
        {
            if (schedule != null && schedule.difficulty == difficulty)
                return schedule;
        }

        return null;
    }

    private void LogSystemMessage(string message)
    {
        string formattedMessage = $"{ROUTINE_SYSTEM_LOG_PREFIX} {message}";
        Debug.Log(formattedMessage);
        LogToConsole(formattedMessage);
    }

    private void LogToConsole(string message)
    {
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log(message, ConsoleLogUI.LogType.System);
    }

    private void EnsureSchedules()
    {
        if (schedules == null)
            schedules = new List<RoutineDifficultySchedule>();

        RoutineDifficulty[] difficulties = (RoutineDifficulty[])Enum.GetValues(typeof(RoutineDifficulty));
        foreach (RoutineDifficulty difficulty in difficulties)
        {
            RoutineDifficultySchedule schedule = FindSchedule(difficulty);
            if (schedule == null)
            {
                schedules.Add(CreateDefaultSchedule(difficulty));
            }
            else if (schedule.entries == null || schedule.entries.Length == 0)
            {
                schedule.entries = CreateDefaultEntries(difficulty);
            }
        }
    }

    private RoutineDifficultySchedule CreateDefaultSchedule(RoutineDifficulty difficulty)
    {
        return new RoutineDifficultySchedule(difficulty, CreateDefaultEntries(difficulty));
    }

    private RoutineScheduleEntry[] CreateDefaultEntries(RoutineDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RoutineDifficulty.Easy:
                return CreateEntries(
                    new RoutineScheduleEntry(0, RoutineActivity.Sleeping),
                    new RoutineScheduleEntry(6, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(7, RoutineActivity.Bathing),
                    new RoutineScheduleEntry(8, RoutineActivity.EatingBreakfast),
                    new RoutineScheduleEntry(9, RoutineActivity.CommutingToWorkOrSchool),
                    new RoutineScheduleEntry(10, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(17, RoutineActivity.Exercising),
                    new RoutineScheduleEntry(18, RoutineActivity.EatingDinner),
                    new RoutineScheduleEntry(20, RoutineActivity.Relaxing),
                    new RoutineScheduleEntry(22, RoutineActivity.Sleeping));
            case RoutineDifficulty.Medium:
                return CreateEntries(
                    new RoutineScheduleEntry(0, RoutineActivity.Sleeping),
                    new RoutineScheduleEntry(7, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(8, RoutineActivity.Bathing),
                    new RoutineScheduleEntry(9, RoutineActivity.EatingBreakfast),
                    new RoutineScheduleEntry(10, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(12, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(14, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(15, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(18, RoutineActivity.EatingDinner),
                    new RoutineScheduleEntry(20, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(23, RoutineActivity.Sleeping));
            case RoutineDifficulty.Hard:
                return CreateEntries(
                    new RoutineScheduleEntry(0, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(2, RoutineActivity.Sleeping),
                    new RoutineScheduleEntry(9, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(10, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(12, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(14, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(16, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(19, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(21, RoutineActivity.LeisureTime));
            default:
                return CreateEntries(
                    new RoutineScheduleEntry(0, RoutineActivity.Sleeping),
                    new RoutineScheduleEntry(6, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(7, RoutineActivity.Bathing),
                    new RoutineScheduleEntry(8, RoutineActivity.EatingBreakfast),
                    new RoutineScheduleEntry(9, RoutineActivity.CommutingToWorkOrSchool),
                    new RoutineScheduleEntry(10, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(12, RoutineActivity.EatingLunch),
                    new RoutineScheduleEntry(15, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(18, RoutineActivity.EatingDinner),
                    new RoutineScheduleEntry(21, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(23, RoutineActivity.Sleeping));
        }
    }

    private RoutineScheduleEntry[] CreateEntries(params RoutineScheduleEntry[] entries)
    {
        return entries;
    }

    public RoutineDifficulty ActiveDifficulty => activeDifficulty;

    public RoutineActivity CurrentActivity => currentActivity;

    public bool IsSleepingAtHour(int hour)
    {
        return GetActivityForHour(activeDifficulty, hour) == RoutineActivity.Sleeping;
    }
}
