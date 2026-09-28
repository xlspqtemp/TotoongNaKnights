using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Controls the main menu and its demo confirmation and difficulty selection flow.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string GameSceneName = "Game";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const int OverlayWidth = 920;
    private const int OverlayHeight = 720;
    private const int PromptWidth = 780;
    private const int PromptHeight = 440;
    private const int PromptTitleFontSize = 48;
    private const int SubtitleFontSize = 22;
    private const int ButtonFontSize = 22;
    private const int DifficultyTitleFontSize = 38;
    private const int RowTitleFontSize = 22;
    private const int ValueFontSize = 18;
    private const int ButtonWidth = 250;
    private const int ButtonHeight = 64;
    private const int RowHeight = 112;
    private const int SliderWidth = 480;
    private const int SliderHeight = 30;
    private const int ControlButtonSize = 42;
    private const int RowLabelWidth = 330;
    private const int LevelValueWidth = 54;
    private const int DifficultyRowTop = 116;
    private const int DifficultyRowSpacing = 132;
    private const int StartButtonY = -290;

    private static readonly Color OverlayColor = new Color(0.012f, 0.022f, 0.032f, 0.82f);
    private static readonly Color PanelColor = new Color(0.035f, 0.072f, 0.09f, 0.98f);
    private static readonly Color RowColor = new Color(0.055f, 0.105f, 0.125f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color ButtonColor = new Color(0.075f, 0.37f, 0.39f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.77f, 0.79f, 1f);

    private Canvas menuCanvas;
    private GameObject overlay;
    private GameObject promptPanel;
    private GameObject difficultyPanel;
    private Slider enemySlider;
    private Slider defenseSlider;
    private Slider randomEventsSlider;

    /// <summary>
    /// Opens the demo confirmation window when the Play button is selected.
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
        promptPanel.SetActive(true);
        difficultyPanel.SetActive(false);
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

        promptPanel = CreatePanel(overlay.transform, "PlayDemoPrompt", PromptWidth, PromptHeight);
        CreateText(promptPanel.transform, "PromptTitle", "Play demo?", PromptTitleFontSize, FontStyle.Bold,
            TextColor, TextAnchor.MiddleCenter, new Vector2(0, 110), new Vector2(680, 90));
        CreateText(promptPanel.transform, "PromptSubtitle", "Choose how you would like to continue.", SubtitleFontSize,
            FontStyle.Normal, MutedTextColor, TextAnchor.MiddleCenter, Vector2.zero,
            new Vector2(680, 52), new Vector2(0.5f, 0.5f));

        CreateButton(promptPanel.transform, "YesButton", "Yes", ButtonWidth, ButtonHeight,
            new Vector2(-142, -112), LoadDemoScene);
        CreateButton(promptPanel.transform, "ContinueAnywaysButton", "Continue Anyways", ButtonWidth, ButtonHeight,
            new Vector2(142, -112), ShowDifficultySelection);

        difficultyPanel = CreatePanel(overlay.transform, "DifficultySelection", OverlayWidth, OverlayHeight);
        CreateText(difficultyPanel.transform, "DifficultyTitle", "Difficulty Selection", DifficultyTitleFontSize,
            FontStyle.Bold, TextColor, TextAnchor.MiddleCenter, new Vector2(0, 292), new Vector2(820, 64));
        CreateText(difficultyPanel.transform, "DifficultySubtitle", "Set each level from 1 to 5.", SubtitleFontSize,
            FontStyle.Normal, MutedTextColor, TextAnchor.MiddleCenter, new Vector2(0, 232),
            new Vector2(800, 42));

        enemySlider = CreateDifficultyRow(difficultyPanel.transform, "Enemy", DifficultyRowTop,
            DifficultySettings.EnemyLevel);
        defenseSlider = CreateDifficultyRow(difficultyPanel.transform, "Defense", DifficultyRowTop - DifficultyRowSpacing,
            DifficultySettings.DefenseLevel);
        randomEventsSlider = CreateDifficultyRow(difficultyPanel.transform, "Random Events",
            DifficultyRowTop - DifficultyRowSpacing * 2, DifficultySettings.RandomEventsLevel);

        CreateButton(difficultyPanel.transform, "StartGameButton", "Start Game", ButtonWidth, ButtonHeight,
            new Vector2(0, StartButtonY), StartGame);
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

    private Slider CreateDifficultyRow(Transform parent, string settingName, int anchoredY, int initialValue)
    {
        GameObject row = new GameObject(settingName + "DifficultyRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        SetRect(rowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, anchoredY), new Vector2(OverlayWidth - 100, RowHeight));
        Image rowImage = row.GetComponent<Image>();
        rowImage.color = RowColor;
        rowImage.raycastTarget = false;

        CreateText(row.transform, settingName + "Label", settingName, RowTitleFontSize, FontStyle.Bold,
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

    private void LoadDemoScene()
    {
        SceneManager.LoadScene("Demo");
    }

    private void ShowDifficultySelection()
    {
        promptPanel.SetActive(false);
        difficultyPanel.SetActive(true);
    }

    private void StartGame()
    {
        DifficultySettings.SaveLevels(
            Mathf.RoundToInt(enemySlider.value),
            Mathf.RoundToInt(defenseSlider.value),
            Mathf.RoundToInt(randomEventsSlider.value));
        SceneManager.LoadScene(GameSceneName);
    }
}
