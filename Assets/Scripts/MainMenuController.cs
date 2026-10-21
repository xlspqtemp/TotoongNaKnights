using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Controls the main menu and difficulty selection flow.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string GameSceneName = "Game";
    private const string MainMenuSceneName = "MainMenu";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const string BankGothicFontResourceName = "BankGothicMediumSDF";
    private const int OverlayWidth = 920;
    private const int OverlayHeight = 720;
    private const int SubtitleFontSize = 22;
    private const int ButtonFontSize = 22;
    private const int DifficultyTitleFontSize = 38;
    private const int RowTitleFontSize = 22;
    private const int ValueFontSize = 18;
    private const int ButtonWidth = 250;
    private const int ButtonHeight = 64;
    private const int DifficultyOptionWidth = 190;
    private const int DifficultyOptionHeight = 112;
    private const int DifficultyInfoPanelWidth = 760;
    private const int DifficultyInfoPanelHeight = 196;
    private const int DifficultyInfoPanelY = -138;
    private const int DifficultyInfoHeaderFontSize = 19;
    private const int DifficultyInfoBodyFontSize = 18;
    private const int RowHeight = 112;
    private const int SliderWidth = 480;
    private const int SliderHeight = 30;
    private const int ControlButtonSize = 42;
    private const int RowLabelWidth = 330;
    private const int LevelValueWidth = 54;
    private const int StartButtonY = -290;

    private static readonly Color OverlayColor = new Color(0.012f, 0.022f, 0.032f, 0.82f);
    private static readonly Color PanelColor = new Color(0.035f, 0.072f, 0.09f, 0.98f);
    private static readonly Color RowColor = new Color(0.055f, 0.105f, 0.125f, 1f);
    private static readonly Color SelectedDifficultyColor = new Color(0.075f, 0.37f, 0.39f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color ButtonColor = new Color(0.075f, 0.37f, 0.39f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.77f, 0.79f, 1f);
    private static readonly string[] DifficultyDescriptions =
    {
        "Infections per day: 2\nMostly Mild/Moderate pathogens.\nNamed/Rare threats are extremely rare.\nPerfect for learning the core loop.",
        "Infections per day: 3\nBalanced mix of Generic and Named pathogens.\nExpect moderate threats and occasional Severe infections.",
        "Infections per day: 5\nFrequent infections with higher chances of Named pathogens.\nBarrier management is critical.",
        "Infections per day: 5\nLethal mix of Rare and Very Rare pathogens.\nSevere infections (Dengue, Mucormycosis) are common.\nOnly for those who master the immune system."
    };


    private Canvas menuCanvas;
    private GameObject overlay;
    private GameObject difficultyPanel;
    private Button[] difficultyOptionButtons;
    private Image[] difficultyOptionBackgrounds;
    private Outline[] difficultyOptionOutlines;
    private TextMeshProUGUI descriptionText;
    private ScrollRect difficultyInfoScrollRect;
    private TMP_FontAsset difficultyInfoFont;
    [SerializeField] private bool showManualButton = false;
    [SerializeField] private DifficultyStats[] difficultyStats = DifficultySettings.CreateDefaultStats();
    private int selectedDifficultyIndex = 1;

    private void Start()
    {
        DifficultySettings.ConfigureStats(difficultyStats);
        selectedDifficultyIndex = DifficultySettings.SelectedDifficultyIndex;
        if (menuCanvas == null)
        {
            menuCanvas = FindFirstObjectByType<Canvas>();
        }

        if (menuCanvas != null)
        {
            ManualScreen.AddMenuEntry(menuCanvas.transform, ShowManual);
            SetManualButtonVisibility();
        }
    }

    private void SetManualButtonVisibility()
    {
        Transform manualButton = menuCanvas != null ? menuCanvas.transform.Find("ManualEntryButton") : null;
        if (manualButton == null)
        {
            return;
        }

        CanvasGroup canvasGroup = manualButton.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = manualButton.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = showManualButton ? 1f : 0f;
        canvasGroup.interactable = showManualButton;
        canvasGroup.blocksRaycasts = showManualButton;
    }

    /// <summary>
    /// Opens the Manual overlay over the main menu.
    /// </summary>
    public void ShowManual()
    {
        if (menuCanvas == null)
        {
            menuCanvas = FindFirstObjectByType<Canvas>();
        }

        if (menuCanvas != null)
        {
            ManualScreen.Show(menuCanvas.transform);
        }
    }

    /// <summary>
    /// Opens the difficulty selection screen when the Play button is selected.
    /// </summary>
    public void PlayGame()
    {
        if (menuCanvas == null)
        {
            menuCanvas = FindFirstObjectByType<Canvas>();
        }

        if (menuCanvas == null)
        {
            SceneManager.LoadScene(GameSceneName);
            return;
        }

        if (overlay == null)
        {
            CreateDialogFlow();
        }

        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        difficultyPanel.SetActive(true);
    }

    /// <summary>
    /// Opens the static placeholder leaderboards panel over the main menu.
    /// </summary>
    public void ShowLeaderboards()
    {
        if (menuCanvas == null)
        {
            menuCanvas = FindFirstObjectByType<Canvas>();
        }

        if (menuCanvas != null)
        {
            LeaderboardsScreen.Show(menuCanvas.transform);
        }
    }

    /// <summary>
    /// Exits the application from the main menu.
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
    }

    private void CreateDialogFlow()
    {
        overlay = new GameObject("DemoAndDifficultyOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(menuCanvas.transform, false);
        overlay.transform.SetAsLastSibling();
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        SetRect(overlayRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = OverlayColor;
        overlayImage.raycastTarget = true;

        difficultyPanel = CreatePanel(overlay.transform, "DifficultySelection", OverlayWidth, OverlayHeight);
        CreateText(difficultyPanel.transform, "DifficultyTitle", "Difficulty Selection", DifficultyTitleFontSize,
            FontStyle.Bold, TextColor, TextAnchor.MiddleCenter, new Vector2(0, 292), new Vector2(820, 64));
        CreateText(difficultyPanel.transform, "DifficultySubtitle", "Choose a difficulty preset.", SubtitleFontSize,
            FontStyle.Normal, MutedTextColor, TextAnchor.MiddleCenter, new Vector2(0, 232), new Vector2(800, 42));

        CreateDifficultySelector(difficultyPanel.transform);

        CreateButton(difficultyPanel.transform, "StartGameButton", "Start Game", ButtonWidth, ButtonHeight,
            new Vector2(0, StartButtonY), StartGame);
        CreateButton(difficultyPanel.transform, "DifficultyBackButton", "Back", ButtonWidth, ButtonHeight,
            new Vector2(-300, StartButtonY), ReturnToMainMenu);
        difficultyPanel.SetActive(false);
    }

    private GameObject CreatePanel(Transform parent, string objectName, int width, int height)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetRect(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;
        return panel;
    }
    private void CreateDifficultySelector(Transform parent)
    {
        string[] optionLabels = { "Easy", "Normal", "Medium", "Hard" };
        difficultyOptionButtons = new Button[optionLabels.Length];
        difficultyOptionBackgrounds = new Image[optionLabels.Length];
        difficultyOptionOutlines = new Outline[optionLabels.Length];
        const int horizontalSpacing = 18;
        int totalWidth = optionLabels.Length * DifficultyOptionWidth + (optionLabels.Length - 1) * horizontalSpacing;
        int leftEdge = -totalWidth / 2 + DifficultyOptionWidth / 2;

        for (int index = 0; index < optionLabels.Length; index++)
        {
            int optionIndex = index;
            Vector2 position = new Vector2(leftEdge + index * (DifficultyOptionWidth + horizontalSpacing), 54f);
            Button button = CreateButton(parent, "DifficultyOption" + optionLabels[index] + "Button",
                optionLabels[index], DifficultyOptionWidth, DifficultyOptionHeight, position,
                () => SetSelectedDifficulty(optionIndex));
            HideDifficultyOptionExtraText(button);
            Outline outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;

            difficultyOptionButtons[index] = button;
            difficultyOptionBackgrounds[index] = button.GetComponent<Image>();
            difficultyOptionOutlines[index] = outline;
        }

        CreateDifficultyInfoPanel(parent);
        SetSelectedDifficulty(selectedDifficultyIndex);
    }

    private static void HideDifficultyOptionExtraText(Button button)
    {
        if (button == null)
            return;

        Text[] labels = button.GetComponentsInChildren<Text>(true);
        foreach (Text label in labels)
        {
            if (label != null && label.gameObject.name != "Label")
                label.enabled = false;
        }

        TMP_Text[] additionalLabels = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in additionalLabels)
        {
            if (label != null && label.gameObject.name != "Label")
                label.enabled = false;
        }
    }

    private void CreateDifficultyInfoPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "SelectedDifficultyInfoPanel",
            DifficultyInfoPanelWidth, DifficultyInfoPanelHeight);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchoredPosition = new Vector2(0f, DifficultyInfoPanelY);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        difficultyInfoFont = Resources.Load<TMP_FontAsset>(BankGothicFontResourceName);
        if (difficultyInfoFont == null)
            difficultyInfoFont = TMP_Settings.defaultFontAsset;

        CreateDifficultyInfoLabel(panel.transform, "DifficultyInfoHeader", "SELECTED DIFFICULTY",
            DifficultyInfoHeaderFontSize, AccentColor, TextAlignmentOptions.Center,
            new Vector2(0f, 70f), new Vector2(DifficultyInfoPanelWidth - 48f, 30f));
        CreateDifficultyInfoScrollView(panel.transform);
    }

    private void CreateDifficultyInfoScrollView(Transform parent)
    {
        GameObject scrollViewObject = new GameObject("DifficultyInfoScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollViewObject.transform.SetParent(parent, false);
        RectTransform scrollViewRect = scrollViewObject.GetComponent<RectTransform>();
        SetRect(scrollViewRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -12f),
            new Vector2(DifficultyInfoPanelWidth - 72f, 126f));

        GameObject viewportObject = new GameObject("DifficultyInfoViewport", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(scrollViewObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetRect(viewportRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-18f, 0f));
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("DifficultyInfoStats", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        SetRect(contentRect, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero);
        TextMeshProUGUI contentText = contentObject.GetComponent<TextMeshProUGUI>();
        contentText.font = difficultyInfoFont;
        contentText.fontSize = DifficultyInfoBodyFontSize;
        contentText.fontStyle = FontStyles.Bold;
        contentText.color = Color.white;
        contentText.alpha = 1f;
        contentText.alignment = TextAlignmentOptions.TopLeft;
        contentText.textWrappingMode = TextWrappingModes.Normal;
        contentText.overflowMode = TextOverflowModes.Overflow;
        contentText.raycastTarget = false;
        ContentSizeFitter contentSizeFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject scrollbarObject = new GameObject("DifficultyInfoVerticalScrollbar", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(scrollViewObject.transform, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        SetRect(scrollbarRect, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f),
            new Vector2(-2f, 0f), new Vector2(10f, 0f));
        Image scrollbarBackground = scrollbarObject.GetComponent<Image>();
        scrollbarBackground.color = RowColor;
        scrollbarBackground.raycastTarget = true;

        Image scrollbarHandle = CreateImage(scrollbarObject.transform, "Handle", AccentColor,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -8f));
        scrollbarHandle.raycastTarget = true;
        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = scrollbarHandle.rectTransform;
        scrollbar.targetGraphic = scrollbarHandle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        ScrollRect scrollRect = scrollViewObject.GetComponent<ScrollRect>();
        difficultyInfoScrollRect = scrollRect;
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.inertia = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = DifficultyInfoBodyFontSize;
        scrollRect.horizontalScrollbar = null;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        descriptionText = contentText;
    }

    private TextMeshProUGUI CreateDifficultyInfoLabel(Transform parent, string objectName, string content,
        int fontSize, Color color, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        SetRect(labelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = difficultyInfoFont;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.text = content;
        label.raycastTarget = false;
        return label;
    }


    private void SetSelectedDifficulty(int optionIndex)
    {
        if (difficultyOptionButtons == null || optionIndex < 0 || optionIndex >= difficultyOptionButtons.Length)
            return;

        selectedDifficultyIndex = optionIndex;
        DifficultySettings.ConfigureStats(difficultyStats);
        DifficultySettings.SaveSelectedDifficulty(selectedDifficultyIndex);
        for (int index = 0; index < difficultyOptionButtons.Length; index++)
        {
            bool isSelected = index == selectedDifficultyIndex;
            difficultyOptionBackgrounds[index].color = isSelected ? SelectedDifficultyColor : ButtonColor;
            difficultyOptionOutlines[index].enabled = isSelected;
            difficultyOptionButtons[index].interactable = true;
        }

        RefreshDifficultyInfoPanel();
    }

    private void RefreshDifficultyInfoPanel()
    {
        if (descriptionText == null)
            return;

        int descriptionIndex = Mathf.Clamp(selectedDifficultyIndex, 0, DifficultyDescriptions.Length - 1);
        descriptionText.text = DifficultyDescriptions[descriptionIndex];
        descriptionText.color = Color.white;
        descriptionText.alpha = 1f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionText.rectTransform);
        Canvas.ForceUpdateCanvases();
        if (difficultyInfoScrollRect != null)
            difficultyInfoScrollRect.verticalNormalizedPosition = 1f;
    }

    private static string FormatDifficultyValue(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }


    private Slider CreateDifficultyRow(Transform parent, string settingName, string displayLabel, int anchoredY, int initialValue)
    {
        GameObject row = new GameObject(settingName + "DifficultyRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        SetRect(rowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, anchoredY), new Vector2(OverlayWidth - 100, RowHeight));
        Image rowImage = row.GetComponent<Image>();
        rowImage.color = RowColor;
        rowImage.raycastTarget = false;

        CreateText(row.transform, settingName + "Label", displayLabel, RowTitleFontSize, FontStyle.Bold,
            TextColor, TextAnchor.MiddleLeft, new Vector2(24, 30), new Vector2(RowLabelWidth, 34),
            new Vector2(0, 0.5f));
        Text valueText = CreateText(row.transform, settingName + "Value", initialValue.ToString(), ValueFontSize,
            FontStyle.Bold, AccentColor, TextAnchor.MiddleRight, new Vector2(-24, 30),
            new Vector2(LevelValueWidth, 34), new Vector2(1f, 0.5f));

        Slider slider = CreateSlider(row.transform, settingName + "Slider", initialValue,
            new Vector2(0, -24), SliderWidth, SliderHeight);
        slider.onValueChanged.AddListener(value => valueText.text = Mathf.RoundToInt(value).ToString());

        CreateButton(row.transform, settingName + "MinusButton", "−", ControlButtonSize, ControlButtonSize,
            new Vector2(-310, -24), () => slider.value = Mathf.Max(slider.minValue, slider.value - 1));
        CreateButton(row.transform, settingName + "PlusButton", "+", ControlButtonSize, ControlButtonSize,
            new Vector2(310, -24), () => slider.value = Mathf.Min(slider.maxValue, slider.value + 1));
        return slider;
    }

    private Slider CreateSlider(Transform parent, string objectName, int initialValue, Vector2 position,
        int width, int height)
    {
        GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        SetRect(sliderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, new Vector2(width, height));

        Image background = CreateImage(sliderObject.transform, "Background", RowColor,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform fillArea = CreateSliderArea(sliderObject.transform, "Fill Area");
        Image fill = CreateImage(fillArea, "Fill", AccentColor,
            new Vector2(0, 0.25f), new Vector2(1, 0.75f), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);
        RectTransform handleArea = CreateSliderArea(sliderObject.transform, "Handle Slide Area");
        Image handle = CreateImage(handleArea, "Handle", TextColor,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(24, 0));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = DifficultySettings.MinimumLevel;
        slider.maxValue = DifficultySettings.MaximumLevel;
        slider.wholeNumbers = true;
        slider.value = Mathf.Clamp(initialValue, DifficultySettings.MinimumLevel, DifficultySettings.MaximumLevel);
        slider.direction = Slider.Direction.LeftToRight;
        slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        background.raycastTarget = true;
        fill.raycastTarget = false;
        handle.raycastTarget = true;
        return slider;
    }

    private static RectTransform CreateSliderArea(Transform parent, string objectName)
    {
        GameObject areaObject = new GameObject(objectName, typeof(RectTransform));
        areaObject.transform.SetParent(parent, false);
        RectTransform areaRect = areaObject.GetComponent<RectTransform>();
        SetRect(areaRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-24, 0));
        return areaRect;
    }


    private Button CreateButton(Transform parent, string objectName, string label, int width, int height,
        Vector2 position, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetRect(buttonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, new Vector2(width, height));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = ButtonColor;
        buttonImage.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(onClick);
        button.colors = new ColorBlock
        {
            normalColor = ButtonColor,
            highlightedColor = AccentColor,
            pressedColor = new Color(0.12f, 0.55f, 0.54f, 1f),
            selectedColor = AccentColor,
            disabledColor = new Color(0.25f, 0.32f, 0.34f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };

        CreateText(buttonObject.transform, "Label", label, ButtonFontSize, FontStyle.Bold, TextColor,
            TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), true);
        return button;
    }

    private static Image CreateImage(Transform parent, string objectName, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string objectName, string content, int fontSize,
        FontStyle fontStyle, Color color, TextAnchor alignment, Vector2 anchoredPosition, Vector2 sizeDelta,
        Vector2? anchor = null, bool stretch = false)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        SetRect(textRect, stretch ? Vector2.zero : resolvedAnchor, stretch ? Vector2.one : resolvedAnchor,
            stretch ? new Vector2(0.5f, 0.5f) : resolvedAnchor, anchoredPosition, sizeDelta);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private void ReturnToMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void StartGame()
    {
        DifficultySettings.ConfigureStats(difficultyStats);
        DifficultySettings.SaveSelectedDifficulty(selectedDifficultyIndex);
        SceneManager.LoadScene(GameSceneName);
    }
}
