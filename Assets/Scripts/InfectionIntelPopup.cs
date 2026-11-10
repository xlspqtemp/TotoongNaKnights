using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows a compact pause-on-open information panel for a selected infection marker.</summary>
public sealed class InfectionIntelPopup : MonoBehaviour
{
    private const string BankGothicResourcePath = "BankGothicMediumSDF";
    private const float PopupWidth = 500f;
    private const float PopupHeight = 390f;
    private const float CloseWidth = 88f;
    private const float CloseHeight = 34f;
    private const float DetailFontSize = 19f;
    private const float TitleFontSize = 21f;
    private static readonly Color PanelColor = new Color(0.018f, 0.035f, 0.065f, 0.98f);
    private static readonly Color CyanColor = new Color(0.16f, 0.88f, 0.98f, 1f);
    private static readonly Color BodyTextColor = new Color(0.9f, 0.96f, 1f, 1f);
    private static readonly Color ButtonColor = new Color(0.035f, 0.1f, 0.15f, 1f);

    private GameObject popupCanvasObject;
    private TextMeshProUGUI detailLabel;
    private InfectionSpawner.InfectionMarker selectedMarker;
    private InfectionSpawner infectionSpawner;
    private bool isOpen;
    private float timeScaleBeforePopup;

    /// <summary>Returns whether infection intel is currently visible.</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        BuildPopup();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        if (selectedMarker == null || selectedMarker.isRemoving || infectionSpawner == null ||
            !infectionSpawner.IsInfectionActive(selectedMarker))
        {
            Close();
            return;
        }

        RefreshDetails();
    }

    private void OnDisable()
    {
        Close();
    }

    private void OnDestroy()
    {
        Close();
    }

    /// <summary>Opens the intel panel for the specified active infection and pauses gameplay time.</summary>
    public void Show(InfectionSpawner.InfectionMarker marker, InfectionSpawner sourceSpawner)
    {
        if (marker == null || marker.infection == null || sourceSpawner == null)
            return;

        selectedMarker = marker;
        infectionSpawner = sourceSpawner;
        if (!isOpen)
        {
            timeScaleBeforePopup = Time.timeScale;
            isOpen = true;
            Time.timeScale = 0f;
        }

        if (popupCanvasObject != null && !popupCanvasObject.activeSelf)
            popupCanvasObject.SetActive(true);
        RefreshDetails();
    }

    /// <summary>Closes the intel panel and restores the gameplay time scale that was active before it opened.</summary>
    public void Close()
    {
        if (popupCanvasObject != null && popupCanvasObject.activeSelf)
            popupCanvasObject.SetActive(false);

        selectedMarker = null;
        infectionSpawner = null;
        RestoreTimeScale();
        isOpen = false;
    }

    private void BuildPopup()
    {
        if (popupCanvasObject != null)
            return;

        popupCanvasObject = new GameObject("Infection Intel Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        popupCanvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = popupCanvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        Canvas canvas = popupCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = popupCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Infection Intel Panel", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Outline));
        panelObject.transform.SetParent(popupCanvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PopupWidth, PopupHeight);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;
        Outline border = panelObject.GetComponent<Outline>();
        border.effectColor = CyanColor;
        border.effectDistance = new Vector2(3f, -3f);
        border.useGraphicAlpha = true;

        CreateText("Infection Intel Title", panelRect, "INFECTION INTEL", new Vector2(22f, -16f),
            new Vector2(-CloseWidth - 30f, 40f), TitleFontSize, CyanColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        CreateCloseButton(panelRect);
        CreateAccentLine(panelRect);

        detailLabel = CreateText("Infection Intel Details", panelRect, string.Empty,
            new Vector2(26f, -74f), new Vector2(-52f, 280f), DetailFontSize, BodyTextColor,
            TextAlignmentOptions.TopLeft, FontStyles.Normal);
        detailLabel.textWrappingMode = TextWrappingModes.Normal;
        detailLabel.lineSpacing = 12f;
        detailLabel.richText = true;

        popupCanvasObject.SetActive(false);
    }

    private TextMeshProUGUI CreateText(string objectName, RectTransform parent, string value,
        Vector2 anchoredPosition, Vector2 size, float fontSize, Color color,
        TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>(BankGothicResourcePath) ?? TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private void CreateCloseButton(RectTransform panelRect)
    {
        GameObject buttonObject = new GameObject("Infection Intel Close Button", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panelRect, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-18f, -16f);
        buttonRect.sizeDelta = new Vector2(CloseWidth, CloseHeight);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = ButtonColor;
        buttonImage.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(Close);

        GameObject labelObject = new GameObject("Close Label", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonRect, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>(BankGothicResourcePath) ?? TMP_Settings.defaultFontAsset;
        label.fontSize = 14f;
        label.fontStyle = FontStyles.Bold;
        label.color = CyanColor;
        label.alignment = TextAlignmentOptions.Center;
        label.text = "CLOSE";
        label.raycastTarget = false;
    }

    private static void CreateAccentLine(RectTransform panelRect)
    {
        GameObject lineObject = new GameObject("Cyan Header Divider", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(panelRect, false);

        RectTransform lineRect = lineObject.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = new Vector2(0f, -58f);
        lineRect.sizeDelta = new Vector2(-36f, 2f);

        Image lineImage = lineObject.GetComponent<Image>();
        lineImage.color = CyanColor;
        lineImage.raycastTarget = false;
    }

    private void RefreshDetails()
    {
        if (detailLabel == null || selectedMarker == null || selectedMarker.infection == null)
            return;

        InfectionData infection = selectedMarker.infection;
        string status = selectedMarker.isTagged
            ? "Tagged"
            : selectedMarker.isContained
                ? "Contained"
                : selectedMarker.resolutionPending || infectionSpawner.IsSquadDispatchedTo(selectedMarker)
                    ? "Ongoing"
                    : "Pending";

        detailLabel.text =
            $"<color=#29E0F3>PATHOGEN</color>  {infection.displayName}\n" +
            $"<color=#29E0F3>CATEGORY</color>   {GetCategoryName(infection.pathogenType)}\n" +
            $"<color=#29E0F3>SEVERITY</color>   {infection.severityStage}\n" +
            $"<color=#29E0F3>STATUS</color>     {status}\n\n" +
            $"<color=#29E0F3>RECOMMENDED UNIT</color>\n{GetRecommendedUnit(infection.pathogenType)}";
    }

    private static string GetCategoryName(InfectionPathogenType pathogenType)
    {
        return pathogenType switch
        {
            InfectionPathogenType.Bacterial => "Bacterial",
            InfectionPathogenType.Viral => "Viral",
            InfectionPathogenType.Fungal => "Fungal",
            InfectionPathogenType.Allergen => "Allergen",
            InfectionPathogenType.Parasite => "Parasite",
            _ => pathogenType.ToString()
        };
    }

    private static string GetRecommendedUnit(InfectionPathogenType pathogenType)
    {
        return pathogenType switch
        {
            InfectionPathogenType.Bacterial => "Neutrophil / Macrophage",
            InfectionPathogenType.Viral => "Killer T-Cell",
            InfectionPathogenType.Fungal => "Neutrophil / Macrophage",
            InfectionPathogenType.Allergen => "Basophil",
            InfectionPathogenType.Parasite => "Eosinophil",
            _ => "White Blood Cell"
        };
    }

    private void RestoreTimeScale()
    {
        if (!isOpen)
            return;

        Time.timeScale = timeScaleBeforePopup;
        isOpen = false;
    }
}
