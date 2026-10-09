using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Runs the first-play tutorial overlay and manages temporary UI visibility and pause state.</summary>
public sealed class TutorialManager : MonoBehaviour
{
    private const string GameplaySceneName = "Game";
    private const string TutorialOverlayResourceName = "TutorialOverlay";
    private const string TutorialSeenPreferenceKey = "tutorialSeen";
    private const string GuestSessionType = "Guest";
    private const string NoSessionType = "NoActiveSession";
    private const float MinimumPanelWidth = 700f;
    private const float HorizontalPanelPadding = 190f;
    private const float VerticalPanelPadding = 174f;

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
    }

    [Header("Tutorial Steps")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

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
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private float maximumPanelWidth = 900f;

    private readonly Dictionary<GameObject, bool> originalTargetStates = new Dictionary<GameObject, bool>();
    private int currentStepIndex;
    private bool currentActionCompleted;
    private bool tutorialIsOpen;
    private bool wasPausedBeforeTutorial;

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
        if (steps == null || steps.Count == 0)
            return;

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

        if (!tutorialIsOpen)
        {
            wasPausedBeforeTutorial = Time.timeScale <= 0f;
            CaptureAndHideInitialTargets();
            Time.timeScale = 0f;
        }

        tutorialIsOpen = true;
        currentStepIndex = 0;
        currentActionCompleted = false;
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
        RefreshNavigationState();
    }

    private void CaptureAndHideInitialTargets()
    {
        originalTargetStates.Clear();
        foreach (TutorialStep step in steps)
        {
            if (step == null)
                continue;

            CaptureTargetState(step.hideTarget);
            CaptureTargetState(step.revealTarget);
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
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;

        TutorialStep step = steps[currentStepIndex];
        if (step == null)
            return;

        if (step.revealTarget != null)
        {
            CaptureTargetState(step.revealTarget);
            step.revealTarget.SetActive(true);
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

        bool isFinalStep = currentStepIndex == steps.Count - 1;
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isFinalStep);
        RefreshNavigationState();
        ResizePanelToBodyContent();
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
        float textWidthAtMaximum = Mathf.Max(120f, maxWidth - HorizontalPanelPadding);
        Vector2 preferredAtMaximumWidth = bodyLabel.GetPreferredValues(bodyLabel.text, textWidthAtMaximum, 0f);
        float panelWidth = Mathf.Clamp(preferredAtMaximumWidth.x + HorizontalPanelPadding, MinimumPanelWidth, maxWidth);
        float bodyWidth = Mathf.Max(120f, panelWidth - HorizontalPanelPadding);
        Vector2 preferredBodySize = bodyLabel.GetPreferredValues(bodyLabel.text, bodyWidth, 0f);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidth);
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredBodySize.y);
        float panelHeight = Mathf.Max(320f, preferredBodySize.y + VerticalPanelPadding);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
    }

    private void RestoreTutorialState()
    {
        foreach (KeyValuePair<GameObject, bool> targetState in originalTargetStates)
        {
            if (targetState.Key != null)
                targetState.Key.SetActive(targetState.Value);
        }
        originalTargetStates.Clear();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (!tutorialIsOpen)
            return;

        tutorialIsOpen = false;
        Time.timeScale = wasPausedBeforeTutorial ? 0f : 1f;
    }
}
