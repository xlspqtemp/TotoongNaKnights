using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays every completed lymphatic batch and its follow, abort, and destroy controls.</summary>
[DisallowMultipleComponent]
public sealed class LymphaticBatchConsoleUI : MonoBehaviour
{
    private const string PanelName = "UnitBatchConsolePanel";
    private const float PanelHeight = 280f;
    private const float PanelLeftOffset = 500f;
    private const float PanelRightMargin = 28f;
    private const float RowHeight = 18f;
    private const float RowTopOffset = 66f;
    private const int MaximumRows = 10;
    private const string FontResourcePath = "BankGothicMediumSDF";

    private static readonly Color PanelColor = new Color(0.012f, 0.035f, 0.047f, 0.96f);
    private static readonly Color AccentColor = new Color(0.12f, 0.84f, 0.91f, 1f);
    private static readonly Color MainTextColor = new Color(0.9f, 0.96f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.55f, 0.74f, 0.78f, 1f);
    private static readonly Color RowColor = new Color(0.035f, 0.095f, 0.12f, 0.92f);
    private static readonly Color SelectedRowColor = new Color(0.06f, 0.32f, 0.36f, 1f);

    private sealed class BatchRow
    {
        public GameObject Root;
        public Button Button;
        public Image Background;
        public TextMeshProUGUI IdLabel;
        public TextMeshProUGUI NameLabel;
        public TextMeshProUGUI StatusLabel;
        public TextMeshProUGUI TravelTimeLabel;
        public LymphaticBatchGroup Batch;
    }

    private readonly List<LymphaticBatchGroup> visibleBatches = new List<LymphaticBatchGroup>(MaximumRows);
    private readonly BatchRow[] rows = new BatchRow[MaximumRows];
    private Canvas hudCanvas;
    private LymphaticBatchSpawner batchSpawner;
    private InfectionSpawner infectionSpawner;
    private CameraScript cameraScript;
    private RectTransform panelRect;
    private TextMeshProUGUI selectionHintLabel;
    private Button followButton;
    private Button abortButton;
    private Button destroyButton;
    private TextMeshProUGUI followButtonLabel;
    private int selectedBatchId = -1;
    private bool awaitingTargetSelection;
    private bool isFollowingBatch;
    private int followedBatchId = -1;

    private void Start()
    {
        if (hudCanvas == null)
        {
            hudCanvas = GetComponent<Canvas>();
            if (hudCanvas == null)
                hudCanvas = GetComponentInParent<Canvas>();
        }

        if (batchSpawner == null)
            batchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (infectionSpawner == null)
            infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (cameraScript == null)
            cameraScript = FindFirstObjectByType<CameraScript>();

        if (panelRect == null && hudCanvas != null)
            BuildPanel(hudCanvas.transform);
        SubscribeToInfections();
    }

    private void Update()
    {
        if (batchSpawner == null)
            batchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (infectionSpawner == null)
            infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (cameraScript == null)
            cameraScript = FindFirstObjectByType<CameraScript>();
        SubscribeToInfections();
        RefreshRows();
    }

    private void OnDisable()
    {
        if (infectionSpawner != null)
            infectionSpawner.OnInfectionTargetClicked -= HandleInfectionTargetClicked;
        if (isFollowingBatch && cameraScript != null)
            cameraScript.ClearFollowTarget();
        isFollowingBatch = false;
        followedBatchId = -1;
    }

    /// <summary>Initializes the panel with the active HUD canvas and lymphatic batch manager.</summary>
    public void Initialize(Canvas canvas, LymphaticBatchSpawner spawner)
    {
        hudCanvas = canvas;
        batchSpawner = spawner;
        if (hudCanvas != null && panelRect == null)
            BuildPanel(hudCanvas.transform);
        if (infectionSpawner == null)
            infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (cameraScript == null)
            cameraScript = FindFirstObjectByType<CameraScript>();
        SubscribeToInfections();
    }

    private void BuildPanel(Transform parent)
    {
        Transform existing = parent.Find(PanelName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(parent, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        SetRect(panelRect, Vector2.zero, new Vector2(1f, 0f), Vector2.zero,
            new Vector2(PanelLeftOffset, 28f), new Vector2(-PanelLeftOffset - PanelRightMargin, PanelHeight));
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;
        panelObject.transform.SetAsLastSibling();

        CreateAccentBar("TopAccent", panelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero,
            Vector2.zero, new Vector2(0f, 3f), AccentColor);
        CreateText("PanelTitle", panelRect, "UNIT BATCHES", 15f, MainTextColor, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(200f, 24f));
        selectionHintLabel = CreateText("SelectionHint", panelRect, "Select a ready batch, then choose an infection.", 10f, MutedTextColor,
            TextAlignmentOptions.MidlineRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -10f), new Vector2(600f, 22f));
        CreateAccentBar("HeaderDivider", panelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -39f), new Vector2(0f, 1f), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.42f));

        CreateColumnHeader("Batch ID", 0.02f, 0.12f);
        CreateColumnHeader("Batch Name", 0.13f, 0.57f);
        CreateColumnHeader("Status", 0.58f, 0.79f);
        CreateColumnHeader("Travel Time", 0.80f, 0.98f);

        for (int index = 0; index < MaximumRows; index++)
            rows[index] = CreateBatchRow(index);

        followButton = CreateActionButton("Follow", panelRect, 16f, 86f, out followButtonLabel);
        followButton.onClick.AddListener(ToggleFollow);
        abortButton = CreateActionButton("Abort Target", panelRect, 112f, 116f, out _);
        abortButton.onClick.AddListener(AbortSelectedBatch);
        destroyButton = CreateActionButton("Destroy Batch", panelRect, 238f, 126f, out _);
        destroyButton.onClick.AddListener(DestroySelectedBatch);
    }

    private BatchRow CreateBatchRow(int index)
    {
        GameObject rowObject = new GameObject("UnitBatchRow_" + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        rowObject.transform.SetParent(panelRect, false);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        float rowTop = RowTopOffset + index * RowHeight;
        SetRect(rowRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -rowTop), new Vector2(-16f, RowHeight - 1f));
        Image background = rowObject.GetComponent<Image>();
        background.color = RowColor;
        background.raycastTarget = true;
        Button button = rowObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateButtonColors(RowColor, SelectedRowColor);
        int rowIndex = index;
        button.onClick.AddListener(() => SelectBatchRow(rowIndex));

        BatchRow row = new BatchRow
        {
            Root = rowObject,
            Button = button,
            Background = background,
            IdLabel = CreateRowText("BatchId", rowRect, 0.02f, 0.12f, TextAlignmentOptions.MidlineLeft),
            NameLabel = CreateRowText("BatchName", rowRect, 0.13f, 0.57f, TextAlignmentOptions.MidlineLeft),
            StatusLabel = CreateRowText("BatchStatus", rowRect, 0.58f, 0.79f, TextAlignmentOptions.MidlineLeft),
            TravelTimeLabel = CreateRowText("TravelTime", rowRect, 0.80f, 0.98f, TextAlignmentOptions.MidlineRight)
        };
        rowObject.SetActive(false);
        return row;
    }

    private void CreateColumnHeader(string value, float anchorMinX, float anchorMaxX)
    {
        CreateText("Column_" + value.Replace(" ", string.Empty), panelRect, value.ToUpperInvariant(), 9f, AccentColor,
            TextAlignmentOptions.MidlineLeft, new Vector2(anchorMinX, 1f), new Vector2(anchorMaxX, 1f),
            new Vector2(0f, 1f), new Vector2(0f, -45f), new Vector2(0f, 18f));
    }

    private TextMeshProUGUI CreateRowText(string objectName, RectTransform parent, float anchorMinX, float anchorMaxX,
        TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = CreateText(objectName, parent, string.Empty, 9f, MainTextColor, alignment,
            new Vector2(anchorMinX, 0f), new Vector2(anchorMaxX, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        label.margin = new Vector4(2f, 0f, 2f, 0f);
        return label;
    }

    private Button CreateActionButton(string label, RectTransform parent, float left, float width, out TextMeshProUGUI buttonLabel)
    {
        GameObject buttonObject = new GameObject("BatchAction_" + label.Replace(" ", string.Empty), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(left, 8f), new Vector2(width, 30f));
        Image image = buttonObject.GetComponent<Image>();
        image.color = RowColor;
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateButtonColors(RowColor, SelectedRowColor);
        buttonLabel = CreateText("Label", rect, label.ToUpperInvariant(), 9f, MainTextColor, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    private void SubscribeToInfections()
    {
        if (infectionSpawner == null)
            return;
        infectionSpawner.OnInfectionTargetClicked -= HandleInfectionTargetClicked;
        infectionSpawner.OnInfectionTargetClicked += HandleInfectionTargetClicked;
    }

    private void RefreshRows()
    {
        if (panelRect == null || batchSpawner == null)
            return;

        batchSpawner.GetBatches(visibleBatches);
        for (int index = 0; index < MaximumRows; index++)
        {
            BatchRow row = rows[index];
            if (index >= visibleBatches.Count)
            {
                row.Batch = null;
                row.Root.SetActive(false);
                continue;
            }

            LymphaticBatchGroup batch = visibleBatches[index];
            row.Batch = batch;
            row.Root.SetActive(true);
            row.IdLabel.text = batch.BatchId.ToString();
            row.NameLabel.text = batch.DisplayName;
            row.StatusLabel.text = batch.Status.ToString().ToUpperInvariant();
            row.TravelTimeLabel.text = batch.Status == LymphaticBatchGroup.BatchStatus.Mobilizing
                ? FormatTravelTime(batchSpawner.EstimateTravelTime(batch))
                : "—";
            bool selected = batch.BatchId == selectedBatchId;
            row.Background.color = selected ? SelectedRowColor : RowColor;
            row.Button.interactable = true;
            row.StatusLabel.color = batch.Status == LymphaticBatchGroup.BatchStatus.Working ? AccentColor : MainTextColor;
        }

        if (isFollowingBatch && batchSpawner.GetBatch(followedBatchId) == null)
        {
            if (cameraScript != null)
                cameraScript.ClearFollowTarget();
            isFollowingBatch = false;
            followedBatchId = -1;
        }

        LymphaticBatchGroup selectedBatch = batchSpawner.GetBatch(selectedBatchId);
        if (selectedBatch == null)
        {
            selectedBatchId = -1;
            awaitingTargetSelection = false;
            selectedBatch = null;
        }
        else if (awaitingTargetSelection && selectedBatch.Status != LymphaticBatchGroup.BatchStatus.Ready)
        {
            awaitingTargetSelection = false;
        }

        followButton.interactable = selectedBatch != null;
        abortButton.interactable = selectedBatch != null && selectedBatch.Status != LymphaticBatchGroup.BatchStatus.Ready;
        destroyButton.interactable = selectedBatch != null;
        followButtonLabel.text = isFollowingBatch && followedBatchId == selectedBatchId ? "UNFOLLOW" : "FOLLOW";
        if (selectionHintLabel != null)
        {
            if (awaitingTargetSelection)
                selectionHintLabel.text = "Batch " + selectedBatchId + " selected — choose a pending infection or map icon.";
            else if (selectedBatch != null && selectedBatch.Status != LymphaticBatchGroup.BatchStatus.Ready)
                selectionHintLabel.text = "Batch " + selectedBatchId + " selected — follow, abort target, or destroy.";
            else if (selectedBatch != null)
                selectionHintLabel.text = "Batch " + selectedBatchId + " selected — select an infection to mobilize.";
            else
                selectionHintLabel.text = "Select a ready batch, then choose an infection.";
        }
    }

    private void SelectBatchRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= rows.Length || rows[rowIndex].Batch == null)
            return;

        LymphaticBatchGroup batch = rows[rowIndex].Batch;
        selectedBatchId = batch.BatchId;
        awaitingTargetSelection = batch.Status == LymphaticBatchGroup.BatchStatus.Ready;
        RefreshRows();
    }

    private void HandleInfectionTargetClicked(InfectionSpawner.InfectionMarker marker)
    {
        if (!awaitingTargetSelection || selectedBatchId < 1 || batchSpawner == null || marker == null)
            return;

        if (!batchSpawner.DispatchBatch(selectedBatchId, marker))
        {
            if (ConsoleLogUI.Instance != null)
                ConsoleLogUI.Instance.Log("Batch " + selectedBatchId + " could not reach the selected infection.", ConsoleLogUI.LogType.Warning);
            return;
        }

        awaitingTargetSelection = false;
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log("Batch " + selectedBatchId + " mobilizing to " + marker.infection.displayName + ".", ConsoleLogUI.LogType.Success);
        RefreshRows();
    }

    private void ToggleFollow()
    {
        LymphaticBatchGroup batch = batchSpawner != null ? batchSpawner.GetBatch(selectedBatchId) : null;
        if (batch == null || cameraScript == null)
            return;

        if (isFollowingBatch && followedBatchId == batch.BatchId)
        {
            cameraScript.ClearFollowTarget();
            isFollowingBatch = false;
            followedBatchId = -1;
            return;
        }

        cameraScript.SetFollowTarget(batch.transform);
        isFollowingBatch = true;
        followedBatchId = batch.BatchId;
        RefreshRows();
    }

    private void AbortSelectedBatch()
    {
        if (batchSpawner == null || !batchSpawner.AbortBatch(selectedBatchId))
            return;

        awaitingTargetSelection = false;
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log("Batch " + selectedBatchId + " target aborted; batch is ready for reassignment.", ConsoleLogUI.LogType.Info);
        RefreshRows();
    }

    private void DestroySelectedBatch()
    {
        if (batchSpawner == null || !batchSpawner.DestroyBatch(selectedBatchId))
            return;

        if (isFollowingBatch && followedBatchId == selectedBatchId && cameraScript != null)
        {
            cameraScript.ClearFollowTarget();
            isFollowingBatch = false;
            followedBatchId = -1;
        }

        int destroyedBatchId = selectedBatchId;
        selectedBatchId = -1;
        awaitingTargetSelection = false;
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log("Batch " + destroyedBatchId + " destroyed; batch slot released.", ConsoleLogUI.LogType.Warning);
        RefreshRows();
    }

    private static string FormatTravelTime(float seconds)
    {
        int roundedSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return string.Format("{0:00}:{1:00}", roundedSeconds / 60, roundedSeconds % 60);
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
        rect.localRotation = Quaternion.identity;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, bool withImage)
    {
        GameObject uiObject = withImage
            ? new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float fontSize,
        Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = Resources.Load<TMP_FontAsset>(FontResourcePath) ?? TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(7f, fontSize - 2f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateAccentBar(string objectName, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject barObject = CreateUiObject(objectName, parent, true);
        RectTransform rect = barObject.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = barObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static ColorBlock CreateButtonColors(Color normalColor, Color highlightedColor)
    {
        return new ColorBlock
        {
            normalColor = normalColor,
            highlightedColor = highlightedColor,
            pressedColor = highlightedColor,
            selectedColor = highlightedColor,
            disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
    }
}
