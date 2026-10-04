using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the Welcome, Login, and Sign Up panels for the LootLocker login scene.
/// </summary>
public class LoginGuestScreen : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string BankGothicResourcePath = "BankGothicMediumSDF";
    private const int PanelWidth = 640;
    private const int PanelHeight = 520;
    private const int MainButtonWidth = 360;
    private const int MainButtonHeight = 64;
    private const int PopupButtonWidth = 360;
    private const int PopupButtonHeight = 56;
    private const int PopupInputWidth = 520;
    private const int PopupInputHeight = 48;
    private const int TitleFontSize = 46;
    private const int SubtitleFontSize = 20;
    private const int ButtonFontSize = 22;
    private const int InputFontSize = 20;
    private const int LabelFontSize = 17;
    private const int StatusFontSize = 16;

    private static readonly Color BackgroundColor = new Color(0.012f, 0.022f, 0.032f, 1f);
    private static readonly Color PanelColor = new Color(0.035f, 0.072f, 0.09f, 0.98f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color ButtonColor = new Color(0.075f, 0.37f, 0.39f, 1f);
    private static readonly Color DisabledColor = new Color(0.25f, 0.32f, 0.34f, 0.65f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.58f, 0.67f, 0.69f, 1f);
    private static readonly Color InputColor = new Color(0.025f, 0.052f, 0.065f, 1f);

    private TMP_FontAsset bankGothicFont;
    private AuthManager authManager;
    private GameObject welcomePanel;
    private PopupControls loginPopup;
    private PopupControls signUpPopup;
    private Button guestButton;
    private TextMeshProUGUI welcomeStatusText;

    private void Start()
    {
        EnsureEventSystem();
        bankGothicFont = Resources.Load<TMP_FontAsset>(BankGothicResourcePath);
        if (bankGothicFont == null)
        {
            Debug.LogError("Bank Gothic TMP Font Asset was not found at Resources/BankGothicMediumSDF.");
            return;
        }

        CreateScreen();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }

    private void CreateScreen()
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

        welcomePanel = CreatePanel(background.transform, "WelcomePanel");
        CreateText(welcomePanel.transform, "Title", "Welcome", TitleFontSize, FontStyles.Bold, TextColor,
            new Vector2(0f, 190f), new Vector2(PanelWidth - 80, 64), TextAlignmentOptions.Center);
        CreateText(welcomePanel.transform, "Subtitle", "Choose how to continue", SubtitleFontSize,
            FontStyles.Normal, MutedTextColor, new Vector2(0f, 132f), new Vector2(PanelWidth - 80, 34),
            TextAlignmentOptions.Center);

        Button loginOpenButton = CreateButton(welcomePanel.transform, "LoginButton", "Login",
            new Vector2(0f, 48f), MainButtonWidth, MainButtonHeight);
        Button signUpOpenButton = CreateButton(welcomePanel.transform, "SignUpButton", "Sign Up",
            new Vector2(0f, -38f), MainButtonWidth, MainButtonHeight);
        guestButton = CreateButton(welcomePanel.transform, "GuestButton", "Guest",
            new Vector2(0f, -142f), MainButtonWidth, MainButtonHeight);
        welcomeStatusText = CreateText(welcomePanel.transform, "StatusText", string.Empty, StatusFontSize,
            FontStyles.Normal, MutedTextColor, new Vector2(0f, -211f), new Vector2(PanelWidth - 80, 34),
            TextAlignmentOptions.Center);

        loginPopup = CreatePopup(background.transform, "LoginPopup", "Login", "Login", false);
        signUpPopup = CreatePopup(background.transform, "SignUpPopup", "Sign Up", "Create Account", true);
        loginPopup.panel.SetActive(false);
        signUpPopup.panel.SetActive(false);

        authManager = gameObject.AddComponent<AuthManager>();
        ConfigureWelcomeAuthentication(loginOpenButton, signUpOpenButton);

        loginOpenButton.onClick.AddListener(ShowLoginPopup);
        signUpOpenButton.onClick.AddListener(ShowSignUpPopup);
        guestButton.onClick.AddListener(authManager.OnGuestClicked);
        loginPopup.submitButton.onClick.AddListener(authManager.OnLoginClicked);
        signUpPopup.submitButton.onClick.AddListener(authManager.OnSignUpClicked);
        loginPopup.backButton.onClick.AddListener(ShowWelcomePanel);
        signUpPopup.backButton.onClick.AddListener(ShowWelcomePanel);
    }

    private GameObject CreatePanel(Transform parent, string panelName)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(PanelWidth, PanelHeight));
        panel.GetComponent<Image>().color = PanelColor;
        return panel;
    }

    private PopupControls CreatePopup(Transform parent, string panelName, string title, string submitLabel,
        bool showPasswordHint)
    {
        GameObject panel = CreatePanel(parent, panelName);
        CreateText(panel.transform, "Title", title, TitleFontSize, FontStyles.Bold, TextColor,
            new Vector2(0f, 195f), new Vector2(PanelWidth - 80, 58), TextAlignmentOptions.Center);
        CreateText(panel.transform, "EmailLabel", "Email", LabelFontSize, FontStyles.Bold, MutedTextColor,
            new Vector2(0f, 130f), new Vector2(PopupInputWidth, 26), TextAlignmentOptions.Left);
        TMP_InputField emailField = CreateInputField(panel.transform, "EmailInput", "Email address",
            new Vector2(0f, 92f), false);
        CreateText(panel.transform, "PasswordLabel", "Password", LabelFontSize, FontStyles.Bold,
            MutedTextColor, new Vector2(0f, 18f), new Vector2(PopupInputWidth, 26), TextAlignmentOptions.Left);
        TMP_InputField passwordField = CreateInputField(panel.transform, "PasswordInput", "Password",
            new Vector2(0f, -20f), true);

        TextMeshProUGUI passwordHint = CreateText(panel.transform, "PasswordHint",
            showPasswordHint ? "At least 8 characters." : string.Empty, StatusFontSize, FontStyles.Normal,
            MutedTextColor, new Vector2(0f, -56f), new Vector2(PopupInputWidth, 24),
            TextAlignmentOptions.Left);
        passwordHint.gameObject.SetActive(showPasswordHint);

        TextMeshProUGUI status = CreateText(panel.transform, "StatusText", string.Empty, StatusFontSize,
            FontStyles.Normal, MutedTextColor, new Vector2(0f, -91f), new Vector2(PanelWidth - 80, 30),
            TextAlignmentOptions.Center);
        Button submitButton = CreateButton(panel.transform, "SubmitButton", submitLabel,
            new Vector2(0f, -151f), PopupButtonWidth, PopupButtonHeight);
        Button backButton = CreateButton(panel.transform, "BackButton", "Back",
            new Vector2(0f, -222f), PopupButtonWidth, PopupButtonHeight);

        return new PopupControls(panel, emailField, passwordField, status, submitButton, backButton);
    }

    private void ConfigureWelcomeAuthentication(Button loginButton, Button signUpButton)
    {
        authManager.Configure(null, null, welcomeStatusText, loginButton, signUpButton, guestButton, null,
            MainMenuSceneName);
    }

    private void ShowLoginPopup()
    {
        OpenPopup(loginPopup);
        authManager.Configure(loginPopup.emailField, loginPopup.passwordField, loginPopup.statusText,
            loginPopup.submitButton, null, guestButton, loginPopup.backButton, MainMenuSceneName);
    }

    private void ShowSignUpPopup()
    {
        OpenPopup(signUpPopup);
        authManager.Configure(signUpPopup.emailField, signUpPopup.passwordField, signUpPopup.statusText,
            null, signUpPopup.submitButton, guestButton, signUpPopup.backButton, MainMenuSceneName);
    }

    private void OpenPopup(PopupControls popup)
    {
        ClearPopup(loginPopup);
        ClearPopup(signUpPopup);
        welcomeStatusText.text = string.Empty;
        welcomePanel.SetActive(false);
        loginPopup.panel.SetActive(popup == loginPopup);
        signUpPopup.panel.SetActive(popup == signUpPopup);
    }

    private void ClearPopup(PopupControls popup)
    {
        popup.emailField.SetTextWithoutNotify(string.Empty);
        popup.passwordField.SetTextWithoutNotify(string.Empty);
        popup.statusText.text = string.Empty;
    }

    private void ShowWelcomePanel()
    {
        ClearPopup(loginPopup);
        ClearPopup(signUpPopup);
        welcomeStatusText.text = string.Empty;
        loginPopup.panel.SetActive(false);
        signUpPopup.panel.SetActive(false);
        welcomePanel.SetActive(true);
        ConfigureWelcomeAuthentication(
            welcomePanel.transform.Find("LoginButton").GetComponent<Button>(),
            welcomePanel.transform.Find("SignUpButton").GetComponent<Button>());
    }

    private TMP_InputField CreateInputField(Transform parent, string objectName, string placeholder,
        Vector2 position, bool isPassword)
    {
        GameObject inputObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);
        SetRect(inputObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
            new Vector2(PopupInputWidth, PopupInputHeight));

        Image background = inputObject.GetComponent<Image>();
        background.color = InputColor;

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(inputObject.transform, false);
        SetRect(textArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -12f));

        TextMeshProUGUI placeholderText = CreateText(textArea.transform, "Placeholder", placeholder,
            InputFontSize, FontStyles.Normal, MutedTextColor, Vector2.zero, Vector2.one,
            TextAlignmentOptions.Left, true);
        TextMeshProUGUI inputText = CreateText(textArea.transform, "Text", string.Empty,
            InputFontSize, FontStyles.Normal, TextColor, Vector2.zero, Vector2.one,
            TextAlignmentOptions.Left, true);

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();
        inputField.textViewport = textArea.GetComponent<RectTransform>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.image = background;
        inputField.targetGraphic = background;
        inputField.contentType = isPassword
            ? TMP_InputField.ContentType.Password
            : TMP_InputField.ContentType.EmailAddress;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.readOnly = false;
        inputField.interactable = true;
        inputField.enabled = true;
        return inputField;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 position,
        int width, int height)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
            new Vector2(width, height));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = ButtonColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.interactable = true;
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

        CreateText(buttonObject.transform, "Label", label, ButtonFontSize, FontStyles.Bold,
            TextColor, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, true);
        return button;
    }

    private TextMeshProUGUI CreateText(Transform parent, string objectName, string content, int fontSize,
        FontStyles fontStyle, Color color, Vector2 position, Vector2 size, TextAlignmentOptions alignment,
        bool stretch = false)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Vector2 anchorMin = stretch ? Vector2.zero : new Vector2(0.5f, 0.5f);
        Vector2 anchorMax = stretch ? Vector2.one : new Vector2(0.5f, 0.5f);
        SetRect(textRect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), position, size);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = bankGothicFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
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

    private sealed class PopupControls
    {
        public readonly GameObject panel;
        public readonly TMP_InputField emailField;
        public readonly TMP_InputField passwordField;
        public readonly TextMeshProUGUI statusText;
        public readonly Button submitButton;
        public readonly Button backButton;

        public PopupControls(GameObject panel, TMP_InputField emailField, TMP_InputField passwordField,
            TextMeshProUGUI statusText, Button submitButton, Button backButton)
        {
            this.panel = panel;
            this.emailField = emailField;
            this.passwordField = passwordField;
            this.statusText = statusText;
            this.submitButton = submitButton;
            this.backButton = backButton;
        }
    }
}
