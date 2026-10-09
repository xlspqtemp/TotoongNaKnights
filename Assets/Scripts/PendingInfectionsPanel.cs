using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Displays live infections in a fixed HUD panel.</summary>
[DefaultExecutionOrder(-100)]
public sealed class PendingInfectionsPanel : MonoBehaviour
{
    public enum RowStatus
    {
        Pending,
        Ongoing,
        Resolved,
        Failed
    }

    private const string PanelPrefabResourcePath = "PendingInfectionsPanel";
    private const string RowPrefabResourcePath = "PendingInfectionRow";
    private const string GameplaySceneName = "Game";
    private const string FontResourcePath = "BankGothicMediumSDF";
    private const string WhiteSwatchResourcePath = "HUDWhiteSwatch";
    private const string CollapsedPreferenceKey = "pendingPanelCollapsed";
    private const float PanelWidth = 340f;
    private const float PanelHeight = 270f;
    private const float PanelRightInset = 18f;
    private const float PanelTopOffset = 92f;
    private const float PanelSlideDuration = 0.25f;
    private const float PanelHiddenOffset = PanelWidth + PanelRightInset;
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
    [SerializeField] private Color failedColor = new Color(0.95f, 0.2f, 0.18f, 0.98f);
    [SerializeField] private bool startCollapsed = false;

    /// <summary>Raised when a live infection row is clicked, even if camera focus is currently blocked.</summary>
    public event System.Action<InfectionSpawner.InfectionMarker> OnRowClickedPublic;

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
        public Button button;
        public Image swatchImage;
        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI statusLabel;
        public float targetY;
        public float resolvedElapsed;
    }

    private readonly List<RowView> rows = new List<RowView>();
    private RectTransform panelContainerRect;
    private RectTransform panelRect;
    private RectTransform contentRect;
    private Canvas rootCanvas;
    private Button collapseButton;
    private TextMeshProUGUI collapseButtonLabel;
    private InfectionSpawner infectionSpawner;
    private Coroutine panelSlideCoroutine;
    private bool isCollapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= EnsurePanelForScene;
        SceneManager.sceneLoaded += EnsurePanelForScene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePanelExists()
    {
        EnsurePanelForScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void EnsurePanelForScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameplaySceneName ||
            Object.FindFirstObjectByType<PendingInfectionsPanel>() != null)
            return;

        GameObject panelPrefab = Resources.Load<GameObject>(PanelPrefabResourcePath);
        if (panelPrefab != null)
            Instantiate(panelPrefab);
    }

    private void Awake()
    {
        isCollapsed = PlayerPrefs.GetInt(CollapsedPreferenceKey, startCollapsed ? 1 : 0) != 0;
        ResolveResources();
        if (SceneManager.GetActiveScene().name == GameplaySceneName)
            TryBuildPanel();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        if (SceneManager.GetActiveScene().name != GameplaySceneName)
        {
            infectionSpawner = null;
            return;
        }

        infectionSpawner = Object.FindFirstObjectByType<InfectionSpawner>();
        ResolveResources();
        TryBuildPanel();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        infectionSpawner = null;
        panelSlideCoroutine = null;
        ClearAll();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAll();
        if (panelSlideCoroutine != null)
            StopCoroutine(panelSlideCoroutine);
        panelSlideCoroutine = null;

        if (scene.name != GameplaySceneName)
        {
            infectionSpawner = null;
            if (panelContainerRect != null)
                panelContainerRect.gameObject.SetActive(false);
            return;
        }

        infectionSpawner = Object.FindFirstObjectByType<InfectionSpawner>();
        ResolveResources();
        if (panelContainerRect == null)
            TryBuildPanel();
        else
            panelContainerRect.gameObject.SetActive(true);
        SyncRows();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != GameplaySceneName)
        {
            if (panelContainerRect != null)
                panelContainerRect.gameObject.SetActive(false);
            return;
        }

        if (panelContainerRect != null && !panelContainerRect.gameObject.activeSelf)
            panelContainerRect.gameObject.SetActive(true);

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
        row.button.onClick.AddListener(() => HandleRowClicked(row));
        rows.Add(row);
        ApplyRowStatusVisuals(row);
        ReflowRows();
    }

    private void HandleRowClicked(RowView row)
    {
        if (row == null || row.marker == null)
            return;

        OnRowClickedPublic?.Invoke(row.marker);
        if (row.marker.isRemoving || !CanFocusInfection())
            return;

        CameraScript cameraController = Object.FindFirstObjectByType<CameraScript>();
        if (cameraController != null)
            cameraController.FocusOnWorldPosition(row.marker.worldPosition);
    }

    private bool CanFocusInfection()
    {
        if (Time.timeScale <= 0f)
            return false;

        if (infectionSpawner == null)
            infectionSpawner = Object.FindFirstObjectByType<InfectionSpawner>();
        if (infectionSpawner != null && infectionSpawner.IsAwaitingInfectionTargetSelection)
            return false;

        GameplayTutorial tutorial = Object.FindFirstObjectByType<GameplayTutorial>();
        if (tutorial != null)
        {
            Transform tutorialOverlay = tutorial.transform.Find("GameplayTutorialOverlay");
            if (tutorialOverlay != null && tutorialOverlay.gameObject.activeInHierarchy)
                return false;
        }

        return true;
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
                MarkRemovedRow(row);
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
        if (row == null || row.status == RowStatus.Resolved || row.status == RowStatus.Failed || row.status == status)
            return;

        row.status = status;
        if (status == RowStatus.Resolved || status == RowStatus.Failed)
        {
            row.resolvedElapsed = 0f;
            row.canvasGroup.interactable = false;
            row.canvasGroup.blocksRaycasts = false;
            row.visualCanvasGroup.interactable = false;
            row.visualCanvasGroup.blocksRaycasts = false;
        }
        ApplyRowStatusVisuals(row);
    }

    private void MarkRemovedRow(RowView row)
    {
        if (row == null || row.status == RowStatus.Resolved || row.status == RowStatus.Failed)
            return;

        bool wasSquadCleared = row.marker != null && (row.marker.resolutionPending || row.marker.isContained);
        RowStatus removalStatus = wasSquadCleared ? RowStatus.Resolved : RowStatus.Failed;
        Debug.Log($"[PendingPanel] Row {row.displayName} removed as {removalStatus}.", this);
        SetRowStatus(row, removalStatus);
    }

    private void ResolveRow(RowView row)
    {
        SetRowStatus(row, RowStatus.Resolved);
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
        if (SceneManager.GetActiveScene().name != GameplaySceneName)
            return false;
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
        panelContainerRect = panelObject.GetComponent<RectTransform>();
        panelContainerRect.anchorMin = Vector2.one;
        panelContainerRect.anchorMax = Vector2.one;
        panelContainerRect.pivot = Vector2.one;
        panelContainerRect.anchoredPosition = new Vector2(-PanelRightInset, -PanelTopOffset);
        panelContainerRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image containerImage = panelObject.GetComponent<Image>();
        containerImage.color = Color.clear;
        containerImage.raycastTarget = false;

        GameObject panelBodyObject = CreateUiObject("Pending Infections Panel Body", panelContainerRect);
        panelRect = panelBodyObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = panelBodyObject.GetComponent<Image>();
        panelImage.sprite = whiteSwatchSprite;
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;
        Outline panelOutline = panelBodyObject.AddComponent<Outline>();
        panelOutline.effectColor = AccentColor;
        panelOutline.effectDistance = new Vector2(1.5f, -1.5f);
        panelOutline.useGraphicAlpha = true;

        TextMeshProUGUI header = CreateText("Pending Infections Header", panelRect, "PENDING INFECTIONS", 17f,
            FontStyles.Bold, AccentColor, TextAlignmentOptions.Left);
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        header.rectTransform.sizeDelta = new Vector2(-60f, HeaderHeight - 8f);

        GameObject toggleObject = CreateUiObject("Pending Infections Toggle", panelContainerRect);
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = Vector2.one;
        toggleRect.anchorMax = Vector2.one;
        toggleRect.pivot = Vector2.one;
        toggleRect.anchoredPosition = new Vector2(-10f, -8f);
        toggleRect.sizeDelta = new Vector2(30f, 28f);
        Image toggleImage = toggleObject.GetComponent<Image>();
        toggleImage.sprite = whiteSwatchSprite;
        toggleImage.color = new Color(0.035f, 0.09f, 0.13f, 0.98f);
        toggleImage.raycastTarget = true;
        Outline toggleOutline = toggleObject.AddComponent<Outline>();
        toggleOutline.effectColor = AccentColor;
        toggleOutline.effectDistance = new Vector2(1f, -1f);
        toggleOutline.useGraphicAlpha = true;
        collapseButton = toggleObject.AddComponent<Button>();
        collapseButton.targetGraphic = toggleImage;
        collapseButton.transition = Selectable.Transition.None;
        collapseButton.onClick.AddListener(TogglePanelCollapsed);
        collapseButtonLabel = CreateText("Pending Infections Toggle Glyph", toggleObject.transform, "▼", 15f,
            FontStyles.Bold, AccentColor, TextAlignmentOptions.Center);
        collapseButtonLabel.rectTransform.anchorMin = Vector2.zero;
        collapseButtonLabel.rectTransform.anchorMax = Vector2.one;
        collapseButtonLabel.rectTransform.offsetMin = Vector2.zero;
        collapseButtonLabel.rectTransform.offsetMax = Vector2.zero;

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
        ApplyPanelCollapsedState();

        return true;
    }

    private void TogglePanelCollapsed()
    {
        isCollapsed = !isCollapsed;
        if (collapseButtonLabel != null)
            collapseButtonLabel.text = isCollapsed ? "▶" : "▼";
        if (panelSlideCoroutine != null)
            StopCoroutine(panelSlideCoroutine);
        panelSlideCoroutine = StartCoroutine(AnimatePanelSlide(isCollapsed ? PanelHiddenOffset : 0f));
        PlayerPrefs.SetInt(CollapsedPreferenceKey, isCollapsed ? 1 : 0);
        PlayerPrefs.Save();
        SyncRows();
    }

    private void ApplyPanelCollapsedState()
    {
        if (panelRect != null)
            panelRect.anchoredPosition = new Vector2(isCollapsed ? PanelHiddenOffset : 0f, 0f);
        if (collapseButtonLabel != null)
            collapseButtonLabel.text = isCollapsed ? "▶" : "▼";
    }

    private IEnumerator AnimatePanelSlide(float targetX)
    {
        if (panelRect == null)
            yield break;

        float startX = panelRect.anchoredPosition.x;
        float elapsed = 0f;
        while (elapsed < PanelSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / PanelSlideDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
            Vector2 position = panelRect.anchoredPosition;
            position.x = Mathf.Lerp(startX, targetX, easedProgress);
            panelRect.anchoredPosition = position;
            yield return null;
        }

        Vector2 finalPosition = panelRect.anchoredPosition;
        finalPosition.x = targetX;
        panelRect.anchoredPosition = finalPosition;
        panelSlideCoroutine = null;
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
        row.visualCanvasGroup.interactable = true;
        row.visualCanvasGroup.blocksRaycasts = true;

        row.backgroundImage = visualObject.GetComponent<Image>();
        row.backgroundImage.sprite = whiteSwatchSprite;
        row.backgroundImage.raycastTarget = true;
        row.button = visualObject.AddComponent<Button>();
        row.button.targetGraphic = row.backgroundImage;
        row.button.transition = Selectable.Transition.None;

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
            case RowStatus.Failed:
                row.statusLabel.text = "Failed";
                row.backgroundImage.color = failedColor;
                row.swatchImage.color = row.isBacterial ? BacterialColor : ViralColor;
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

                if (row.status == RowStatus.Resolved || row.status == RowStatus.Failed)
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
