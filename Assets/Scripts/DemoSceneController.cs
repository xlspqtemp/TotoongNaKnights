using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows the placeholder demo completion prompt and handles retry or menu navigation.
/// </summary>
public class DemoSceneController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const float RetryDelaySeconds = 5f;
    private const int PanelWidth = 760;
    private const int PanelHeight = 420;
    private const int ButtonWidth = 280;
    private const int ButtonHeight = 64;
    private const int TitleFontSize = 48;
    private const int ButtonFontSize = 22;

    private static readonly Color OverlayColor = new Color(0.012f, 0.022f, 0.032f, 0.82f);
    private static readonly Color PanelColor = new Color(0.035f, 0.072f, 0.09f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.075f, 0.37f, 0.39f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 0.97f, 1f);

    private GameObject promptOverlay;
    private Button retryButton;

    private void Start()
    {
        EnsureEventSystem();
        CreatePrompt();
    }

    private void CreatePrompt()
    {
        GameObject canvasObject = new GameObject("DemoPromptCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        promptOverlay = new GameObject("DemoFinishedOverlay", typeof(RectTransform), typeof(Image));
        promptOverlay.transform.SetParent(canvasObject.transform, false);
        SetRect(promptOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image overlayImage = promptOverlay.GetComponent<Image>();
        overlayImage.color = OverlayColor;
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("DemoFinishedPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(promptOverlay.transform, false);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        CreateText(panel.transform, "DemoFinishedTitle", "Demo finished!", TitleFontSize,
            FontStyle.Bold, TextColor, new Vector2(0, 92), new Vector2(PanelWidth - 80, 96));
        retryButton = CreateButton(panel.transform, "RetryButton", "Retry?", ButtonWidth, ButtonHeight,
            new Vector2(0, 6), RetryDemo);
        CreateButton(panel.transform, "BackToMainMenuButton", "Back to Main Menu", ButtonWidth,
            ButtonHeight, new Vector2(0, -88), ReturnToMainMenu);
    }

    private void RetryDemo()
    {
        if (retryButton == null || !retryButton.interactable)
        {
            return;
        }

        retryButton.interactable = false;
        promptOverlay.SetActive(false);
        StartCoroutine(ReopenPromptAfterDelay());
    }

    private IEnumerator ReopenPromptAfterDelay()
    {
        yield return GameplaySpeed.WaitForGameplaySeconds(RetryDelaySeconds);
        if (promptOverlay != null)
        {
            promptOverlay.SetActive(true);
        }

        if (retryButton != null)
        {
            retryButton.interactable = true;
        }
    }

    private void ReturnToMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Button CreateButton(Transform parent, string objectName, string label, int width,
        int height, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
            new Vector2(width, height));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = ButtonColor;
        buttonImage.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.ColorTint;
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
        button.onClick.AddListener(onClick);
        CreateText(buttonObject.transform, "Label", label, ButtonFontSize, FontStyle.Bold,
            TextColor, Vector2.zero, Vector2.one, true);
        return button;
    }

    private static void CreateText(Transform parent, string objectName, string content, int fontSize,
        FontStyle fontStyle, Color color, Vector2 position, Vector2 size, bool stretch = false)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Vector2 anchorMin = stretch ? Vector2.zero : new Vector2(0.5f, 0.5f);
        Vector2 anchorMax = stretch ? Vector2.one : new Vector2(0.5f, 0.5f);
        SetRect(textRect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), position, size);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }
}
