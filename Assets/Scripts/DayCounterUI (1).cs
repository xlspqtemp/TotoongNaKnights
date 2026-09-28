using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Drives the "DAY COUNTER" panel (mock region #1):
/// DAY [n], ACTIVE badge, HH:MM AM/PM, weather label.
///
/// Fully tunable via ProgressionMode:
///   - RealTime      : day/clock ticks up on its own (classic sim clock)
///   - Manual        : nothing advances automatically; call AdvanceDay() /
///                     TickMinutes() yourself (good for testing or turn-based flow)
///   - ObjectiveBased: clock does NOT run; day only advances when you call
///                     CompleteObjective() (e.g. sector cleared)
///   - Hybrid        : clock ticks like RealTime, but you can also force an
///                     early day-advance via CompleteObjective()
///
/// Attach to an empty GameObject in your HUD scene and wire the
/// TMP fields in the Inspector.
/// </summary>
public class DayCounterUI : MonoBehaviour
{
    public enum ProgressionMode { RealTime, Manual, ObjectiveBased, Hybrid }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI dayLabel;      // "DAY 23"
    [SerializeField] private TextMeshProUGUI statusBadge;   // "ACTIVE"
    [SerializeField] private TextMeshProUGUI timeLabel;     // "06:42 AM"
    [SerializeField] private TextMeshProUGUI weatherLabel;  // "Clear Weather"
    [SerializeField] private GameObject statusBadgeBG;      // optional colored pill behind the badge

    [Header("Progression Mode")]
    [Tooltip("RealTime: ticks on its own. Manual: you call AdvanceDay()/TickMinutes(). " +
             "ObjectiveBased: only advances via CompleteObjective(). Hybrid: ticks AND can be force-advanced.")]
    [SerializeField] private ProgressionMode progressionMode = ProgressionMode.RealTime;

    [Header("Real-Time Tuning")]
    [Tooltip("If enabled, set 'Day Length In Real Seconds' below and the per-minute tick rate is calculated for you.")]
    [SerializeField] private bool useDayLengthShortcut = true;
    [Tooltip("How many real seconds a full in-game day should take (RealTime/Hybrid only).")]
    [SerializeField] private float dayLengthInRealSeconds = 180f;
    [Tooltip("Manual tick rate: real seconds per in-game minute. Only used if the shortcut above is off.")]
    [SerializeField] private float secondsPerGameMinute = 1f;
    [Tooltip("Global speed multiplier you can nudge at runtime (e.g. a fast-forward button).")]
    [Range(0.1f, 10f)]
    [SerializeField] private float timeScale = 1f;

    [Header("Objective-Based Tuning")]
    [Tooltip("If true, CompleteObjective() only advances the day once progress reaches the threshold below.")]
    [SerializeField] private bool requireFullObjective = true;
    [Range(0f, 1f)]
    [SerializeField] private float objectiveCompletionThreshold = 1f;

    [Header("Start State")]
    [SerializeField] private bool isRunning = true;
    [SerializeField] private int startDay = 1;
    [SerializeField] private int startHour = 6;
    [SerializeField] private int startMinute = 0;
    [SerializeField] private string currentWeather = "Clear Weather";

    public event Action<int> OnDayAdvanced;
    public event Action<int, int> OnTimeChanged; // hour, minute
    public event Action<int, int> OnHourAdvanced; // day, hour

    private int currentDay;
    private int currentHour;
    private int currentMinute;
    private float minuteTimer;
    private float runtimeSecondsPerGameMinute;

    private const int MINUTES_PER_DAY = 24 * 60;

    private void Awake()
    {
        currentDay = startDay;
        currentHour = startHour;
        currentMinute = startMinute;
        RecalculateTickRate();
    }

    private void Start()
    {
        RefreshAll();
    }

    private void OnValidate()
    {
        RecalculateTickRate();
    }

    private void RecalculateTickRate()
    {
        runtimeSecondsPerGameMinute = useDayLengthShortcut
            ? Mathf.Max(0.001f, dayLengthInRealSeconds / MINUTES_PER_DAY)
            : Mathf.Max(0.001f, secondsPerGameMinute);
    }

    private void Update()
    {
        bool clockShouldRun = isRunning &&
            (progressionMode == ProgressionMode.RealTime || progressionMode == ProgressionMode.Hybrid);

        if (!clockShouldRun) return;

        minuteTimer += Time.deltaTime * Mathf.Max(0.01f, timeScale);
        if (minuteTimer >= runtimeSecondsPerGameMinute)
        {
            minuteTimer -= runtimeSecondsPerGameMinute;
            TickMinutes(1);
        }
    }

    /// <summary>Manually advance the clock by N in-game minutes. Works in any mode
    /// (useful for Manual mode, turn-based ticks, or debug skip buttons).</summary>
    public void TickMinutes(int minutes)
    {
        for (int i = 0; i < minutes; i++)
        {
            currentMinute++;
            if (currentMinute >= 60)
            {
                currentMinute = 0;
                currentHour++;
                if (currentHour >= 24)
                {
                    currentHour = 0;
                    AdvanceDay();
                }

                OnHourAdvanced?.Invoke(currentDay, currentHour);
            }
        }

        OnTimeChanged?.Invoke(currentHour, currentMinute);
        RefreshTime();
    }

    /// <summary>Force the day to advance by one, regardless of mode.
    /// Always available for debug/testing.</summary>
    public void AdvanceDay()
    {
        currentDay++;
        OnDayAdvanced?.Invoke(currentDay);
        RefreshDay();

        // Optional: auto-log to the console.
        // ConsoleLogUI.Instance?.Log($"Day {currentDay} began.", ConsoleLogUI.LogType.System);
    }

    /// <summary>Call this when a sector/objective's progress changes.
    /// In ObjectiveBased mode, this is what actually drives day advancement.
    /// In Hybrid mode, it lets you force an early advance on top of the running clock.
    /// In RealTime/Manual mode, this is a no-op (clock/manual calls drive it instead).</summary>
    /// <param name="progress01">Completion progress from 0 to 1 (e.g. 0.68 for 68%).</param>
    public void CompleteObjective(float progress01)
    {
        if (progressionMode != ProgressionMode.ObjectiveBased && progressionMode != ProgressionMode.Hybrid)
            return;

        bool meetsThreshold = requireFullObjective
            ? progress01 >= objectiveCompletionThreshold
            : progress01 > 0f;

        if (meetsThreshold)
            AdvanceDay();
    }

    public void SetActive(bool active)
    {
        isRunning = active;
        if (statusBadge != null)
            statusBadge.text = active ? "ACTIVE" : "PAUSED";
        if (statusBadgeBG != null)
        {
            var img = statusBadgeBG.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
                img.color = active ? new Color(0.15f, 0.55f, 0.3f) : new Color(0.5f, 0.15f, 0.15f);
        }
    }

    public void SetWeather(string weather)
    {
        currentWeather = weather;
        if (weatherLabel != null)
            weatherLabel.text = weather;
    }

    public void SetDay(int day)
    {
        currentDay = day;
        RefreshDay();
    }

    public void SetTime(int hour, int minute)
    {
        currentHour = Mathf.Clamp(hour, 0, 23);
        currentMinute = Mathf.Clamp(minute, 0, 59);
        RefreshTime();
    }

    /// <summary>Runtime speed control, e.g. wire to a fast-forward/slow-mo button.</summary>
    public void SetTimeScale(float scale)
    {
        timeScale = Mathf.Max(0.01f, scale);
    }

    /// <summary>Switch modes at runtime if needed (e.g. lock the clock during cutscenes).</summary>
    public void SetProgressionMode(ProgressionMode mode)
    {
        progressionMode = mode;
    }

    private void RefreshAll()
    {
        RefreshDay();
        RefreshTime();
        SetActive(isRunning);
        SetWeather(currentWeather);
    }

    private void RefreshDay()
    {
        if (dayLabel != null)
            dayLabel.text = $"DAY {currentDay}";
    }

    private void RefreshTime()
    {
        if (timeLabel == null) return;

        int displayHour = currentHour % 12;
        if (displayHour == 0) displayHour = 12;
        string ampm = currentHour < 12 ? "AM" : "PM";
        timeLabel.text = $"{displayHour:00}:{currentMinute:00} {ampm}";
    }

    public int CurrentDay => currentDay;
    public int CurrentHour => currentHour;
    public int CurrentMinute => currentMinute;
    public bool IsRunning => isRunning;
    public ProgressionMode CurrentMode => progressionMode;
}
