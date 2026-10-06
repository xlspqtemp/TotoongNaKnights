using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Owns wellness, connects existing routine and random-event broadcasts, and resolves the run outcome.</summary>
public class WellnessManager : MonoBehaviour
{
    [Serializable]
    public sealed class WellnessEventDelta
    {
        public string eventId;
        public float delta;

        public WellnessEventDelta(string eventId, float delta)
        {
            this.eventId = eventId;
            this.delta = delta;
        }
    }

    [Serializable] public sealed class WellnessChangedUnityEvent : UnityEvent<float, float> { }
    [Serializable] public sealed class WellnessResultUnityEvent : UnityEvent<WellnessRunResult> { }

    private sealed class WellnessEventProgress
    {
        public string qteId;
        public bool reachedTarget;
        public readonly HashSet<string> awardedStages = new HashSet<string>(StringComparer.Ordinal);
    }

    public static WellnessManager Instance { get; private set; }

    public event Action<float, float> OnWellnessChanged;
    public event Action<float> OnWellnessDeltaApplied;
    public event Action OnCriticalEntered;
    public event Action<WellnessRunResult> OnGameWon;
    public event Action<WellnessRunResult> OnGameLost;

    [Header("Wellness")]
    [SerializeField, Min(1f)] private float maxWellness = 100f;
    [SerializeField, Range(0f, 100f)] private float currentWellness = 100f;
    [SerializeField, Min(0f)] private float criticalThreshold = 25f;
    [SerializeField, Min(0f)] private float winThreshold = 50f;

    [Header("Routine Event Deltas")]
    [SerializeField] private List<WellnessEventDelta> routineEventDeltas = new List<WellnessEventDelta>
    {
        new WellnessEventDelta("EatingBreakfast", 0f),
        new WellnessEventDelta("SkippingMeal", 0f),
        new WellnessEventDelta("SleepingOnSchedule", 0f),
        new WellnessEventDelta("SleepingLate", 0f),
        new WellnessEventDelta("Jogging", 0f),
        new WellnessEventDelta("Overexertion", 0f),
        new WellnessEventDelta("PlayingOrRecreation", 0f),
        new WellnessEventDelta("LeisureTime", 0f),
        new WellnessEventDelta("Idling", 0f),
        new WellnessEventDelta("EatingLunch", 0f),
        new WellnessEventDelta("EatingJunkFood", 0f),
        new WellnessEventDelta("EatingDinner", 0f),
        new WellnessEventDelta("WakingUp", 0f),
        new WellnessEventDelta("Bathing", 0f),
        new WellnessEventDelta("CommutingToWorkOrSchool", 0f),
        new WellnessEventDelta("WorkingOrStudying", 0f),
        new WellnessEventDelta("Relaxing", 0f),
        new WellnessEventDelta("DrinkingWater", 0f),
        new WellnessEventDelta("WalkingLightActivity", 0f),
        new WellnessEventDelta("RecoveryBreak", 0f),
        new WellnessEventDelta("SleepingIn", 0f),
        new WellnessEventDelta("ProlongedInactivity", 0f)
    };

    [Header("Random Event Deltas")]
    [SerializeField] private List<WellnessEventDelta> randomEventDeltas = new List<WellnessEventDelta>
    {
        new WellnessEventDelta("AteExpiredFood", -5f),
        new WellnessEventDelta("InhaledDustOrAllergen", -5f),
        new WellnessEventDelta("SkippedMeal", 0f),
        new WellnessEventDelta("Overexertion", 0f),
        new WellnessEventDelta("StressOrPoorHydration", 0f),
        new WellnessEventDelta("JunkFoodBinge", 0f),
        new WellnessEventDelta("ArgumentOrConflict", 0f),
        new WellnessEventDelta("ColdFromSickPerson", -5f),
        new WellnessEventDelta("SunOrFreshAir", 0f),
        new WellnessEventDelta("NickedOrScraped", -5f),
        new WellnessEventDelta("ContaminatedWater", -5f),
        new WellnessEventDelta("TrippedAndScratched", -5f),
        new WellnessEventDelta("AccidentallyAteSpoiledFood", -5f),
        new WellnessEventDelta("ContactWithSickPerson", -5f),
        new WellnessEventDelta("SmokedCigarette", -5f)
    };

    [Header("Random Event Compatibility")]
    [Tooltip("Re-enable the legacy direct wellness penalty from recognized random events. Disabled for dispatch-style infections.")]
    [SerializeField] private bool applyRandomEventWellnessEffects;

    [Header("Hazard and QTE Result Deltas")]
    [SerializeField] private List<WellnessEventDelta> hazardEventDeltas = new List<WellnessEventDelta>
    {
        new WellnessEventDelta("SpoiledFood_QTESuccess", -2f),
        new WellnessEventDelta("SpoiledFood_QTEFail", -8f),
        new WellnessEventDelta("RespiratoryBreach_QTESuccess", -2f),
        new WellnessEventDelta("RespiratoryBreach_QTEFail", -8f),
        new WellnessEventDelta("MinorInjury", -4f),
        new WellnessEventDelta("EscortFailure", -10f)
    };

    [Header("Bacteremia (currently not connected to a project state)")]
    [SerializeField, Min(0f)] private float bacteremiaDrainPerSecond = 1f;
    [SerializeField, Min(0f)] private float feverThresholdSeconds = 60f;

    [Header("Day Counter")]
    [SerializeField] private DayCounterUI dayCounter;

    [Header("Events for Unity Inspector listeners")]
    public WellnessChangedUnityEvent onWellnessChanged = new WellnessChangedUnityEvent();
    public UnityEvent onCriticalEntered = new UnityEvent();
    public WellnessResultUnityEvent onGameWon = new WellnessResultUnityEvent();
    public WellnessResultUnityEvent onGameLost = new WellnessResultUnityEvent();

    private const string GameOverMessage = "Your human has reached a critical state.\nGame over!";
    private const string VictoryMessage = "Human survived. You won!";
    private const string MainMenuSceneName = "MainMenu";
    private const float RequiredWinWellness = 50f;
    private const int OutcomeButtonWidth = 300;
    private const int OutcomeButtonHeight = 76;
    private const float OutcomeButtonFontSize = 28f;
    private static readonly Color OutcomeButtonColor = new Color(0.08f, 0.38f, 0.42f, 1f);

    private Canvas hudCanvas;
    private GameObject outcomePanel;
    private TextMeshProUGUI outcomeLabel;
    private TextMeshProUGUI leaderboardStatusLabel;
    private Button restartButton;
    private Button mainMenuButton;
    private bool criticalWasEntered;
    private bool finalDayWasEvaluated;
    private bool runHasEnded;
    private bool bacteremiaIsActive;
    private bool feverOverrideTriggered;
    private float bacteremiaActiveSeconds;
    private float bacteremiaDrainTimer;
    private int runStartDay;
    private readonly Dictionary<string, WellnessEventProgress> wellnessEventProgress = new Dictionary<string, WellnessEventProgress>(StringComparer.Ordinal);

    /// <summary>Returns whether this gameplay run has ended and its world simulation should remain stopped.</summary>
    public bool HasRunEnded => runHasEnded;

    /// <summary>Returns the current wellness value.</summary>
    public float CurrentWellness => currentWellness;

    /// <summary>Returns the configured wellness maximum.</summary>
    public float MaxWellness => maxWellness;

    /// <summary>Returns the configured critical threshold.</summary>
    public float CriticalThreshold => criticalThreshold;

    /// <summary>Returns the configured win threshold.</summary>
    public float WinThreshold => winThreshold;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        winThreshold = RequiredWinWellness;
        EnsureAddedRoutineEventEntries();
        EnsureAddedRandomEventEntries();
        if (dayCounter == null)
            dayCounter = FindFirstObjectByType<DayCounterUI>();

        hudCanvas = GetComponentInParent<Canvas>();
        currentWellness = Mathf.Clamp(currentWellness, 0f, Mathf.Max(1f, maxWellness));
    }

    private void OnEnable()
    {
        RoutineSystem.OnRoutineActivityChanged += HandleRoutineActivityChanged;
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;

        if (dayCounter != null)
            dayCounter.OnDayAdvanced += HandleDayAdvanced;
    }

    private void Start()
    {
        if (dayCounter != null)
            runStartDay = dayCounter.CurrentDay;
        else
            Debug.LogWarning("[WELLNESS] DayCounterUI was not found; final-day evaluation is disabled.");

        CreateOutcomePanel();
        PublishWellnessChanged();

        if (currentWellness <= criticalThreshold)
            EnterCriticalState();

        if (currentWellness <= 0f)
            EndRun(WellnessRunResult.Loss);
    }

    private void OnDisable()
    {
        RoutineSystem.OnRoutineActivityChanged -= HandleRoutineActivityChanged;
        RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;

        if (dayCounter != null)
            dayCounter.OnDayAdvanced -= HandleDayAdvanced;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        EnsureAddedRoutineEventEntries();
        EnsureAddedRandomEventEntries();
        maxWellness = Mathf.Max(1f, maxWellness);
        criticalThreshold = Mathf.Clamp(criticalThreshold, 0f, maxWellness);
        winThreshold = RequiredWinWellness;
        currentWellness = Mathf.Clamp(currentWellness, 0f, maxWellness);
        bacteremiaDrainPerSecond = Mathf.Max(0f, bacteremiaDrainPerSecond);
        feverThresholdSeconds = Mathf.Max(0f, feverThresholdSeconds);
    }

    private void Update()
    {
        if (!bacteremiaIsActive || runHasEnded)
            return;

        float elapsed = GameplaySpeed.DeltaTime;
        bacteremiaActiveSeconds += elapsed;
        bacteremiaDrainTimer += elapsed;

        if (!feverOverrideTriggered && bacteremiaActiveSeconds >= feverThresholdSeconds)
        {
            feverOverrideTriggered = true;
            EndRun(WellnessRunResult.Loss);
            return;
        }

        if (bacteremiaDrainTimer >= 1f)
        {
            int wholeSeconds = Mathf.FloorToInt(bacteremiaDrainTimer);
            bacteremiaDrainTimer -= wholeSeconds;
            ApplyWellnessDelta("BacteremiaDrain", -bacteremiaDrainPerSecond * wholeSeconds, false);
        }
    }

    /// <summary>Applies a configured wellness event; unlisted event IDs do not change wellness.</summary>
    public void ApplyEvent(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) || runHasEnded)
            return;

        float delta = FindDelta(hazardEventDeltas, eventId,
            FindDelta(randomEventDeltas, eventId,
                FindDelta(routineEventDeltas, eventId, 0f)));
        ApplyWellnessDelta(eventId, delta);
    }

    /// <summary>Creates a stable key for one random event occurrence.</summary>
    public static string BuildEventKey(string eventId, int day, int hour)
    {
        return $"{eventId}:{day}:{hour}";
    }

    /// <summary>Marks that contamination from an active random event reached its target organ.</summary>
    public void MarkEventReachedTarget(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return;

        LayerSelectionHUD.EndThreatGlow(eventKey);
        if (wellnessEventProgress.TryGetValue(eventKey, out WellnessEventProgress progress))
            progress.reachedTarget = true;
    }

    /// <summary>Awards one wellness milestone for an active random event, at most once per milestone.</summary>
    public bool TryAwardEventPoints(string eventKey, string rewardStage, float points)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || string.IsNullOrWhiteSpace(rewardStage) || runHasEnded ||
            !wellnessEventProgress.TryGetValue(eventKey, out WellnessEventProgress progress) ||
            !progress.awardedStages.Add(rewardStage))
        {
            return false;
        }

        ApplyWellnessDelta($"{eventKey}:{rewardStage}", points);
        return true;
    }

    /// <summary>Awards a response reward based on whether contamination had reached its target.</summary>
    public bool TryAwardEventResponse(string eventKey, string rewardStage, float pointsAfterTarget, float pointsBeforeTarget, bool onlyBeforeTarget = false)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || string.IsNullOrWhiteSpace(rewardStage) || runHasEnded ||
            !wellnessEventProgress.TryGetValue(eventKey, out WellnessEventProgress progress) ||
            (onlyBeforeTarget && progress.reachedTarget) || progress.awardedStages.Contains(rewardStage))
        {
            return false;
        }

        float points = progress.reachedTarget ? pointsAfterTarget : pointsBeforeTarget;
        return TryAwardEventPoints(eventKey, rewardStage, points);
    }

    /// <summary>Resolves the QTE associated with an event after the player completes its response.</summary>
    public void ResolveEventQTE(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return;

        LayerSelectionHUD.EndThreatGlow(eventKey);
        if (wellnessEventProgress.TryGetValue(eventKey, out WellnessEventProgress progress))
            GameplaySpeed.ResolveQTE(progress.qteId);
    }

    /// <summary>Sets whether the currently unwired bacteremia state is active.</summary>
    public void SetBacteremiaActive(bool isActive)
    {
        if (bacteremiaIsActive == isActive || runHasEnded)
            return;

        bacteremiaIsActive = isActive;
        bacteremiaActiveSeconds = 0f;
        bacteremiaDrainTimer = 0f;
    }

    private void HandleRoutineActivityChanged(RoutineActivity activity)
    {
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (!applyRandomEventWellnessEffects || eventData == null || runHasEnded)
            return;

        string eventId;
        switch (eventData.eventName)
        {
            case "Ate expired/spoiled food": eventId = "AteExpiredFood"; break;
            case "Inhaled dust/allergen": eventId = "InhaledDustOrAllergen"; break;
            case "Skipped a meal": eventId = "SkippedMeal"; break;
            case "Overexertion/fatigue from exercise": eventId = "Overexertion"; break;
            case "Mild stress/poor hydration": eventId = "StressOrPoorHydration"; break;
            case "Junk food binge": eventId = "JunkFoodBinge"; break;
            case "Got into an argument / stressful conflict": eventId = "ArgumentOrConflict"; break;
            case "Caught a cold from a sick classmate/coworker": eventId = "ColdFromSickPerson"; break;
            case "Sat in the sun / got fresh air": eventId = "SunOrFreshAir"; break;
            case "Nicked by a sharp object or tripped and scraped knees": eventId = "NickedOrScraped"; break;
            case "Accidentally Drank Contaminated Water": eventId = "ContaminatedWater"; break;
            case "Tripped on a Rock and Got Scratches": eventId = "TrippedAndScratched"; break;
            case "Accidentally Ate Spoiled Food": eventId = "AccidentallyAteSpoiledFood"; break;
            case "Came Into Contact With Someone Who Was Sick": eventId = "ContactWithSickPerson"; break;
            case "Smoked cigarette": eventId = "SmokedCigarette"; break;
            default: return;
        }

        if (eventId != "AteExpiredFood" && eventId != "ContaminatedWater" && eventId != "AccidentallyAteSpoiledFood" &&
            eventId != "InhaledDustOrAllergen" && eventId != "ColdFromSickPerson" && eventId != "ContactWithSickPerson" &&
            eventId != "SmokedCigarette" && eventId != "NickedOrScraped" && eventId != "TrippedAndScratched")
        {
            return;
        }

        string eventKey = BuildEventKey(eventId, eventData.day, eventData.hour);
        wellnessEventProgress[eventKey] = new WellnessEventProgress { qteId = eventData.triggersQTE ? eventData.eventName : null };
        ApplyWellnessDelta(eventId, -5f);
    }

    private void HandleDayAdvanced(int currentDay)
    {
        if (runHasEnded || finalDayWasEvaluated || dayCounter == null)
            return;

        int elapsedDays = currentDay - runStartDay;
        if (elapsedDays < dayCounter.TotalGameDays)
            return;

        finalDayWasEvaluated = true;
        EvaluateFinalWellness();
    }

    private void EvaluateFinalWellness()
    {
        EndRun(currentWellness >= RequiredWinWellness
            ? WellnessRunResult.Win
            : WellnessRunResult.Loss);
    }

    private void ApplyWellnessDelta(string eventId, float requestedDelta, bool showChangeIndicator = true)
    {
        if (runHasEnded)
            return;

        float previousWellness = currentWellness;
        currentWellness = Mathf.Clamp(currentWellness + requestedDelta, 0f, maxWellness);
        float appliedDelta = currentWellness - previousWellness;

        Debug.Log($"[WELLNESS] {eventId}: {FormatSignedDelta(appliedDelta)} -> {Mathf.RoundToInt(currentWellness)}");

        if (!Mathf.Approximately(previousWellness, currentWellness))
        {
            PublishWellnessChanged();
            if (showChangeIndicator)
                OnWellnessDeltaApplied?.Invoke(appliedDelta);

            if (!criticalWasEntered && previousWellness > criticalThreshold && currentWellness <= criticalThreshold)
                EnterCriticalState();
        }

        if (currentWellness <= 0f)
            EndRun(WellnessRunResult.Loss);
    }

    private void PublishWellnessChanged()
    {
        OnWellnessChanged?.Invoke(currentWellness, maxWellness);
        onWellnessChanged?.Invoke(currentWellness, maxWellness);
    }

    private void EnterCriticalState()
    {
        if (criticalWasEntered)
            return;

        criticalWasEntered = true;
        string warning = $"[WELLNESS] Critical wellness entered: {Mathf.RoundToInt(currentWellness)}/{Mathf.RoundToInt(maxWellness)}.";
        Debug.LogWarning(warning);
        LogToConsole(warning, ConsoleLogUI.LogType.Warning);
        OnCriticalEntered?.Invoke();
        onCriticalEntered?.Invoke();
    }

    private void EndRun(WellnessRunResult result)
    {
        if (runHasEnded)
            return;

        runHasEnded = true;
        GameplaySpeed.ResetForRunEnd();
        Time.timeScale = 0f;
        if (dayCounter != null)
            dayCounter.SetActive(false);

        string outcome = result == WellnessRunResult.PerfectWin || result == WellnessRunResult.Win
            ? "WIN"
            : "LOSS";
        string sessionType = LeaderboardManager.GetActiveSessionType();
        Debug.Log($"[WELLNESS] End of run: final wellness={currentWellness:0.##}, outcome={outcome}, session type={sessionType}.");

        string resultMessage = $"[WELLNESS] Final result: {result} ({Mathf.RoundToInt(currentWellness)}/{Mathf.RoundToInt(maxWellness)}).";
        Debug.Log(resultMessage);

        if (leaderboardStatusLabel != null)
            leaderboardStatusLabel.gameObject.SetActive(false);

        if (result == WellnessRunResult.PerfectWin || result == WellnessRunResult.Win)
        {
            LogToConsole(resultMessage, ConsoleLogUI.LogType.Success);
            OnGameWon?.Invoke(result);
            onGameWon?.Invoke(result);
            ShowOutcome(VictoryMessage, false, "Return to Main Menu");
            if (leaderboardStatusLabel != null)
            {
                leaderboardStatusLabel.text = string.Empty;
                leaderboardStatusLabel.gameObject.SetActive(false);
            }

            LeaderboardManager leaderboardManager = GetComponent<LeaderboardManager>();
            if (leaderboardManager == null)
            {
                leaderboardManager = gameObject.AddComponent<LeaderboardManager>();
            }
            leaderboardManager.SubmitWinningScore(currentWellness, SetLeaderboardStatus);
        }
        else
        {
            LogToConsole(resultMessage, ConsoleLogUI.LogType.Danger);
            OnGameLost?.Invoke(result);
            onGameLost?.Invoke(result);
            ShowOutcome(GameOverMessage, true, "Back to Main Menu");
        }
    }

    private void CreateOutcomePanel()
    {
        if (hudCanvas == null)
        {
            Debug.LogWarning("[WELLNESS] No HUD Canvas was found; outcome panel cannot be created.");
            return;
        }

        outcomePanel = new GameObject("WellnessOutcomePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        outcomePanel.transform.SetParent(hudCanvas.transform, false);

        RectTransform panelRect = outcomePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = outcomePanel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.025f, 0.04f, 0.9f);
        panelImage.raycastTarget = true;

        GameObject labelObject = new GameObject("OutcomeLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(outcomePanel.transform, false);
        outcomeLabel = labelObject.GetComponent<TextMeshProUGUI>();
        outcomeLabel.rectTransform.anchorMin = new Vector2(0.08f, 0.35f);
        outcomeLabel.rectTransform.anchorMax = new Vector2(0.92f, 0.65f);
        outcomeLabel.rectTransform.offsetMin = Vector2.zero;
        outcomeLabel.rectTransform.offsetMax = Vector2.zero;
        outcomeLabel.font = TMP_Settings.defaultFontAsset;
        outcomeLabel.fontSize = 52f;
        outcomeLabel.fontWeight = FontWeight.Bold;
        outcomeLabel.fontStyle = FontStyles.Bold;
        outcomeLabel.alignment = TextAlignmentOptions.Center;
        outcomeLabel.color = Color.white;
        outcomeLabel.textWrappingMode = TextWrappingModes.Normal;
        outcomeLabel.raycastTarget = false;

        GameObject leaderboardStatusObject = new GameObject("LeaderboardStatus", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        leaderboardStatusObject.transform.SetParent(outcomePanel.transform, false);
        leaderboardStatusLabel = leaderboardStatusObject.GetComponent<TextMeshProUGUI>();
        leaderboardStatusLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        leaderboardStatusLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        leaderboardStatusLabel.rectTransform.anchoredPosition = new Vector2(0f, -72f);
        leaderboardStatusLabel.rectTransform.sizeDelta = new Vector2(900f, 40f);
        leaderboardStatusLabel.font = TMP_Settings.defaultFontAsset;
        leaderboardStatusLabel.fontSize = 20f;
        leaderboardStatusLabel.alignment = TextAlignmentOptions.Center;
        leaderboardStatusLabel.color = new Color(0.65f, 0.77f, 0.79f, 1f);
        leaderboardStatusLabel.raycastTarget = false;
        leaderboardStatusLabel.gameObject.SetActive(false);

        restartButton = CreateOutcomeButton("RestartButton", "Restart", new Vector2(-170f, -132f), RestartRun);
        mainMenuButton = CreateOutcomeButton("MainMenuButton", "Back to Main Menu", new Vector2(170f, -132f), ReturnToMainMenu);
        restartButton.gameObject.SetActive(false);
        mainMenuButton.gameObject.SetActive(false);
        outcomePanel.SetActive(false);
    }

    private Button CreateOutcomeButton(string objectName, string label, Vector2 anchoredPosition, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(outcomePanel.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(OutcomeButtonWidth, OutcomeButtonHeight);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = OutcomeButtonColor;
        buttonImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI buttonLabel = labelObject.GetComponent<TextMeshProUGUI>();
        buttonLabel.font = TMP_Settings.defaultFontAsset;
        buttonLabel.text = label;
        buttonLabel.fontSize = OutcomeButtonFontSize;
        buttonLabel.fontWeight = FontWeight.Bold;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.color = Color.white;
        buttonLabel.raycastTarget = false;
        return button;
    }

    private void SetLeaderboardStatus(string message)
    {
        if (leaderboardStatusLabel == null || string.IsNullOrWhiteSpace(message))
            return;

        leaderboardStatusLabel.text = message;
        leaderboardStatusLabel.gameObject.SetActive(true);
    }

    private void ShowOutcome(string message, bool showRestartButton, string mainMenuLabel)
    {
        if (outcomePanel == null || outcomeLabel == null || mainMenuButton == null)
            return;

        outcomeLabel.text = message;
        restartButton.gameObject.SetActive(showRestartButton);
        mainMenuButton.GetComponentInChildren<TextMeshProUGUI>().text = mainMenuLabel;

        RectTransform mainMenuRect = mainMenuButton.GetComponent<RectTransform>();
        mainMenuRect.anchoredPosition = showRestartButton ? new Vector2(170f, -132f) : new Vector2(0f, -132f);
        mainMenuButton.gameObject.SetActive(true);
        outcomePanel.SetActive(true);
    }

    /// <summary>Restarts the gameplay scene without changing the selected difficulty settings.</summary>
    public void RestartRun()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Returns to the MainMenu scene after restoring normal time and cursor visibility.</summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void LogToConsole(string message, ConsoleLogUI.LogType logType)
    {
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log(message, logType);
    }

    private void EnsureRoutineEventEntry(string eventId, float defaultDelta)
    {
        if (routineEventDeltas == null)
            routineEventDeltas = new List<WellnessEventDelta>();

        foreach (WellnessEventDelta entry in routineEventDeltas)
        {
            if (entry != null && string.Equals(entry.eventId, eventId, StringComparison.Ordinal))
            {
                entry.delta = defaultDelta;
                return;
            }
        }

        routineEventDeltas.Add(new WellnessEventDelta(eventId, defaultDelta));
    }

    private void EnsureAddedRoutineEventEntries()
    {
        if (routineEventDeltas == null)
            routineEventDeltas = new List<WellnessEventDelta>();

        foreach (WellnessEventDelta entry in routineEventDeltas)
        {
            if (entry != null)
                entry.delta = 0f;
        }

        EnsureRoutineEventEntry("SkippingMeal", 0f);
        EnsureRoutineEventEntry("DrinkingWater", 0f);
        EnsureRoutineEventEntry("WalkingLightActivity", 0f);
        EnsureRoutineEventEntry("RecoveryBreak", 0f);
        EnsureRoutineEventEntry("SleepingIn", 0f);
        EnsureRoutineEventEntry("ProlongedInactivity", 0f);
    }

    private void EnsureAddedRandomEventEntries()
    {
        EnsureRandomEventEntry("AteExpiredFood", -5f);
        EnsureRandomEventEntry("InhaledDustOrAllergen", -5f);
        EnsureRandomEventEntry("SkippedMeal", 0f);
        EnsureRandomEventEntry("Overexertion", 0f);
        EnsureRandomEventEntry("StressOrPoorHydration", 0f);
        EnsureRandomEventEntry("JunkFoodBinge", 0f);
        EnsureRandomEventEntry("ArgumentOrConflict", 0f);
        EnsureRandomEventEntry("ColdFromSickPerson", -5f);
        EnsureRandomEventEntry("SunOrFreshAir", 0f);
        EnsureRandomEventEntry("NickedOrScraped", -5f);
        EnsureRandomEventEntry("ContaminatedWater", -5f);
        EnsureRandomEventEntry("TrippedAndScratched", -5f);
        EnsureRandomEventEntry("AccidentallyAteSpoiledFood", -5f);
        EnsureRandomEventEntry("ContactWithSickPerson", -5f);
        EnsureRandomEventEntry("SmokedCigarette", -5f);
    }

    private void EnsureRandomEventEntry(string eventId, float defaultDelta)
    {
        if (randomEventDeltas == null)
            randomEventDeltas = new List<WellnessEventDelta>();

        foreach (WellnessEventDelta entry in randomEventDeltas)
        {
            if (entry != null && string.Equals(entry.eventId, eventId, StringComparison.Ordinal))
            {
                entry.delta = defaultDelta;
                return;
            }
        }

        randomEventDeltas.Add(new WellnessEventDelta(eventId, defaultDelta));
    }

    private float FindDelta(List<WellnessEventDelta> deltas, string eventId, float fallback)
    {
        if (deltas == null)
            return fallback;

        foreach (WellnessEventDelta entry in deltas)
        {
            if (entry != null && string.Equals(entry.eventId, eventId, StringComparison.Ordinal))
                return entry.delta;
        }

        return fallback;
    }

    private string FormatSignedDelta(float delta)
    {
        return delta >= 0f ? $"+{delta:0.##}" : delta.ToString("0.##");
    }
}

/// <summary>Classification produced when a wellness run ends.</summary>
public enum WellnessRunResult
{
    PerfectWin,
    Win,
    Loss,
    SevereLoss
}
