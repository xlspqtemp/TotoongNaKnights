using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the timestamped, color-coded Console Log panel and its category-filtered content.
/// </summary>
public class ConsoleLogUI : MonoBehaviour
{
    public static ConsoleLogUI Instance { get; private set; }

    public enum LogType { Info, Success, Warning, Danger, System }
    public enum LogCategory { Routines, RandomEvents, DevMode, Shared }

    private sealed class LogEntry
    {
        public string Message;
        public LogType Type;
        public LogCategory Category;
        public TextMeshProUGUI VisualLine;
    }

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

    private readonly List<LogEntry> retainedEntries = new List<LogEntry>();
    private readonly List<Button> categoryButtons = new List<Button>();
    private LogCategory selectedCategory = LogCategory.Routines;
    private int totalEvents;
    private bool scrollStateInitialized;
    private bool userReadingOlderEntries;
    private float lastScrollPosition;
    private float lastManualScrollTime;
    private float ignoreScrollChangesUntil;
    private Coroutine pendingScrollCoroutine;

    private static readonly Color TabNormalColor = new Color(0.035f, 0.075f, 0.09f, 0.98f);
    private static readonly Color TabSelectedColor = new Color(0.06f, 0.32f, 0.36f, 1f);
    private static readonly Color TabTextColor = new Color(0.75f, 0.86f, 0.89f, 1f);
    private static readonly string[] TabLabels = { "Routines", "Random Events", "Dev Mode" };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateCategoryTabs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        UpdateScrollReadingState();
    }

    /// <summary>Pushes a shared line into the console, visible in each category tab.</summary>
    public void Log(string message, LogType type = LogType.Info)
    {
        AddEntry(message, type, LogCategory.Shared);
    }

    /// <summary>Logs routine activity and routine-system feedback in the Routines tab.</summary>
    public void LogRoutine(string message, LogType type = LogType.System)
    {
        AddEntry(message, type, LogCategory.Routines);
    }

    /// <summary>Logs random-event details in the Random Events tab.</summary>
    public void LogRandomEvent(string message, RandomEventData eventData, LogType type)
    {
        string eventMessage = eventData == null
            ? message
            : $"{eventData.eventName} ({eventData.severity}) — {eventData.eventDescription}";
        AddEntry(eventMessage, type, LogCategory.RandomEvents);
    }

    /// <summary>Logs debugger shortcuts and debugger-mode feedback in the Dev Mode tab.</summary>
    public void LogDevMode(string message, LogType type = LogType.Info)
    {
        AddEntry(message, type, LogCategory.DevMode);
    }

    private void AddEntry(string message, LogType type, LogCategory category)
    {
        if (logLinePrefab == null || contentParent == null)
        {
            Debug.LogWarning("[ConsoleLogUI] Missing prefab or content parent.");
            return;
        }

        LogEntry entry = new LogEntry
        {
            Message = StripRichText(message ?? string.Empty),
            Type = type,
            Category = category
        };
        retainedEntries.Add(entry);
        totalEvents++;
        UpdateEventCount();
        RenderEntryIfVisible(entry);
        TrimOldLines();

        if (entry.VisualLine != null && autoScroll)
        {
            if (!pauseAutoScrollWhenReading || !userReadingOlderEntries)
                RequestAutoScroll();
            else
                ignoreScrollChangesUntil = Time.unscaledTime + 0.2f;
        }
    }

    private void CreateCategoryTabs()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
            return;

        RectTransform viewportRect = scrollRect != null ? scrollRect.viewport : null;
        if (viewportRect != null)
        {
            Vector2 bottomOffset = viewportRect.offsetMin;
            Vector2 topOffset = viewportRect.offsetMax;
            bottomOffset.y = 10f;
            topOffset.y = -80f;
            viewportRect.offsetMin = bottomOffset;
            viewportRect.offsetMax = topOffset;
        }

        GameObject tabBarObject = new GameObject("ConsoleCategoryTabs", typeof(RectTransform));
        tabBarObject.transform.SetParent(panelRect, false);
        RectTransform tabBarRect = tabBarObject.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0f, 1f);
        tabBarRect.anchorMax = new Vector2(1f, 1f);
        tabBarRect.pivot = new Vector2(0.5f, 1f);
        tabBarRect.anchoredPosition = new Vector2(0f, -38f);
        tabBarRect.sizeDelta = new Vector2(-20f, 24f);

        for (int tabIndex = 0; tabIndex < TabLabels.Length; tabIndex++)
        {
            int capturedIndex = tabIndex;
            GameObject buttonObject = new GameObject(TabLabels[tabIndex].Replace(" ", "") + "Tab",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(tabBarRect, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            float anchorMinX = tabIndex / (float)TabLabels.Length;
            float anchorMaxX = (tabIndex + 1f) / TabLabels.Length;
            buttonRect.anchorMin = new Vector2(anchorMinX, 0f);
            buttonRect.anchorMax = new Vector2(anchorMaxX, 1f);
            buttonRect.offsetMin = new Vector2(2f, 0f);
            buttonRect.offsetMax = new Vector2(-2f, 0f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = TabNormalColor;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = TabNormalColor,
                highlightedColor = new Color(0.07f, 0.19f, 0.22f, 1f),
                pressedColor = TabSelectedColor,
                selectedColor = TabSelectedColor,
                disabledColor = TabNormalColor,
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            LogCategory category = (LogCategory)tabIndex;
            button.onClick.AddListener(() => SelectCategory(category));
            categoryButtons.Add(button);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 11f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = TabTextColor;
            label.text = TabLabels[tabIndex];
            label.raycastTarget = false;
        }

        RefreshCategoryTabColors();
    }

    private void SelectCategory(LogCategory category)
    {
        if (category == LogCategory.Shared || selectedCategory == category)
            return;

        selectedCategory = category;
        RefreshCategoryTabColors();
        foreach (LogEntry entry in retainedEntries)
        {
            if (IsVisibleInSelectedCategory(entry))
                RenderEntryIfVisible(entry);
            else if (entry.VisualLine != null)
            {
                Destroy(entry.VisualLine.gameObject);
                entry.VisualLine = null;
            }
        }

        userReadingOlderEntries = false;
        RequestAutoScroll();
    }

    private void RefreshCategoryTabColors()
    {
        for (int index = 0; index < categoryButtons.Count; index++)
        {
            Image background = categoryButtons[index].GetComponent<Image>();
            if (background != null)
                background.color = (int)selectedCategory == index ? TabSelectedColor : TabNormalColor;
        }
    }

    private bool IsVisibleInSelectedCategory(LogEntry entry)
    {
        return entry.Category == LogCategory.Shared || entry.Category == selectedCategory;
    }

    private void RenderEntryIfVisible(LogEntry entry)
    {
        if (entry.VisualLine != null || !IsVisibleInSelectedCategory(entry))
            return;

        TextMeshProUGUI line = Instantiate(logLinePrefab, contentParent);
        line.text = $"[{GetTimestamp()}] {entry.Message}";
        line.color = GetColor(entry.Type);
        line.gameObject.SetActive(true);
        entry.VisualLine = line;
    }

    private static string StripRichText(string message)
    {
        return Regex.Replace(message, "<.*?>", string.Empty).Trim();
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
        int hour = useGameClockTimestamp && dayCounter != null
            ? dayCounter.CurrentHour
            : System.DateTime.Now.Hour;
        int displayHour = hour % 12;
        if (displayHour == 0)
            displayHour = 12;
        return $"{displayHour} {(hour < 12 ? "AM" : "PM")}";
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
        while (retainedEntries.Count > lineLimit)
        {
            LogEntry oldest = retainedEntries[0];
            if (oldest.VisualLine != null)
                Destroy(oldest.VisualLine.gameObject);
            retainedEntries.RemoveAt(0);
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

    /// <summary>Clears all retained log entries and resets the event count.</summary>
    public void ClearLog()
    {
        foreach (LogEntry entry in retainedEntries)
        {
            if (entry.VisualLine != null)
                Destroy(entry.VisualLine.gameObject);
        }

        retainedEntries.Clear();
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
