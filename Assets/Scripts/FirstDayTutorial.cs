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
    private const float DimCanvasOrderOffset = -1f;
    private const float TutorialCanvasOrderOffset = 1f;
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
        "Wellness"
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
        "This is the body's overall health. Keep it above 50 by day 14. At 0, it's game over."
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
        WellnessTargetName
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
    private readonly List<Sprite> pendingDemoSprites = new List<Sprite>();

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
    private ScrollRect consoleScrollRect;
    private float consoleScrollPositionBeforeDemo;
    private bool hasSavedConsoleScrollPosition;
    private bool restoreConsoleScrollPositionPending;
    private GameObject currentTarget;
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
        if (scene.name != GameplaySceneName || FindFirstObjectByType<FirstDayTutorial>() != null)
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
        tutorialIsOpen = true;
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
        Button nextButton = nextButtonObject.GetComponent<Button>();
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
            state.group.interactable = state.interactable;
            state.group.blocksRaycasts = state.blocksRaycasts;
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
        titleText.text = StepTitles[currentStepIndex];
        string actionWord = Application.isMobilePlatform ? "Tap" : "Click";
        nextText.text = currentStepIndex == StepTitles.Length - 1
            ? "Finish · " + actionWord
            : "Next · " + actionWord;
        bodyText.text = StepBodies[currentStepIndex];

        string targetName = StepTargets[currentStepIndex];
        currentTarget = string.IsNullOrEmpty(targetName) ? null : GameObject.Find(targetName);
        RevealTargetHudGroups(currentTarget);

        if (currentStepIndex == 4)
            CreateConsoleDemoLines();
        else if (currentStepIndex == 10)
            CreatePendingDemoRows();

        highlightRect.gameObject.SetActive(currentTarget != null);
        UpdateHighlight();
        UpdatePanelPlacement();
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
        Texture2D iconTexture = Resources.Load<Texture2D>(DemoRowIconPaths[index]);
        if (iconTexture != null)
        {
            Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0f, 0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f), 100f);
            pendingDemoSprites.Add(iconSprite);
            iconImage.sprite = iconSprite;
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

    private void AdvanceStep()
    {
        if (!tutorialIsOpen)
            return;

        currentStepIndex++;
        if (currentStepIndex >= StepTitles.Length)
        {
            FinishTutorial();
            return;
        }

        ShowCurrentStep();
    }

    private void FinishTutorial()
    {
        if (!tutorialIsOpen)
            return;

        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();
        RestoreTutorialState();
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
        CleanupDemoContent();
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
        if (currentTarget == null || highlightRect == null || !TryGetScreenRect(currentTarget, out Rect targetRect))
        {
            if (highlightRect != null)
                highlightRect.gameObject.SetActive(false);
            return;
        }

        highlightRect.gameObject.SetActive(true);
        Vector2 center = targetRect.center;
        highlightRect.anchoredPosition = new Vector2(center.x - Screen.width * 0.5f, center.y - Screen.height * 0.5f);
        highlightRect.sizeDelta = new Vector2(targetRect.width + FramePadding * 2f, targetRect.height + FramePadding * 2f);

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

        if (currentTarget == null || !TryGetScreenRect(currentTarget, out Rect targetRect))
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
            Rect candidateRect = CenteredRect(candidate, panelWidth, panelHeight);
            if (FitsScreen(candidateRect) && !candidateRect.Overlaps(targetRect))
            {
                SetPanelCenter(candidate);
                return;
            }
        }

        Vector2 bestCandidate = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float bestDistance = -1f;
        foreach (Vector2 candidate in GetCornerCandidates(panelWidth, panelHeight))
        {
            Rect candidateRect = CenteredRect(candidate, panelWidth, panelHeight);
            float distance = (candidate - targetCenter).sqrMagnitude;
            if (FitsScreen(candidateRect) && !candidateRect.Overlaps(targetRect) && distance > bestDistance)
            {
                bestCandidate = candidate;
                bestDistance = distance;
            }
        }

        SetPanelCenter(bestCandidate);
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

    private bool FitsScreen(Rect rect)
    {
        return rect.xMin >= ScreenEdgeMargin && rect.xMax <= Screen.width - ScreenEdgeMargin &&
               rect.yMin >= ScreenEdgeMargin && rect.yMax <= Screen.height - ScreenEdgeMargin;
    }

    private static bool TryGetScreenRect(GameObject target, out Rect screenRect)
    {
        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (targetRect == null)
        {
            screenRect = default;
            return false;
        }

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        Vector2 minimum = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 maximum = minimum;
        for (int index = 1; index < corners.Length; index++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corners[index]);
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }

        screenRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
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
