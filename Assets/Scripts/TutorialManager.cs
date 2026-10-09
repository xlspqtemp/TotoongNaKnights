using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Runs the first-play tutorial overlay and manages temporary UI visibility and pause state.</summary>
public sealed class TutorialManager : MonoBehaviour
{
    public enum ContainerAnchor
    {
        Center,
        TopLeft,
        TopRight,
        BottomCenter,
        BottomLeft,
        BottomRight
    }

    private const string GameplaySceneName = "Game";
    private const string TutorialOverlayResourceName = "TutorialOverlay";
    private const string TutorialSeenPreferenceKey = "tutorialSeen";
    private const string GuestSessionType = "Guest";
    private const string NoSessionType = "NoActiveSession";
    private const float MinimumPanelWidth = 700f;
    private const float HorizontalPanelPadding = 190f;
    private const float VerticalPanelPadding = 174f;
    private const float PanelScreenMargin = 28f;
    private const float DefaultBodyFontSize = 21f;
    private const float MinimumBodyFontSize = 12f;
    private const float HighlightPulseSpeed = 2f;
    private static readonly Color HighlightPulseColor = new Color(0.35f, 0.95f, 1f, 1f);
    private const float TutorialWellnessBaseline = 95f;

    [Serializable]
    public sealed class TutorialStep
    {
        public string title;
        [TextArea(3, 8)] public string body;
        public Sprite icon;
        public GameObject revealTarget;
        public GameObject hideTarget;
        public bool requiresAction;
        public string actionId;
        public string targetKey;
        public Sprite secondaryIcon;
        public ContainerAnchor containerAnchor = ContainerAnchor.Center;
        public GameObject highlightTarget;
    }

    [Header("Tutorial Steps")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();
    [SerializeField] private List<GameObject> hideAllTargets = new List<GameObject>();

    [Header("Launch Behavior")]
    [SerializeField] private bool ForceShowOnEveryStart = true;
    [SerializeField] private bool AlwaysShowForGuest = true;

    [Header("Overlay References")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [SerializeField] private TextMeshProUGUI stepCounterLabel;
    [SerializeField] private GameObject iconSlot;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject secondaryIconSlot;
    [SerializeField] private Image secondaryIconImage;
    [SerializeField] private TextMeshProUGUI bacterialIconLabel;
    [SerializeField] private TextMeshProUGUI viralIconLabel;
    [SerializeField] private RectTransform bodyViewport;
    [SerializeField] private ScrollRect bodyScrollRect;
    [SerializeField] private RectMask2D bodyViewportMask;
    [SerializeField] private Scrollbar bodyVerticalScrollbar;
    [SerializeField, Min(1f)] private float bodyFontSize = DefaultBodyFontSize;
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private float maximumPanelWidth = 900f;

    private readonly Dictionary<GameObject, bool> originalTargetStates = new Dictionary<GameObject, bool>();
    private readonly HashSet<int> completedStepEntryActions = new HashSet<int>();
    private readonly HashSet<int> completedActionSteps = new HashSet<int>();
    private readonly HashSet<GameObject> revealedTargets = new HashSet<GameObject>();
    private readonly List<GameObject> tutorialNotificationObjects = new List<GameObject>();
    private int currentStepIndex;
    private bool currentActionCompleted;
    private bool tutorialIsOpen;
    private bool wasPausedBeforeTutorial;
    private InfectionSpawner infectionSpawner;
    private PendingInfectionsPanel pendingInfectionsPanel;
    private WellnessManager wellnessManager;
    private InfectionSpawner.InfectionMarker tutorialSpawnedMarker;
    private bool wellnessResolutionListenerAttached;
    private bool pendingInfectionRowListenerAttached;
    private bool fastForwardListenerAttached;
    private Button fastForwardButton;
    private bool wellnessValueStored;
    private float wellnessValueBeforeTutorial;
    private Graphic highlightedGraphic;
    private Color highlightedGraphicOriginalColor;
    private int currentScrollStepIndex = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOverlayInActiveScene()
    {
        EnsureOverlayForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsureOverlayForScene(scene);
    }

    private static void EnsureOverlayForScene(Scene scene)
    {
        if (scene.name != GameplaySceneName || FindFirstObjectByType<TutorialManager>() != null)
            return;

        GameObject overlayPrefab = Resources.Load<GameObject>(TutorialOverlayResourceName);
        if (overlayPrefab == null)
        {
            Debug.LogError($"[Tutorial] Missing Resources/{TutorialOverlayResourceName}.prefab.");
            return;
        }

        Instantiate(overlayPrefab);
    }

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(PreviousStep);
        if (nextButton != null)
            nextButton.onClick.AddListener(AdvanceStep);
        if (skipButton != null)
            skipButton.onClick.AddListener(CloseTutorial);
        if (startGameButton != null)
            startGameButton.onClick.AddListener(CloseTutorial);

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(false);
        if (bodyScrollRect != null)
            bodyScrollRect.enabled = false;
        if (bodyViewportMask != null)
            bodyViewportMask.enabled = false;
        if (bodyVerticalScrollbar != null)
            bodyVerticalScrollbar.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterSceneSetup());
    }

    private IEnumerator InitializeAfterSceneSetup()
    {
        yield return null;
        ResolveSceneTargets();
        BindPendingInfectionsPanel();

        if (steps == null || steps.Count == 0)
            yield break;
        InitializeOptionalPathogenIcons();

        string sessionType = LeaderboardManager.GetActiveSessionType();
        bool isGuest = string.Equals(sessionType, GuestSessionType, StringComparison.OrdinalIgnoreCase);
        bool hasAuthenticatedSession = !string.IsNullOrWhiteSpace(sessionType) &&
                                       !string.Equals(sessionType, NoSessionType, StringComparison.OrdinalIgnoreCase);
        bool shouldOpenForGuest = isGuest && AlwaysShowForGuest;
        bool shouldOpenForAccount = !isGuest && hasAuthenticatedSession &&
                                    (PlayerPrefs.GetInt(TutorialSeenPreferenceKey, 0) == 0 || ForceShowOnEveryStart);

        if (shouldOpenForGuest || shouldOpenForAccount)
            OpenTutorial();
    }

    private void Update()
    {
        if (!tutorialIsOpen)
            return;

        ResolveSceneTargets();
        BindPendingInfectionsPanel();
        BindFastForwardButtonForCurrentStep();
        RevealSpawnedNotificationIcon();
        EnforceHiddenTargets();
        UpdateHighlightPulse();
    }

    private void LateUpdate()
    {
        if (!tutorialIsOpen)
            return;

        for (int index = tutorialNotificationObjects.Count - 1; index >= 0; index--)
        {
            GameObject notificationObject = tutorialNotificationObjects[index];
            if (notificationObject == null)
            {
                tutorialNotificationObjects.RemoveAt(index);

    
                continue;
            }

            SpriteRenderer spriteRenderer = notificationObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                continue;

            spriteRenderer.enabled = true;
            Color color = spriteRenderer.color;

    
            color.a = 1f;
            spriteRenderer.color = color;
        }
    }

    private readonly Dictionary<string, List<GameObject>> targetsByKey = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);

    private void ResolveSceneTargets()
    {
        Transform hudCanvas = GameObject.Find("HUDCanvas")?.transform;
        if (hudCanvas != null)
        {
            RegisterTarget("wellness", hudCanvas.Find("GameProgressBar")?.gameObject, true);
            RegisterTarget("wellness", hudCanvas.Find("ProgressValueLabel")?.gameObject, true);
            RegisterTarget("wellness", hudCanvas.Find("WellnessChangeIndicator")?.gameObject, true);
            RegisterTarget("humanSnapshot", hudCanvas.Find("HumanSnapshotPanel")?.gameObject, true);
            RegisterTarget("consoleLog", hudCanvas.Find("ConsoleLogPanel")?.gameObject, true);
            RegisterTarget("layers", hudCanvas.Find("LayerSelectionButton")?.gameObject, true);
            RegisterTarget("layers", hudCanvas.Find("SystemLayerButtons")?.gameObject, true);
            RegisterTarget("pendingInfections", hudCanvas.Find("Pending Infections Panel/Pending Infections Panel Body")?.gameObject, true);
            RegisterTarget("pendingInfections", hudCanvas.Find("Pending Infections Panel/Pending Infections Toggle")?.gameObject, true);
            RegisterTarget("dayCounter", hudCanvas.Find("DayCounterPanel")?.gameObject, true);
            RegisterTarget("pauseButton", hudCanvas.Find("PauseButton")?.gameObject, true);
            RegisterTarget("fastForward", FindFastForwardButton()?.gameObject, true);
            RegisterTarget("tacticalOrders", hudCanvas.Find("TacticalOrdersContainer")?.gameObject, true);
            RegisterTarget("digestiveOrders", hudCanvas.Find("DigestiveOrdersContainer")?.gameObject, true);
            RegisterTarget("respiratoryOrders", hudCanvas.Find("RespiratoryOrdersContainer")?.gameObject, true);
            RegisterTarget("lymphaticOrders", hudCanvas.Find("LymphaticOrdersContainer")?.gameObject, true);
        }

        RegisterTarget("wbcHeadshot", GameObject.Find("WBC Squad HUD"), true);
        RegisterTarget("wbcHeadshot", GameObject.Find("WBC Squad Headshot Button"), true);
        CaptureExistingNotificationTargets();
    }

    private static Button FindFastForwardButton()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name == "FastForwardButton")
                return button;
        }

        return null;
    }

    private void EnforceHiddenTargets()
    {
        foreach (GameObject target in hideAllTargets)
        {
            if (target == null || revealedTargets.Contains(target) || !target.activeSelf)
                continue;

            CaptureTargetState(target);
            target.SetActive(false);
        }
    }

    private void RegisterTarget(string targetKey, GameObject target, bool hideAtStart)
    {
        if (target == null)
            return;

        if (!targetsByKey.TryGetValue(targetKey, out List<GameObject> targets))
        {
            targets = new List<GameObject>();
            targetsByKey.Add(targetKey, targets);
        }
        if (!targets.Contains(target))
            targets.Add(target);
        if (hideAtStart && !hideAllTargets.Contains(target))
            hideAllTargets.Add(target);
    }

    private void CaptureExistingNotificationTargets()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null && spriteRenderer.gameObject.name.StartsWith("Infection Notification - ", StringComparison.Ordinal))
                RegisterTarget("notificationIcon", spriteRenderer.gameObject, true);
        }
    }

    private void BindPendingInfectionsPanel()
    {
        if (pendingInfectionsPanel == null)
            pendingInfectionsPanel = FindFirstObjectByType<PendingInfectionsPanel>();
        if (pendingInfectionsPanel == null)
            return;

        if (tutorialIsOpen && !pendingInfectionRowListenerAttached)
        {
            pendingInfectionsPanel.OnRowClickedPublic += HandlePendingInfectionRowClicked;
            pendingInfectionRowListenerAttached = true;
        }
        else if (!tutorialIsOpen && pendingInfectionRowListenerAttached)
        {
            pendingInfectionsPanel.OnRowClickedPublic -= HandlePendingInfectionRowClicked;
            pendingInfectionRowListenerAttached = false;
        }
    }

    private void HandlePendingInfectionRowClicked(InfectionSpawner.InfectionMarker marker)
    {
        if (marker == null || !tutorialIsOpen)
            return;

        if (IsCurrentAction("clickPendingRow"))
        {
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
                StartCoroutine(CompletePendingRowActionAfterCameraFocus());
            }
            else
            {
                CompleteCurrentStep();
            }
            return;
        }

        infectionSpawner = infectionSpawner != null ? infectionSpawner : FindFirstObjectByType<InfectionSpawner>();
        if (IsCurrentAction("resolveInfection") && infectionSpawner != null &&
            infectionSpawner.IsAwaitingInfectionTargetSelection)
        {
            infectionSpawner.RequestDispatchToInfection(marker);
        }
    }

    private IEnumerator CompletePendingRowActionAfterCameraFocus()
    {
        float elapsed = 0f;
        const float CameraFocusReleaseDelay = 0.5f;
        while (elapsed < CameraFocusReleaseDelay && tutorialIsOpen && IsCurrentAction("clickPendingRow"))
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (tutorialIsOpen && IsCurrentAction("clickPendingRow"))
            CompleteCurrentStep();
    }

    private void BindFastForwardButtonForCurrentStep()
    {
        bool shouldListen = tutorialIsOpen && IsCurrentAction("pressFastForward") && !currentActionCompleted;
        if (!shouldListen)
        {
            if (fastForwardButton != null && fastForwardListenerAttached)
                fastForwardButton.onClick.RemoveListener(HandleFastForwardPressed);
            fastForwardListenerAttached = false;
            fastForwardButton = null;
            return;
        }

        Button foundButton = FindFastForwardButton();
        if (foundButton == fastForwardButton && fastForwardListenerAttached)
            return;

        if (fastForwardButton != null && fastForwardListenerAttached)
            fastForwardButton.onClick.RemoveListener(HandleFastForwardPressed);

        fastForwardButton = foundButton;
        fastForwardListenerAttached = fastForwardButton != null;
        if (fastForwardListenerAttached)
            fastForwardButton.onClick.AddListener(HandleFastForwardPressed);
    }

    private void UnbindFastForwardButton()
    {
        if (fastForwardButton != null && fastForwardListenerAttached)
            fastForwardButton.onClick.RemoveListener(HandleFastForwardPressed);
        fastForwardButton = null;
        fastForwardListenerAttached = false;
    }

    private void HandleFastForwardPressed()
    {
        if (!tutorialIsOpen || !IsCurrentAction("pressFastForward"))
            return;

        CompleteCurrentStep();
    }

    private void SetWellnessForTutorial()
    {
        wellnessManager = WellnessManager.Instance != null ? WellnessManager.Instance : FindFirstObjectByType<WellnessManager>();
        if (wellnessManager == null || wellnessManager.HasRunEnded || wellnessValueStored)
            return;

        wellnessValueBeforeTutorial = wellnessManager.CurrentWellness;
        wellnessValueStored = true;
        wellnessManager.ApplyInfectionResolutionDelta("Tutorial wellness baseline", TutorialWellnessBaseline - wellnessManager.CurrentWellness);
    }

    private void RestoreWellnessAfterTutorial()
    {
        if (!wellnessValueStored)
            return;

        if (wellnessManager == null)
            wellnessManager = WellnessManager.Instance;
        if (wellnessManager != null && !wellnessManager.HasRunEnded)
            wellnessManager.ApplyInfectionResolutionDelta("Tutorial wellness restore", wellnessValueBeforeTutorial - wellnessManager.CurrentWellness);

        wellnessValueStored = false;
    }

    private void BindResolutionEventForCurrentStep()
    {
        if (wellnessManager == null)
            wellnessManager = WellnessManager.Instance;

        if (wellnessManager != null && wellnessResolutionListenerAttached)
        {
            wellnessManager.OnWellnessDeltaApplied -= HandleWellnessDeltaApplied;
            wellnessResolutionListenerAttached = false;
        }

        if (tutorialIsOpen && IsCurrentAction("resolveInfection") && wellnessManager != null)
        {
            wellnessManager.OnWellnessDeltaApplied += HandleWellnessDeltaApplied;
            wellnessResolutionListenerAttached = true;
        }
    }

    private void HandleWellnessDeltaApplied(float delta)
    {
        if (delta > 0f && tutorialIsOpen && IsCurrentAction("resolveInfection"))
            CompleteCurrentStep();
    }

    private bool IsCurrentAction(string actionId)
    {
        return steps != null && currentStepIndex >= 0 && currentStepIndex < steps.Count &&
               string.Equals(steps[currentStepIndex].actionId, actionId, StringComparison.Ordinal);
    }

    private void RunStepEntryAction(TutorialStep step)
    {
        if (step == null || !completedStepEntryActions.Add(currentStepIndex))
            return;

        switch (step.actionId)
        {
            case "spawnFirstInfection":
                ResolveSceneTargets();
                CameraScript cameraController = FindFirstObjectByType<CameraScript>();
                if (cameraController != null)
                {
                    cameraController.SelectLayer(2);
                    Debug.Log("[Tutorial] Selected the Circulatory layer before the first infection step.", this);
                }
                else
                {
                    Debug.LogWarning("[Tutorial] Cannot select the Circulatory layer because no CameraScript was found.", this);
                }
                infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
                tutorialSpawnedMarker = null;
                if (infectionSpawner != null)
                {
                    HashSet<InfectionSpawner.InfectionMarker> existingMarkers = new HashSet<InfectionSpawner.InfectionMarker>(infectionSpawner.ActiveInfections);
                    Debug.Log("[Tutorial] Forced spawn fired on the 'YOUR FIRST INFECTION' step.", this);
                    infectionSpawner.ForceSpawnOneBacterialInfection();
                    foreach (InfectionSpawner.InfectionMarker marker in infectionSpawner.ActiveInfections)
                    {
                        if (marker != null && !existingMarkers.Contains(marker))
                        {
                            tutorialSpawnedMarker = marker;
                            Debug.Log($"[Tutorial] Forced spawn created '{marker.infection?.displayName ?? "infection"}' at {marker.worldPosition}.", this);
                            CameraScript focusController = FindFirstObjectByType<CameraScript>();
                            if (focusController != null)
                            {
                                focusController.FocusOnWorldPosition(marker.worldPosition);
                                Debug.Log("[Tutorial] Requested camera focus on the spawned infection.", this);
                            }
                            break;
                        }
                    }

                    if (tutorialSpawnedMarker == null)
                        Debug.LogWarning("[Tutorial] Forced spawn call completed without adding a new active infection marker. Check spawner warnings above.", this);
                }
                else
                {
                    Debug.LogWarning("[Tutorial] Forced infection spawn was skipped because no InfectionSpawner was found.", this);
                }
                break;
            case "applyWellnessDrop":
                wellnessManager = WellnessManager.Instance != null ? WellnessManager.Instance : FindFirstObjectByType<WellnessManager>();
                if (wellnessManager != null)
                    wellnessManager.ApplyInfectionResolutionDelta("Tutorial consequences example", -10f);
                break;
        }
    }

    private void RevealSpawnedNotificationIcon()
    {
        if (tutorialSpawnedMarker == null || tutorialSpawnedMarker.infection == null)
            return;

        string notificationName = "Infection Notification - " + tutorialSpawnedMarker.infection.displayName;
        GameObject notificationObject = GameObject.Find(notificationName);
        if (notificationObject == null)
            return;

        CaptureTargetState(notificationObject);
        notificationObject.SetActive(true);
        revealedTargets.Add(notificationObject);
        RegisterTarget("notificationIcon", notificationObject, false);
        if (!tutorialNotificationObjects.Contains(notificationObject))
            tutorialNotificationObjects.Add(notificationObject);
    }


    private void InitializeOptionalPathogenIcons()
    {
        Sprite bacterialIcon = LoadIconFromTexture("UI/bacteria_notification-removebg-preview");
        Sprite viralIcon = LoadIconFromTexture("UI/Virus_notification-removebg-preview");
        foreach (TutorialStep step in steps)
        {
            if (step == null)
                continue;

            if (step.actionId == "spawnFirstInfection" && step.icon == null)
                step.icon = bacterialIcon;
            if (step.actionId == "infectionTypes")
            {
                if (step.icon == null)
                    step.icon = bacterialIcon;
                if (step.secondaryIcon == null)
                    step.secondaryIcon = viralIcon;
            }
        }
    }

    private static Sprite LoadIconFromTexture(string resourcePath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDisable()
    {
        RestoreTutorialState();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        RestoreTutorialState();
    }

    /// <summary>Opens the tutorial at its first step, hides configured targets, and pauses gameplay.</summary>
    public void OpenTutorial()
    {
        if (steps == null || steps.Count == 0 || tutorialPanel == null)
            return;

        ResolveSceneTargets();
        if (!tutorialIsOpen)
        {
            wasPausedBeforeTutorial = Time.timeScale <= 0f;
            CaptureAndHideInitialTargets();
            Time.timeScale = 0f;
        }

        tutorialIsOpen = true;
        SetWellnessForTutorial();
        BindPendingInfectionsPanel();
        currentStepIndex = 0;
        currentActionCompleted = false;
        completedActionSteps.Clear();
        completedStepEntryActions.Clear();
        tutorialPanel.SetActive(true);
        ShowCurrentStep();
    }

    /// <summary>Closes the tutorial, restores UI and pause state, and records completion.</summary>
    public void CloseTutorial()
    {
        if (!tutorialIsOpen)
            return;

        PlayerPrefs.SetInt(TutorialSeenPreferenceKey, 1);
        PlayerPrefs.Save();
        RestoreTutorialState();
    }

    /// <summary>Advances to the next step unless the current step is waiting for an action.</summary>
    public void AdvanceStep()
    {
        if (!tutorialIsOpen || steps == null || currentStepIndex >= steps.Count - 1)
            return;

        TutorialStep step = steps[currentStepIndex];
        if (step.requiresAction && !currentActionCompleted)
            return;

        currentStepIndex++;
        currentActionCompleted = false;
        ShowCurrentStep();
    }

    /// <summary>Returns to the previous step without hiding previously revealed targets.</summary>
    public void PreviousStep()
    {
        if (!tutorialIsOpen || currentStepIndex <= 0)
            return;

        currentStepIndex--;
        currentActionCompleted = false;
        ShowCurrentStep();
    }

    /// <summary>Marks the current action-required step complete so the player can advance.</summary>
    public void CompleteCurrentStep()
    {
        if (!tutorialIsOpen || steps == null || currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;

        currentActionCompleted = true;
        completedActionSteps.Add(currentStepIndex);
        Time.timeScale = 0f;
        RefreshNavigationState();
    }

    private void CaptureAndHideInitialTargets()
    {
        originalTargetStates.Clear();
        foreach (GameObject target in hideAllTargets)
            CaptureTargetState(target);

        foreach (TutorialStep step in steps)
        {
            if (step == null)
                continue;

            CaptureTargetState(step.hideTarget);
            CaptureTargetState(step.revealTarget);
        }

        foreach (GameObject target in hideAllTargets)
        {
            if (target != null)
                target.SetActive(false);
        }

        foreach (TutorialStep step in steps)
        {
            if (step != null && step.hideTarget != null)
                step.hideTarget.SetActive(false);
        }
    }

    private void CaptureTargetState(GameObject target)
    {
        if (target != null && !originalTargetStates.ContainsKey(target))
            originalTargetStates.Add(target, target.activeSelf);
    }

    private void ShowCurrentStep()
    {
        StopHighlightPulse();
        ResolveSceneTargets();
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;

        TutorialStep step = steps[currentStepIndex];
        if (step == null)
            return;
        GameObject primaryStepTarget = ResolvePrimaryTarget(step.targetKey);
        if (step.revealTarget == null)
            step.revealTarget = primaryStepTarget;
        if (step.highlightTarget == null)
            step.highlightTarget = primaryStepTarget;
        currentActionCompleted = completedActionSteps.Contains(currentStepIndex);

        if (step.revealTarget != null)
        {
            CaptureTargetState(step.revealTarget);
            revealedTargets.Add(step.revealTarget);
            step.revealTarget.SetActive(true);
        }
        if (!string.IsNullOrWhiteSpace(step.targetKey) && targetsByKey.TryGetValue(step.targetKey, out List<GameObject> revealTargets))
        {
            foreach (GameObject revealTarget in revealTargets)
            {
                CaptureTargetState(revealTarget);
                if (revealTarget != null)
                {
                    revealedTargets.Add(revealTarget);
                    revealTarget.SetActive(true);
                }
            }
        }

        if (titleLabel != null)
            titleLabel.text = step.title ?? string.Empty;
        if (bodyLabel != null)
        {
            bodyLabel.text = step.body ?? string.Empty;
            bodyLabel.enableAutoSizing = false;
            bodyLabel.fontSize = bodyFontSize;
        }
        if (stepCounterLabel != null)
            stepCounterLabel.text = $"Step {currentStepIndex + 1} of {steps.Count}";
        if (iconImage != null)
            iconImage.sprite = step.icon;
        if (iconSlot != null)
            iconSlot.SetActive(step.icon != null);
        if (secondaryIconImage != null)
            secondaryIconImage.sprite = step.secondaryIcon;
        if (secondaryIconSlot != null)
            secondaryIconSlot.SetActive(step.secondaryIcon != null);
        bool isPathogenComparisonStep = string.Equals(step.actionId, "infectionTypes", StringComparison.Ordinal);
        if (bacterialIconLabel != null)
        {
            bacterialIconLabel.text = "BACTERIAL";
            bacterialIconLabel.gameObject.SetActive(isPathogenComparisonStep);
        }
        if (viralIconLabel != null)
        {
            viralIconLabel.text = "VIRAL";
            viralIconLabel.gameObject.SetActive(isPathogenComparisonStep);
        }

        bool isFinalStep = currentStepIndex == steps.Count - 1;
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isFinalStep);
        RefreshNavigationState();
        ResizePanelToBodyContent();
        ApplyContainerAnchor(step.containerAnchor);
        Debug.Log($"[Tutorial] Applied {step.containerAnchor} container anchor for step {currentStepIndex + 1} of {steps.Count}.", this);
        if (IsCurrentAction("resolveInfection") && !currentActionCompleted)
        {
            Time.timeScale = 1f;
            if (tutorialSpawnedMarker != null)
            {
                CameraScript focusController = FindFirstObjectByType<CameraScript>();
                if (focusController != null)
                {
                    focusController.FocusOnWorldPosition(tutorialSpawnedMarker.worldPosition);
                    Debug.Log("[Tutorial] Resumed the camera focus on the first infection for the live dispatch step.", this);
                }
            }
        }
        BindResolutionEventForCurrentStep();
        RunStepEntryAction(step);
        ApplyHighlightPulse(step.highlightTarget != null ? step.highlightTarget : ResolvePrimaryTarget(step.targetKey));
        BindFastForwardButtonForCurrentStep();
    }

    private GameObject ResolvePrimaryTarget(string targetKey)
    {
        if (string.IsNullOrWhiteSpace(targetKey) || !targetsByKey.TryGetValue(targetKey, out List<GameObject> targets))
            return null;

        foreach (GameObject target in targets)
        {
            if (target != null && target.GetComponent<RectTransform>() != null)
                return target;
        }

        return null;
    }

    private void ApplyHighlightPulse(GameObject target)
    {
        if (target == null || target.GetComponent<RectTransform>() == null)
            return;

        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic == null)
            graphic = target.GetComponentInChildren<Graphic>();
        if (graphic == null)
            return;

        highlightedGraphic = graphic;
        highlightedGraphicOriginalColor = graphic.color;
    }

    private void UpdateHighlightPulse()
    {
        if (highlightedGraphic == null)
            return;

        float pulse = Mathf.PingPong(Time.unscaledTime * HighlightPulseSpeed, 1f);
        highlightedGraphic.color = Color.Lerp(highlightedGraphicOriginalColor, HighlightPulseColor, pulse * 0.28f);
    }

    private void StopHighlightPulse()
    {
        if (highlightedGraphic != null)
            highlightedGraphic.color = highlightedGraphicOriginalColor;
        highlightedGraphic = null;
    }

    private void ApplyContainerAnchor(ContainerAnchor anchor)
    {
        if (panelRect == null)
            return;

        Vector2 anchorPoint;
        Vector2 pivot;
        Vector2 anchoredPosition;
        const float ScreenMargin = PanelScreenMargin;
        switch (anchor)
        {
            case ContainerAnchor.TopLeft:
                anchorPoint = new Vector2(0f, 1f);
                pivot = new Vector2(0f, 1f);
                anchoredPosition = new Vector2(ScreenMargin, -ScreenMargin);
                break;
            case ContainerAnchor.TopRight:
                anchorPoint = new Vector2(1f, 1f);
                pivot = new Vector2(1f, 1f);
                anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);
                break;
            case ContainerAnchor.BottomCenter:
                anchorPoint = new Vector2(0.5f, 0f);
                pivot = new Vector2(0.5f, 0f);
                anchoredPosition = new Vector2(0f, ScreenMargin);
                break;
            case ContainerAnchor.BottomLeft:
                anchorPoint = Vector2.zero;
                pivot = Vector2.zero;
                anchoredPosition = new Vector2(ScreenMargin, ScreenMargin);
                break;
            case ContainerAnchor.BottomRight:
                anchorPoint = new Vector2(1f, 0f);
                pivot = new Vector2(1f, 0f);
                anchoredPosition = new Vector2(-ScreenMargin, ScreenMargin);
                break;
            default:
                anchorPoint = new Vector2(0.5f, 0.5f);
                pivot = new Vector2(0.5f, 0.5f);
                anchoredPosition = Vector2.zero;
                break;
        }

        panelRect.anchorMin = anchorPoint;
        panelRect.anchorMax = anchorPoint;
        panelRect.pivot = pivot;
        panelRect.anchoredPosition = anchoredPosition;
    }

    private void RefreshNavigationState()
    {
        if (backButton != null)
            backButton.interactable = currentStepIndex > 0;
        if (nextButton != null)
        {
            bool waitingForAction = steps[currentStepIndex].requiresAction && !currentActionCompleted;
            nextButton.interactable = !waitingForAction && currentStepIndex < steps.Count - 1;
        }
    }

    private void ResizePanelToBodyContent()
    {
        if (panelRect == null || bodyLabel == null)
            return;

        float maxWidth = Mathf.Max(MinimumPanelWidth, maximumPanelWidth);
        RectTransform canvasRect = panelRect.parent as RectTransform;
        float availableWidth = canvasRect != null ? Mathf.Max(320f, canvasRect.rect.width - 2f * PanelScreenMargin) : maxWidth;
        float availableHeight = canvasRect != null ? Mathf.Max(300f, canvasRect.rect.height - 2f * PanelScreenMargin) : 720f;
        maxWidth = Mathf.Min(maxWidth, availableWidth);
        float minimumWidth = Mathf.Min(MinimumPanelWidth, maxWidth);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bool isPathogenComparisonStep = IsCurrentAction("infectionTypes");

        if (bodyViewport == null)
        {
            float textWidth = Mathf.Max(120f, maxWidth - HorizontalPanelPadding);
            Vector2 preferred = bodyLabel.GetPreferredValues(bodyLabel.text, textWidth, 0f);
            float panelWidthFallback = Mathf.Clamp(preferred.x + HorizontalPanelPadding, minimumWidth, maxWidth);
            float bodyWidthFallback = Mathf.Max(120f, panelWidthFallback - HorizontalPanelPadding);
            float panelHeightFallback = Mathf.Clamp(Mathf.Max(320f, preferred.y + VerticalPanelPadding), Mathf.Min(320f, availableHeight), availableHeight);
            bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidthFallback);
            bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(preferred.y, panelHeightFallback - VerticalPanelPadding));
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidthFallback);
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeightFallback);
            return;
        }

        float bodyWidth = isPathogenComparisonStep
            ? Mathf.Max(240f, maxWidth - 2f * PanelScreenMargin)
            : Mathf.Max(120f, maxWidth - HorizontalPanelPadding);
        float maximumBodyHeight = Mathf.Max(80f, availableHeight - VerticalPanelPadding);
        bodyLabel.fontSize = bodyFontSize;
        Vector2 preferredBodySize = bodyLabel.GetPreferredValues(bodyLabel.text, bodyWidth, 0f);
        if (!isPathogenComparisonStep && preferredBodySize.y > maximumBodyHeight)
        {
            float fittedFontSize = Mathf.Max(MinimumBodyFontSize, bodyFontSize * maximumBodyHeight / preferredBodySize.y);
            bodyLabel.fontSize = fittedFontSize;
            preferredBodySize = bodyLabel.GetPreferredValues(bodyLabel.text, bodyWidth, 0f);
        }

        RectTransform viewportRect = bodyViewport;
        float viewportTop = isPathogenComparisonStep ? -190f : -94f;
        float viewportX = isPathogenComparisonStep ? PanelScreenMargin : 142f;
        viewportRect.anchorMin = new Vector2(0f, 1f);
        viewportRect.anchorMax = new Vector2(0f, 1f);
        viewportRect.pivot = new Vector2(0f, 1f);
        viewportRect.anchoredPosition = new Vector2(viewportX, viewportTop);
        viewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidth);

        float maxScrollViewportHeight = 190f;
        float viewportHeight = isPathogenComparisonStep
            ? Mathf.Min(maxScrollViewportHeight, Mathf.Max(96f, preferredBodySize.y))
            : Mathf.Min(preferredBodySize.y, Mathf.Max(100f, availableHeight - VerticalPanelPadding));
        viewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);

        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(0f, 1f);
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidth);
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredBodySize.y);
        RectTransform scrollContentRect = bodyRect.parent as RectTransform;
        if (scrollContentRect != null)
        {
            scrollContentRect.anchorMin = new Vector2(0f, 1f);
            scrollContentRect.anchorMax = new Vector2(0f, 1f);
            scrollContentRect.pivot = new Vector2(0f, 1f);
            scrollContentRect.anchoredPosition = Vector2.zero;
            scrollContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidth);
            scrollContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredBodySize.y);
        }

        bool needsPathogenScroll = isPathogenComparisonStep && preferredBodySize.y > viewportHeight + 1f;
        if (bodyViewportMask != null)
            bodyViewportMask.enabled = needsPathogenScroll;
        if (bodyScrollRect != null)
        {
            bodyScrollRect.enabled = false;
            bodyScrollRect.horizontal = false;
            bodyScrollRect.vertical = needsPathogenScroll && scrollContentRect != null && viewportRect != null;
            bodyScrollRect.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            if (bodyScrollRect.vertical)
            {
                bodyScrollRect.content = scrollContentRect;
                bodyScrollRect.viewport = viewportRect;
                bodyScrollRect.verticalScrollbar = bodyVerticalScrollbar;
                bodyScrollRect.enabled = true;
                if (bodyScrollRect.content != null && bodyScrollRect.viewport != null)
                    bodyScrollRect.verticalNormalizedPosition = 1f;
            }
            else
            {
                bodyScrollRect.content = null;
                bodyScrollRect.viewport = null;
                bodyScrollRect.verticalScrollbar = null;
            }
        }
        if (bodyVerticalScrollbar != null)
            bodyVerticalScrollbar.gameObject.SetActive(needsPathogenScroll);
        currentScrollStepIndex = needsPathogenScroll ? currentStepIndex : -1;

        float panelHeight = isPathogenComparisonStep
            ? Mathf.Max(380f, viewportHeight + 300f)
            : Mathf.Max(320f, preferredBodySize.y + VerticalPanelPadding);
        panelHeight = Mathf.Clamp(panelHeight, Mathf.Min(320f, availableHeight), availableHeight);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
    }

    private void RestoreTutorialState()
    {
        StopHighlightPulse();
        UnbindFastForwardButton();
        if (bodyScrollRect != null)
            bodyScrollRect.enabled = false;
        if (bodyViewportMask != null)
            bodyViewportMask.enabled = false;
        if (bodyVerticalScrollbar != null)
            bodyVerticalScrollbar.gameObject.SetActive(false);

        if (pendingInfectionsPanel != null && pendingInfectionRowListenerAttached)
        {
            pendingInfectionsPanel.OnRowClickedPublic -= HandlePendingInfectionRowClicked;
            pendingInfectionRowListenerAttached = false;
        }
        if (wellnessManager != null && wellnessResolutionListenerAttached)
        {
            wellnessManager.OnWellnessDeltaApplied -= HandleWellnessDeltaApplied;
            wellnessResolutionListenerAttached = false;
        }
        RestoreWellnessAfterTutorial();

        foreach (KeyValuePair<GameObject, bool> targetState in originalTargetStates)
        {
            if (targetState.Key != null)
                targetState.Key.SetActive(targetState.Value);
        }
        originalTargetStates.Clear();
        revealedTargets.Clear();
        tutorialNotificationObjects.Clear();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (!tutorialIsOpen)
            return;

        tutorialIsOpen = false;
        Time.timeScale = wasPausedBeforeTutorial ? 0f : 1f;
    }
}
