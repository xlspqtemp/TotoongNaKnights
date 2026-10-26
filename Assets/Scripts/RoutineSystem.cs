using System;
using System.Collections.Generic;
using UnityEngine;
public class RoutineSystem : MonoBehaviour
{
    private const int HOURS_PER_DAY = 24;
    private const int DEFAULT_DEBUG_JUMP_HOUR = 12;

    private enum DifficultyListener { None, Routine, RandomEvent }

    public static RoutineSystem Instance { get; private set; }

    public static event Action<RoutineActivity> OnRoutineActivityChanged;

    [Header("References")]
    [SerializeField] private DayCounterUI dayCounter;

    [Header("Runtime Difficulty")]
    [SerializeField] private RoutineDifficulty activeDifficulty = RoutineDifficulty.Normal;

    [Header("Difficulty Schedules")]
    [SerializeField] private List<RoutineDifficultySchedule> schedules = new List<RoutineDifficultySchedule>();

    private RoutineActivity currentActivity;
    private bool hasCurrentActivity;
    private bool debuggerModeEnabled;
    private DifficultyListener activeDifficultyListener;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        HandleDebuggerInput();
#endif
    }

    private void HandleDebuggerInput()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            debuggerModeEnabled = !debuggerModeEnabled;
            activeDifficultyListener = DifficultyListener.None;
            LogDebuggerFeedback(debuggerModeEnabled
                ? "F1: Debugger mode enabled. F2/F3 set difficulty; F4 forces an event; F6-F8 and F10 run legacy debug controls."
                : "F1: Debugger mode disabled.");
            return;
        }

        if (!debuggerModeEnabled)
            return;

        if (Input.GetKeyDown(KeyCode.F2))
        {
            activeDifficultyListener = activeDifficultyListener == DifficultyListener.Routine
                ? DifficultyListener.None
                : DifficultyListener.Routine;
            LogDebuggerFeedback(activeDifficultyListener == DifficultyListener.Routine
                ? "F2: Choose routine difficulty with 1 Easy, 2 Normal, 3 Medium, or 4 Hard. Press F2 again to cancel."
                : "F2: Routine difficulty selection closed.");
            return;
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            activeDifficultyListener = activeDifficultyListener == DifficultyListener.RandomEvent
                ? DifficultyListener.None
                : DifficultyListener.RandomEvent;
            LogDebuggerFeedback(activeDifficultyListener == DifficultyListener.RandomEvent
                ? "F3: Choose random event difficulty with 1 Easy, 2 Normal, 3 Medium, or 4 Hard. Press F3 again to cancel."
                : "F3: Random event difficulty selection closed.");
            return;
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            RandomEventSystem randomEventSystem = RandomEventSystem.Instance;
            bool eventTriggered = randomEventSystem != null && randomEventSystem.TryForceTriggerEvent();
            LogDebuggerFeedback(eventTriggered
                ? "F4: Random event forced."
                : "F4: No random event could be triggered.");
            return;
        }

        if (activeDifficultyListener == DifficultyListener.None)
        {
            if (Input.GetKeyDown(KeyCode.F6))
            {
                JumpToHour(DEFAULT_DEBUG_JUMP_HOUR);
                LogDebuggerFeedback("F6: Clock jumped to 12 PM.");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                ForceAdvanceHour();
                LogDebuggerFeedback("F7: Advanced the game clock by one hour.");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                CycleDifficulty();
                LogDebuggerFeedback("F8: Cycled routine difficulty to " + activeDifficulty + ".");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                RandomEventSystem randomEventSystem = RandomEventSystem.Instance;
                if (randomEventSystem != null)
                {
                    randomEventSystem.CycleDifficulty();
                    LogDebuggerFeedback("F10: Cycled random event difficulty to " + randomEventSystem.ActiveDifficulty + ".");
                }
                else
                {
                    LogDebuggerFeedback("F10: Random Event System is unavailable.", ConsoleLogUI.LogType.Warning);
                }
                return;
            }
        }

        if (activeDifficultyListener == DifficultyListener.None || !TryGetDifficultyKey(out int difficultyIndex))
            return;

        if (activeDifficultyListener == DifficultyListener.Routine)
        {
            RoutineDifficulty difficulty = (RoutineDifficulty)(difficultyIndex - 1);
            SetDifficulty(difficulty);
            LogDebuggerFeedback("F2: Routine difficulty set to " + difficulty + ".");
        }
        else
        {
            RandomEventDifficulty difficulty = (RandomEventDifficulty)(difficultyIndex - 1);
            RandomEventSystem randomEventSystem = RandomEventSystem.Instance;
            if (randomEventSystem != null)
            {
                randomEventSystem.SetDifficulty(difficulty);
                LogDebuggerFeedback("F3: Random event difficulty set to " + difficulty + ".");
            }
            else
            {
                LogDebuggerFeedback("F3: Random Event System is unavailable.", ConsoleLogUI.LogType.Warning);
            }
        }

        activeDifficultyListener = DifficultyListener.None;
    }

    private static bool TryGetDifficultyKey(out int difficultyIndex)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            difficultyIndex = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            difficultyIndex = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            difficultyIndex = 3;
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            difficultyIndex = 4;
        else
        {
            difficultyIndex = 0;
            return false;
        }

        return true;
    }

    private static void LogDebuggerFeedback(string message, ConsoleLogUI.LogType type = ConsoleLogUI.LogType.Info)
    {
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.LogDevMode(message, type);
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

    [ContextMenu("Routine/Jump To Default Debug Hour")]
    public void JumpToConfiguredHour()
    {
        JumpToHour(DEFAULT_DEBUG_JUMP_HOUR);
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
        string message = currentActivity.ToString();
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
        Debug.Log(message);
        LogToConsole(message);
    }

    private void LogToConsole(string message)
    {
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.LogRoutine(message, ConsoleLogUI.LogType.System);
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

        foreach (RoutineDifficultySchedule schedule in schedules)
        {
            if (schedule != null)
                EnsureRoutineActivities(schedule);
        }
    }

    private void EnsureRoutineActivities(RoutineDifficultySchedule schedule)
    {
        int sleepingInHour;
        int drinkingWaterHour = 12;
        int walkingLightActivityHour;
        int recoveryBreakHour;
        int prolongedInactivityHour;

        switch (schedule.difficulty)
        {
            case RoutineDifficulty.Easy:
                sleepingInHour = 6;
                walkingLightActivityHour = 16;
                recoveryBreakHour = 14;
                prolongedInactivityHour = 21;
                break;
            case RoutineDifficulty.Medium:
                sleepingInHour = 7;
                walkingLightActivityHour = 16;
                recoveryBreakHour = 15;
                prolongedInactivityHour = 21;
                break;
            case RoutineDifficulty.Hard:
                sleepingInHour = 9;
                walkingLightActivityHour = 17;
                recoveryBreakHour = 15;
                prolongedInactivityHour = 20;
                break;
            default:
                sleepingInHour = 6;
                walkingLightActivityHour = 16;
                recoveryBreakHour = 14;
                prolongedInactivityHour = 22;
                break;
        }

        EnsureRoutineActivityEntry(schedule, RoutineActivity.DrinkingWater, drinkingWaterHour);
        EnsureRoutineActivityEntry(schedule, RoutineActivity.WalkingLightActivity, walkingLightActivityHour);
        EnsureRoutineActivityEntry(schedule, RoutineActivity.RecoveryBreak, recoveryBreakHour);
        EnsureRoutineActivityEntry(schedule, RoutineActivity.SleepingIn, sleepingInHour);
        EnsureRoutineActivityEntry(schedule, RoutineActivity.ProlongedInactivity, prolongedInactivityHour);
    }

    private static void EnsureRoutineActivityEntry(RoutineDifficultySchedule schedule, RoutineActivity activity, int preferredHour)
    {
        if (schedule.entries == null)
            schedule.entries = new RoutineScheduleEntry[0];

        foreach (RoutineScheduleEntry entry in schedule.entries)
        {
            if (entry != null && entry.activity == activity)
                return;
        }

        for (int offset = 0; offset < HOURS_PER_DAY; offset++)
        {
            int laterHour = preferredHour + offset;
            if (laterHour < HOURS_PER_DAY && !IsRoutineHourOccupied(schedule.entries, laterHour))
            {
                AddRoutineActivityEntry(schedule, activity, laterHour);
                return;
            }

            int earlierHour = preferredHour - offset;
            if (offset > 0 && earlierHour >= 0 && !IsRoutineHourOccupied(schedule.entries, earlierHour))
            {
                AddRoutineActivityEntry(schedule, activity, earlierHour);
                return;
            }
        }
    }

    private static bool IsRoutineHourOccupied(RoutineScheduleEntry[] entries, int hour)
    {
        foreach (RoutineScheduleEntry entry in entries)
        {
            if (entry != null && entry.hour == hour)
                return true;
        }

        return false;
    }

    private static void AddRoutineActivityEntry(RoutineDifficultySchedule schedule, RoutineActivity activity, int hour)
    {
        List<RoutineScheduleEntry> entries = new List<RoutineScheduleEntry>(schedule.entries);
        entries.Add(new RoutineScheduleEntry(hour, activity));
        schedule.entries = entries.ToArray();
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
                    new RoutineScheduleEntry(6, RoutineActivity.SleepingIn),
                    new RoutineScheduleEntry(7, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(8, RoutineActivity.Bathing),
                    new RoutineScheduleEntry(9, RoutineActivity.EatingBreakfast),
                    new RoutineScheduleEntry(10, RoutineActivity.CommutingToWorkOrSchool),
                    new RoutineScheduleEntry(11, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(12, RoutineActivity.DrinkingWater),
                    new RoutineScheduleEntry(13, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(14, RoutineActivity.RecoveryBreak),
                    new RoutineScheduleEntry(15, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(16, RoutineActivity.WalkingLightActivity),
                    new RoutineScheduleEntry(17, RoutineActivity.Exercising),
                    new RoutineScheduleEntry(18, RoutineActivity.EatingDinner),
                    new RoutineScheduleEntry(20, RoutineActivity.Relaxing),
                    new RoutineScheduleEntry(21, RoutineActivity.ProlongedInactivity),
                    new RoutineScheduleEntry(22, RoutineActivity.Sleeping));
            case RoutineDifficulty.Medium:
                return CreateEntries(
                    new RoutineScheduleEntry(0, RoutineActivity.Sleeping),
                    new RoutineScheduleEntry(7, RoutineActivity.SleepingIn),
                    new RoutineScheduleEntry(8, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(9, RoutineActivity.Bathing),
                    new RoutineScheduleEntry(10, RoutineActivity.EatingBreakfast),
                    new RoutineScheduleEntry(11, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(12, RoutineActivity.DrinkingWater),
                    new RoutineScheduleEntry(13, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(14, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(15, RoutineActivity.RecoveryBreak),
                    new RoutineScheduleEntry(16, RoutineActivity.WalkingLightActivity),
                    new RoutineScheduleEntry(18, RoutineActivity.EatingDinner),
                    new RoutineScheduleEntry(20, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(21, RoutineActivity.ProlongedInactivity),
                    new RoutineScheduleEntry(23, RoutineActivity.Sleeping));
            case RoutineDifficulty.Hard:
                return CreateEntries(
                    new RoutineScheduleEntry(0, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(2, RoutineActivity.Sleeping),
                    new RoutineScheduleEntry(9, RoutineActivity.SleepingIn),
                    new RoutineScheduleEntry(10, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(11, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(12, RoutineActivity.DrinkingWater),
                    new RoutineScheduleEntry(14, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(15, RoutineActivity.RecoveryBreak),
                    new RoutineScheduleEntry(16, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(17, RoutineActivity.WalkingLightActivity),
                    new RoutineScheduleEntry(19, RoutineActivity.EatingJunkFood),
                    new RoutineScheduleEntry(20, RoutineActivity.ProlongedInactivity),
                    new RoutineScheduleEntry(21, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(22, RoutineActivity.Sleeping));
            default:
                return CreateEntries(
                    new RoutineScheduleEntry(0, RoutineActivity.Sleeping),
                    new RoutineScheduleEntry(6, RoutineActivity.SleepingIn),
                    new RoutineScheduleEntry(7, RoutineActivity.WakingUp),
                    new RoutineScheduleEntry(8, RoutineActivity.Bathing),
                    new RoutineScheduleEntry(9, RoutineActivity.EatingBreakfast),
                    new RoutineScheduleEntry(10, RoutineActivity.CommutingToWorkOrSchool),
                    new RoutineScheduleEntry(11, RoutineActivity.WorkingOrStudying),
                    new RoutineScheduleEntry(12, RoutineActivity.DrinkingWater),
                    new RoutineScheduleEntry(13, RoutineActivity.EatingLunch),
                    new RoutineScheduleEntry(14, RoutineActivity.RecoveryBreak),
                    new RoutineScheduleEntry(15, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(16, RoutineActivity.WalkingLightActivity),
                    new RoutineScheduleEntry(18, RoutineActivity.EatingDinner),
                    new RoutineScheduleEntry(20, RoutineActivity.LeisureTime),
                    new RoutineScheduleEntry(22, RoutineActivity.ProlongedInactivity),
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
