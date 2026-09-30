using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates and updates the in-game fast-forward button beside the Day Counter.</summary>
public sealed class GameplaySpeedButton : MonoBehaviour
{
    private static readonly Color AccentColor = new Color(0.1f, 0.75f, 0.88f, 1f);
    private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.075f, 0.96f);
    private const float ButtonWidth = 64f;
    private const float ButtonHeight = 60f;
    private const float ButtonGap = 10f;

    private Button speedButton;
    private TextMeshProUGUI speedLabel;
    private TextMeshProUGUI qteLabel;

    private void OnEnable()
    {
        GameplaySpeed.OnSpeedChanged += RefreshLabel;
    }

    private void Start()
    {
        CreateButton();
        RefreshLabel();
    }

    private void OnDisable()
    {
        GameplaySpeed.OnSpeedChanged -= RefreshLabel;
    }

    private void OnDestroy()
    {
        if (speedButton != null)
            speedButton.onClick.RemoveListener(GameplaySpeed.Cycle);
    }

    private void CreateButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform panelRect = transform as RectTransform;
        if (canvas == null || panelRect == null)
        {
            Debug.LogWarning("GameplaySpeedButton requires a Day Counter RectTransform under a Canvas.", this);
            enabled = false;
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            Debug.LogWarning("GameplaySpeedButton could not find the Canvas RectTransform.", this);
            enabled = false;
            return;
        }

        GameObject buttonObject = new GameObject("FastForwardButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(canvasRect, false);
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        Vector3[] panelCorners = new Vector3[4];
        panelRect.GetWorldCorners(panelCorners);
        Vector3 panelTopRight = canvasRect.InverseTransformPoint(panelCorners[2]);
        Rect canvasBounds = canvasRect.rect;
        buttonRect.anchoredPosition = new Vector2(
            panelTopRight.x - canvasBounds.xMin + ButtonGap,
            panelTopRight.y - canvasBounds.yMax);

        Image background = buttonObject.GetComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = true;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        speedButton = buttonObject.GetComponent<Button>();
        speedButton.targetGraphic = background;
        speedButton.transition = Selectable.Transition.ColorTint;
        speedButton.colors = new ColorBlock
        {
            normalColor = PanelColor,
            highlightedColor = new Color(0.08f, 0.18f, 0.22f, 1f),
            pressedColor = new Color(0.1f, 0.75f, 0.88f, 0.45f),
            selectedColor = new Color(0.08f, 0.18f, 0.22f, 1f),
            disabledColor = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.55f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        speedButton.onClick.AddListener(GameplaySpeed.Cycle);

        CreateText("FastForwardSymbol", buttonRect, "▶▶", 21f, FontStyles.Bold,
            new Vector2(0f, 0.36f), new Vector2(1f, 1f), AccentColor, out _);
        CreateText("SpeedModeLabel", buttonRect, "1x", 12f, FontStyles.Bold,
            new Vector2(0f, 0.1f), new Vector2(1f, 0.4f), AccentColor, out speedLabel);
        CreateText("QTEOverrideLabel", buttonRect, string.Empty, 8f, FontStyles.Bold,
            new Vector2(0f, 0f), new Vector2(1f, 0.16f), AccentColor, out qteLabel);
    }

    private void CreateText(
        string objectName,
        RectTransform parent,
        string textValue,
        float fontSize,
        FontStyles fontStyle,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color textColor,
        out TextMeshProUGUI label)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = anchorMin;
        labelRect.anchorMax = anchorMax;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.Center;
        label.color = textColor;
        label.text = textValue;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }

    private void RefreshLabel()
    {
        if (speedLabel == null || qteLabel == null)
            return;

        float displayedSpeed = GameplaySpeed.Multiplier;
        speedLabel.text = $"{displayedSpeed:0.#}x";
        qteLabel.text = GameplaySpeed.IsQTEActive ? "QTE" : string.Empty;
        speedLabel.color = AccentColor;
        qteLabel.color = GameplaySpeed.IsQTEActive ? new Color(1f, 0.55f, 0.3f, 1f) : AccentColor;
    }
}
