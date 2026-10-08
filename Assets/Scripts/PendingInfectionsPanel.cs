using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Displays live infections in a fixed HUD panel.</summary>
[DefaultExecutionOrder(-100)]
public sealed class PendingInfectionsPanel : MonoBehaviour
{
    public enum RowStatus
    {
        Pending,
        Ongoing,
        Resolved
    }

    private const string PanelPrefabResourcePath = "PendingInfectionsPanel";
    private const string RowPrefabResourcePath = "PendingInfectionRow";
    private const string FontResourcePath = "BankGothicMediumSDF";
    private const string WhiteSwatchResourcePath = "HUDWhiteSwatch";
    private const float PanelWidth = 340f;
    private const float PanelHeight = 270f;
    private const float PanelRightInset = 18f;
    private const float PanelTopOffset = 92f;
    private const float HeaderHeight = 40f;
    private const float RowHeight = 38f;
    private const float RowSpacing = 6f;
    private const int MaximumRows = 5;
    private const float RowSlideSpeed = 480f;
    private const float ResolvedFlashDuration = 0.5f;
    private const float ResolvedFadeDuration = 0.8f;
    private static readonly Color PanelColor = new Color(0.025f, 0.045f, 0.065f, 0.94f);
    private static readonly Color DefaultRowColor = new Color(0.055f, 0.09f, 0.115f, 0.98f);
    private static readonly Color OngoingRowColor = new Color(0.035f, 0.24f, 0.29f, 0.98f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color BacterialColor = new Color(0.95f, 0.2f, 0.18f, 1f);
    private static readonly Color ViralColor = new Color(0.2f, 0.55f, 1f, 1f);
    private static readonly Color ResolvedColor = new Color(0.18f, 0.85f, 0.42f, 1f);

    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private TMP_FontAsset panelFont;
    [SerializeField] private Sprite whiteSwatchSprite;

    private sealed class RowView
    {
        public InfectionSpawner.InfectionMarker marker;
        public string displayName;
        public bool isBacterial;
        public RowStatus status;
        public GameObject rootObject;
        public CanvasGroup canvasGroup;
        public CanvasGroup visualCanvasGroup;
        public RectTransform visualRect;
        public Image backgroundImage;
        public Image swatchImage;
        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI statusLabel;
        public float targetY;
        public float resolvedElapsed;
    }

    private readonly List<RowView> rows = new List<RowView>();
    private RectTransform panelRect;
    private RectTransform contentRect;
    private Canvas rootCanvas;
    private InfectionSpawner infectionSpawner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePanelExists()
    {
        if (Object.FindFirstObjectByType<PendingInfectionsPanel>() != null)
            return;

        GameObject panelPrefab = Resources.Load<GameObject>(PanelPrefabResourcePath);
        if (panelPrefab != null)
            Instantiate(panelPrefab);
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        ResolveResources();
        TryBuildPanel();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        infectionSpawner = Object.FindFirstObjectByType<InfectionSpawner>();
        ResolveResources();
        TryBuildPanel();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        infectionSpawner = null;
        ClearAll();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAll();
        infectionSpawner = Object.FindFirstObjectByType<InfectionSpawner>();
        panelRect = null;
        contentRect = null;
        rootCanvas = null;
        ResolveResources();
        TryBuildPanel();
        SyncRows();
    }

    private void Update()
    {
        ResolveResources();
        if (infectionSpawner == null)
            infectionSpawner = Object.FindFirstObjectByType<InfectionSpawner>();
        if (panelRect == null && !TryBuildPanel())
            return;

        SyncRows();
        UpdateRows(GameplaySpeed.DeltaTime);
    }

    /// <summary>Adds a standalone row; live infection rows are synchronized from InfectionSpawner.</summary>
    public void AddRow(string displayName, bool isBacterial)
    {
        CreateRow(null, displayName, isBacterial);
    }

    private void AddLiveRow(InfectionSpawner.InfectionMarker marker)
    {
        if (marker == null || marker.infection == null)
            return;

        CreateRow(marker, marker.infection.displayName, marker.infection.pathogenType == InfectionPathogenType.Bacterial);
    }

    private void CreateRow(InfectionSpawner.InfectionMarker marker, string displayName, bool isBacterial)
    {
        if (string.IsNullOrWhiteSpace(displayName) || rows.Count >= MaximumRows)
            return;
        if (panelRect == null && !TryBuildPanel())
            return;

        GameObject rowObject = rowPrefab != null ? Instantiate(rowPrefab, contentRect, false) : new GameObject("Pending Infection Row");
        if (rowObject.transform.parent != contentRect)
            rowObject.transform.SetParent(contentRect, false);
        rowObject.name = $"Pending Infection Row - {displayName}";

        CanvasGroup canvasGroup = rowObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = rowObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RowView row = new RowView
        {
            marker = marker,
            displayName = displayName,
            isBacterial = isBacterial,
            status = RowStatus.Pending,
            rootObject = rowObject,
            canvasGroup = canvasGroup
        };
        CreateRowVisuals(row);
        rows.Add(row);
        ApplyRowStatusVisuals(row);
        ReflowRows();
    }

    private void SyncRows()
    {
        if (infectionSpawner == null)
            return;

        IReadOnlyList<InfectionSpawner.InfectionMarker> activeInfections = infectionSpawner.ActiveInfections;
        HashSet<InfectionSpawner.InfectionMarker> displayedMarkers = new HashSet<InfectionSpawner.InfectionMarker>();
        int activeCount = 0;
        for (int index = 0; index < activeInfections.Count && activeCount < MaximumRows; index++)
        {
            InfectionSpawner.InfectionMarker marker = activeInfections[index];
            if (marker == null || marker.isRemoving || marker.infection == null)
                continue;

            activeCount++;
            displayedMarkers.Add(marker);
            RowView row = FindRow(marker);
            if (row == null)
            {
                if (rows.Count >= MaximumRows)
                    continue;

                AddLiveRow(marker);
                row = FindRow(marker);
            }

            if (row == null || row.status == RowStatus.Resolved)
                continue;

            SetRowStatus(row, infectionSpawner.IsSquadDispatchedTo(marker) ? RowStatus.Ongoing : RowStatus.Pending);
        }

        foreach (RowView row in rows)
        {
            if (row.status == RowStatus.Resolved)
                continue;

            if (row.marker == null || row.marker.isRemoving || !displayedMarkers.Contains(row.marker))
                ResolveRow(row);
        }
    }

    private RowView FindRow(InfectionSpawner.InfectionMarker marker)
    {
        foreach (RowView row in rows)
        {
            if (row.marker == marker)
                return row;
        }

        return null;
    }

    private void SetRowStatus(RowView row, RowStatus status)
    {
        if (row == null || row.status == RowStatus.Resolved || row.status == status)
            return;

        row.status = status;
        ApplyRowStatusVisuals(row);
    }

    private void ResolveRow(RowView row)
    {
        if (row == null || row.status == RowStatus.Resolved)
            return;

        row.status = RowStatus.Resolved;
        row.resolvedElapsed = 0f;
        ApplyRowStatusVisuals(row);
    }

    /// <summary>Changes the status of rows with the specified display name.</summary>
    public void SetRowStatus(string displayName, RowStatus status)
    {
        foreach (RowView row in rows)
        {
            if (row.displayName == displayName)
                SetRowStatus(row, status);
        }
    }

    /// <summary>Starts the resolved flash and fade before removing matching rows.</summary>
    public void ResolveRow(string displayName)
    {
        foreach (RowView row in rows)
        {
            if (row.displayName == displayName)
                ResolveRow(row);
        }
    }

    /// <summary>Immediately clears all runtime rows from the panel.</summary>
    public void ClearAll()
    {
        foreach (RowView row in rows)
        {
            if (row.rootObject != null)
                Destroy(row.rootObject);
            if (row.visualRect != null)
                Destroy(row.visualRect.gameObject);
        }

        rows.Clear();
    }

    private void ResolveResources()
    {
        if (rowPrefab == null)
            rowPrefab = Resources.Load<GameObject>(RowPrefabResourcePath);
        if (panelFont == null)
            panelFont = Resources.Load<TMP_FontAsset>(FontResourcePath);
        if (whiteSwatchSprite == null)
            whiteSwatchSprite = Resources.Load<Sprite>(WhiteSwatchResourcePath);
    }

    private bool TryBuildPanel()
    {
        if (panelRect != null)
            return true;

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas candidate in canvases)
        {
            if (candidate != null && candidate.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rootCanvas = candidate;
                break;
            }
        }
        if (rootCanvas == null && canvases.Length > 0)
            rootCanvas = canvases[0];
        if (rootCanvas == null)
            return false;

        GameObject panelObject = CreateUiObject("Pending Infections Panel", rootCanvas.transform);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.anchoredPosition = new Vector2(-PanelRightInset, -PanelTopOffset);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = whiteSwatchSprite;
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;
        Outline panelOutline = panelObject.AddComponent<Outline>();
        panelOutline.effectColor = AccentColor;
        panelOutline.effectDistance = new Vector2(1.5f, -1.5f);
        panelOutline.useGraphicAlpha = true;

        TextMeshProUGUI header = CreateText("Pending Infections Header", panelRect, "PENDING INFECTIONS", 17f,
            FontStyles.Bold, AccentColor, TextAlignmentOptions.Left);
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        header.rectTransform.sizeDelta = new Vector2(-24f, HeaderHeight - 8f);

        GameObject accentObject = CreateUiObject("Header Accent", panelRect);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = new Vector2(0f, -HeaderHeight);
        accentRect.sizeDelta = new Vector2(-20f, 2f);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.sprite = whiteSwatchSprite;
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        GameObject contentObject = new GameObject("Pending Infection Rows", typeof(RectTransform));
        contentObject.transform.SetParent(panelRect, false);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(10f, 10f);
        contentRect.offsetMax = new Vector2(-10f, -HeaderHeight - 10f);

        return true;
    }

    private void CreateRowVisuals(RowView row)
    {
        GameObject visualObject = CreateUiObject("Row Visual", contentRect);
        row.visualRect = visualObject.GetComponent<RectTransform>();
        row.visualRect.anchorMin = new Vector2(0f, 1f);
        row.visualRect.anchorMax = new Vector2(1f, 1f);
        row.visualRect.pivot = new Vector2(0.5f, 1f);
        row.visualRect.anchoredPosition = new Vector2(0f, row.targetY);
        row.visualRect.sizeDelta = new Vector2(0f, RowHeight);
        row.visualCanvasGroup = visualObject.AddComponent<CanvasGroup>();
        row.visualCanvasGroup.interactable = false;
        row.visualCanvasGroup.blocksRaycasts = false;

        row.backgroundImage = visualObject.GetComponent<Image>();
        row.backgroundImage.sprite = whiteSwatchSprite;
        row.backgroundImage.raycastTarget = false;

        GameObject swatchObject = CreateUiObject("Pathogen Color Swatch", visualObject.transform);
        RectTransform swatchRect = swatchObject.GetComponent<RectTransform>();
        swatchRect.anchorMin = new Vector2(0f, 0.5f);
        swatchRect.anchorMax = new Vector2(0f, 0.5f);
        swatchRect.pivot = new Vector2(0f, 0.5f);
        swatchRect.anchoredPosition = new Vector2(10f, 0f);
        swatchRect.sizeDelta = new Vector2(19f, 19f);
        row.swatchImage = swatchObject.GetComponent<Image>();
        row.swatchImage.sprite = whiteSwatchSprite;
        row.swatchImage.raycastTarget = false;

        row.nameLabel = CreateText("Pathogen Name", visualObject.transform, row.displayName, 14f,
            FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
        row.nameLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        row.nameLabel.rectTransform.anchorMax = new Vector2(0.7f, 1f);
        row.nameLabel.rectTransform.offsetMin = new Vector2(38f, 2f);
        row.nameLabel.rectTransform.offsetMax = new Vector2(-2f, -2f);
        row.nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        row.nameLabel.overflowMode = TextOverflowModes.Ellipsis;

        row.statusLabel = CreateText("Infection Status", visualObject.transform, "Pending", 12f,
            FontStyles.Bold, Color.white, TextAlignmentOptions.Right);
        row.statusLabel.rectTransform.anchorMin = new Vector2(0.7f, 0f);
        row.statusLabel.rectTransform.anchorMax = Vector2.one;
        row.statusLabel.rectTransform.offsetMin = new Vector2(0f, 2f);
        row.statusLabel.rectTransform.offsetMax = new Vector2(-10f, -2f);
        row.statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float fontSize,
        FontStyles fontStyle, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = panelFont != null ? panelFont : TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.text = value;
        return label;
    }

    private void ApplyRowStatusVisuals(RowView row)
    {
        if (row == null || row.backgroundImage == null || row.swatchImage == null || row.statusLabel == null)
            return;

        switch (row.status)
        {
            case RowStatus.Ongoing:
                row.statusLabel.text = "Ongoing";
                row.backgroundImage.color = OngoingRowColor;
                row.swatchImage.color = row.isBacterial ? BacterialColor : ViralColor;
                row.statusLabel.color = AccentColor;
                break;
            case RowStatus.Resolved:
                row.statusLabel.text = "Resolved";
                row.backgroundImage.color = ResolvedColor;
                row.swatchImage.color = ResolvedColor;
                row.statusLabel.color = Color.white;
                break;
            default:
                row.statusLabel.text = "Pending";
                row.backgroundImage.color = DefaultRowColor;
                row.swatchImage.color = row.isBacterial ? BacterialColor : ViralColor;
                row.statusLabel.color = Color.white;
                break;
        }
    }



    private void UpdateRows(float gameplayDeltaTime)
    {
        for (int index = rows.Count - 1; index >= 0; index--)
        {
            RowView row = rows[index];
            if (row.rootObject == null)
            {
                rows.RemoveAt(index);
                continue;
            }

            if (gameplayDeltaTime > 0f)
            {
                Vector2 currentPosition = row.visualRect.anchoredPosition;
                Vector2 targetPosition = new Vector2(0f, row.targetY);
                row.visualRect.anchoredPosition = Vector2.MoveTowards(currentPosition, targetPosition, RowSlideSpeed * gameplayDeltaTime);

                if (row.status == RowStatus.Resolved)
                {
                    row.resolvedElapsed += gameplayDeltaTime;
                    if (row.resolvedElapsed >= ResolvedFlashDuration)
                    {
                        float fadeProgress = Mathf.Clamp01((row.resolvedElapsed - ResolvedFlashDuration) / ResolvedFadeDuration);
                        row.canvasGroup.alpha = 1f - fadeProgress;
                    }
                    if (row.resolvedElapsed >= ResolvedFlashDuration + ResolvedFadeDuration)
                    {
                        Destroy(row.rootObject);
                        Destroy(row.visualRect.gameObject);
                        rows.RemoveAt(index);
                        ReflowRows();
                        continue;
                    }
                }

                row.visualCanvasGroup.alpha = row.canvasGroup.alpha;
            }
        }
    }

    private void ReflowRows()
    {
        for (int index = 0; index < rows.Count; index++)
            rows[index].targetY = -index * (RowHeight + RowSpacing);
    }
}
