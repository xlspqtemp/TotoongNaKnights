using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shows a replayable, skippable, interactive guide to gameplay controls and systems.</summary>
public sealed class GameplayTutorial : MonoBehaviour
{
    private enum StepType
    {
        Information,
        CameraMovement,
        LayerSelection,
        LymphaticCommands,
        RespiratoryQte,
        DigestiveCommands,
        SpeedCycle,
        FinaleEvent
    }

    private enum DockPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        BottomCenter
    }

    private const string TutorialSeenKey = "tutorialSeen";
    private const string BankGothicResourcePath = "BankGothicMediumSDF";
    private const string BuiltInSpritePath = "UI/Skin/UISprite.psd";
    private const float TutorialPanelWidth = 420f;
    private const float ActionTimeoutSeconds = 20f;
    private const float CameraMovementThreshold = 0.05f;
    private const float DockMargin = 20f;
    private const string ContactEventName = "Came Into Contact With Someone Who Was Sick";

    private static readonly Color RevealedWorldOverlayColor = new Color(0f, 0f, 0f, 0.38f);
    private static readonly Color PanelColor = new Color(0.018f, 0.045f, 0.062f, 0.99f);
    private static readonly Color AccentColor = new Color(0.12f, 0.78f, 0.84f, 1f);
    private static readonly Color TextColor = new Color(0.91f, 0.97f, 0.98f, 1f);
    private static readonly Color MutedColor = new Color(0.61f, 0.76f, 0.8f, 1f);
    private static readonly Color WarningColor = new Color(1f, 0.28f, 0.22f, 1f);

    private static readonly string[] StepTitles =
    {
        "THE GOAL",
        "MOVE THE CAMERA",
        "SNAPSHOT + CONSOLE",
        "CIRCULATORY",
        "LYMPHATIC",
        "RESPIRATORY + QTE",
        "DIGESTIVE",
        "FAST-FORWARD",
        "WELLNESS RULES",
        "A REAL RANDOM EVENT",
        "READY FOR DAY 1"
    };

    private static readonly string[] StepBodies =
    {
        "Keep Wellness at 50 or higher through the 14-day run to win.",
        "Press W, A, S, or D until the camera moves; arrow keys also pan.",
        "The Snapshot animates routines; the Console logs [ROUTINE] changes and random events that may affect Wellness.",
        "Circulatory moves oxygen/nutrients. Its five orders should deploy neutrophil/macrophage pairs, but the controller is unassigned; no placement or escort exists.",
        "Lymphatic balances fluid and filters pathogens. Select it, then click B Cell and T Cell to spawn each at its configured point.",
        "Respiratory takes in oxygen and expels CO₂. Click COUGH during the dust event; if air reaches a lung, Repair sends T cells/macrophages.",
        "Digestive breaks food down and removes waste. Select it, then use Eat/Vomit; both may be unavailable during sleep, a batch, or cooldown.",
        "Click >> to cycle 1x, 2x, 4x, and 6x; an active QTE forces effective speed to 1x.",
        "After 14 full in-game days, 50 or higher wins; below 50 is game over. Reaching 0 is an immediate game over.",
        "A guaranteed sick-contact event demonstrates a real Wellness change; the measured loss and event name appear here.",
        "Keep Wellness at 50 or higher through Day 14.\nWatch red glows and respond quickly to QTEs.\nPress ? to replay this guide."
    };

    private static readonly StepType[] StepTypes =
    {
        StepType.Information,
        StepType.CameraMovement,
        StepType.Information,
        StepType.LayerSelection,
        StepType.LymphaticCommands,
        StepType.RespiratoryQte,
        StepType.DigestiveCommands,
        StepType.SpeedCycle,
        StepType.Information,
        StepType.FinaleEvent,
        StepType.Information
    };

    private static readonly DockPosition[] StepDocks =
    {
        DockPosition.BottomCenter,
        DockPosition.BottomCenter,
        DockPosition.BottomCenter,
        DockPosition.BottomLeft,
        DockPosition.TopRight,
        DockPosition.TopLeft,
        DockPosition.TopRight,
        DockPosition.BottomLeft,
        DockPosition.BottomCenter,
        DockPosition.BottomCenter,
        DockPosition.BottomCenter
    };

    private static readonly string[] LayerButtonNames =
    {
        "SelectCirculatoryButton",
        "SelectLymphaticButton",
        "SelectRespiratoryButton",
        "SelectDigestiveButton"
    };

    private static readonly string[] LayerLabels = { "Circulatory", "Lymphatic", "Respiratory", "Digestive" };
    private static readonly string[] OrderContainerNames =
    {
        "TacticalOrdersContainer", "LymphaticOrdersContainer", "RespiratoryOrdersContainer", "DigestiveOrdersContainer"
    };
    private static readonly string[] CirculatoryOrderButtonNames =
    {
        "Head Order", "Left Hand Order", "Right Hand Order", "Left Foot Order", "Right Foot Order"
    };
    private static readonly string[] LymphaticCommandButtonNames = { "B Cell Button", "T Cell Button" };
    private static readonly string[] DigestiveCommandButtonNames = { "EatButton", "VomitButton" };
    private static readonly Color[] StepAccents =
    {
        AccentColor, AccentColor, AccentColor, AccentColor, AccentColor, WarningColor,
        AccentColor, AccentColor, WarningColor, WarningColor, AccentColor
    };

    /// <summary>Indicates whether the tutorial is running or is intentionally starting for the selected Play choice.</summary>
    public static bool IsTutorialActive { get; private set; }

    private static bool shouldStartTutorialOnNextRun;

    private readonly bool[] completedSteps = new bool[StepTypes.Length];
    private readonly Dictionary<string, Button> trackedButtons = new Dictionary<string, Button>();
    private readonly Dictionary<GameObject, CanvasGroup> visibilityGroups = new Dictionary<GameObject, CanvasGroup>();
    private readonly Dictionary<Image, Color> orderContainerBackgrounds = new Dictionary<Image, Color>();
    private readonly List<RectTransform> cardControlRects = new List<RectTransform>();

    private TMP_FontAsset tutorialFont;
    private GameObject overlayRoot;
    private Image blockerImage;
    private Image panelImage;
    private Image accentImage;
    private RectTransform targetHighlightRect;
    private RectTransform secondaryHighlightRect;
    private TextMeshProUGUI arrowLabel;
    private TextMeshProUGUI secondaryArrowLabel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private TextMeshProUGUI stepLabel;
    private Button backButton;
    private Button nextButton;
    private Button skipTutorialButton;
    private TutorialInputGate inputGate;
    private CameraScript cameraScript;
    private RectTransform canvasRect;
    private int currentStep;
    private int commandPhase;
    private int speedClickCount;
    private int visitedSpeedModes;
    private float actionElapsed;
    private float qteWellnessBeforeEvent;
    private float qteWellnessAfterEvent;
    private Vector3 cameraPositionAtInput;
    private bool wasdInputStarted;
    private bool actionTimedOut;
    private bool isOpen;
    private bool hasTriggeredQteEvent;
    private bool hasTriggeredFinaleEvent;
    private string finaleEventBody;

    private IEnumerator Start()
    {
        // LayerSelectionHUD and the other HUD Start methods create runtime targets after sceneLoaded.
        yield return null;

        tutorialFont = Resources.Load<TMP_FontAsset>(BankGothicResourcePath);
        if (tutorialFont == null)
            Debug.LogWarning("[TUTORIAL] Bank Gothic TMP font was not found; using the default TMP font.");

        canvasRect = transform as RectTransform;
        cameraScript = Object.FindFirstObjectByType<CameraScript>();
        CreateReplayButtons();
        BindTrackedButtons();

        bool shouldOpen = shouldStartTutorialOnNextRun || PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0;
        shouldStartTutorialOnNextRun = false;
        if (shouldOpen)
        {
            IsTutorialActive = true;
            OpenTutorial();
        }
        else
        {
            IsTutorialActive = false;
        }
    }

    /// <summary>Configures the next gameplay run's guide; choosing Skip records the tutorial preference.</summary>
    public static void PrepareForNextRun(bool showTutorial)
    {
        shouldStartTutorialOnNextRun = showTutorial;
        IsTutorialActive = showTutorial;
        PlayerPrefs.SetInt(TutorialSeenKey, showTutorial ? 0 : 1);
        PlayerPrefs.Save();
    }

    /// <summary>Opens the guide over the current run, unless a run-ending result is already active.</summary>
    public void OpenTutorial()
    {
        if (isOpen)
            return;

        if ((WellnessManager.Instance != null && WellnessManager.Instance.HasRunEnded) ||
            (!shouldStartTutorialOnNextRun && PlayerPrefs.GetInt(TutorialSeenKey, 0) != 0))
        {
            RestartForTutorial();
            return;
        }

        if (overlayRoot == null)
            BuildOverlay();
        if (overlayRoot == null)
            return;

        IsTutorialActive = true;
        HideGameplayHud();
        isOpen = true;
        currentStep = 0;
        PauseController pauseController = Object.FindFirstObjectByType<PauseController>();
        if (pauseController != null)
            pauseController.SetPaused(false);
        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        ShowCurrentStep();
    }

    private void RestartForTutorial()
    {
        shouldStartTutorialOnNextRun = true;
        IsTutorialActive = true;
        PlayerPrefs.SetInt(TutorialSeenKey, 0);
        PlayerPrefs.Save();
        Time.timeScale = 1f;

        PauseController pauseController = Object.FindFirstObjectByType<PauseController>();
        if (pauseController != null)
            pauseController.RestartGame();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void CreateReplayButtons()
    {
        if (canvasRect == null)
            return;

        RectTransform dayPanel = canvasRect.Find("DayCounterPanel") as RectTransform;
        if (dayPanel != null)
        {
            CreateButton("RunTutorialButton", canvasRect, "?", new Vector2(58f, 58f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), OpenTutorial,
                AccentColor, 25f, GetTutorialButtonPosition(dayPanel));
        }

        Transform pausePanel = transform.Find("PausePanel/PauseCard");
        if (pausePanel != null && pausePanel.Find("TutorialReplayButton") == null)
        {
            Button pauseButton = CreateButton("TutorialReplayButton", pausePanel, "TUTORIAL",
                new Vector2(150f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                OpenTutorial, AccentColor, 16f);
            pauseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-18f, -18f);
        }
    }

    private Vector2 GetTutorialButtonPosition(RectTransform dayPanel)
    {
        Vector3[] corners = new Vector3[4];
        dayPanel.GetWorldCorners(corners);
        Vector3 panelTopRight = canvasRect.InverseTransformPoint(corners[2]);
        return new Vector2(panelTopRight.x + 10f + 64f + 8f, panelTopRight.y - 3f);
    }

    private void HideGameplayHud()
    {
        SetVisible(FindTransform("DayCounterPanel"), false);
        SetVisible(FindTransform("HumanSnapshotPanel"), false);
        SetVisible(FindTransform("ConsoleLogPanel"), false);
        SetVisible(FindGameObject("GameProgressBar"), false);
        SetVisible(FindGameObject("WellnessChangeIndicator"), false);
        SetVisible(FindGameObject("FastForwardButton"), false);
        SetVisible(FindGameObject("PauseButton"), false);
        SetVisible(FindGameObject("RunTutorialButton"), false);
        SetVisible(FindGameObject("PausePanel"), false);
        foreach (string containerName in OrderContainerNames)
            SetOrderContainerVisible(containerName, false);

        for (int index = 0; index < LayerLabels.Length; index++)
        {
            Transform row = canvasRect.Find("SystemLayerButtons/LayerButtonRow_" + LayerLabels[index]);
            SetVisible(row, false);
            SetVisible(FindTrackedObject(LayerButtonNames[index]), false);
        }
        foreach (string buttonName in CirculatoryOrderButtonNames)
            SetVisible(FindTrackedObject(buttonName), false);
        foreach (string buttonName in LymphaticCommandButtonNames)
            SetVisible(FindTrackedObject(buttonName), false);
        foreach (string buttonName in DigestiveCommandButtonNames)
            SetVisible(FindTrackedObject(buttonName), false);
        SetVisible(FindTrackedObject("CoughButton"), false);
    }

    private void RestoreGameplayHud()
    {
        foreach (CanvasGroup group in visibilityGroups.Values)
        {
            if (group == null)
                continue;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private void SetVisible(Transform target, bool visible)
    {
        if (target != null)
            SetVisible(target.gameObject, visible);
    }

    private void SetVisible(GameObject target, bool visible)
    {
        if (target == null)
            return;

        if (!visibilityGroups.TryGetValue(target, out CanvasGroup group) || group == null)
        {
            group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.AddComponent<CanvasGroup>();
            visibilityGroups[target] = group;
        }
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void SetOrderContainerVisible(string containerName, bool visible)
    {
        Transform container = canvasRect != null ? canvasRect.Find(containerName) : null;
        if (container == null)
            return;

        SetVisible(container, visible);
        Image background = container.GetComponent<Image>();
        if (background == null)
            return;

        if (!orderContainerBackgrounds.ContainsKey(background))
            orderContainerBackgrounds[background] = background.color;
        Color originalColor = orderContainerBackgrounds[background];
        background.color = visible
            ? new Color(originalColor.r, originalColor.g, originalColor.b, 0.06f)
            : originalColor;
    }

    private Transform FindTransform(string objectName)
    {
        Transform child = canvasRect != null ? canvasRect.Find(objectName) : null;
        return child != null ? child : FindObject(objectName)?.transform;
    }

    private GameObject FindGameObject(string objectName)
    {
        return FindObject(objectName);
    }

    private GameObject FindObject(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found;
    }

    private GameObject FindTrackedObject(string objectName)
    {
        if (trackedButtons.TryGetValue(objectName, out Button button) && button != null)
            return button.gameObject;
        return FindObject(objectName);
    }

    private void BuildOverlay()
    {
        if (canvasRect == null)
        {
            Debug.LogWarning("[TUTORIAL] A Canvas RectTransform is required.", this);
            return;
        }

        overlayRoot = new GameObject("GameplayTutorialOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(canvasRect, false);
        overlayRoot.transform.SetAsLastSibling();
        SetStretch(overlayRoot.GetComponent<RectTransform>());

        GameObject blockerObject = new GameObject("TutorialRaycastBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TutorialInputGate));
        blockerObject.transform.SetParent(overlayRoot.transform, false);
        SetStretch(blockerObject.GetComponent<RectTransform>());
        blockerImage = blockerObject.GetComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        blockerImage.raycastTarget = true;
        inputGate = blockerObject.GetComponent<TutorialInputGate>();

        GameObject panelObject = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(TutorialPanelWidth, 0f);
        panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = Resources.GetBuiltinResource<Sprite>(BuiltInSpritePath);
        panelImage.type = Image.Type.Sliced;
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject accentObject = new GameObject("TutorialAccent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        accentObject.transform.SetParent(panelObject.transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(5f, 0f);
        accentObject.GetComponent<LayoutElement>().ignoreLayout = true;
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        titleLabel = CreateLayoutText("TutorialTitle", panelObject.transform, 18f,
            TextAlignmentOptions.MidlineLeft, AccentColor, FontStyles.Bold, 1, 24f);
        bodyLabel = CreateLayoutText("TutorialBody", panelObject.transform, 14.5f,
            TextAlignmentOptions.TopLeft, TextColor, FontStyles.Normal, 3, -1f);

        GameObject footerObject = new GameObject("TutorialFooter", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        footerObject.transform.SetParent(panelObject.transform, false);
        LayoutElement footerLayoutElement = footerObject.GetComponent<LayoutElement>();
        footerLayoutElement.minHeight = 34f;
        footerLayoutElement.preferredHeight = 34f;
        HorizontalLayoutGroup footerLayout = footerObject.GetComponent<HorizontalLayoutGroup>();
        footerLayout.padding = new RectOffset(0, 0, 0, 0);
        footerLayout.spacing = 4f;
        footerLayout.childAlignment = TextAnchor.MiddleLeft;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = false;
        footerLayout.childForceExpandHeight = false;

        stepLabel = CreateLayoutText("TutorialStepCounter", footerObject.transform, 11f,
            TextAlignmentOptions.MidlineLeft, MutedColor, FontStyles.Normal, 1, 32f);
        LayoutElement stepLayoutElement = stepLabel.GetComponent<LayoutElement>();
        stepLayoutElement.minWidth = 0f;
        stepLayoutElement.flexibleWidth = 1f;
        backButton = CreateButton("TutorialBackButton", footerObject.transform, "BACK",
            new Vector2(53f, 34f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), GoBack, AccentColor, 11f);
        nextButton = CreateButton("TutorialNextButton", footerObject.transform, "NEXT",
            new Vector2(82f, 34f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), GoNext, AccentColor, 10f);
        skipTutorialButton = CreateButton("TutorialSkipButton", footerObject.transform, "SKIP TUTORIAL",
            new Vector2(94f, 34f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), FinishTutorial, MutedColor, 10f);
        SetButtonLayout(backButton, 53f);
        SetButtonLayout(nextButton, 82f);
        SetButtonLayout(skipTutorialButton, 94f);
        cardControlRects.Add(backButton.transform as RectTransform);
        cardControlRects.Add(nextButton.transform as RectTransform);
        cardControlRects.Add(skipTutorialButton.transform as RectTransform);
        inputGate.Configure(cardControlRects, null);

        targetHighlightRect = CreateTargetHighlight("TutorialTargetPulse");
        secondaryHighlightRect = CreateTargetHighlight("TutorialSecondaryTargetPulse");
        arrowLabel = CreateArrow("TutorialTargetArrow");
        secondaryArrowLabel = CreateArrow("TutorialSecondaryTargetArrow");
        targetHighlightRect.gameObject.SetActive(false);
        secondaryHighlightRect.gameObject.SetActive(false);
        arrowLabel.gameObject.SetActive(false);
        secondaryArrowLabel.gameObject.SetActive(false);
        overlayRoot.SetActive(false);
    }

    private RectTransform CreateTargetHighlight(string objectName)
    {
        GameObject highlightObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Outline));
        highlightObject.transform.SetParent(overlayRoot.transform, false);
        Image highlightImage = highlightObject.GetComponent<Image>();
        highlightImage.sprite = Resources.GetBuiltinResource<Sprite>(BuiltInSpritePath);
        highlightImage.type = Image.Type.Sliced;
        highlightImage.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.035f);
        highlightImage.raycastTarget = false;
        Outline highlightOutline = highlightObject.GetComponent<Outline>();
        highlightOutline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.95f);
        highlightOutline.effectDistance = new Vector2(3f, -3f);
        highlightOutline.useGraphicAlpha = false;
        return highlightObject.GetComponent<RectTransform>();
    }

    private TextMeshProUGUI CreateArrow(string objectName)
    {
        TextMeshProUGUI arrow = CreateLayoutText(objectName, overlayRoot.transform, 24f,
            TextAlignmentOptions.Center, AccentColor, FontStyles.Bold, 1, 30f);
        SetCentered(arrow.rectTransform, Vector2.zero, new Vector2(44f, 30f));
        arrow.text = "▼";
        return arrow;
    }

    private TextMeshProUGUI CreateLayoutText(string objectName, Transform parent, float fontSize,
        TextAlignmentOptions alignment, Color color, FontStyles style, int maxVisibleLines, float preferredHeight)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = tutorialFont != null ? tutorialFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = maxVisibleLines == 1 ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        text.maxVisibleLines = maxVisibleLines;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;
        return text;
    }

    private static void SetButtonLayout(Button button, float preferredWidth)
    {
        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = preferredWidth;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.minHeight = 34f;
        layoutElement.preferredHeight = 34f;
    }


    private Button CreateButton(string objectName, Transform parent, string label, Vector2 size,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, UnityEngine.Events.UnityAction onClick,
        Color color, float fontSize, Vector2? anchoredPosition = null)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>(BuiltInSpritePath);
        image.type = Image.Type.Sliced;
        image.color = new Color(0.055f, 0.19f, 0.23f, 1f);
        image.raycastTarget = true;
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(color.r, color.g, color.b, 0.72f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = image.color,
            highlightedColor = color,
            pressedColor = new Color(color.r, color.g, color.b, 0.72f),
            selectedColor = color,
            disabledColor = MutedColor,
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        button.onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        SetStretch(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI buttonLabel = labelObject.GetComponent<TextMeshProUGUI>();
        buttonLabel.font = tutorialFont != null ? tutorialFont : TMP_Settings.defaultFontAsset;
        buttonLabel.text = label;
        buttonLabel.fontSize = fontSize;
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.color = TextColor;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.textWrappingMode = TextWrappingModes.NoWrap;
        buttonLabel.raycastTarget = false;
        return button;
    }

    private void BindTrackedButtons()
    {
        for (int index = 0; index < LayerButtonNames.Length; index++)
        {
            string buttonName = LayerButtonNames[index];
            Transform layerButton = canvasRect.Find("SystemLayerButtons/LayerButtonRow_" + LayerLabels[index] + "/" + buttonName);
            RegisterTrackedButton(buttonName, layerButton != null ? layerButton.GetComponent<Button>() : null);
        }

        for (int index = 0; index < CirculatoryOrderButtonNames.Length; index++)
            RegisterTrackedButton(CirculatoryOrderButtonNames[index], FindButton(CirculatoryOrderButtonNames[index]));
        for (int index = 0; index < LymphaticCommandButtonNames.Length; index++)
            RegisterTrackedButton(LymphaticCommandButtonNames[index], FindButton(LymphaticCommandButtonNames[index]));
        for (int index = 0; index < DigestiveCommandButtonNames.Length; index++)
            RegisterTrackedButton(DigestiveCommandButtonNames[index], FindButton(DigestiveCommandButtonNames[index]));
        RegisterTrackedButton("CoughButton", FindButton("CoughButton"));
        RegisterTrackedButton("FastForwardButton", FindButton("FastForwardButton"));
    }

    private Button FindButton(string objectName)
    {
        GameObject found = FindObject(objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private void RegisterTrackedButton(string objectName, Button button)
    {
        if (button == null)
            return;

        trackedButtons[objectName] = button;
        button.onClick.AddListener(() => HandleTrackedButtonClick(objectName));
    }

    private void ShowCurrentStep()
    {
        if (!isOpen || overlayRoot == null)
            return;

        StepType stepType = StepTypes[currentStep];
        bool actionStep = IsActionStep(stepType);
        blockerImage.color = actionStep ? new Color(0f, 0f, 0f, 0f) : RevealedWorldOverlayColor;
        blockerImage.raycastTarget = actionStep;
        panelImage.color = stepType == StepType.RespiratoryQte
            ? new Color(0.065f, 0.025f, 0.028f, 0.99f)
            : PanelColor;
        titleLabel.text = StepTitles[currentStep];
        titleLabel.color = StepAccents[currentStep];
        bodyLabel.text = stepType == StepType.FinaleEvent && hasTriggeredFinaleEvent ? finaleEventBody : StepBodies[currentStep];
        stepLabel.text = $"{currentStep + 1} / {StepBodies.Length}";
        stepLabel.color = MutedColor;
        backButton.interactable = currentStep > 0;
        DockPanelForCurrentStep();
        ClearTargetHighlight();
        RevealStepElements();

        actionElapsed = 0f;
        actionTimedOut = false;
        commandPhase = 0;
        speedClickCount = 0;
        visitedSpeedModes = 1 << GameplaySpeed.SelectedModeIndex;
        Time.timeScale = stepType == StepType.CameraMovement ? 1f : 0f;

        if (actionStep && !completedSteps[currentStep])
        {
            nextButton.gameObject.SetActive(false);
            InitializeActionStep(stepType);
        }
        else
        {
            nextButton.gameObject.SetActive(true);
            UpdateNextButtonLabel();
            if (actionStep)
                UpdateActionPrompt("Complete. Select Next to continue.");
        }

        if (stepType == StepType.FinaleEvent && !hasTriggeredFinaleEvent)
            TriggerFinaleEvent();

        UpdateTargetHighlight();
        UpdateInputGate();
        DockPanelForCurrentStep();
    }

    private void DockPanelForCurrentStep()
    {
        RectTransform panelRect = panelImage.rectTransform;
        DockPosition preferredDock = StepDocks[currentStep];
        DockPosition[] candidates =
        {
            preferredDock,
            DockPosition.TopLeft,
            DockPosition.TopRight,
            DockPosition.BottomLeft,
            DockPosition.BottomRight,
            DockPosition.BottomCenter
        };

        for (int index = 0; index < candidates.Length; index++)
        {
            SetDockPosition(panelRect, candidates[index]);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            if (!CardOverlapsHighlightedTargets(panelRect))
                return;
        }
    }

    private void SetDockPosition(RectTransform panelRect, DockPosition dock)
    {
        switch (dock)
        {
            case DockPosition.TopLeft:
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = new Vector2(DockMargin, -DockMargin);
                break;
            case DockPosition.TopRight:
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(1f, 1f);
                panelRect.anchoredPosition = new Vector2(-DockMargin, -DockMargin);
                break;
            case DockPosition.BottomLeft:
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.zero;
                panelRect.pivot = Vector2.zero;
                panelRect.anchoredPosition = new Vector2(DockMargin, DockMargin);
                break;
            case DockPosition.BottomRight:
                panelRect.anchorMin = new Vector2(1f, 0f);
                panelRect.anchorMax = new Vector2(1f, 0f);
                panelRect.pivot = new Vector2(1f, 0f);
                panelRect.anchoredPosition = new Vector2(-DockMargin, DockMargin);
                break;
            case DockPosition.BottomCenter:
                panelRect.anchorMin = new Vector2(0.5f, 0f);
                panelRect.anchorMax = new Vector2(0.5f, 0f);
                panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.anchoredPosition = new Vector2(0f, DockMargin);
                break;
        }
    }

    private bool CardOverlapsHighlightedTargets(RectTransform panelRect)
    {
        RectTransform[] targets = GetDockTargets();
        if (targets.Length == 0)
            return false;

        Rect cardBounds = GetCanvasLocalBounds(panelRect);
        foreach (RectTransform target in targets)
        {
            if (target != null && target.gameObject.activeInHierarchy && cardBounds.Overlaps(GetCanvasLocalBounds(target)))
                return true;
        }
        return false;
    }

    private RectTransform[] GetDockTargets()
    {
        if (IsActionStep(StepTypes[currentStep]))
        {
            Button targetButton = GetCurrentTargetButton();
            return targetButton != null ? new[] { targetButton.transform as RectTransform } : new RectTransform[0];
        }

        if (currentStep == 2)
        {
            return new[]
            {
                FindTransform("HumanSnapshotPanel") as RectTransform,
                FindTransform("ConsoleLogPanel") as RectTransform
            };
        }

        if (currentStep == 10)
        {
            GameObject replayButton = FindGameObject("RunTutorialButton");
            return replayButton != null ? new[] { replayButton.transform as RectTransform } : new RectTransform[0];
        }

        if (currentStep == 8 || currentStep == 9)
        {
            GameObject wellnessBar = FindGameObject("GameProgressBar");
            return wellnessBar != null ? new[] { wellnessBar.transform as RectTransform } : new RectTransform[0];
        }

        return new RectTransform[0];
    }

    private Rect GetCanvasLocalBounds(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector3 bottomLeft = canvasRect.InverseTransformPoint(corners[0]);
        Vector3 topRight = canvasRect.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
    }

    private void RevealStepElements()
    {
        foreach (string containerName in OrderContainerNames)
            SetOrderContainerVisible(containerName, false);
        for (int index = 0; index < LayerButtonNames.Length; index++)
        {
            Transform row = canvasRect.Find("SystemLayerButtons/LayerButtonRow_" + LayerLabels[index]);
            SetVisible(row, false);
            SetVisible(FindTrackedObject(LayerButtonNames[index]), false);
        }
        foreach (string buttonName in LymphaticCommandButtonNames)
            SetVisible(FindTrackedObject(buttonName), false);
        foreach (string buttonName in DigestiveCommandButtonNames)
            SetVisible(FindTrackedObject(buttonName), false);
        SetVisible(FindTrackedObject("CoughButton"), false);
        SetVisible(FindTrackedObject("FastForwardButton"), false);

        switch (currentStep)
        {
            case 2:
                SetVisible(FindTransform("HumanSnapshotPanel"), true);
                SetVisible(FindTransform("ConsoleLogPanel"), true);
                break;
            case 3:
                RevealLayerButton(0);
                SetOrderContainerVisible("TacticalOrdersContainer", true);
                foreach (string buttonName in CirculatoryOrderButtonNames)
                    RevealButton(buttonName);
                break;
            case 4:
                RevealLayerButton(1);
                SetOrderContainerVisible("LymphaticOrdersContainer", true);
                break;
            case 5:
                RevealLayerButton(2);
                SetOrderContainerVisible("RespiratoryOrdersContainer", true);
                RevealButton("CoughButton");
                break;
            case 6:
                RevealLayerButton(3);
                SetOrderContainerVisible("DigestiveOrdersContainer", true);
                break;
            case 7:
                SetVisible(FindTransform("DayCounterPanel"), true);
                SetVisible(FindGameObject("PauseButton"), true);
                RevealButton("FastForwardButton");
                break;
            case 8:
            case 9:
                SetVisible(FindGameObject("GameProgressBar"), true);
                if (currentStep == 9)
                    SetVisible(FindGameObject("WellnessChangeIndicator"), true);
                break;
            case 10:
                SetVisible(FindGameObject("RunTutorialButton"), true);
                break;
        }
    }

    private void RevealLayerButton(int index)
    {
        if (index < 0 || index >= LayerButtonNames.Length)
            return;
        Transform row = canvasRect.Find("SystemLayerButtons/LayerButtonRow_" + LayerLabels[index]);
        SetVisible(row, true);
        RevealButton(LayerButtonNames[index]);
    }

    private void RevealButton(string objectName)
    {
        SetVisible(FindTrackedObject(objectName), true);
    }

    private void InitializeActionStep(StepType stepType)
    {
        wasdInputStarted = false;
        if (stepType == StepType.CameraMovement && cameraScript != null)
            cameraPositionAtInput = cameraScript.transform.position;
        else if (stepType == StepType.RespiratoryQte)
        {
            commandPhase = 1;
            if (cameraScript != null)
                cameraScript.SelectLayer(4);
            SetOrderContainerVisible("RespiratoryOrdersContainer", true);
            RevealButton("CoughButton");
        }
        UpdateActionPrompt();
        if (stepType == StepType.RespiratoryQte)
            TriggerRespiratoryTutorialEvent();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        UpdateTargetHighlight();
        UpdateInputGate();
        StepType stepType = StepTypes[currentStep];
        if (!IsActionStep(stepType) || completedSteps[currentStep] || actionTimedOut)
            return;

        actionElapsed += Time.unscaledDeltaTime;
        if (stepType == StepType.CameraMovement && cameraScript != null)
        {
            bool isWasdPressed = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                                 Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
            if (isWasdPressed)
                wasdInputStarted = true;

            if (wasdInputStarted && Vector3.Distance(cameraPositionAtInput, cameraScript.transform.position) >= CameraMovementThreshold)
                CompleteCurrentAction();
        }

        if (actionElapsed >= ActionTimeoutSeconds)
        {
            actionTimedOut = true;
            nextButton.gameObject.SetActive(true);
            UpdateNextButtonLabel();
            UpdateActionPrompt("Time is up. Skip this step to continue, or keep trying.");
        }
    }

    private void HandleTrackedButtonClick(string buttonName)
    {
        if (!isOpen || completedSteps[currentStep])
            return;

        switch (StepTypes[currentStep])
        {
            case StepType.LayerSelection:
                if (buttonName == "SelectCirculatoryButton")
                {
                    foreach (string containerName in OrderContainerNames)
                        SetOrderContainerVisible(containerName, false);
                    CompleteCurrentAction();
                }
                break;

            case StepType.LymphaticCommands:
                if (commandPhase == 0 && buttonName == "SelectLymphaticButton")
                {
                    commandPhase = 1;
                    SetOrderContainerVisible("LymphaticOrdersContainer", true);
                    RevealButton(LymphaticCommandButtonNames[0]);
                    UpdateActionPrompt();
                }
                else if (commandPhase == 1 && buttonName == LymphaticCommandButtonNames[0])
                {
                    commandPhase = 2;
                    RevealButton(LymphaticCommandButtonNames[1]);
                    UpdateActionPrompt();
                }
                else if (commandPhase == 2 && buttonName == LymphaticCommandButtonNames[1])
                    CompleteCurrentAction();
                break;
            case StepType.DigestiveCommands:
                if (commandPhase == 0 && buttonName == "SelectDigestiveButton")
                {
                    commandPhase = 1;
                    SetOrderContainerVisible("DigestiveOrdersContainer", true);
                    RevealButton(DigestiveCommandButtonNames[0]);
                    UpdateActionPrompt();
                }
                else if (commandPhase == 1 && buttonName == DigestiveCommandButtonNames[0])
                {
                    commandPhase = 2;
                    RevealButton(DigestiveCommandButtonNames[1]);
                    UpdateActionPrompt();
                }
                else if (commandPhase == 2 && buttonName == DigestiveCommandButtonNames[1])
                    CompleteCurrentAction();
                break;
            case StepType.RespiratoryQte:
                if (commandPhase == 1 && buttonName == "CoughButton")
                {
                    ExplainQteResult();
                    CompleteCurrentAction();
                }
                break;
            case StepType.SpeedCycle:
                if (buttonName == "FastForwardButton")
                {
                    speedClickCount++;
                    visitedSpeedModes |= 1 << GameplaySpeed.SelectedModeIndex;
                    UpdateActionPrompt();
                    if (speedClickCount >= 4 && visitedSpeedModes == 0b1111)
                        CompleteCurrentAction();
                }
                break;
        }

        UpdateTargetHighlight();
        UpdateInputGate();
    }

    private void TriggerRespiratoryTutorialEvent()
    {
        if (hasTriggeredQteEvent)
            return;
        hasTriggeredQteEvent = true;
        WellnessManager wellnessManager = WellnessManager.Instance;
        qteWellnessBeforeEvent = wellnessManager != null ? wellnessManager.CurrentWellness : 0f;
        bool triggered = RandomEventSystem.Instance != null &&
                         RandomEventSystem.Instance.TriggerTutorialEvent("InhaledDustOrAllergen");
        qteWellnessAfterEvent = wellnessManager != null ? wellnessManager.CurrentWellness : qteWellnessBeforeEvent;
        if (!triggered)
            bodyLabel.text = "The scripted dust/allergen event could not be triggered. Use SKIP THIS STEP after the 20-second action timeout.";
    }

    private void ExplainQteResult()
    {
        WellnessManager wellnessManager = WellnessManager.Instance;
        float wellnessAfterResponse = wellnessManager != null ? wellnessManager.CurrentWellness : qteWellnessAfterEvent;
        float eventChange = qteWellnessAfterEvent - qteWellnessBeforeEvent;
        float responseChange = wellnessAfterResponse - qteWellnessAfterEvent;
        bodyLabel.text = $"The scripted event changed Wellness by {eventChange:+0.##;-0.##;0}; COUGH cleared active air before it reached a lung and changed Wellness by {responseChange:+0.##;-0.##;0}. Select Next to continue.";
    }

    private void TriggerFinaleEvent()
    {
        hasTriggeredFinaleEvent = true;
        WellnessManager wellnessManager = WellnessManager.Instance;
        float wellnessBefore = wellnessManager != null ? wellnessManager.CurrentWellness : 0f;
        if (RandomEventSystem.Instance != null)
            RandomEventSystem.Instance.TriggerTutorialEvent("ContactWithSickPerson");
        float wellnessAfter = wellnessManager != null ? wellnessManager.CurrentWellness : wellnessBefore;
        float wellnessDropped = Mathf.Max(0f, wellnessBefore - wellnessAfter);
        finaleEventBody = $"Wellness dropped by {wellnessDropped:0.##} because {ContactEventName}";
        bodyLabel.text = finaleEventBody;
    }

    private void CompleteCurrentAction()
    {
        if (completedSteps[currentStep])
            return;
        completedSteps[currentStep] = true;
        Time.timeScale = 0f;
        nextButton.gameObject.SetActive(true);
        UpdateNextButtonLabel();
        ClearTargetHighlight();
        if (StepTypes[currentStep] != StepType.RespiratoryQte)
            UpdateActionPrompt("Complete. Select Next to continue.");
        UpdateInputGate();
    }

    private void UpdateActionPrompt(string overridePrompt = null)
    {
        string prompt = overridePrompt;
        if (prompt == null)
        {
            switch (StepTypes[currentStep])
            {
                case StepType.CameraMovement:
                    prompt = "Press W, A, S, or D until the camera moves; arrow keys also pan.";
                    break;
                case StepType.LayerSelection:
                    prompt = "Circulatory moves oxygen/nutrients. Its five orders should deploy neutrophil/macrophage pairs, but the controller is unassigned; no placement or escort exists.";
                    break;
                case StepType.LymphaticCommands:
                    prompt = commandPhase == 0 ? "Lymphatic balances fluid and filters pathogens. Select it, then use B Cell and T Cell."
                        : commandPhase == 1 ? "Click B Cell to spawn one at its configured point."
                        : "Click T Cell to spawn one at its configured point.";
                    break;
                case StepType.DigestiveCommands:
                    prompt = commandPhase == 0 ? "Digestive breaks food down and removes waste. Select it, then use Eat and Vomit."
                        : commandPhase == 1 ? "Click Eat to queue food if available."
                        : "Click Vomit to clear contaminated items or respond to eligible events.";
                    break;
                case StepType.RespiratoryQte:
                    prompt = "Respiratory takes in oxygen and expels CO₂. Dust changes Wellness; click COUGH before lung arrival, or Repair sends T cells/macrophages.";
                    break;
                case StepType.SpeedCycle:
                    prompt = $"Click >> through 1x, 2x, 4x, 6x; active QTEs force 1x. {Mathf.Max(0, 4 - speedClickCount)} click(s) remain.";
                    break;
            }
        }
        if (!string.IsNullOrEmpty(prompt))
        {
            bodyLabel.text = prompt;
            DockPanelForCurrentStep();
        }
    }

    private void UpdateNextButtonLabel()
    {
        if (nextButton == null)
            return;
        TextMeshProUGUI label = nextButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null)
            return;
        if (actionTimedOut && !completedSteps[currentStep])
        {
            label.text = "SKIP THIS STEP";
            label.fontSize = 8.5f;
        }
        else
        {
            label.text = currentStep == StepBodies.Length - 1 ? "FINISH" : "NEXT";
            label.fontSize = 11f;
        }
    }

    private void UpdateTargetHighlight()
    {
        if (!isOpen || targetHighlightRect == null)
        {
            HideTargetHighlight(targetHighlightRect, arrowLabel);
            HideTargetHighlight(secondaryHighlightRect, secondaryArrowLabel);
            return;
        }

        bool actionStep = IsActionStep(StepTypes[currentStep]);
        if (actionStep && completedSteps[currentStep])
        {
            HideTargetHighlight(targetHighlightRect, arrowLabel);
            HideTargetHighlight(secondaryHighlightRect, secondaryArrowLabel);
            return;
        }

        RectTransform primaryTarget;
        if (actionStep)
        {
            Button targetButton = GetCurrentTargetButton();
            primaryTarget = targetButton != null ? targetButton.transform as RectTransform : null;
        }
        else
        {
            primaryTarget = GetCurrentInformationTarget();
        }

        UpdateSingleTargetHighlight(targetHighlightRect, arrowLabel, primaryTarget, StepAccents[currentStep]);
        RectTransform secondaryTarget = currentStep == 2
            ? FindTransform("ConsoleLogPanel") as RectTransform
            : null;
        UpdateSingleTargetHighlight(secondaryHighlightRect, secondaryArrowLabel, secondaryTarget, StepAccents[currentStep]);
    }

    private void UpdateSingleTargetHighlight(RectTransform highlightRect, TextMeshProUGUI arrow,
        RectTransform target, Color accent)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            HideTargetHighlight(highlightRect, arrow);
            return;
        }

        Rect targetBounds = GetCanvasLocalBounds(target);
        highlightRect.anchoredPosition = targetBounds.center;
        highlightRect.sizeDelta = new Vector2(targetBounds.width + 12f, targetBounds.height + 12f);
        float pulse = 1f + 0.045f * Mathf.Sin(Time.unscaledTime * 4.5f);
        highlightRect.localScale = Vector3.one * pulse;
        Outline outline = highlightRect.GetComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.95f);
        highlightRect.gameObject.SetActive(true);

        bool targetAboveCenter = targetBounds.center.y > 0f;
        arrow.text = targetAboveCenter ? "▲" : "▼";
        arrow.color = accent;
        arrow.rectTransform.anchoredPosition = new Vector2(targetBounds.center.x,
            targetAboveCenter ? targetBounds.yMin - 17f : targetBounds.yMax + 17f);
        arrow.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4.5f));
        arrow.gameObject.SetActive(true);
    }

    private static void HideTargetHighlight(RectTransform highlightRect, TextMeshProUGUI arrow)
    {
        if (highlightRect != null)
            highlightRect.gameObject.SetActive(false);
        if (arrow != null)
            arrow.gameObject.SetActive(false);
    }

    private RectTransform GetCurrentInformationTarget()
    {
        Transform target = null;
        if (currentStep == 2)
            target = FindTransform("HumanSnapshotPanel");
        else if (currentStep == 10)
            target = FindGameObject("RunTutorialButton")?.transform;
        else if (currentStep == 8 || currentStep == 9)
        {
            GameObject wellnessBar = FindGameObject("GameProgressBar");
            target = wellnessBar != null ? wellnessBar.transform : null;
        }
        return target as RectTransform;
    }

    private Button GetCurrentTargetButton()
    {
        switch (StepTypes[currentStep])
        {
            case StepType.CameraMovement:
                return null;
            case StepType.LayerSelection:
                return GetTrackedButton(LayerButtonNames[currentStep - 3]);
            case StepType.LymphaticCommands:
                if (commandPhase == 0) return GetTrackedButton("SelectLymphaticButton");
                return GetTrackedButton(LymphaticCommandButtonNames[commandPhase - 1]);
            case StepType.DigestiveCommands:
                if (commandPhase == 0) return GetTrackedButton("SelectDigestiveButton");
                return GetTrackedButton(DigestiveCommandButtonNames[commandPhase - 1]);
            case StepType.RespiratoryQte:
                return GetTrackedButton("CoughButton");
            case StepType.SpeedCycle:
                return GetTrackedButton("FastForwardButton");
            default:
                return null;
        }
    }

    private Button GetTrackedButton(string objectName)
    {
        return trackedButtons.TryGetValue(objectName, out Button button) ? button : null;
    }

    private void UpdateInputGate()
    {
        if (inputGate == null)
            return;
        RectTransform target = null;
        if (isOpen && IsActionStep(StepTypes[currentStep]) && !completedSteps[currentStep])
        {
            Button targetButton = GetCurrentTargetButton();
            target = targetButton != null ? targetButton.transform as RectTransform : null;
        }
        inputGate.Configure(cardControlRects, target);
    }

    private void ClearTargetHighlight()
    {
        HideTargetHighlight(targetHighlightRect, arrowLabel);
        HideTargetHighlight(secondaryHighlightRect, secondaryArrowLabel);
    }

    private static bool IsActionStep(StepType stepType)
    {
        return stepType == StepType.CameraMovement || stepType == StepType.LayerSelection ||
               stepType == StepType.LymphaticCommands || stepType == StepType.DigestiveCommands ||
               stepType == StepType.RespiratoryQte || stepType == StepType.SpeedCycle;
    }

    private void GoBack()
    {
        if (!isOpen || currentStep <= 0)
            return;
        currentStep--;
        ShowCurrentStep();
    }

    private void GoNext()
    {
        if (!isOpen)
            return;
        if (IsActionStep(StepTypes[currentStep]) && !completedSteps[currentStep] && !actionTimedOut)
            return;
        if (currentStep == StepBodies.Length - 1)
        {
            FinishTutorial();
            return;
        }
        currentStep++;
        ShowCurrentStep();
    }

    private void FinishTutorial()
    {
        if (!isOpen)
            return;
        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();
        IsTutorialActive = false;
        isOpen = false;
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
        Time.timeScale = 1f;
        PauseController pauseController = Object.FindFirstObjectByType<PauseController>();
        if (pauseController != null)
            pauseController.RestartGame();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            IsTutorialActive = false;
            Time.timeScale = 1f;
        }
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
}
