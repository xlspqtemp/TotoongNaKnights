using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs the first-day gameplay tutorial and restores HUD visibility and pause state afterward.</summary>
public sealed class FirstDayTutorial : MonoBehaviour
{
    private const string GameplaySceneName = "Game";
    private const string HudCanvasName = "HUDCanvas";
    private const string TutorialSeenKey = "tutorialSeen";
    private const string RuntimeObjectName = "FirstDayTutorialRuntime";
    private const string DayCounterTargetName = "DayCounterPanel";
    private const string FastForwardTargetName = "FastForwardButton";
    private const string HumanSnapshotTargetName = "HumanSnapshotPanel";
    private const string ConsoleTargetName = "ConsoleLogPanel";
    private const string BatchContainerTargetName = "UnitBatchConsolePanel";
    private const string LymphaticTargetName = "SelectLymphaticButton";
    private const string CirculatoryTargetName = "SelectCirculatoryButton";
    private const string DigestiveTargetName = "SelectDigestiveButton";
    private const string RespiratoryTargetName = "SelectRespiratoryButton";
    private const string PendingPanelTargetName = "Pending Infections Panel Body";
    private const string WellnessTargetName = "GameProgressBar";
    private const string FungusIconResourcePath = "UI/Neon_Circuit_Mushroom_Emblem-removebg-preview";
    private const int GameplayPhaseStartStep = 12;
    private const int FinalTutorialStep = 25;
    private const float DimCanvasOrderOffset = -1f;
    private const float TutorialCanvasOrderOffset = 1f;
    private const int MaximumTutorialResearchDays = 3;
    private const float TutorialResearchDayPauseSeconds = 0.35f;
    private const float FramePadding = 10f;
    private const float FrameThickness = 3f;
    private const float PanelMaximumWidth = 560f;
    private const float PanelMaximumHeight = 242f;
    private const float ScreenEdgeMargin = 20f;
    private const float TargetGap = 18f;
    private const float PulseSpeed = 3.5f;

    private static readonly Color DimColor = new Color(0.28f, 0.30f, 0.32f, 0.58f);
    private static readonly Color PanelColor = new Color(0.025f, 0.055f, 0.075f, 0.98f);
    private static readonly Color AccentColor = new Color(0.10f, 0.85f, 0.94f, 1f);
    private static readonly Color MainTextColor = new Color(0.92f, 0.97f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.67f, 0.78f, 0.82f, 1f);
    private static readonly Color ButtonColor = new Color(0.06f, 0.45f, 0.53f, 1f);

    private static readonly string[] StepTitles =
    {
        "Welcome, boss",
        "Day and time",
        "Fast forward",
        "Human Snapshot",
        "Console",
        "Batches",
        "Lymphatic",
        "Circulatory",
        "Digestive",
        "Respiratory",
        "Pending Infections",
        "Wellness",
        "THE LYMPHATIC FACTORY",
        "SWITCH TO CIRCULATORY",
        "YOUR FIRST THREAT",
        "INFECTION INTEL",
        "DEPLOY YOUR DEFENDERS",
        "THE VIRAL THREAT",
        "THE ADAPTIVE RESPONSE",
        "SWITCH TO CIRCULATORY",
        "CONTAIN THE VIRUS",
        "ANTIGEN COLLECTED",
        "DEFEND THE BARRIERS",
        "MAINTAIN THE BARRIERS",
        "THE FINAL RULE",
        "CONGRATULATIONS!"
    };

    private static readonly string[] StepBodies =
    {
        "Welcome to your first day, boss! Let's get you briefed up, shall we?",
        "This is the current day and time. You have 14 days.",
        "Speed up the game with 1x, 2x, 4x, and 6x.",
        "This is the person you're protecting. It shows what their body is doing right now. Their routines and events can expose them to infection.",
        "The log reports what's happening: random events and daily routines.",
        "This is where you deploy your units. Each batch is a squad of cells.",
        "The body's drainage network. Pathogens drain here, and the Factory trains your T and B cells.",
        "The bloodstream, where infections appear and where your units fight.",
        "Food and drink can carry pathogens. Protect its barrier.",
        "Dust and coughs carry pathogens. Protect its barrier.",
        "Every infection in the body is listed here, with its type.",
        "This is the body's overall health. Keep it above 50 by day 14. At 0, it's game over.",
        "This is where you train your immune cells. Let's start by training a Neutrophil. Tap the Neutrophil to train it. Use Fast Forward to speed up training!",
        "Click the Circulatory layer to return to the bloodstream.",
        "A bacterial infection has appeared! Tap the floating red icon to see what it is.",
        "This popup tells you everything you need to know about the pathogen and the best unit to deploy. Close it when you're ready.",
        "Tap the Neutrophil batch, then tap the bacteria icon to send your immune cells. Use Fast Forward to speed up their arrival, then wait for the Neutrophil to clear the bacteria.",
        "A virus has appeared! WBCs can't kill viruses. They can only contain them. Click the highlighted viral icon to see what it is, close the intel popup, then click Next.",
        "We need a specialized unit for this. Let's go back to the Factory and train a Macrophage. Use Fast Forward to speed it up!",
        "Click the Circulatory layer to return to the bloodstream.",
        "Deploy the Macrophage to the virus to contain it. This will buy you time.",
        "The Dendritic Cell is researching the contained virus. The tutorial will advance 2–3 days for the antigen to be ready; click the icon or anywhere inside its cyan highlight to collect it.",
        "The lungs are breached! Tap the Cough button to clear the infection, then Repair to seal the breach. Completing this grants the Oxygen Boost perk—your units move twice as fast for 15 seconds!",
        "You must maintain barrier integrity to prevent the Breached State. If a barrier is breached, pathogens become mutated and far deadlier. The Digestive layer's Vomit button only works when the body is awake.",
        "Reach Day 14 with a Wellness score of 50 or above to win. Go lower than 50, or hit 0, and you lose. Good luck, Defender.",
        "You've completed the tutorial. You're ready to defend the body."
    };

    private static readonly string[] StepTargets =
    {
        null,
        DayCounterTargetName,
        FastForwardTargetName,
        HumanSnapshotTargetName,
        ConsoleTargetName,
        BatchContainerTargetName,
        LymphaticTargetName,
        CirculatoryTargetName,
        DigestiveTargetName,
        RespiratoryTargetName,
        PendingPanelTargetName,
        WellnessTargetName,
        LymphaticTargetName,
        CirculatoryTargetName,
        null,
        "Infection Intel Panel",
        null,
        null,
        LymphaticTargetName,
        CirculatoryTargetName,
        CirculatoryTargetName,
        null,
        "CoughButton",
        null,
        WellnessTargetName,
        null
    };

    private static readonly string[] DemoRowNames = { "Bacteria", "Virus", "Fungus", "Allergen", "Parasite" };
    private static readonly string[] DemoRowIconPaths =
    {
        "UI/Red bacteria icon",
        "UI/Blue virus icon",
        FungusIconResourcePath,
        "UI/Allergen icon",
        "UI/Parasite icon"
    };

    private static readonly Color[] DemoRowColors =
    {
        new Color(0.95f, 0.2f, 0.18f, 1f),
        new Color(0.2f, 0.55f, 1f, 1f),
        new Color(0.3f, 0.85f, 0.35f, 1f),
        new Color(0.96f, 0.97f, 1f, 1f),
        new Color(1f, 0.78f, 0.04f, 1f)
    };

    private static Sprite sharedWhiteSprite;
    private static bool suppressTutorialOnNextGameLoad;

    private sealed class HudGroupState
    {
        public CanvasGroup group;
        public float alpha;
        public bool interactable;
        public bool blocksRaycasts;
        public bool addedByTutorial;
    }

    private sealed class CanvasGroupState
    {
        public float alpha;
        public bool interactable;
        public bool blocksRaycasts;
    }

    private readonly Dictionary<Transform, HudGroupState> hudGroups = new Dictionary<Transform, HudGroupState>();
    private readonly Dictionary<CanvasGroup, CanvasGroupState> nestedCanvasGroupStates = new Dictionary<CanvasGroup, CanvasGroupState>();
    private readonly HashSet<Transform> revealedHudRoots = new HashSet<Transform>();
    private readonly List<GameObject> sampleConsoleLines = new List<GameObject>();
    private readonly List<GameObject> pendingDemoRows = new List<GameObject>();
    private GameObject pendingDemoEmptyState;
    private bool pendingDemoEmptyStateWasActive;
    private readonly List<Sprite> pendingDemoSprites = new List<Sprite>();
    private readonly List<InfectionSpawner.InfectionMarker> tutorialInfectionMarkers = new List<InfectionSpawner.InfectionMarker>();
    private readonly HashSet<int> knownBatchIds = new HashSet<int>();
    private readonly HashSet<int> preparedGameplaySteps = new HashSet<int>();
    private readonly List<LymphaticBatchGroup> cachedBatches = new List<LymphaticBatchGroup>();

    private Canvas hudCanvas;
    private Canvas dimCanvas;
    private Canvas tutorialCanvas;
    private RectTransform tutorialCanvasRect;
    private RectTransform panelRect;
    private RectTransform highlightRect;
    private Image[] highlightEdges;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI nextText;
    private Button nextButton;
    private Button boundTrainingButton;
    private Button boundCoughButton;
    private Button boundCirculatoryButton;
    private ScrollRect consoleScrollRect;
    private float consoleScrollPositionBeforeDemo;
    private bool hasSavedConsoleScrollPosition;
    private bool restoreConsoleScrollPositionPending;
    private GameObject currentTarget;
    private InfectionSpawner infectionSpawner;
    private InfectionNotificationManager infectionNotificationManager;
    private InfectionIntelPopup infectionIntelPopup;
    private LymphaticBatchSpawner batchSpawner;
    private CameraScript cameraController;
    private BarrierHealth respiratoryBarrier;
    private LungInfectionResponse tutorialLungResponse;
    private InfectionSpawner.InfectionMarker tutorialBacterialMarker;
    private InfectionSpawner.InfectionMarker tutorialViralMarker;
    private int neutrophilBatchId = -1;
    private int macrophageBatchId = -1;
    private int originalSelectedLayer = 1;
    private bool hasOriginalSelectedLayer;
    private bool gameplayPhase;
    private bool actionGateComplete = true;
    private bool trainingButtonClicked;
    private bool popupWasOpened;
    private bool coughButtonClicked;
    private bool repairResponseStarted;
    private bool repairGateComplete;
    private float originalRespiratoryBarrier;
    private bool hasOriginalRespiratoryBarrier;
    private float previousTimeScale = 1f;
    private int currentStepIndex;
    private bool tutorialIsOpen;
    private bool ownsTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameplaySceneName)
            return;
        if (suppressTutorialOnNextGameLoad)
        {
            suppressTutorialOnNextGameLoad = false;
            return;
        }
        if (FindFirstObjectByType<FirstDayTutorial>() != null)
            return;

        new GameObject(RuntimeObjectName).AddComponent<FirstDayTutorial>();
    }

    private void Start()
    {
        bool isGuest = string.Equals(
            LeaderboardManager.GetActiveSessionType(),
            "Guest",
            System.StringComparison.OrdinalIgnoreCase);

        if (!isGuest && PlayerPrefs.GetInt(TutorialSeenKey, 0) != 0)
            return;

        StartCoroutine(BeginTutorial());
    }

    private IEnumerator BeginTutorial()
    {
        preparedGameplaySteps.Clear();
        knownBatchIds.Clear();
        tutorialInfectionMarkers.Clear();
        tutorialBacterialMarker = null;
        tutorialViralMarker = null;
        neutrophilBatchId = -1;
        macrophageBatchId = -1;
        trainingButtonClicked = false;
        popupWasOpened = false;
        coughButtonClicked = false;
        repairResponseStarted = false;
        repairGateComplete = false;
        gameplayPhase = false;
        actionGateComplete = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        ownsTimeScale = true;
        yield return null;

        GameObject hudObject = GameObject.Find(HudCanvasName);
        hudCanvas = hudObject != null ? hudObject.GetComponent<Canvas>() : null;
        if (hudCanvas == null)
        {
            RestoreTutorialState();
            yield break;
        }

        BuildTutorialCanvases();
        CaptureAndHideHud();
        ResolveGameplayReferences();
        if (cameraController != null)
        {
            originalSelectedLayer = cameraController.SelectedLayer;
            hasOriginalSelectedLayer = true;
        }
        tutorialIsOpen = true;
        gameplayPhase = false;
        actionGateComplete = true;
        currentStepIndex = 0;
        ShowCurrentStep();
    }

    /// <summary>Replays the first-day tutorial from its welcome step.</summary>
    public void ReplayTutorial()
    {
        if (tutorialIsOpen)
        {
            currentStepIndex = 0;
            ShowCurrentStep();
            return;
        }

        StartCoroutine(BeginTutorial());
    }

    private void Update()
    {
        if (restoreConsoleScrollPositionPending)
        {
            Canvas.ForceUpdateCanvases();
            if (consoleScrollRect != null)
                consoleScrollRect.verticalNormalizedPosition = consoleScrollPositionBeforeDemo;
            consoleScrollRect = null;
            hasSavedConsoleScrollPosition = false;
            restoreConsoleScrollPositionPending = false;
        }

        if (!tutorialIsOpen)
            return;

        EnsureHudGroups();
        TryHandleTutorialNotificationClick();
        TryHandleTutorialAntigenClick();
        UpdateGameplayGates();
        RefreshDynamicGameplayTarget();
        UpdateHighlight();
        UpdatePanelPlacement();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        RestoreTutorialState();
        if (sharedWhiteSprite != null)
        {
            Destroy(sharedWhiteSprite);
            sharedWhiteSprite = null;
        }
    }

    private void BuildTutorialCanvases()
    {
        int hudOrder = hudCanvas.sortingOrder;
        dimCanvas = CreateCanvas("FirstDayTutorialDimCanvas", hudOrder + (int)DimCanvasOrderOffset);
        RectTransform dimRect = dimCanvas.transform as RectTransform;
        Image dimImage = CreateImage("BackgroundGreyOverlay", dimRect, DimColor, false);
        StretchToParent(dimImage.rectTransform);

        tutorialCanvas = CreateCanvas("FirstDayTutorialCanvas", hudOrder + (int)TutorialCanvasOrderOffset + 1);
        tutorialCanvasRect = tutorialCanvas.transform as RectTransform;
        BuildExplanationPanel();
        BuildSkipButton();
        BuildHighlightFrame();
    }

    private static Canvas CreateCanvas(string objectName, int sortingOrder)
    {
        GameObject canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        return canvas;
    }

    private void BuildExplanationPanel()
    {
        GameObject panelObject = CreateUiObject("ExplanationContainer", tutorialCanvasRect, typeof(Image));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelMaximumWidth, PanelMaximumHeight);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        titleText = CreateText("Title", panelRect, string.Empty, 26f, MainTextColor, TextAlignmentOptions.MidlineLeft);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(-48f, 48f));

        bodyText = CreateText("Body", panelRect, string.Empty, 19f, MainTextColor, TextAlignmentOptions.TopLeft);
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 15f;
        bodyText.fontSizeMax = 19f;
        SetRect(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -82f), new Vector2(-48f, 110f));

        GameObject nextButtonObject = CreateUiObject("NextButton", panelRect, typeof(Image), typeof(Button));
        RectTransform nextButtonRect = nextButtonObject.GetComponent<RectTransform>();
        SetRect(nextButtonRect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-20f, 18f), new Vector2(150f, 46f));
        Image nextImage = nextButtonObject.GetComponent<Image>();
        nextImage.color = ButtonColor;
        nextButton = nextButtonObject.GetComponent<Button>();
        nextButton.targetGraphic = nextImage;
        nextButton.onClick.AddListener(AdvanceStep);
        nextText = CreateText("NextLabel", nextButtonRect, string.Empty, 17f, MainTextColor, TextAlignmentOptions.Center);
        StretchToParent(nextText.rectTransform);
    }

    private void BuildSkipButton()
    {
        GameObject skipButtonObject = CreateUiObject("SkipTutorialButton", tutorialCanvasRect, typeof(Image), typeof(Button));
        RectTransform skipRect = skipButtonObject.GetComponent<RectTransform>();
        SetRect(skipRect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -22f), new Vector2(172f, 44f));
        Image skipImage = skipButtonObject.GetComponent<Image>();
        skipImage.color = new Color(0.025f, 0.055f, 0.075f, 0.96f);
        Button skipButton = skipButtonObject.GetComponent<Button>();
        skipButton.targetGraphic = skipImage;
        skipButton.onClick.AddListener(FinishTutorial);
        TextMeshProUGUI skipText = CreateText("SkipTutorialLabel", skipRect, "Skip Tutorial", 16f, MutedTextColor, TextAlignmentOptions.Center);
        StretchToParent(skipText.rectTransform);
    }

    private void BuildHighlightFrame()
    {
        GameObject frameObject = new GameObject("CyanTargetFrame", typeof(RectTransform));
        frameObject.transform.SetParent(tutorialCanvasRect, false);
        highlightRect = frameObject.GetComponent<RectTransform>();
        highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
        highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
        highlightRect.pivot = new Vector2(0.5f, 0.5f);
        highlightEdges = new Image[4];
        for (int index = 0; index < highlightEdges.Length; index++)
        {
            highlightEdges[index] = CreateImage("FrameEdge" + index, highlightRect, AccentColor, false);
        }
        highlightRect.gameObject.SetActive(false);
    }

    private void CaptureAndHideHud()
    {
        EnsureHudGroups();
        foreach (KeyValuePair<Transform, HudGroupState> entry in hudGroups)
            SetHudRootVisible(entry.Key, false);
    }

    private void EnsureHudGroups()
    {
        if (hudCanvas == null)
            return;

        for (int index = 0; index < hudCanvas.transform.childCount; index++)
        {
            Transform child = hudCanvas.transform.GetChild(index);
            if (hudGroups.ContainsKey(child))
                continue;

            CanvasGroup group = child.GetComponent<CanvasGroup>();
            bool addedByTutorial = group == null;
            if (addedByTutorial)
                group = child.gameObject.AddComponent<CanvasGroup>();

            hudGroups.Add(child, new HudGroupState
            {
                group = group,
                alpha = group.alpha,
                interactable = group.interactable,
                blocksRaycasts = group.blocksRaycasts,
                addedByTutorial = addedByTutorial
            });

            if (!revealedHudRoots.Contains(child))
                SetHudRootVisible(child, false);
        }
    }

    private void SetHudRootVisible(Transform root, bool isVisible)
    {
        if (root == null || !hudGroups.TryGetValue(root, out HudGroupState state) || state.group == null)
            return;

        if (isVisible)
        {
            state.group.alpha = 1f;
            state.group.interactable = true;
            state.group.blocksRaycasts = true;
        }
        else
        {
            state.group.alpha = 0f;
            state.group.interactable = false;
            state.group.blocksRaycasts = false;
        }
    }

    private void ShowCurrentStep()
    {
        gameplayPhase = currentStepIndex >= GameplayPhaseStartStep;
        titleText.text = StepTitles[currentStepIndex];
        bodyText.text = StepBodies[currentStepIndex];
        string actionWord = Application.isMobilePlatform ? "Tap" : "Click";
        nextText.text = currentStepIndex == FinalTutorialStep
            ? "Begin!"
            : "Next · " + actionWord;

        if (gameplayPhase)
        {
            if (currentStepIndex == GameplayPhaseStartStep)
                CleanupDemoContent();
            PrepareGameplayStep(currentStepIndex);
        }
        else
        {
            SetGameplayClockRunning(false);
            if (currentStepIndex == 4)
                CreateConsoleDemoLines();
            else if (currentStepIndex == 10)
                CreatePendingDemoRows();
        }

        string targetName = StepTargets[currentStepIndex];
        currentTarget = string.IsNullOrEmpty(targetName) ? null : GameObject.Find(targetName);
        if (gameplayPhase)
            SetGameplayTarget(currentStepIndex);
        RevealTargetHudGroups(currentTarget);

        Image panelImage = panelRect.GetComponent<Image>();
        if (panelImage != null)
            panelImage.raycastTarget = false;
        tutorialCanvas.sortingOrder = currentStepIndex == 15 && infectionIntelPopup != null && infectionIntelPopup.IsOpen
            ? 101
            : hudCanvas.sortingOrder + (int)TutorialCanvasOrderOffset + 1;

        if (nextButton != null)
            nextButton.interactable = !IsGameplayActionGated(currentStepIndex) || actionGateComplete;

        highlightRect.gameObject.SetActive(currentTarget != null);
        UpdateHighlight();
        UpdatePanelPlacement();
    }

    private void SetGameplayTarget(int stepIndex)
    {
        if (stepIndex == 14 || stepIndex == 17)
        {
            InfectionSpawner.InfectionMarker marker = stepIndex == 14 ? tutorialBacterialMarker : tutorialViralMarker;
            currentTarget = FindInfectionNotificationTarget(marker);
        }
        else if (stepIndex == 16)
        {
            currentTarget = neutrophilBatchId > 0 ? GameObject.Find("BatchIcon_" + neutrophilBatchId) : null;
        }
        else if (stepIndex == 20)
        {
            currentTarget = macrophageBatchId > 0 ? GameObject.Find("BatchIcon_" + macrophageBatchId) : null;
        }
        else if (stepIndex == 21)
        {
            currentTarget = infectionSpawner != null
                ? infectionSpawner.GetTutorialAntigenIcon(tutorialViralMarker)
                : null;
        }
        else if (stepIndex == 22 && coughButtonClicked)
        {
            currentTarget = GameObject.Find("Lung Virus Response UI/Panel/Repair Button");
        }
    }

    private GameObject FindInfectionNotificationTarget(InfectionSpawner.InfectionMarker marker)
    {
        if (marker == null || marker.infection == null)
            return null;

        return marker.notificationRenderer != null ? marker.notificationRenderer.gameObject : null;
    }

    private static bool IsGameplayActionGated(int stepIndex)
    {
        return stepIndex >= GameplayPhaseStartStep && stepIndex <= 22;
    }

    private void ResolveGameplayReferences()
    {
        infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        infectionNotificationManager = FindFirstObjectByType<InfectionNotificationManager>();
        infectionIntelPopup = FindFirstObjectByType<InfectionIntelPopup>();
        batchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        cameraController = FindFirstObjectByType<CameraScript>();
        GameObject respiratoryRoot = GameObject.Find("/Layers/Respiratory");
        respiratoryBarrier = respiratoryRoot != null ? respiratoryRoot.GetComponent<BarrierHealth>() : null;
        if (infectionNotificationManager != null)
        {
            infectionNotificationManager.OnIntelOpened -= HandleIntelOpened;
            infectionNotificationManager.OnIntelOpened += HandleIntelOpened;
        }
    }

    private void PrepareGameplayStep(int stepIndex)
    {
        SetGameplayClockRunning(false);
        if (!preparedGameplaySteps.Add(stepIndex))
            return;

        actionGateComplete = !IsGameplayActionGated(stepIndex);
        if (stepIndex == 12 || stepIndex == 18)
        {
            if (stepIndex == 18 && infectionIntelPopup != null && infectionIntelPopup.IsOpen)
                infectionIntelPopup.Close();
            RevealTargetHudGroups(GameObject.Find(FastForwardTargetName));
            SelectTutorialLayer(1);
            CacheExistingBatchIds();
            trainingButtonClicked = false;
            BindTrainingButton(stepIndex == 12 ? "UnitIcon_1" : "UnitIcon_2");
            StartCoroutine(HighlightTrainingButtonAfterLayer(stepIndex == 12 ? "UnitIcon_1" : "UnitIcon_2"));
        }
        else if (stepIndex == 13 || stepIndex == 19)
        {
            BindCirculatoryButton();
            actionGateComplete = false;
        }
        else if (stepIndex == 14)
        {
            SelectTutorialLayer(2);
            SetGameplayClockRunning(true);
            SpawnTutorialBacteria();
            actionGateComplete = false;
            if (tutorialBacterialMarker != null)
                StartCoroutine(PauseAfterNotificationAppears(tutorialBacterialMarker, stepIndex));
        }
        else if (stepIndex == 15)
        {
            popupWasOpened = infectionIntelPopup != null && infectionIntelPopup.IsOpen;
            actionGateComplete = false;
        }
        else if (stepIndex == 16)
        {
            SelectTutorialLayer(2);
            SetGameplayClockRunning(true);
            actionGateComplete = false;
        }
        else if (stepIndex == 17)
        {
            SelectTutorialLayer(2);
            SetGameplayClockRunning(true);
            SpawnTutorialVirus();
            actionGateComplete = false;
            if (tutorialViralMarker != null)
                StartCoroutine(PauseAfterNotificationAppears(tutorialViralMarker, stepIndex));
        }
        else if (stepIndex == 20)
        {
            SelectTutorialLayer(2);
            SetGameplayClockRunning(true);
            actionGateComplete = false;
            StartCoroutine(HighlightBatchAfterLayer());
        }
        else if (stepIndex == 21)
        {
            SetGameplayClockRunning(false);
            actionGateComplete = false;
            if (tutorialViralMarker != null)
                StartCoroutine(AdvanceTutorialResearchDays(tutorialViralMarker));
        }
        else if (stepIndex == 22)
        {
            SelectTutorialLayer(4);
            PrepareBarrierRepairStep();
            actionGateComplete = false;
        }
        else if (stepIndex == FinalTutorialStep)
        {
            SetGameplayClockRunning(false);
            actionGateComplete = true;
        }
    }

    private IEnumerator AdvanceTutorialResearchDays(InfectionSpawner.InfectionMarker marker)
    {
        if (marker == null || infectionSpawner == null || !marker.isContained)
            yield break;

        DayCounterUI dayCounter = FindFirstObjectByType<DayCounterUI>();
        if (dayCounter == null)
        {
            Debug.LogWarning("[FirstDayTutorial] DayCounterUI is missing; completing tutorial viral research immediately.", this);
            infectionSpawner.ResolveTutorialContainmentImmediately(marker);
            yield break;
        }

        int targetDay = marker.containedResolveDay;
        int daysToAdvance = Mathf.Clamp(targetDay - dayCounter.CurrentDay, 0, MaximumTutorialResearchDays);
        for (int dayOffset = 0; dayOffset < daysToAdvance; dayOffset++)
        {
            if (!tutorialIsOpen || currentStepIndex != 21)
                yield break;
            if (marker.tutorialAntigenCollectionAttempted)
                break;

            dayCounter.AdvanceDay();
            yield return new WaitForSecondsRealtime(TutorialResearchDayPauseSeconds);
        }

        if (!tutorialIsOpen || currentStepIndex != 21)
            yield break;

        if (!marker.tutorialAntigenCollectionAttempted)
            infectionSpawner.ResolveTutorialContainmentImmediately(marker);

        if (marker.tutorialAntigenCollectionAttempted && bodyText != null)
            bodyText.text = "The Dendritic Cell has finished its research. Click the antigen icon to collect it.";
    }

    private void SelectTutorialLayer(int layerNumber)
    {
        if (cameraController != null && cameraController.SelectedLayer != layerNumber)
            cameraController.SelectLayerWithoutCameraMove(layerNumber);
    }

    private IEnumerator WaitForCirculatoryLayerSelection()
    {
        float timeoutAt = Time.unscaledTime + 3f;
        while (tutorialIsOpen && cameraController != null && cameraController.SelectedLayer != 2 && Time.unscaledTime < timeoutAt)
            yield return null;

        if (!tutorialIsOpen || (currentStepIndex != 13 && currentStepIndex != 19) ||
            cameraController == null || cameraController.SelectedLayer != 2)
            yield break;

        actionGateComplete = true;
        SetGameplayClockRunning(false);
        if (nextButton != null)
            nextButton.interactable = true;
    }

    private void BindCirculatoryButton()
    {
        if (boundCirculatoryButton != null)
            boundCirculatoryButton.onClick.RemoveListener(HandleCirculatoryButtonClicked);

        GameObject layerButton = GameObject.Find(CirculatoryTargetName);
        boundCirculatoryButton = layerButton != null ? layerButton.GetComponent<Button>() : null;
        if (boundCirculatoryButton != null)
            boundCirculatoryButton.onClick.AddListener(HandleCirculatoryButtonClicked);
    }

    private void HandleCirculatoryButtonClicked()
    {
        if (currentStepIndex != 13 && currentStepIndex != 19)
            return;

        StartCoroutine(WaitForCirculatoryLayerSelection());
    }

    private IEnumerator PauseAfterNotificationAppears(InfectionSpawner.InfectionMarker marker, int expectedStep)
    {
        float timeoutAt = Time.unscaledTime + 5f;
        SpriteRenderer notificationRenderer = null;
        while (tutorialIsOpen && currentStepIndex == expectedStep && marker != null && Time.unscaledTime < timeoutAt)
        {
            GameObject notificationObject = FindInfectionNotificationTarget(marker);
            notificationRenderer = notificationObject != null ? notificationObject.GetComponent<SpriteRenderer>() : null;
            if (notificationRenderer != null && notificationRenderer.color.a >= 0.95f)
                break;
            yield return null;
        }

        if (!tutorialIsOpen || currentStepIndex != expectedStep || marker == null)
            yield break;

        if (notificationRenderer == null)
        {
            GameObject notificationObject = FindInfectionNotificationTarget(marker);
            notificationRenderer = notificationObject != null ? notificationObject.GetComponent<SpriteRenderer>() : null;
        }
        if (notificationRenderer == null)
            yield break;

        Color notificationColor = notificationRenderer.color;
        notificationColor.a = 1f;
        notificationRenderer.color = notificationColor;
        SetGameplayClockRunning(false);
    }

    private void SetGameplayClockRunning(bool isRunning)
    {
        if (isRunning)
            GameplaySpeed.ApplyResumeTimeScale();
        else
            Time.timeScale = 0f;
    }

    private IEnumerator HighlightTrainingButtonAfterLayer(string buttonName)
    {
        yield return new WaitForSecondsRealtime(0.65f);
        if (!tutorialIsOpen || !gameplayPhase || currentStepIndex != (buttonName == "UnitIcon_1" ? 12 : 18))
            yield break;

        currentTarget = GameObject.Find(buttonName);
        RevealTargetHudGroups(currentTarget);
        highlightRect.gameObject.SetActive(currentTarget != null);
        UpdateHighlight();
        UpdatePanelPlacement();
    }

    private IEnumerator HighlightBatchAfterLayer()
    {
        yield return new WaitForSecondsRealtime(0.65f);
        if (!tutorialIsOpen || currentStepIndex != 20)
            yield break;

        currentTarget = macrophageBatchId > 0 ? GameObject.Find("BatchIcon_" + macrophageBatchId) : null;
        RevealTargetHudGroups(currentTarget);
        highlightRect.gameObject.SetActive(currentTarget != null);
        UpdateHighlight();
        UpdatePanelPlacement();
    }

    private void BindTrainingButton(string buttonName)
    {
        if (boundTrainingButton != null)
            boundTrainingButton.onClick.RemoveListener(HandleTrainingButtonClicked);
        GameObject trainingButtonObject = GameObject.Find(buttonName);
        boundTrainingButton = trainingButtonObject != null ? trainingButtonObject.GetComponent<Button>() : null;
        if (boundTrainingButton != null)
            boundTrainingButton.onClick.AddListener(HandleTrainingButtonClicked);
    }

    private void HandleTrainingButtonClicked()
    {
        if (currentStepIndex != 12 && currentStepIndex != 18)
            return;

        trainingButtonClicked = true;
        SetGameplayClockRunning(true);
    }

    private void CacheExistingBatchIds()
    {
        knownBatchIds.Clear();
        if (batchSpawner == null)
            batchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (batchSpawner == null)
            return;

        batchSpawner.GetBatches(cachedBatches);
        foreach (LymphaticBatchGroup batch in cachedBatches)
        {
            if (batch != null)
                knownBatchIds.Add(batch.BatchId);
        }
    }

    private LymphaticBatchGroup FindNewCompletedBatch(string unitType)
    {
        if (batchSpawner == null)
            return null;

        batchSpawner.GetBatches(cachedBatches);
        foreach (LymphaticBatchGroup batch in cachedBatches)
        {
            if (batch != null && !knownBatchIds.Contains(batch.BatchId) &&
                string.Equals(batch.UnitType, unitType, System.StringComparison.OrdinalIgnoreCase))
                return batch;
        }

        return null;
    }

    private void SpawnTutorialBacteria()
    {
        if (infectionSpawner == null)
            infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (infectionSpawner == null)
            return;

        HashSet<InfectionSpawner.InfectionMarker> existingMarkers = new HashSet<InfectionSpawner.InfectionMarker>(infectionSpawner.ActiveInfections);
        infectionSpawner.ForceSpawnOneBacterialInfection();
        foreach (InfectionSpawner.InfectionMarker marker in infectionSpawner.ActiveInfections)
        {
            if (marker == null || existingMarkers.Contains(marker) || marker.infection == null ||
                marker.infection.pathogenType != InfectionPathogenType.Bacterial)
                continue;

            marker.isTutorialMarker = true;
            tutorialBacterialMarker = marker;
            tutorialInfectionMarkers.Add(marker);
            return;
        }
    }

    private void SpawnTutorialVirus()
    {
        if (infectionSpawner == null)
            infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        tutorialViralMarker = infectionSpawner != null ? infectionSpawner.SpawnTutorialCommonColdInfection() : null;
        if (tutorialViralMarker != null && !tutorialInfectionMarkers.Contains(tutorialViralMarker))
            tutorialInfectionMarkers.Add(tutorialViralMarker);
    }

    private void TryHandleTutorialNotificationClick()
    {
        if (!Input.GetMouseButtonDown(0) || (currentStepIndex != 14 && currentStepIndex != 17))
            return;

        InfectionSpawner.InfectionMarker marker = currentStepIndex == 14
            ? tutorialBacterialMarker
            : tutorialViralMarker;
        if (marker == null || infectionSpawner == null || !infectionSpawner.IsInfectionActive(marker))
            return;

        GameObject notificationObject = FindInfectionNotificationTarget(marker);
        if (notificationObject == null)
            return;

        Collider notificationCollider = notificationObject.GetComponent<Collider>();
        Camera gameplayCamera = GetGameplayCamera();
        if (gameplayCamera == null)
            return;

        Ray clickRay = gameplayCamera.ScreenPointToRay(Input.mousePosition);
        bool clickedNotification = notificationCollider != null && notificationCollider.enabled &&
            notificationCollider.Raycast(clickRay, out _, gameplayCamera.farClipPlane);
        if (!clickedNotification && TryGetScreenRect(notificationObject, gameplayCamera, out Rect notificationScreenRect))
            clickedNotification = notificationScreenRect.Contains(Input.mousePosition);
        if (!clickedNotification)
            return;

        infectionIntelPopup = infectionIntelPopup != null
            ? infectionIntelPopup
            : FindFirstObjectByType<InfectionIntelPopup>();
        if (infectionIntelPopup == null)
            return;

        infectionIntelPopup.Show(marker, infectionSpawner);
        HandleIntelOpened(marker);
    }
    private void TryHandleTutorialAntigenClick()
    {
        if (currentStepIndex != 21 || !Input.GetMouseButtonDown(0) ||
            tutorialViralMarker == null || infectionSpawner == null)
        {
            return;
        }

        GameObject antigenIcon = infectionSpawner.GetTutorialAntigenIcon(tutorialViralMarker);
        if (antigenIcon == null || highlightRect == null || !highlightRect.gameObject.activeInHierarchy)
            return;

        Vector2 highlightCenter = new Vector2(
            Screen.width * 0.5f + highlightRect.anchoredPosition.x,
            Screen.height * 0.5f + highlightRect.anchoredPosition.y);
        Rect clickableRect = CenteredRect(highlightCenter, highlightRect.sizeDelta.x, highlightRect.sizeDelta.y);
        if (!clickableRect.Contains(Input.mousePosition))
            return;

        infectionSpawner.TryCollectTutorialAntigen(tutorialViralMarker);
    }



    private void HandleIntelOpened(InfectionSpawner.InfectionMarker marker)
    {
        if (!tutorialIsOpen)
            return;

        infectionIntelPopup = FindFirstObjectByType<InfectionIntelPopup>();
        popupWasOpened = true;
        if (currentStepIndex == 14 && marker == tutorialBacterialMarker)
        {
            SetGameplayClockRunning(false);
            currentStepIndex = 15;
            actionGateComplete = false;
            ShowCurrentStep();
        }
        else if (currentStepIndex == 17 && marker == tutorialViralMarker)
        {
            SetGameplayClockRunning(false);
            popupWasOpened = true;
            actionGateComplete = false;
            if (panelRect != null)
            {
                TextMeshProUGUI body = panelRect.Find("Body")?.GetComponent<TextMeshProUGUI>();
                if (body != null)
                    body.text = "Review the virus intel, close the popup, then click Next to continue.";
            }
            tutorialCanvas.sortingOrder = hudCanvas.sortingOrder + (int)TutorialCanvasOrderOffset + 1;
            if (nextButton != null)
                nextButton.interactable = false;
        }
    }

    private void PrepareBarrierRepairStep()
    {
        if (respiratoryBarrier == null)
        {
            GameObject respiratoryRoot = GameObject.Find("/Layers/Respiratory");
            respiratoryBarrier = respiratoryRoot != null ? respiratoryRoot.GetComponent<BarrierHealth>() : null;
        }
        if (respiratoryBarrier != null)
        {
            if (!hasOriginalRespiratoryBarrier)
            {
                originalRespiratoryBarrier = respiratoryBarrier.CurrentBarrier;
                hasOriginalRespiratoryBarrier = true;
            }
            respiratoryBarrier.TakeDamage(100000f);
        }

        Transform leftLung = GameObject.Find("/Layers/Respiratory/Pathway/Point - Left Lung")?.transform;
        tutorialLungResponse = leftLung != null ? leftLung.GetComponent<LungInfectionResponse>() : null;
        if (tutorialLungResponse != null)
            tutorialLungResponse.RegisterInfiltration("TutorialRespiratoryRepair");

        GameObject coughObject = GameObject.Find("CoughButton");
        boundCoughButton = coughObject != null ? coughObject.GetComponent<Button>() : null;
        if (boundCoughButton != null)
        {
            boundCoughButton.onClick.RemoveListener(HandleCoughButtonClicked);
            boundCoughButton.onClick.AddListener(HandleCoughButtonClicked);
        }
    }

    private void HandleCoughButtonClicked()
    {
        if (currentStepIndex != 22)
            return;

        coughButtonClicked = true;
        SetGameplayClockRunning(true);
        currentTarget = GameObject.Find("Lung Virus Response UI/Panel/Repair Button");
        RevealTargetHudGroups(currentTarget);
    }

    private void RefreshDynamicGameplayTarget()
    {
        GameObject target = null;
        if (currentStepIndex == 14)
            target = FindInfectionNotificationTarget(tutorialBacterialMarker);
        else if (currentStepIndex == 17)
            target = FindInfectionNotificationTarget(tutorialViralMarker);
        else if (currentStepIndex == 21)
            target = infectionSpawner != null
                ? infectionSpawner.GetTutorialAntigenIcon(tutorialViralMarker)
                : null;
        else if (currentStepIndex == 22 && coughButtonClicked)
            target = GameObject.Find("Lung Virus Response UI/Panel/Repair Button");

        if (target == null)
        {
            if (currentStepIndex == 14 || currentStepIndex == 17 || currentStepIndex == 21 ||
                (currentStepIndex == 22 && coughButtonClicked))
            {
                currentTarget = null;
                highlightRect.gameObject.SetActive(false);
                UpdatePanelPlacement();
            }
            return;
        }

        if (target == currentTarget)
            return;

        currentTarget = target;
        RevealTargetHudGroups(currentTarget);
        highlightRect.gameObject.SetActive(true);
        UpdateHighlight();
        UpdatePanelPlacement();
    }

    private void UpdateGameplayGates()
    {
        if (!gameplayPhase)
            return;

        if ((currentStepIndex == 12 || currentStepIndex == 18) && trainingButtonClicked)
        {
            LymphaticBatchGroup newBatch = FindNewCompletedBatch(currentStepIndex == 12 ? "Neutrophil" : "Macrophage");
            if (newBatch != null)
            {
                if (currentStepIndex == 12)
                    neutrophilBatchId = newBatch.BatchId;
                else
                    macrophageBatchId = newBatch.BatchId;
                actionGateComplete = true;
                SetGameplayClockRunning(false);
                if (nextButton != null)
                    nextButton.interactable = true;
            }
        }
        else if (currentStepIndex == 15 && popupWasOpened && infectionIntelPopup != null && !infectionIntelPopup.IsOpen)
        {
            actionGateComplete = true;
            popupWasOpened = false;
            tutorialCanvas.sortingOrder = hudCanvas.sortingOrder + (int)TutorialCanvasOrderOffset + 1;
            if (nextButton != null)
                nextButton.interactable = true;
            SetGameplayClockRunning(false);
        }
        else if (currentStepIndex == 17 && popupWasOpened && infectionIntelPopup != null && !infectionIntelPopup.IsOpen)
        {
            actionGateComplete = true;
            popupWasOpened = false;
            tutorialCanvas.sortingOrder = hudCanvas.sortingOrder + (int)TutorialCanvasOrderOffset + 1;
            if (nextButton != null)
                nextButton.interactable = true;
            SetGameplayClockRunning(false);
        }
        else if (currentStepIndex == 16 && tutorialBacterialMarker != null &&
                 infectionSpawner != null && !infectionSpawner.IsInfectionActive(tutorialBacterialMarker))
        {
            if (!actionGateComplete)
            {
                bodyText.text = "Great job! You cleared your first infection!";
                actionGateComplete = true;
                SetGameplayClockRunning(false);
                if (nextButton != null)
                    nextButton.interactable = true;
            }
        }
        else if (currentStepIndex == 20 && tutorialViralMarker != null && tutorialViralMarker.isContained)
        {
            actionGateComplete = true;
            SetGameplayClockRunning(false);
            if (nextButton != null)
                nextButton.interactable = true;
        }
        else if (currentStepIndex == 21 && tutorialViralMarker != null && tutorialViralMarker.tutorialAntigenCollected)
        {
            if (!actionGateComplete)
            {
                bodyText.text = "You've learned the Adaptive Immune System! In the real game, you'd use this to train a Killer T-Cell.";
                actionGateComplete = true;
                SetGameplayClockRunning(false);
                if (nextButton != null)
                    nextButton.interactable = true;
            }
        }
        else if (currentStepIndex == 22 && tutorialLungResponse != null)
        {
            if (tutorialLungResponse.IsResponseInProgress)
                repairResponseStarted = true;
            if (coughButtonClicked && repairResponseStarted && !tutorialLungResponse.IsResponseInProgress &&
                tutorialLungResponse.InfiltrationCount == 0 && !repairGateComplete)
            {
                repairGateComplete = true;
                actionGateComplete = true;
                SetGameplayClockRunning(false);
                if (nextButton != null)
                    nextButton.interactable = true;
            }
        }
    }

    private void AdvanceStep()
    {
        if (!tutorialIsOpen || (IsGameplayActionGated(currentStepIndex) && !actionGateComplete))
            return;

        if (currentStepIndex == FinalTutorialStep)
        {
            BeginFreshRun();
            return;
        }

        if (currentStepIndex == 21)
            infectionSpawner?.EndTutorialViralIsolation();

        currentStepIndex++;
        if (currentStepIndex >= StepTitles.Length)
        {
            FinishTutorial();
            return;
        }

        ShowCurrentStep();
    }

    private void RevealTargetHudGroups(GameObject target)
    {
        if (target == null || hudCanvas == null)
            return;

        Transform hudRoot = target.transform;
        while (hudRoot.parent != null && hudRoot.parent != hudCanvas.transform)
            hudRoot = hudRoot.parent;

        if (hudRoot.parent == hudCanvas.transform && hudGroups.ContainsKey(hudRoot))
        {
            revealedHudRoots.Add(hudRoot);
            SetHudRootVisible(hudRoot, true);
        }

        for (Transform current = target.transform; current != null && current != hudCanvas.transform; current = current.parent)
        {
            CanvasGroup nestedGroup = current.GetComponent<CanvasGroup>();
            if (nestedGroup == null)
                continue;

            if (!nestedCanvasGroupStates.ContainsKey(nestedGroup))
            {
                nestedCanvasGroupStates.Add(nestedGroup, new CanvasGroupState
                {
                    alpha = nestedGroup.alpha,
                    interactable = nestedGroup.interactable,
                    blocksRaycasts = nestedGroup.blocksRaycasts
                });
            }

            nestedGroup.alpha = 1f;
            nestedGroup.interactable = true;
            nestedGroup.blocksRaycasts = true;
        }
    }

    private void CreateConsoleDemoLines()
    {
        if (sampleConsoleLines.Count > 0 || hudCanvas == null)
            return;

        Transform consoleRoot = hudCanvas.transform.Find(ConsoleTargetName);
        Transform content = consoleRoot != null ? consoleRoot.Find("Viewport/Content") : null;
        if (content == null)
            return;

        consoleScrollRect = consoleRoot.GetComponentInChildren<ScrollRect>(true);
        if (consoleScrollRect != null)
        {
            consoleScrollPositionBeforeDemo = consoleScrollRect.verticalNormalizedPosition;
            hasSavedConsoleScrollPosition = true;
        }

        CreateConsoleDemoLine(content, "TutorialDemoLog_RandomEvent",
            "[12 PM] RANDOM EVENT — A cool front passes through.", new Color(1f, 0.72f, 0.24f, 1f));
        CreateConsoleDemoLine(content, "TutorialDemoLog_Routine",
            "[12 PM] ROUTINE — Midday rest and hydration.", new Color(0.62f, 0.82f, 0.94f, 1f));

        Canvas.ForceUpdateCanvases();
        if (consoleScrollRect != null)
            consoleScrollRect.verticalNormalizedPosition = 0f;
    }

    private void CreateConsoleDemoLine(Transform content, string objectName, string message, Color color)
    {
        TextMeshProUGUI line = CreateText(objectName, content, message, 13f, color, TextAlignmentOptions.MidlineLeft);
        line.font = Resources.Load<TMP_FontAsset>("BankGothicMediumSDF") ?? TMP_Settings.defaultFontAsset;
        line.textWrappingMode = TextWrappingModes.Normal;
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = new Vector2(0f, 1f);
        line.rectTransform.pivot = new Vector2(0f, 1f);
        line.rectTransform.sizeDelta = new Vector2(430f, 20f);
        sampleConsoleLines.Add(line.gameObject);
    }

    private void CreatePendingDemoRows()
    {
        if (pendingDemoRows.Count > 0)
            return;

        GameObject panelBody = GameObject.Find(PendingPanelTargetName);
        Transform rowContent = panelBody != null ? panelBody.transform.Find("Pending Infection Rows") : null;
        if (rowContent == null)
            return;

        pendingDemoEmptyState = rowContent.Find("Pending Infection Empty State")?.gameObject;
        pendingDemoEmptyStateWasActive = pendingDemoEmptyState != null && pendingDemoEmptyState.activeSelf;
        if (pendingDemoEmptyState != null)
            pendingDemoEmptyState.SetActive(false);

        GameObject demoContainer = new GameObject("FirstDayTutorialDemoRows", typeof(RectTransform));
        demoContainer.transform.SetParent(rowContent, false);
        StretchToParent(demoContainer.GetComponent<RectTransform>());
        demoContainer.transform.SetAsLastSibling();
        pendingDemoRows.Add(demoContainer);

        for (int index = 0; index < DemoRowNames.Length; index++)
            CreatePendingDemoRow(demoContainer.transform, index);
    }

    private void CreatePendingDemoRow(Transform parent, int index)
    {
        GameObject rowObject = CreateUiObject("FirstDayTutorialDemoRow - " + DemoRowNames[index], parent, typeof(Image));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -index * 38f);
        rowRect.sizeDelta = new Vector2(0f, 34f);

        Image rowBackground = rowObject.GetComponent<Image>();
        rowBackground.sprite = sharedWhiteSprite;
        rowBackground.color = new Color(0.035f, 0.09f, 0.115f, 0.98f);
        rowBackground.raycastTarget = false;

        GameObject iconObject = CreateUiObject("Demo Pathogen Icon", rowRect, typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(8f, 0f);
        iconRect.sizeDelta = new Vector2(24f, 24f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;
        Sprite iconSprite = Resources.Load<Sprite>(DemoRowIconPaths[index]);
        if (iconSprite == null)
        {
            Texture2D iconTexture = Resources.Load<Texture2D>(DemoRowIconPaths[index]);
            if (iconTexture != null)
            {
                iconSprite = Sprite.Create(iconTexture, new Rect(0f, 0f, iconTexture.width, iconTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                pendingDemoSprites.Add(iconSprite);
            }
        }

        if (iconSprite != null)
            iconImage.sprite = iconSprite;
        else
        {
            iconImage.sprite = sharedWhiteSprite;
            iconImage.color = DemoRowColors[index];
            iconRect.sizeDelta = new Vector2(14f, 14f);
        }

        TextMeshProUGUI nameLabel = CreateText("Demo Pathogen Name", rowRect, DemoRowNames[index], 14f,
            MainTextColor, TextAlignmentOptions.MidlineLeft);
        nameLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameLabel.rectTransform.anchorMax = new Vector2(0.72f, 1f);
        nameLabel.rectTransform.offsetMin = new Vector2(40f, 1f);
        nameLabel.rectTransform.offsetMax = new Vector2(-2f, -1f);

        TextMeshProUGUI demoLabel = CreateText("Demo Status", rowRect, "DEMO", 12f,
            DemoRowColors[index], TextAlignmentOptions.MidlineRight);
        demoLabel.fontStyle = FontStyles.Bold;
        demoLabel.rectTransform.anchorMin = new Vector2(0.72f, 0f);
        demoLabel.rectTransform.anchorMax = Vector2.one;
        demoLabel.rectTransform.offsetMin = new Vector2(0f, 1f);
        demoLabel.rectTransform.offsetMax = new Vector2(-10f, -1f);
    }



    private void FinishTutorial()
    {
        if (!tutorialIsOpen)
            return;

        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();
        RestoreTutorialState();
    }

    private void BeginFreshRun()
    {
        if (!tutorialIsOpen)
            return;

        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();
        suppressTutorialOnNextGameLoad = true;
        RestoreTutorialState();

        PauseController pauseController = FindFirstObjectByType<PauseController>();
        if (pauseController != null)
            pauseController.RestartGame();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void CleanupDemoContent()
    {
        foreach (GameObject line in sampleConsoleLines)
        {
            if (line != null)
                Destroy(line);
        }
        sampleConsoleLines.Clear();

        foreach (GameObject row in pendingDemoRows)
        {
            if (row != null)
                Destroy(row);
        }
        pendingDemoRows.Clear();
        if (pendingDemoEmptyState != null)
            pendingDemoEmptyState.SetActive(pendingDemoEmptyStateWasActive);
        pendingDemoEmptyState = null;
        pendingDemoEmptyStateWasActive = false;

        foreach (Sprite sprite in pendingDemoSprites)
        {
            if (sprite != null)
                Destroy(sprite);
        }
        pendingDemoSprites.Clear();

        restoreConsoleScrollPositionPending = hasSavedConsoleScrollPosition;
    }

    private void RestoreTutorialState()
    {
        if (infectionIntelPopup != null && infectionIntelPopup.IsOpen)
            infectionIntelPopup.Close();
        if (infectionNotificationManager != null)
            infectionNotificationManager.OnIntelOpened -= HandleIntelOpened;
        if (boundTrainingButton != null)
            boundTrainingButton.onClick.RemoveListener(HandleTrainingButtonClicked);
        if (boundCoughButton != null)
            boundCoughButton.onClick.RemoveListener(HandleCoughButtonClicked);
        if (boundCirculatoryButton != null)
            boundCirculatoryButton.onClick.RemoveListener(HandleCirculatoryButtonClicked);
        boundTrainingButton = null;
        boundCoughButton = null;
        boundCirculatoryButton = null;

        if (hasOriginalRespiratoryBarrier && respiratoryBarrier != null &&
            respiratoryBarrier.CurrentBarrier < originalRespiratoryBarrier)
        {
            respiratoryBarrier.RestoreBarrier(originalRespiratoryBarrier - respiratoryBarrier.CurrentBarrier);
        }
        hasOriginalRespiratoryBarrier = false;

        if (infectionSpawner != null)
        {
            infectionSpawner.EndTutorialViralIsolation();
            foreach (InfectionSpawner.InfectionMarker marker in tutorialInfectionMarkers)
            {
                if (marker != null && marker.isTutorialMarker)
                    infectionSpawner.RemoveTutorialMarker(marker);
            }
        }
        tutorialInfectionMarkers.Clear();
        tutorialBacterialMarker = null;
        tutorialViralMarker = null;

        CleanupDemoContent();
        if (hasOriginalSelectedLayer && cameraController != null && cameraController.SelectedLayer != originalSelectedLayer)
            cameraController.SelectLayerWithoutCameraMove(originalSelectedLayer);
        foreach (KeyValuePair<CanvasGroup, CanvasGroupState> entry in nestedCanvasGroupStates)
        {
            CanvasGroup group = entry.Key;
            CanvasGroupState state = entry.Value;
            if (group == null)
                continue;

            group.alpha = state.alpha;
            group.interactable = state.interactable;
            group.blocksRaycasts = state.blocksRaycasts;
        }
        nestedCanvasGroupStates.Clear();

        foreach (KeyValuePair<Transform, HudGroupState> entry in hudGroups)
        {
            HudGroupState state = entry.Value;
            if (state.group == null)
                continue;

            state.group.alpha = state.alpha;
            state.group.interactable = state.interactable;
            state.group.blocksRaycasts = state.blocksRaycasts;
            if (state.addedByTutorial)
                Destroy(state.group);
        }

        hudGroups.Clear();
        revealedHudRoots.Clear();
        if (ownsTimeScale)
        {
            Time.timeScale = previousTimeScale;
            ownsTimeScale = false;
        }
        tutorialIsOpen = false;
        currentTarget = null;

        if (dimCanvas != null)
            Destroy(dimCanvas.gameObject);
        if (tutorialCanvas != null)
            Destroy(tutorialCanvas.gameObject);
        dimCanvas = null;
        tutorialCanvas = null;
        tutorialCanvasRect = null;
        panelRect = null;
        highlightRect = null;
        highlightEdges = null;
        titleText = null;
        bodyText = null;
        nextText = null;
    }

    private void UpdateHighlight()
    {
        if (currentTarget == null || highlightRect == null ||
            !TryGetScreenRect(currentTarget, GetGameplayCamera(), out Rect targetRect))
        {
            if (highlightRect != null)
                highlightRect.gameObject.SetActive(false);
            return;
        }

        highlightRect.gameObject.SetActive(true);
        Vector2 center = targetRect.center;
        highlightRect.anchoredPosition = new Vector2(center.x - Screen.width * 0.5f, center.y - Screen.height * 0.5f);
        float highlightPadding = currentStepIndex == 17 ? FramePadding * 2f : FramePadding;
        highlightRect.sizeDelta = new Vector2(
            targetRect.width + highlightPadding * 2f,
            targetRect.height + highlightPadding * 2f);

        float pulse = 0.62f + 0.38f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed));
        Color edgeColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, pulse);
        for (int index = 0; index < highlightEdges.Length; index++)
            highlightEdges[index].color = edgeColor;

        SetRect(highlightEdges[0].rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, FrameThickness));
        SetRect(highlightEdges[1].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, FrameThickness));
        SetRect(highlightEdges[2].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(FrameThickness, 0f));
        SetRect(highlightEdges[3].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(FrameThickness, 0f));
    }

    private void UpdatePanelPlacement()
    {
        if (panelRect == null)
            return;

        float panelWidth = Mathf.Min(PanelMaximumWidth, Mathf.Max(240f, Screen.width - ScreenEdgeMargin * 2f));
        float panelHeight = Mathf.Min(PanelMaximumHeight, Mathf.Max(180f, Screen.height - ScreenEdgeMargin * 2f));
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        if (currentTarget == null || !TryGetScreenRect(currentTarget, GetGameplayCamera(), out Rect targetRect))
        {
            SetPanelCenter(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            return;
        }

        Vector2 targetCenter = targetRect.center;
        Vector2[] candidates =
        {
            new Vector2(targetRect.xMax + TargetGap + panelWidth * 0.5f, targetCenter.y),
            new Vector2(targetRect.xMin - TargetGap - panelWidth * 0.5f, targetCenter.y),
            new Vector2(targetCenter.x, targetRect.yMax + TargetGap + panelHeight * 0.5f),
            new Vector2(targetCenter.x, targetRect.yMin - TargetGap - panelHeight * 0.5f)
        };

        foreach (Vector2 candidate in candidates)
        {
            Vector2 adjustedCandidate = ClampPanelCenterToScreen(candidate, panelWidth, panelHeight);
            Rect candidateRect = CenteredRect(adjustedCandidate, panelWidth, panelHeight);
            if (!candidateRect.Overlaps(targetRect))
            {
                SetPanelCenter(adjustedCandidate);
                return;
            }
        }

        Vector2 bestCandidate = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float bestOverlapArea = float.PositiveInfinity;
        float bestDistance = -1f;
        foreach (Vector2 candidate in GetCornerCandidates(panelWidth, panelHeight))
        {
            Vector2 adjustedCandidate = ClampPanelCenterToScreen(candidate, panelWidth, panelHeight);
            Rect candidateRect = CenteredRect(adjustedCandidate, panelWidth, panelHeight);
            float overlapArea = GetOverlapArea(candidateRect, targetRect);
            float distance = (adjustedCandidate - targetCenter).sqrMagnitude;
            if (overlapArea < bestOverlapArea ||
                (Mathf.Approximately(overlapArea, bestOverlapArea) && distance > bestDistance))
            {
                bestCandidate = adjustedCandidate;
                bestOverlapArea = overlapArea;
                bestDistance = distance;
            }
        }

        SetPanelCenter(bestCandidate);
    }

    private Vector2 ClampPanelCenterToScreen(Vector2 candidate, float width, float height)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        return new Vector2(
            Mathf.Clamp(candidate.x, ScreenEdgeMargin + halfWidth, Screen.width - ScreenEdgeMargin - halfWidth),
            Mathf.Clamp(candidate.y, ScreenEdgeMargin + halfHeight, Screen.height - ScreenEdgeMargin - halfHeight));
    }

    private static float GetOverlapArea(Rect first, Rect second)
    {
        float overlapWidth = Mathf.Max(0f, Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin));
        float overlapHeight = Mathf.Max(0f, Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin));
        return overlapWidth * overlapHeight;
    }

    private Vector2[] GetCornerCandidates(float width, float height)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        return new[]
        {
            new Vector2(ScreenEdgeMargin + halfWidth, Screen.height - ScreenEdgeMargin - halfHeight),
            new Vector2(Screen.width - ScreenEdgeMargin - halfWidth, Screen.height - ScreenEdgeMargin - halfHeight),
            new Vector2(ScreenEdgeMargin + halfWidth, ScreenEdgeMargin + halfHeight),
            new Vector2(Screen.width - ScreenEdgeMargin - halfWidth, ScreenEdgeMargin + halfHeight)
        };
    }

    private void SetPanelCenter(Vector2 screenPosition)
    {
        panelRect.anchoredPosition = new Vector2(screenPosition.x - Screen.width * 0.5f, screenPosition.y - Screen.height * 0.5f);
    }

    private static Rect CenteredRect(Vector2 center, float width, float height)
    {
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    private Camera GetGameplayCamera()
    {
        Camera gameplayCamera = cameraController != null
            ? cameraController.GetComponentInChildren<Camera>()
            : null;
        return gameplayCamera != null ? gameplayCamera : Camera.main;
    }

    private static bool TryGetScreenRect(GameObject target, Camera gameplayCamera, out Rect screenRect)
    {
        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (targetRect != null)
        {
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera uiCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera != null ? targetCanvas.worldCamera : gameplayCamera
                : null;
            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 maximum = minimum;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[index]);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }

            screenRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return true;
        }

        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer == null || gameplayCamera == null)
        {
            screenRect = default;
            return false;
        }

        Bounds bounds = targetRenderer.bounds;
        Vector3 minimumWorld = bounds.min;
        Vector3 maximumWorld = bounds.max;
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 worldCorner = new Vector3(
                (corner & 1) == 0 ? minimumWorld.x : maximumWorld.x,
                (corner & 2) == 0 ? minimumWorld.y : maximumWorld.y,
                (corner & 4) == 0 ? minimumWorld.z : maximumWorld.z);
            Vector3 screenCorner = gameplayCamera.WorldToScreenPoint(worldCorner);
            if (screenCorner.z <= 0f)
                continue;

            minimumX = Mathf.Min(minimumX, screenCorner.x);
            maximumX = Mathf.Max(maximumX, screenCorner.x);
            minimumY = Mathf.Min(minimumY, screenCorner.y);
            maximumY = Mathf.Max(maximumY, screenCorner.y);
        }

        if (float.IsInfinity(minimumX) || float.IsInfinity(minimumY))
        {
            screenRect = default;
            return false;
        }

        screenRect = Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);
        return true;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color, bool raycastTarget)
    {
        GameObject imageObject = CreateUiObject(objectName, parent, typeof(Image));
        Image image = imageObject.GetComponent<Image>();
        if (sharedWhiteSprite == null)
            sharedWhiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        image.sprite = sharedWhiteSprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] components)
    {
        System.Type[] componentTypes = new System.Type[components.Length + 2];
        componentTypes[0] = typeof(RectTransform);
        componentTypes[1] = typeof(CanvasRenderer);
        for (int index = 0; index < components.Length; index++)
            componentTypes[index + 2] = components[index];

        GameObject uiObject = new GameObject(objectName, componentTypes);
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
