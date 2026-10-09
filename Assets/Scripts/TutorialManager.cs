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
        if (tutorialIsOpen)
        {
            ResolveSceneTargets();
            BindPendingInfectionsPanel();
            RevealSpawnedNotificationIcon();
            EnforceHiddenTargets();
        }
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
            RegisterTarget("tacticalOrders", hudCanvas.Find("TacticalOrdersContainer")?.gameObject, true);
            RegisterTarget("digestiveOrders", hudCanvas.Find("DigestiveOrdersContainer")?.gameObject, true);
            RegisterTarget("respiratoryOrders", hudCanvas.Find("RespiratoryOrdersContainer")?.gameObject, true);
            RegisterTarget("lymphaticOrders", hudCanvas.Find("LymphaticOrdersContainer")?.gameObject, true);
        }

        RegisterTarget("wbcHeadshot", GameObject.Find("WBC Squad HUD"), true);
        RegisterTarget("wbcHeadshot", GameObject.Find("WBC Squad Headshot Button"), true);
        CaptureExistingNotificationTargets();
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
                    cameraController.SelectLayer(2);
                infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
                if (infectionSpawner != null)
                {
                    HashSet<InfectionSpawner.InfectionMarker> existingMarkers = new HashSet<InfectionSpawner.InfectionMarker>(infectionSpawner.ActiveInfections);
                    infectionSpawner.ForceSpawnOneBacterialInfection();
                    foreach (InfectionSpawner.InfectionMarker marker in infectionSpawner.ActiveInfections)
                    {
                        if (marker != null && !existingMarkers.Contains(marker))
                        {
                            tutorialSpawnedMarker = marker;
                            break;
                        }
                    }
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
        ResolveSceneTargets();
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;

        TutorialStep step = steps[currentStepIndex];
        if (step == null)
            return;
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
            bodyLabel.text = step.body ?? string.Empty;
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

        bool isFinalStep = currentStepIndex == steps.Count - 1;
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isFinalStep);
        RefreshNavigationState();
        ResizePanelToBodyContent();
        ApplyContainerAnchor(step.containerAnchor);
        if (IsCurrentAction("resolveInfection") && !currentActionCompleted)
            Time.timeScale = 1f;
        BindResolutionEventForCurrentStep();
        RunStepEntryAction(step);
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
        float textWidthAtMaximum = Mathf.Max(120f, maxWidth - HorizontalPanelPadding);
        Vector2 preferredAtMaximumWidth = bodyLabel.GetPreferredValues(bodyLabel.text, textWidthAtMaximum, 0f);
        float panelWidth = Mathf.Clamp(preferredAtMaximumWidth.x + HorizontalPanelPadding, minimumWidth, maxWidth);
        float bodyWidth = Mathf.Max(120f, panelWidth - HorizontalPanelPadding);
        Vector2 preferredBodySize = bodyLabel.GetPreferredValues(bodyLabel.text, bodyWidth, 0f);
        float panelHeight = Mathf.Clamp(Mathf.Max(320f, preferredBodySize.y + VerticalPanelPadding), Mathf.Min(320f, availableHeight), availableHeight);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidth);
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(preferredBodySize.y, Mathf.Max(80f, panelHeight - VerticalPanelPadding)));
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
    }

    private void RestoreTutorialState()
    {
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
