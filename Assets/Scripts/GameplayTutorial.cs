using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows a replayable, skippable introduction to the current gameplay controls.</summary>
public sealed class GameplayTutorial : MonoBehaviour
{
    private const string TutorialSeenKey = "tutorialSeen";
    private const string BankGothicResourcePath = "BankGothicMediumSDF";
    private const string BuiltInSpritePath = "UI/Skin/UISprite.psd";
    private const float TutorialPanelWidth = 1120f;
    private const float TutorialPanelHeight = 700f;

    private static readonly Color OverlayColor = new Color(0.005f, 0.014f, 0.022f, 0.88f);
    private static readonly Color PanelColor = new Color(0.018f, 0.045f, 0.062f, 0.99f);
    private static readonly Color AccentColor = new Color(0.12f, 0.78f, 0.84f, 1f);
    private static readonly Color TextColor = new Color(0.91f, 0.97f, 0.98f, 1f);
    private static readonly Color MutedColor = new Color(0.61f, 0.76f, 0.8f, 1f);
    private static readonly Color WarningColor = new Color(1f, 0.28f, 0.22f, 1f);
    private static readonly Color[] StepAccents =
    {
        new Color(0.12f, 0.78f, 0.84f, 1f),
        new Color(0.12f, 0.78f, 0.84f, 1f),
        new Color(0.12f, 0.78f, 0.84f, 1f),
        new Color(0.12f, 0.78f, 0.84f, 1f),
        new Color(0.12f, 0.78f, 0.84f, 1f),
        WarningColor,
        new Color(0.12f, 0.78f, 0.84f, 1f)
    };

    private static readonly string[] StepTitles =
    {
        "THE 14-DAY RUN",
        "WELLNESS",
        "HUMAN SNAPSHOT & CONSOLE",
        "BODY SYSTEMS",
        "DEPLOYMENT",
        "WARNING: QTE RESPONSES",
        "SPEED, PAUSE & GUIDE"
    };

    private static readonly string[] StepBodies =
    {
        "Your run lasts 14 in-game days. The Day label and clock show the current day and time.",
        "Wellness starts at 100. Finish day 14 at 50 or higher to win; below 50 on day 14 or reaching 0 at any time is a loss.",
        "The Human Snapshot animates the current routine, such as eating, walking, or sleeping. The Console Log timestamps routine changes and random-event messages.",
        "Select a layer to view its controls; each (i) icon explains that body system.\nRESPIRATORY: Use COUGH during its contamination window; if contamination reaches a lung, use Repair;\nDIGESTIVE: Tap EAT to queue a food batch or VOMIT to clear contaminated food and water;\nCIRCULATORY: Choose Head, Left Hand, Right Hand, Left Foot, or Right Foot to order an immune-cell pair;\nLYMPHATIC: Tap B Cell or T Cell to spawn that cell at its configured point.\nA red pulse marks active respiratory or digestive threats and ends on response, target arrival, or respiratory-window expiry.",
        "CIRCULATORY: Tap Head, Left Hand, Right Hand, Left Foot, or Right Foot to send a neutrophil-macrophage pair to that location.\nLYMPHATIC: Tap B Cell or T Cell to spawn that cell at its configured point; click a deployed unit to inspect its name and HP.\nThere is no separate select-a-unit-then-place control in the current deployment UI.",
        "WARNING: The current code applies a 5-point Wellness loss when it processes a recognized hazard event. There is no automatic QTE timeout or missed-QTE penalty implemented; use the available COUGH, VOMIT, or REPAIR response while the threat is active.",
        "The >> button cycles 1x, 2x, 4x, and 6x; an active QTE forces effective speed to 1x. Pause opens Continue, Restart, and Main Menu; the Manual is on the Main Menu, and ? reopens this guide during a run."
    };

    private TMP_FontAsset tutorialFont;
    private GameObject overlayRoot;
    private Image panelImage;
    private Image accentImage;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private TextMeshProUGUI stepLabel;
    private Button backButton;
    private Button nextButton;
    private Button tutorialButton;
    private Button pauseTutorialButton;
    private PauseController pauseController;
    private int currentStep;
    private bool isOpen;

    private void Start()
    {
        tutorialFont = Resources.Load<TMP_FontAsset>(BankGothicResourcePath);
        if (tutorialFont == null)
            Debug.LogWarning("[TUTORIAL] Bank Gothic TMP font was not found; using the default TMP font.");

        pauseController = Object.FindFirstObjectByType<PauseController>();
        CreateReplayButtons();
        if (PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0)
            OpenTutorial();
    }

    /// <summary>Opens the gameplay guide if it is not already open and the run is active.</summary>
    public void OpenTutorial()
    {
        if (isOpen || (WellnessManager.Instance != null && WellnessManager.Instance.HasRunEnded))
            return;

        if (overlayRoot == null)
            BuildOverlay();

        if (overlayRoot == null)
            return;

        isOpen = true;
        currentStep = 0;
        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        if (pauseController == null)
            pauseController = Object.FindFirstObjectByType<PauseController>();

        if (pauseController != null)
            pauseController.SetPaused(true);
        else
            Time.timeScale = 0f;

        RefreshStep();
    }

    private void CreateReplayButtons()
    {
        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
            return;

        RectTransform dayPanel = canvasRect.Find("DayCounterPanel") as RectTransform;
        if (dayPanel != null)
        {
            tutorialButton = CreateButton("RunTutorialButton", canvasRect, "?", new Vector2(58f, 58f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), OpenTutorial,
                AccentColor, 25f);
            RectTransform buttonRect = tutorialButton.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            dayPanel.GetWorldCorners(corners);
            Vector3 panelTopRight = canvasRect.InverseTransformPoint(corners[2]);
            buttonRect.anchoredPosition = new Vector2(panelTopRight.x + 10f + 64f + 8f,
                panelTopRight.y - 3f);
        }

        Transform pausePanel = transform.Find("PausePanel/PauseCard");
        if (pausePanel != null && pausePanel.Find("TutorialReplayButton") == null)
        {
            pauseTutorialButton = CreateButton("TutorialReplayButton", pausePanel, "TUTORIAL",
                new Vector2(150f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                OpenTutorial, AccentColor, 16f);
            RectTransform pauseButtonRect = pauseTutorialButton.GetComponent<RectTransform>();
            pauseButtonRect.anchoredPosition = new Vector2(-18f, -18f);
        }
    }

    private void BuildOverlay()
    {
        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            Debug.LogWarning("[TUTORIAL] A Canvas RectTransform is required.", this);
            return;
        }

        overlayRoot = new GameObject("GameplayTutorialOverlay", typeof(RectTransform), typeof(Image));
        overlayRoot.transform.SetParent(canvasRect, false);
        overlayRoot.transform.SetAsLastSibling();
        SetStretch(overlayRoot.GetComponent<RectTransform>());
        Image overlayImage = overlayRoot.GetComponent<Image>();
        overlayImage.color = OverlayColor;
        overlayImage.raycastTarget = true;

        GameObject panelObject = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        SetCentered(panelRect, Vector2.zero, new Vector2(TutorialPanelWidth, TutorialPanelHeight));
        panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = Resources.GetBuiltinResource<Sprite>(BuiltInSpritePath);
        panelImage.type = Image.Type.Sliced;
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        GameObject accentObject = new GameObject("TutorialAccent", typeof(RectTransform), typeof(Image));
        accentObject.transform.SetParent(panelObject.transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(10f, 0f);
        accentImage = accentObject.GetComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        titleLabel = CreateText("TutorialTitle", panelObject.transform, new Vector2(0f, 240f),
            new Vector2(920f, 64f), 37f, TextAlignmentOptions.Center, AccentColor, FontStyles.Bold);
        stepLabel = CreateText("TutorialStepCounter", panelObject.transform, new Vector2(0f, 185f),
            new Vector2(860f, 36f), 19f, TextAlignmentOptions.Center, MutedColor, FontStyles.Normal);
        bodyLabel = CreateText("TutorialBody", panelObject.transform, new Vector2(0f, -5f),
            new Vector2(920f, 330f), 22f, TextAlignmentOptions.TopLeft, TextColor, FontStyles.Normal);

        backButton = CreateButton("TutorialBackButton", panelObject.transform, "BACK",
            new Vector2(190f, 58f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), GoBack, AccentColor, 18f, new Vector2(-300f, -275f));
        nextButton = CreateButton("TutorialNextButton", panelObject.transform, "NEXT",
            new Vector2(210f, 58f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), GoNext, AccentColor, 18f, new Vector2(0f, -275f));
        CreateButton("TutorialSkipButton", panelObject.transform, "SKIP",
            new Vector2(190f, 58f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), FinishTutorial, MutedColor, 18f, new Vector2(300f, -275f));
        overlayRoot.SetActive(false);
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetCentered(rect, position, size);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = tutorialFont != null ? tutorialFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
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

    private void RefreshStep()
    {
        titleLabel.text = StepTitles[currentStep];
        bodyLabel.text = StepBodies[currentStep];
        stepLabel.text = $"{currentStep + 1}/{StepBodies.Length}";
        Color accent = StepAccents[currentStep];
        titleLabel.color = accent;
        accentImage.color = accent;
        panelImage.color = currentStep == 5
            ? new Color(0.065f, 0.025f, 0.028f, 0.99f)
            : PanelColor;
        backButton.interactable = currentStep > 0;
        TextMeshProUGUI nextLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>();
        nextLabel.text = currentStep == StepBodies.Length - 1 ? "FINISH" : "NEXT";
    }

    private void GoBack()
    {
        if (!isOpen || currentStep <= 0)
            return;

        currentStep--;
        RefreshStep();
    }

    private void GoNext()
    {
        if (!isOpen)
            return;

        if (currentStep >= StepBodies.Length - 1)
        {
            FinishTutorial();
            return;
        }

        currentStep++;
        RefreshStep();
    }

    private void FinishTutorial()
    {
        if (!isOpen)
            return;

        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();
        isOpen = false;
        overlayRoot.SetActive(false);
        if (pauseController == null)
            pauseController = Object.FindFirstObjectByType<PauseController>();

        if (pauseController != null)
            pauseController.SetPaused(false);
        else
            Time.timeScale = 1f;
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
