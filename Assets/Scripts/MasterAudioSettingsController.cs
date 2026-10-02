using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>Provides persistent master-volume settings from either the main menu or pause menu.</summary>
public class MasterAudioSettingsController : MonoBehaviour
{
    private const string MasterVolumePreferenceKey = "MasterVolume";
    private const string MasterVolumeParameterName = "MasterVolume";
    private const float DefaultMasterVolume = 1f;
    private const float MinimumMasterDecibels = -80f;
    private const float VolumeRowAnchorY = 0.62f;
    private const float VolumeSliderWidth = 360f;
    private const float VolumeSliderHorizontalOffset = -68f;
    private const float VolumePercentageWidth = 120f;
    private const float VolumePercentageHorizontalOffset = 180f;

    private static readonly Color OverlayColor = new Color(0.005f, 0.015f, 0.025f, 0.88f);
    private static readonly Color PanelColor = new Color(0.02f, 0.055f, 0.075f, 0.99f);
    private static readonly Color AccentColor = new Color(0.05f, 0.88f, 0.95f, 1f);
    private static readonly Color ButtonColor = new Color(0.08f, 0.22f, 0.27f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip buttonHoverClip;

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuPanel;

    private Canvas targetCanvas;
    private AudioMixerGroup masterAudioGroup;
    private GameObject settingsPanel;
    private Slider masterVolumeSlider;
    private TextMeshProUGUI percentageLabel;
    private bool returnToPauseMenu;
    private bool masterParameterAvailable;

    private void Awake()
    {
        targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null)
            targetCanvas = FindFirstObjectByType<Canvas>();
    }

    private void Start()
    {
        InitializeMixerRouting();
        CreateSettingsPanel();

        float savedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePreferenceKey, DefaultMasterVolume));
        masterVolumeSlider.SetValueWithoutNotify(savedVolume);
        UpdatePercentageLabel(savedVolume);
        ApplyMasterVolume(savedVolume, false);
        settingsPanel.SetActive(false);
    }

    /// <summary>Opens settings over the main menu.</summary>
    public void OpenSettingsFromMenu()
    {
        returnToPauseMenu = false;
        ShowSettingsPanel();
    }

    /// <summary>Opens settings from the pause menu while keeping the game paused.</summary>
    public void OpenSettingsFromPause()
    {
        returnToPauseMenu = true;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        ShowSettingsPanel();
    }

    /// <summary>Closes settings and returns to the view from which it was opened.</summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (returnToPauseMenu && pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
    }

    private void ShowSettingsPanel()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
        RouteUnassignedAudioSources();
    }

    private void InitializeMixerRouting()
    {
        if (audioMixer != null)
        {
            AudioMixerGroup[] masterGroups = audioMixer.FindMatchingGroups("Master");
            if (masterGroups.Length > 0)
                masterAudioGroup = masterGroups[0];

            masterParameterAvailable = audioMixer.GetFloat(MasterVolumeParameterName, out _);
        }

        RouteUnassignedAudioSources();
    }

    private void RouteUnassignedAudioSources()
    {
        if (masterAudioGroup == null)
            return;

        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioSource source in audioSources)
        {
            if (source != null && source.outputAudioMixerGroup == null)
                source.outputAudioMixerGroup = masterAudioGroup;
        }
    }

    private void ApplyMasterVolume(float normalizedVolume, bool savePreference)
    {
        float clampedVolume = Mathf.Clamp01(normalizedVolume);
        if (audioMixer != null && masterParameterAvailable)
        {
            float decibels = clampedVolume <= 0f
                ? MinimumMasterDecibels
                : Mathf.Clamp(Mathf.Log10(clampedVolume) * 20f, MinimumMasterDecibels, 0f);
            if (!audioMixer.SetFloat(MasterVolumeParameterName, decibels))
                AudioListener.volume = clampedVolume;
        }
        else
        {
            AudioListener.volume = clampedVolume;
        }

        UpdatePercentageLabel(clampedVolume);
        if (savePreference)
        {
            PlayerPrefs.SetFloat(MasterVolumePreferenceKey, clampedVolume);
            PlayerPrefs.Save();
        }
    }

    private void HandleMasterVolumeChanged(float value)
    {
        ApplyMasterVolume(value, true);
    }

    private void UpdatePercentageLabel(float normalizedVolume)
    {
        if (percentageLabel != null)
            percentageLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(normalizedVolume) * 100f)}%";
    }

    private void CreateSettingsPanel()
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("[Audio Settings] No Canvas was found; settings UI cannot be created.");
            return;
        }

        settingsPanel = new GameObject("MasterAudioSettingsPanel", typeof(RectTransform), typeof(Image));
        settingsPanel.transform.SetParent(targetCanvas.transform, false);
        settingsPanel.transform.SetAsLastSibling();
        RectTransform overlayRect = settingsPanel.GetComponent<RectTransform>();
        SetRect(overlayRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image overlayImage = settingsPanel.GetComponent<Image>();
        overlayImage.color = OverlayColor;
        overlayImage.raycastTarget = true;

        GameObject card = CreateImageObject(settingsPanel.transform, "SettingsCard", PanelColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(620f, 360f), true);
        CreateText(card.transform, "SettingsTitle", "SETTINGS", 34, FontStyles.Bold,
            new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(520f, 54f), TextAlignmentOptions.Center);

        percentageLabel = CreateText(card.transform, "MasterVolumePercentage", "100%", 22, FontStyles.Bold,
            new Vector2(0.5f, VolumeRowAnchorY), new Vector2(VolumePercentageHorizontalOffset, 0f),
            new Vector2(VolumePercentageWidth, 44f), TextAlignmentOptions.Right);
        percentageLabel.overflowMode = TextOverflowModes.Overflow;
        masterVolumeSlider = CreateMasterSlider(card.transform);

        CreateButton(card.transform, "SettingsBackButton", "BACK", new Vector2(0.5f, 0f),
            new Vector2(0f, 58f), new Vector2(200f, 52f), CloseSettings);

        settingsPanel.SetActive(false);
    }

    private Slider CreateMasterSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject("MasterVolumeSlider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        SetRect(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, VolumeRowAnchorY),
            new Vector2(0.5f, VolumeRowAnchorY), new Vector2(0.5f, 0.5f),
            new Vector2(VolumeSliderHorizontalOffset, 0f), new Vector2(VolumeSliderWidth, 30f));

        Image background = CreateImage(sliderObject.transform, "Background", new Color(0.035f, 0.09f, 0.11f, 1f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        RectTransform fillArea = CreateRectTransform(sliderObject.transform, "FillArea",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, 0f));
        Image fill = CreateImage(fillArea, "Fill", AccentColor,
            new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, false);
        RectTransform handleArea = CreateRectTransform(sliderObject.transform, "HandleArea",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, 0f));
        Image handle = CreateImage(handleArea, "Handle", TextColor,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 0f), true);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        background.raycastTarget = true;
        fill.raycastTarget = false;
        handle.raycastTarget = true;
        return slider;
    }

    private void CreateButton(Transform parent, string objectName, string label, Vector2 anchor,
        Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(AudioSource));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), anchor, anchor, anchor, anchoredPosition, size);

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
            pressedColor = new Color(0.04f, 0.12f, 0.16f, 1f),
            selectedColor = AccentColor,
            disabledColor = new Color(0.2f, 0.25f, 0.28f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 20, FontStyles.Bold,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        AudioSource audioSource = buttonObject.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        ButtonSFX buttonSfx = buttonObject.AddComponent<ButtonSFX>();
        buttonSfx.ConfigureFeedback(buttonClickClip, buttonHoverClip, masterAudioGroup);
    }

    private static GameObject CreateImageObject(Transform parent, string objectName, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, bool raycastTarget)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetRect(imageObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return imageObject;
    }

    private static Image CreateImage(Transform parent, string objectName, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, bool raycastTarget)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetRect(imageObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static RectTransform CreateRectTransform(Transform parent, string objectName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        return rect;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string objectName, string content, int fontSize,
        FontStyles fontStyle, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, anchor, anchor, anchoredPosition, sizeDelta);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.fontWeight = FontWeight.Bold;
        text.color = TextColor;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
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
}
