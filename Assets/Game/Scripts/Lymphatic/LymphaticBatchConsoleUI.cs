using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays ten batch slots and the command controls for the selected lymphatic batch.</summary>
[DisallowMultipleComponent]
public sealed class LymphaticBatchConsoleUI : MonoBehaviour
{
    private const string PanelName = "UnitBatchConsolePanel";
    private const int MaximumSlots = 10;
    private const float IconSize = 58f;
    private const float FollowZoom = 18f;
    private const string FontResourcePath = "BankGothicMediumSDF";

    private static readonly Color PanelColor = new Color(0.012f, 0.035f, 0.047f, 0.96f);
    private static readonly Color AccentColor = new Color(0.12f, 0.84f, 0.91f, 1f);
    private static readonly Color MainTextColor = new Color(0.9f, 0.96f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.55f, 0.74f, 0.78f, 1f);
    private static readonly Color EmptySlotColor = Color.black;
    private static readonly Color ActiveSlotColor = new Color(0.035f, 0.095f, 0.12f, 1f);
    private static readonly Color SelectedSlotColor = new Color(0.06f, 0.32f, 0.36f, 1f);
    private static readonly Color ReadyColor = new Color(0.25f, 0.9f, 0.4f, 1f);
    private static readonly Color MobilizingColor = new Color(0.25f, 0.65f, 1f, 1f);
    private static readonly Color WorkingColor = new Color(1f, 0.32f, 0.28f, 1f);

    private sealed class BatchSlot
    {
        public Button Button;
        public Image Background;
        public TextMeshProUGUI NameLabel;
        public TextMeshProUGUI StatusLabel;
        public TextMeshProUGUI TravelTimeLabel;
    }

    private readonly BatchSlot[] slots = new BatchSlot[MaximumSlots];
    private Canvas hudCanvas;
    private LymphaticBatchSpawner batchSpawner;
    private InfectionSpawner infectionSpawner;
    private CameraScript cameraScript;
    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private TextMeshProUGUI selectionHintLabel;
    private Button followButton;
    private Button abortButton;
    private Button destroyButton;
    private TextMeshProUGUI followButtonLabel;
    private int selectedBatchId = -1;
    private bool awaitingTargetSelection;
    private bool isFollowingBatch;
    private int followedBatchId = -1;
    private bool layerAvailable;

    /// <summary>Gets whether a selected ready batch is awaiting an infection target click.</summary>
    public bool IsAwaitingTargetSelection => awaitingTargetSelection;

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
        RefreshSlots();
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
            new Vector2(500f, 28f), new Vector2(-528f, 210f));
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;
        panelCanvasGroup = panelObject.GetComponent<CanvasGroup>();
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        panelObject.transform.SetAsLastSibling();

        CreateAccentBar("TopAccent", panelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero,
            Vector2.zero, new Vector2(0f, 3f), AccentColor);
        CreateText("PanelTitle", panelRect, "UNIT BATCHES", 13f, MainTextColor, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(180f, 22f));
        selectionHintLabel = CreateText("SelectionHint", panelRect, "Select a ready batch, then choose an infection.", 9f, MutedTextColor,
            TextAlignmentOptions.MidlineRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-14f, -8f), new Vector2(560f, 20f));
        CreateAccentBar("HeaderDivider", panelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(0f, 1f), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.42f));

        for (int index = 0; index < MaximumSlots; index++)
            slots[index] = CreateSlot(index);

        followButton = CreateActionButton("Follow", panelRect, 14f, 86f, out followButtonLabel);
        followButton.onClick.AddListener(ToggleFollow);
        abortButton = CreateActionButton("Abort Target", panelRect, 110f, 116f, out _);
        abortButton.onClick.AddListener(AbortSelectedBatch);
        destroyButton = CreateActionButton("Destroy Batch", panelRect, 236f, 126f, out _);
        destroyButton.onClick.AddListener(DestroySelectedBatch);
    }

    private BatchSlot CreateSlot(int index)
    {
        float anchorX = 0.05f + (index + 0.5f) * 0.09f;
        GameObject iconObject = new GameObject("BatchIcon_" + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        iconObject.transform.SetParent(panelRect, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetRect(iconRect, new Vector2(anchorX, 0.5f), new Vector2(anchorX, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 27f), new Vector2(IconSize, IconSize));
        Image background = iconObject.GetComponent<Image>();
        background.color = EmptySlotColor;
        background.raycastTarget = true;
        Button button = iconObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock slotColors = CreateButtonColors(ActiveSlotColor, SelectedSlotColor);
        slotColors.disabledColor = EmptySlotColor;
        button.colors = slotColors;
        int slotIndex = index;
        button.onClick.AddListener(() => SelectSlot(slotIndex));

        TextMeshProUGUI travelLabel = CreateText("TravelTime_" + (index + 1), panelRect, string.Empty, 9f, MobilizingColor,
            TextAlignmentOptions.Center, new Vector2(anchorX, 0.5f), new Vector2(anchorX, 0.5f), new Vector2(0.5f, 0f),
            new Vector2(0f, 61f), new Vector2(92f, 18f));
        TextMeshProUGUI nameLabel = CreateText("BatchName_" + (index + 1), panelRect, string.Empty, 8f, MainTextColor,
            TextAlignmentOptions.Center, new Vector2(anchorX, 0.5f), new Vector2(anchorX, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(0f, -7f), new Vector2(96f, 18f));
        TextMeshProUGUI statusLabel = CreateText("BatchStatus_" + (index + 1), panelRect, string.Empty, 8f, MutedTextColor,
            TextAlignmentOptions.Center, new Vector2(anchorX, 0.5f), new Vector2(anchorX, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(0f, -23f), new Vector2(96f, 16f));

        return new BatchSlot
        {
            Button = button,
            Background = background,
            NameLabel = nameLabel,
            StatusLabel = statusLabel,
            TravelTimeLabel = travelLabel
        };
    }

    /// <summary>Shows the batch console only on the circulatory layer and clears selection when leaving it.</summary>
    public void SetLayerAvailable(bool isAvailable)
    {
        layerAvailable = isAvailable;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = isAvailable ? 1f : 0f;
            panelCanvasGroup.interactable = isAvailable;
            panelCanvasGroup.blocksRaycasts = isAvailable;
        }

        if (isAvailable)
            return;

        awaitingTargetSelection = false;
        selectedBatchId = -1;
        if (isFollowingBatch && cameraScript != null)
            cameraScript.ClearFollowTarget();
        isFollowingBatch = false;
        followedBatchId = -1;
    }

    private void SubscribeToInfections()
    {
        if (infectionSpawner == null)
            return;
        infectionSpawner.OnInfectionTargetClicked -= HandleInfectionTargetClicked;
        infectionSpawner.OnInfectionTargetClicked += HandleInfectionTargetClicked;
    }

    private void RefreshSlots()
    {
        if (panelRect == null || batchSpawner == null)
            return;

        for (int index = 0; index < MaximumSlots; index++)
        {
            int batchId = index + 1;
            BatchSlot slot = slots[index];
            LymphaticBatchGroup batch = batchSpawner.GetBatch(batchId);
            bool isOccupied = batch != null;
            bool isSelected = isOccupied && batchId == selectedBatchId;
            slot.Button.interactable = isOccupied;
            slot.Background.color = !isOccupied ? EmptySlotColor : isSelected ? SelectedSlotColor : ActiveSlotColor;
            slot.NameLabel.enabled = isOccupied;
            slot.StatusLabel.enabled = isOccupied;
            slot.TravelTimeLabel.enabled = isOccupied && batch.Status == LymphaticBatchGroup.BatchStatus.Mobilizing;

            if (!isOccupied)
            {
                slot.NameLabel.text = string.Empty;
                slot.StatusLabel.text = string.Empty;
                slot.TravelTimeLabel.text = string.Empty;
                continue;
            }

            slot.NameLabel.text = batch.DisplayName;
            slot.StatusLabel.text = batch.Status.ToString();
            slot.StatusLabel.color = GetStatusColor(batch.Status);
            slot.TravelTimeLabel.text = batch.Status == LymphaticBatchGroup.BatchStatus.Mobilizing
                ? FormatTravelTime(batchSpawner.EstimateTravelTime(batch))
                : string.Empty;
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
        }
        else if (awaitingTargetSelection && selectedBatch.Status != LymphaticBatchGroup.BatchStatus.Ready)
        {
            awaitingTargetSelection = false;
        }

        followButton.interactable = selectedBatch != null && selectedBatch.HasEnteredCirculatorySystem;
        abortButton.interactable = selectedBatch != null && selectedBatch.Status != LymphaticBatchGroup.BatchStatus.Ready;
        destroyButton.interactable = selectedBatch != null;
        followButtonLabel.text = isFollowingBatch && followedBatchId == selectedBatchId ? "UNFOLLOW" : "FOLLOW";

        if (selectionHintLabel != null)
        {
            if (awaitingTargetSelection)
                selectionHintLabel.text = "Batch " + selectedBatchId + " selected — choose an infection icon or pending infection.";
            else if (selectedBatch != null && selectedBatch.Status != LymphaticBatchGroup.BatchStatus.Ready)
                selectionHintLabel.text = "Batch " + selectedBatchId + " selected — follow, abort target, or destroy.";
            else if (selectedBatch != null)
                selectionHintLabel.text = "Batch " + selectedBatchId + " selected — select an infection to mobilize.";
            else
                selectionHintLabel.text = "Select a ready batch, then choose an infection.";
        }
    }

    private void SelectSlot(int slotIndex)
    {
        if (!layerAvailable)
            return;

        int batchId = slotIndex + 1;
        LymphaticBatchGroup batch = batchSpawner != null ? batchSpawner.GetBatch(batchId) : null;
        if (batch == null)
            return;

        selectedBatchId = batchId;
        awaitingTargetSelection = batch.Status == LymphaticBatchGroup.BatchStatus.Ready;
        RefreshSlots();
    }

    private void HandleInfectionTargetClicked(InfectionSpawner.InfectionMarker marker)
    {
        if (!layerAvailable || !awaitingTargetSelection || selectedBatchId < 1 || batchSpawner == null || marker == null)
            return;

        if (!batchSpawner.DispatchBatch(selectedBatchId, marker, out string failureReason))
        {
            if (ConsoleLogUI.Instance != null)
            {
                string message = string.IsNullOrWhiteSpace(failureReason)
                    ? "Batch " + selectedBatchId + " could not reach the selected infection."
                    : failureReason;
                ConsoleLogUI.Instance.Log(message, ConsoleLogUI.LogType.Warning);
            }
            return;
        }

        awaitingTargetSelection = false;
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log("Batch " + selectedBatchId + " mobilizing to " + marker.infection.displayName + ".", ConsoleLogUI.LogType.Success);
        RefreshSlots();
    }

    private void ToggleFollow()
    {
        LymphaticBatchGroup batch = batchSpawner != null ? batchSpawner.GetBatch(selectedBatchId) : null;
        if (batch == null || !batch.HasEnteredCirculatorySystem || cameraScript == null)
            return;

        if (isFollowingBatch && followedBatchId == batch.BatchId)
        {
            cameraScript.ClearFollowTarget();
            isFollowingBatch = false;
            followedBatchId = -1;
            return;
        }

        Transform unitTransform = GetFirstLiveUnit(batch);
        if (unitTransform == null)
            return;

        cameraScript.SetFollowTarget(unitTransform, FollowZoom);
        isFollowingBatch = true;
        followedBatchId = batch.BatchId;
        RefreshSlots();
    }

    private void AbortSelectedBatch()
    {
        if (batchSpawner == null || !batchSpawner.AbortBatch(selectedBatchId))
            return;

        awaitingTargetSelection = false;
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log("Batch " + selectedBatchId + " target aborted; batch is ready for reassignment.", ConsoleLogUI.LogType.Info);
        RefreshSlots();
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
        RefreshSlots();
    }

    private static Transform GetFirstLiveUnit(LymphaticBatchGroup batch)
    {
        foreach (GameObject member in batch.Members)
        {
            if (member != null && member.activeInHierarchy)
                return member.transform;
        }

        return null;
    }

    private static Color GetStatusColor(LymphaticBatchGroup.BatchStatus status)
    {
        switch (status)
        {
            case LymphaticBatchGroup.BatchStatus.Ready: return ReadyColor;
            case LymphaticBatchGroup.BatchStatus.Mobilizing: return MobilizingColor;
            case LymphaticBatchGroup.BatchStatus.Working: return WorkingColor;
            default: return MutedTextColor;
        }
    }

    private static string FormatTravelTime(float seconds)
    {
        int roundedSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return string.Format("{0:00}:{1:00}", roundedSeconds / 60, roundedSeconds % 60);
    }

    private Button CreateActionButton(string label, RectTransform parent, float left, float width, out TextMeshProUGUI buttonLabel)
    {
        GameObject buttonObject = new GameObject("BatchAction_" + label.Replace(" ", string.Empty), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(left, 8f), new Vector2(width, 30f));
        Image image = buttonObject.GetComponent<Image>();
        image.color = ActiveSlotColor;
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateButtonColors(ActiveSlotColor, SelectedSlotColor);
        buttonLabel = CreateText("Label", rect, label.ToUpperInvariant(), 9f, MainTextColor, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
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
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(parent, false);
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
