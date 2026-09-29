using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Displays the launch placeholder screen and routes guests to the existing main menu.
/// </summary>
public class LoginGuestScreen : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const int PanelWidth = 640;
    private const int PanelHeight = 520;
    private const int ButtonWidth = 360;
    private const int ButtonHeight = 64;
    private const int TitleFontSize = 46;
    private const int SubtitleFontSize = 20;
    private const int ButtonFontSize = 22;

    private static readonly Color BackgroundColor = new Color(0.012f, 0.022f, 0.032f, 1f);
    private static readonly Color PanelColor = new Color(0.035f, 0.072f, 0.09f, 0.98f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color ButtonColor = new Color(0.075f, 0.37f, 0.39f, 1f);
    private static readonly Color DisabledColor = new Color(0.25f, 0.32f, 0.34f, 0.65f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.58f, 0.67f, 0.69f, 1f);

    private void Start()
    {
        EnsureEventSystem();
        CreateScreen();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private static void CreateScreen()
    {
        GameObject canvasObject = new GameObject("LoginGuestCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        SetRect(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        background.GetComponent<Image>().color = BackgroundColor;

        GameObject panel = new GameObject("LoginGuestPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(background.transform, false);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));
        panel.GetComponent<Image>().color = PanelColor;

        CreateText(panel.transform, "Title", "Welcome", TitleFontSize, FontStyle.Bold, TextColor,
            new Vector2(0f, 190f), new Vector2(PanelWidth - 80, 72));
        CreateText(panel.transform, "Subtitle", "Choose how to continue", SubtitleFontSize, FontStyle.Normal,
            MutedTextColor, new Vector2(0f, 132f), new Vector2(PanelWidth - 80, 42));
        CreateButton(panel.transform, "LoginButton", "Login", new Vector2(0f, 48f), false, null);
        CreateButton(panel.transform, "SignUpButton", "Sign Up", new Vector2(0f, -38f), false, null);
        CreateButton(panel.transform, "GuestButton", "Guest", new Vector2(0f, -142f), true, LoadMainMenu);
    }

    private static void CreateButton(Transform parent, string objectName, string label, Vector2 position,
        bool interactable, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
            new Vector2(ButtonWidth, ButtonHeight));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = interactable ? ButtonColor : DisabledColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.interactable = interactable;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = ButtonColor,
            highlightedColor = AccentColor,
            pressedColor = new Color(0.12f, 0.55f, 0.54f, 1f),
            selectedColor = AccentColor,
            disabledColor = DisabledColor,
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        CreateText(buttonObject.transform, "Label", label, ButtonFontSize, FontStyle.Bold,
            interactable ? TextColor : MutedTextColor, Vector2.zero, Vector2.one, true);
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

    private static void LoadMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }
}
