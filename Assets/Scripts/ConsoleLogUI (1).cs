using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the timestamped, color-coded Console Log panel and its scrollable content.
/// </summary>
public class ConsoleLogUI : MonoBehaviour
{
    public static ConsoleLogUI Instance { get; private set; }

    public enum LogType { Info, Success, Warning, Danger, System }

    [Header("UI References")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private TextMeshProUGUI logLinePrefab;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TextMeshProUGUI eventCountLabel;

    [Header("Behavior")]
    [Min(1)]
    [SerializeField] private int maxLines = 200;
    [SerializeField] private bool autoScroll = true;
    [SerializeField] private bool useGameClockTimestamp = true;
    [SerializeField] private DayCounterUI dayCounter;

    [Header("Scrolling")]
    [Tooltip("Normalized vertical position above which the player is considered to be reading older entries.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float manualScrollThreshold = 0.03f;
    [Tooltip("How long the console waits after the player's last upward scroll before resuming auto-scroll.")]
    [Min(0.1f)]
    [SerializeField] private float autoScrollIdleTimeout = 2.5f;
    [SerializeField] private bool pauseAutoScrollWhenReading = true;

    [Header("Colors")]
    [SerializeField] private Color infoColor = new Color(0.6f, 0.75f, 0.85f);
    [SerializeField] private Color successColor = new Color(0.35f, 0.85f, 0.45f);
    [SerializeField] private Color warningColor = new Color(0.95f, 0.75f, 0.2f);
    [SerializeField] private Color dangerColor = new Color(0.95f, 0.35f, 0.3f);
    [SerializeField] private Color systemColor = new Color(0.6f, 0.6f, 0.65f);

    private readonly Queue<TextMeshProUGUI> activeLines = new Queue<TextMeshProUGUI>();
    private int totalEvents;
    private bool scrollStateInitialized;
    private bool userReadingOlderEntries;
    private float lastScrollPosition;
    private float lastManualScrollTime;
    private float ignoreScrollChangesUntil;
    private Coroutine pendingScrollCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        UpdateScrollReadingState();
    }

    /// <summary>
    /// Pushes a new line into the console and keeps the newest entries visible unless the player is reading older lines.
    /// </summary>
    public void Log(string message, LogType type = LogType.Info)
    {
        if (logLinePrefab == null || contentParent == null)
        {
            Debug.LogWarning("[ConsoleLogUI] Missing prefab or content parent.");
            return;
        }

        string timestamp = GetTimestamp();
        TextMeshProUGUI line = Instantiate(logLinePrefab, contentParent);
        line.text = $"[{timestamp}] > {message}";
        line.color = GetColor(type);
        line.gameObject.SetActive(true);

        activeLines.Enqueue(line);
        totalEvents++;
        UpdateEventCount();
        TrimOldLines();

        if (autoScroll)
        {
            if (!pauseAutoScrollWhenReading || !userReadingOlderEntries)
                RequestAutoScroll();
            else
                ignoreScrollChangesUntil = Time.unscaledTime + 0.2f;
        }
    }

    private void UpdateScrollReadingState()
    {
        if (scrollRect == null)
            return;

        float currentPosition = scrollRect.verticalNormalizedPosition;
        if (!scrollStateInitialized)
        {
            lastScrollPosition = currentPosition;
            scrollStateInitialized = true;
            return;
        }

        if (Time.unscaledTime < ignoreScrollChangesUntil)
        {
            lastScrollPosition = currentPosition;
            return;
        }

        if (Mathf.Abs(currentPosition - lastScrollPosition) > 0.001f)
        {
            lastScrollPosition = currentPosition;
            lastManualScrollTime = Time.unscaledTime;
            userReadingOlderEntries = currentPosition > manualScrollThreshold;
        }

        if (pauseAutoScrollWhenReading &&
            userReadingOlderEntries &&
            Time.unscaledTime - lastManualScrollTime >= Mathf.Max(0.1f, autoScrollIdleTimeout))
        {
            userReadingOlderEntries = false;
            RequestAutoScroll();
        }
    }

    private string GetTimestamp()
    {
        if (useGameClockTimestamp && dayCounter != null)
            return $"{dayCounter.CurrentHour:00}:{dayCounter.CurrentMinute:00}";

        return System.DateTime.Now.ToString("HH:mm");
    }

    private Color GetColor(LogType type)
    {
        switch (type)
        {
            case LogType.Success: return successColor;
            case LogType.Warning: return warningColor;
            case LogType.Danger: return dangerColor;
            case LogType.System: return systemColor;
            default: return infoColor;
        }
    }

    private void TrimOldLines()
    {
        int lineLimit = Mathf.Max(1, maxLines);
        while (activeLines.Count > lineLimit)
        {
            TextMeshProUGUI oldest = activeLines.Dequeue();
            if (oldest != null)
                Destroy(oldest.gameObject);
        }
    }

    private void UpdateEventCount()
    {
        if (eventCountLabel != null)
            eventCountLabel.text = $"{totalEvents} EVTS";
    }

    private void RequestAutoScroll()
    {
        if (scrollRect == null)
            return;

        if (pendingScrollCoroutine != null)
            StopCoroutine(pendingScrollCoroutine);

        pendingScrollCoroutine = StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        yield return null;

        if (scrollRect != null && (!pauseAutoScrollWhenReading || !userReadingOlderEntries))
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            lastScrollPosition = 0f;
            scrollStateInitialized = true;
            ignoreScrollChangesUntil = Time.unscaledTime + 0.25f;
        }

        pendingScrollCoroutine = null;
    }

    /// <summary>Clears all currently retained visual log lines and resets the console count.</summary>
    public void ClearLog()
    {
        while (activeLines.Count > 0)
        {
            TextMeshProUGUI line = activeLines.Dequeue();
            if (line != null)
                Destroy(line.gameObject);
        }

        totalEvents = 0;
        userReadingOlderEntries = false;
        UpdateEventCount();
        RequestAutoScroll();
    }

    /// <summary>Logs a unit deployment message using the success color.</summary>
    public void LogDeployment(string unitName, string location)
    {
        Log($"{unitName} deployed to {location}", LogType.Success);
    }

    /// <summary>Logs a unit command message.</summary>
    public void LogCommand(string unitName, string command, string target = null)
    {
        string message = string.IsNullOrEmpty(target)
            ? $"{unitName} ordered to {command}"
            : $"{unitName} ordered to {command} → {target}";
        Log(message, LogType.Info);
    }

    /// <summary>Logs a unit status or vitals message.</summary>
    public void LogStatus(string unitName, string statusMessage, LogType type = LogType.Info)
    {
        Log($"{unitName} – {statusMessage}", type);
    }

    /// <summary>Logs a unit loss message using the danger color.</summary>
    public void LogUnitLost(string unitName, string cause = null)
    {
        string message = string.IsNullOrEmpty(cause)
            ? $"{unitName} lost"
            : $"{unitName} lost – {cause}";
        Log(message, LogType.Danger);
    }

    [ContextMenu("Test Log Line")]
    private void TestLog()
    {
        Log("Test event fired from ConsoleLogUI.", LogType.Info);
    }
}
