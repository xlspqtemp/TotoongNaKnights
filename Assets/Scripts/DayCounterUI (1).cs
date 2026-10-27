using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
    [Tooltip("Additional per-clock multiplier. Global fast-forward is applied separately by GameplaySpeed.DeltaTime.")]
    [Range(0.1f, 10f)]
    [SerializeField] private float timeScale = 1f;

    [Header("Objective-Based Tuning")]
    [Tooltip("If true, CompleteObjective() only advances the day once progress reaches the threshold below.")]
    [SerializeField] private bool requireFullObjective = true;
    [Range(0f, 1f)]
    [SerializeField] private float objectiveCompletionThreshold = 1f;

    [Header("Run Length")]
    [SerializeField, Min(1)] private int totalGameDays = 14;

    [Header("Start State")]
    [SerializeField] private bool isRunning = true;
    [SerializeField] private string currentWeather = "Clear Weather";

    public event Action<int> OnDayAdvanced;
    public event Action<int, int> OnTimeChanged; // hour, minute
    public event Action<int, int> OnHourAdvanced; // day, hour

    private int currentDay;
    private int currentHour;
    private int currentMinute;
    private double minuteTimer;

    private const int MINUTES_PER_GAME_HOUR = 60;
    private const int INITIAL_DAY = 0;
    private const int INITIAL_HOUR = 22;
    private const double REAL_SECONDS_PER_GAME_HOUR = 5d;
    private const double REAL_SECONDS_PER_GAME_MINUTE = REAL_SECONDS_PER_GAME_HOUR / MINUTES_PER_GAME_HOUR;

    private void Awake()
    {
        GameplaySpeed.ResetForNewRun();
        currentDay = INITIAL_DAY;
        currentHour = INITIAL_HOUR;
        currentMinute = 0;
    }

    private void Start()
    {
        ConfigureCompactHeader();
        RefreshAll();
        if (GetComponent<GameplaySpeedButton>() == null)
            gameObject.AddComponent<GameplaySpeedButton>();
    }

    private void ConfigureCompactHeader()
    {
        VerticalLayoutGroup layoutGroup = GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
            layoutGroup.enabled = false;

        ConfigureHeaderLabel(dayLabel, new Vector2(12f, 0f), new Vector2(84f, 0f));
        ConfigureHeaderLabel(timeLabel, new Vector2(98f, 0f), new Vector2(94f, 0f));

        if (dayLabel != null)
        {
            dayLabel.fontSize = 16f;
            dayLabel.fontStyle = FontStyles.Normal;
            dayLabel.alignment = TextAlignmentOptions.MidlineLeft;
            dayLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (timeLabel != null)
        {
            timeLabel.fontSize = 16f;
            timeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            timeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    private static void ConfigureHeaderLabel(TextMeshProUGUI label, Vector2 position, Vector2 size)
    {
        if (label == null)
            return;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = position;
        labelRect.sizeDelta = size;
    }

    private void Update()
    {
        bool clockShouldRun = isRunning &&
            (progressionMode == ProgressionMode.RealTime || progressionMode == ProgressionMode.Hybrid);

        if (!clockShouldRun) return;

        minuteTimer += (double)GameplaySpeed.DeltaTime * Mathf.Max(0.01f, timeScale);
        while (minuteTimer >= REAL_SECONDS_PER_GAME_MINUTE)
        {
            minuteTimer -= REAL_SECONDS_PER_GAME_MINUTE;
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
        minuteTimer = 0d;
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
            dayLabel.text = $"Day {currentDay} |";
    }

    private void RefreshTime()
    {
        if (timeLabel == null) return;

        int displayHour = currentHour % 12;
        if (displayHour == 0) displayHour = 12;
        string ampm = currentHour < 12 ? "AM" : "PM";
        timeLabel.text = $"{displayHour} {ampm}";
    }

    public int CurrentDay => currentDay;
    public int CurrentHour => currentHour;
    public int CurrentMinute => currentMinute;
    public bool IsRunning => isRunning;
    public ProgressionMode CurrentMode => progressionMode;
    public int TotalGameDays => totalGameDays;
}
