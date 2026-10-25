using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Builds the lymphatic training console and its visual-only training interactions.</summary>
[DisallowMultipleComponent]
public sealed class LymphaticTrainingConsoleUI : MonoBehaviour
{
    private const string ConsoleRootName = "LymphaticTrainingConsole";
    private const string ModalRootName = "LymphaticTrainingModal";
    private const float ConsoleHeight = 280f;
    private const float IconSize = 116f;
    private const int NeutrophilTrainingSeconds = 15;
    private const int MacrophageTrainingSeconds = 15;
    private const int TCellTrainingSeconds = 30;
    private const int BCellTrainingSeconds = 60;
    private const float DefaultModalPanelWidth = 760f;
    private const float DefaultModalPanelHeight = 620f;
    private const float VariantModalPanelHeight = 700f;
    private const float VariantRowHeight = 38f;
    private const float VariantRowGap = 5f;
    private const string BankGothicFontResourcePath = "BankGothicMediumSDF";

    private static readonly Color ConsoleBackgroundColor = new Color(0.012f, 0.035f, 0.047f, 0.96f);
    private static readonly Color IconBackgroundColor = new Color(0.035f, 0.095f, 0.12f, 1f);
    private static readonly Color IconHighlightColor = new Color(0.06f, 0.22f, 0.26f, 1f);
    private static readonly Color AccentColor = new Color(0.12f, 0.84f, 0.91f, 1f);
    private static readonly Color MainTextColor = new Color(0.9f, 0.96f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.55f, 0.74f, 0.78f, 1f);
    private static readonly Color DarkOverlayColor = new Color(0.008f, 0.02f, 0.027f, 0.88f);
    private static readonly Color ModalDimColor = new Color(0.005f, 0.012f, 0.018f, 0.78f);
    private static readonly Color ModalPanelColor = new Color(0.018f, 0.055f, 0.07f, 0.99f);
    private static readonly Color VariantRowColor = new Color(0.035f, 0.095f, 0.12f, 1f);
    private static readonly Color VariantSelectedColor = new Color(0.06f, 0.32f, 0.36f, 1f);

    private readonly TrainingSlot[] trainingSlots =
    {
        new TrainingSlot(new UnitDefinition(
            "Neutrophil", NeutrophilTrainingSeconds, "Fast", "Medium", "Low",
            "An early-response innate immune cell that rapidly moves to infection sites, engulfs pathogens, and releases antimicrobial substances.",
            "Rapid response to threats", "Reliable general-purpose defense", "Low durability", "Less suited to prolonged engagements")),
        new TrainingSlot(new UnitDefinition(
            "Macrophage", MacrophageTrainingSeconds, "Slow", "Medium", "High",
            "A large phagocytic immune cell that engulfs pathogens and cellular debris while supporting inflammation and tissue repair.",
            "High durability", "Useful for sustained defense", "Slow movement", "Less suited to chasing fast targets")),
        new TrainingSlot(new UnitDefinition(
            "T Cell", TCellTrainingSeconds, "High", "High", "Medium",
            "An adaptive immune lymphocyte that recognizes infected cells and coordinates or carries out a targeted cellular response.",
            "High speed and damage", "Specialized pathogen response", "Longer training time", "Less durable than a macrophage")),
        new TrainingSlot(new UnitDefinition(
            "B Cell", BCellTrainingSeconds, "Slow", "High", "High",
            "An adaptive immune lymphocyte that produces antibodies and can form memory responses against recognized pathogens.",
            "High durability and damage", "Can be specialized to a pathogen", "Slow movement", "Longest training time"))
    };

    private RectTransform consoleRect;
    private CanvasGroup consoleCanvasGroup;
    private TextMeshProUGUI batchCountLabel;
    private LymphaticBatchSpawner batchSpawner;
    private GameObject modalRoot;
    private RectTransform modalPanelRect;
    private Button variantConfirmButton;
    private UnitDefinition activeVariantUnit;
    private string selectedVariantName;
    private readonly Dictionary<string, Image> variantRowBackgrounds = new Dictionary<string, Image>();
    private bool layerAvailable;

    private void Awake()
    {
        Canvas hudCanvas = GetComponent<Canvas>();
        if (hudCanvas == null)
            hudCanvas = GetComponentInParent<Canvas>();
        if (hudCanvas == null)
        {
            Debug.LogError("LymphaticTrainingConsoleUI must be attached to HUDCanvas or one of its children.", this);
            enabled = false;
            return;
        }

        Transform existingConsole = hudCanvas.transform.Find(ConsoleRootName);
        if (existingConsole != null)
            Destroy(existingConsole.gameObject);

        Transform oldOrdersContainer = hudCanvas.transform.Find("LymphaticOrdersContainer");
        if (oldOrdersContainer != null)
            Destroy(oldOrdersContainer.gameObject);

        Transform existingModal = hudCanvas.transform.Find(ModalRootName);
        if (existingModal != null)
            Destroy(existingModal.gameObject);

        batchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        LymphaticBatchConsoleUI batchConsole = hudCanvas.GetComponent<LymphaticBatchConsoleUI>();
        if (batchConsole == null)
            batchConsole = hudCanvas.gameObject.AddComponent<LymphaticBatchConsoleUI>();
        batchConsole.Initialize(hudCanvas, batchSpawner);
        BuildConsole(hudCanvas.transform);
        BuildModalRoot(hudCanvas.transform);
        SetLayerAvailable(false);
    }

    private void Update()
    {
        RefreshBatchCountLabel();
        float deltaTime = GameplaySpeed.DeltaTime;
        if (deltaTime <= 0f)
            return;

        foreach (TrainingSlot slot in trainingSlots)
        {
            if (!slot.IsTraining)
                continue;

            slot.TimeRemaining = Mathf.Max(0f, slot.TimeRemaining - deltaTime);
            UpdateTrainingVisuals(slot);
            if (slot.TimeRemaining <= 0f)
            {
                slot.IsTraining = false;
                bool batchCreated = batchSpawner != null && batchSpawner.CompleteReservedBatch(
                    slot.ReservedNodeIndex, slot.Unit.Name, slot.ActiveVariantName);
                if (batchCreated)
                {
                    slot.HasCompleted = true;
                    slot.StatusLabel.text = "5 UNITS READY - NODE " + (slot.ReservedNodeIndex + 1);
                    slot.TimerLabel.text = "COMPLETE";
                    SetOverlayCoverage(slot, 0f);
                }
                else
                {
                    slot.HasCompleted = false;
                    slot.StatusLabel.text = "BATCH DEPLOYMENT FAILED";
                    slot.TimerLabel.text = "RETRY";
                    SetOverlayCoverage(slot, 0f);
                }

                slot.ReservedNodeIndex = -1;
            }
        }
    }

    /// <summary>Shows the training console only while the player is on the lymphatic layer.</summary>
    public void SetLayerAvailable(bool isAvailable)
    {
        layerAvailable = isAvailable;
        if (consoleCanvasGroup != null)
        {
            consoleCanvasGroup.alpha = isAvailable ? 1f : 0f;
            consoleCanvasGroup.interactable = isAvailable;
            consoleCanvasGroup.blocksRaycasts = isAvailable;
        }

        if (!isAvailable)
            CloseModal();
    }

    private void BuildConsole(Transform hudCanvas)
    {
        GameObject consoleObject = CreateUiObject(ConsoleRootName, hudCanvas, true);
        consoleRect = consoleObject.GetComponent<RectTransform>();
        SetRect(consoleRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(80f, -96f), new Vector2(760f, ConsoleHeight));

        Image consoleBackground = consoleObject.GetComponent<Image>();
        consoleBackground.color = ConsoleBackgroundColor;
        consoleBackground.raycastTarget = true;
        consoleCanvasGroup = consoleObject.AddComponent<CanvasGroup>();
        consoleCanvasGroup.alpha = 0f;
        consoleCanvasGroup.interactable = false;
        consoleCanvasGroup.blocksRaycasts = false;

        CreateAccentBar("TopAccent", consoleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 3f), AccentColor);
        batchCountLabel = CreateText("BatchCount", consoleRect, "BATCHES 0/10", 12f, AccentColor,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(22f, -13f), new Vector2(132f, 28f));
        CreateText("ConsoleTitle", consoleRect, "LYMPHATIC TRAINING CONSOLE", 17f, MainTextColor,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(164f, -13f), new Vector2(250f, 28f));
        CreateText("ConsoleInstructions", consoleRect, "LEFT CLICK TRAIN   |   RIGHT CLICK FIELD GUIDE", 10f, MutedTextColor,
            TextAlignmentOptions.MidlineRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-22f, -15f), new Vector2(320f, 24f));
        CreateAccentBar("HeaderDivider", consoleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -48f), new Vector2(0f, 1f), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.42f));

        for (int index = 0; index < trainingSlots.Length; index++)
            BuildTrainingSlot(consoleRect, trainingSlots[index], index);
    }

    private void BuildTrainingSlot(RectTransform parent, TrainingSlot slot, int index)
    {
        float horizontalCenter = (index + 0.5f) / trainingSlots.Length;
        slot.Button = CreateButton("UnitIcon_" + (index + 1), parent, out Image iconBackground,
            new Vector2(horizontalCenter, 0.5f), new Vector2(horizontalCenter, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 17f), new Vector2(IconSize, IconSize), IconBackgroundColor);
        slot.Button.targetGraphic = iconBackground;
        slot.Button.transition = Selectable.Transition.ColorTint;
        slot.Button.colors = CreateButtonColors(IconBackgroundColor, IconHighlightColor);
        slot.Button.onClick.AddListener(() => HandleLeftClick(slot));
        AddRightClickHandler(slot.Button.gameObject, () => ShowUnitInfo(slot.Unit));

        slot.IconRect = slot.Button.GetComponent<RectTransform>();
        GameObject overlayObject = CreateUiObject("TrainingDarkOverlay", slot.Button.transform, true);
        slot.OverlayRect = overlayObject.GetComponent<RectTransform>();
        SetRect(slot.OverlayRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, IconSize));
        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.color = DarkOverlayColor;
        overlayImage.raycastTarget = false;

        CreateText("UnitNumber", slot.Button.transform, (index + 1).ToString(), 44f, MainTextColor,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        slot.TimerLabel = CreateText("TrainingTimer", parent, FormatTrainingDuration(slot.Unit.TrainingSeconds), 13f, AccentColor,
            TextAlignmentOptions.Center, new Vector2(horizontalCenter, 1f), new Vector2(horizontalCenter, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -64f), new Vector2(150f, 24f));
        slot.NameLabel = CreateText("UnitName", parent, slot.Unit.Name.ToUpperInvariant(), 12f, MainTextColor,
            TextAlignmentOptions.Center, new Vector2(horizontalCenter, 0f), new Vector2(horizontalCenter, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 13f), new Vector2(230f, 20f));
        slot.StatusLabel = CreateText("TrainingStatus", parent, "READY", 9f, MutedTextColor,
            TextAlignmentOptions.Center, new Vector2(horizontalCenter, 0f), new Vector2(horizontalCenter, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(280f, 16f));
        slot.TimerLabel.raycastTarget = false;
        slot.NameLabel.raycastTarget = false;
        slot.StatusLabel.raycastTarget = false;
        SetOverlayCoverage(slot, 0f);
    }

    private void BuildModalRoot(Transform hudCanvas)
    {
        modalRoot = new GameObject(ModalRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalRoot.transform.SetParent(hudCanvas, false);
        RectTransform modalRootRect = modalRoot.GetComponent<RectTransform>();
        SetRect(modalRootRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image dimImage = modalRoot.GetComponent<Image>();
        dimImage.color = ModalDimColor;
        dimImage.raycastTarget = true;
        modalRoot.SetActive(false);
    }

    private void HandleLeftClick(TrainingSlot slot)
    {
        if (!layerAvailable || slot.IsTraining)
            return;

        if (slot.Unit.Name == "T Cell" || slot.Unit.Name == "B Cell")
        {
            ShowVariantSelection(slot.Unit);
            return;
        }

        StartTraining(slot, null);
    }

    private bool StartTraining(TrainingSlot slot, string variantName)
    {
        if (batchSpawner == null)
        {
            slot.StatusLabel.text = "BATCH MANAGER UNAVAILABLE";
            return false;
        }

        if (!batchSpawner.TryReserveBatch(out int reservedNodeIndex))
        {
            slot.StatusLabel.text = batchSpawner.CurrentBatchCount >= batchSpawner.MaximumBatches
                ? "BATCH CAPACITY FULL"
                : "NO REACHABLE EMPTY NODE";
            return false;
        }

        slot.ReservedNodeIndex = reservedNodeIndex;
        slot.IsTraining = true;
        slot.HasCompleted = false;
        slot.TrainingDuration = slot.Unit.TrainingSeconds;
        slot.TimeRemaining = slot.TrainingDuration;
        slot.ActiveVariantName = variantName;
        slot.StatusLabel.text = string.IsNullOrEmpty(variantName) ? "TRAINING - NODE " + (reservedNodeIndex + 1)
            : "TRAINING: " + variantName.ToUpperInvariant();
        UpdateTrainingVisuals(slot);
        return true;
    }

    private void RefreshBatchCountLabel()
    {
        if (batchCountLabel == null)
            return;

        int currentCount = batchSpawner != null ? batchSpawner.CurrentBatchCount : 0;
        int maximumCount = batchSpawner != null ? batchSpawner.MaximumBatches : 10;
        batchCountLabel.text = "BATCHES " + currentCount + "/" + maximumCount;
    }

    private void UpdateTrainingVisuals(TrainingSlot slot)
    {
        slot.TimerLabel.text = FormatCountdown(slot.TimeRemaining);
        float coverage = slot.TrainingDuration <= 0f ? 0f : slot.TimeRemaining / slot.TrainingDuration;
        SetOverlayCoverage(slot, coverage);
    }

    private static void SetOverlayCoverage(TrainingSlot slot, float coverage)
    {
        if (slot.OverlayRect == null)
            return;

        Vector2 size = slot.OverlayRect.sizeDelta;
        size.y = IconSize * Mathf.Clamp01(coverage);
        slot.OverlayRect.sizeDelta = size;
    }

    private void ShowUnitInfo(UnitDefinition unit)
    {
        if (!layerAvailable)
            return;

        ClearModalContents();
        modalRoot.SetActive(true);
        modalPanelRect = CreateModalPanel("UnitFieldGuide", DefaultModalPanelWidth, DefaultModalPanelHeight);
        CreateModalHeader(modalPanelRect, unit.Name.ToUpperInvariant() + " FIELD GUIDE", "CLOSE", CloseModal);

        CreateText("DescriptionHeader", modalPanelRect, "TEXTBOOK DESCRIPTION", 12f, AccentColor,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(32f, -81f), new Vector2(-64f, 22f));
        CreateText("UnitDescription", modalPanelRect, unit.Description, 17f, MainTextColor,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(32f, -107f), new Vector2(-64f, 82f));

        string[] statNames = { "SPEED", "DAMAGE", "HEALTH", "TRAINING" };
        string[] statValues = { unit.Speed, unit.Damage, unit.Health, FormatTrainingDuration(unit.TrainingSeconds) };
        for (int index = 0; index < statNames.Length; index++)
        {
            float center = (index + 0.5f) / statNames.Length;
            RectTransform statCard = CreateRect("StatCard_" + statNames[index], modalPanelRect);
            SetRect(statCard, new Vector2(center, 1f), new Vector2(center, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -210f), new Vector2(150f, 66f));
            Image statBackground = statCard.gameObject.AddComponent<Image>();
            statBackground.color = new Color(IconBackgroundColor.r, IconBackgroundColor.g, IconBackgroundColor.b, 0.92f);
            statBackground.raycastTarget = false;
            CreateText("StatName", statCard, statNames[index], 10f, MutedTextColor, TextAlignmentOptions.Center,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-8f, 20f));
            CreateText("StatValue", statCard, statValues[index], 18f, MainTextColor, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -20f));
        }

        CreateInfoColumn(modalPanelRect, "Pros", unit.Pros, new Vector2(0f, 0f), new Vector2(0.5f, 0f),
            new Vector2(32f, 26f), new Vector2(-42f, 154f), true);
        CreateInfoColumn(modalPanelRect, "Cons", unit.Cons, new Vector2(0.5f, 0f), new Vector2(1f, 0f),
            new Vector2(10f, 26f), new Vector2(-42f, 154f), false);
    }

    private void ShowVariantSelection(UnitDefinition unit)
    {
        if (!layerAvailable)
            return;

        ClearModalContents();
        activeVariantUnit = unit;
        selectedVariantName = null;
        variantRowBackgrounds.Clear();
        modalRoot.SetActive(true);
        modalPanelRect = CreateModalPanel("VariantSelection", DefaultModalPanelWidth, VariantModalPanelHeight);
        CreateModalHeader(modalPanelRect, "SELECT " + unit.Name.ToUpperInvariant() + " VARIANT", "BACK", CloseModal);

        CreateText("VariantInstructions", modalPanelRect, "Choose a named bacterial or viral profile, then confirm training.", 14f, MutedTextColor,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(32f, -74f), new Vector2(-64f, 28f));

        RectTransform viewportRect = CreateRect("VariantViewport", modalPanelRect);
        SetRect(viewportRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -112f), new Vector2(-64f, 470f));
        Image viewportImage = viewportRect.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0.008f, 0.025f, 0.034f, 0.9f);
        viewportImage.raycastTarget = true;
        Mask mask = viewportRect.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform contentRect = CreateRect("VariantList", viewportRect);
        SetRect(contentRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = VariantRowGap;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = viewportRect.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 35f;

        IReadOnlyList<string> pathogenNames = InfectionSpawner.GetKnownTrainingPathogenNames();
        for (int index = 0; index < pathogenNames.Count; index++)
            CreateVariantRow(contentRect, unit, pathogenNames[index]);

        variantConfirmButton = CreateButton("ConfirmTraining", modalPanelRect, out Image confirmBackground,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 24f), new Vector2(220f, 46f), new Color(0.025f, 0.28f, 0.32f, 0.7f));
        variantConfirmButton.targetGraphic = confirmBackground;
        variantConfirmButton.transition = Selectable.Transition.ColorTint;
        variantConfirmButton.colors = CreateButtonColors(new Color(0.025f, 0.28f, 0.32f, 0.7f), VariantSelectedColor);
        variantConfirmButton.interactable = false;
        CreateText("ConfirmLabel", variantConfirmButton.transform, "CONFIRM TRAINING", 14f, MainTextColor,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        variantConfirmButton.onClick.AddListener(ConfirmVariantTraining);
    }

    private void CreateVariantRow(RectTransform content, UnitDefinition unit, string pathogenName)
    {
        GameObject rowObject = CreateUiObject(unit.Name + " - " + pathogenName, content, true);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, VariantRowHeight);
        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = VariantRowHeight;
        rowLayout.preferredHeight = VariantRowHeight;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = VariantRowColor;
        Button rowButton = rowObject.AddComponent<Button>();
        rowButton.targetGraphic = rowImage;
        rowButton.transition = Selectable.Transition.None;
        string rowName = unit.Name + " - " + pathogenName;
        rowButton.onClick.AddListener(() => SelectVariant(rowName));
        CreateText("VariantName", rowRect, rowName, 14f, MainTextColor, TextAlignmentOptions.MidlineLeft,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(16f, 0f), new Vector2(-32f, 0f));
        variantRowBackgrounds.Add(rowName, rowImage);
    }

    private void SelectVariant(string variantName)
    {
        selectedVariantName = variantName;
        foreach (KeyValuePair<string, Image> row in variantRowBackgrounds)
            row.Value.color = row.Key == variantName ? VariantSelectedColor : VariantRowColor;
        if (variantConfirmButton != null)
        {
            variantConfirmButton.interactable = true;
            Image confirmBackground = variantConfirmButton.GetComponent<Image>();
            if (confirmBackground != null)
                confirmBackground.color = new Color(0.035f, 0.48f, 0.52f, 1f);
        }
    }

    private void ConfirmVariantTraining()
    {
        if (activeVariantUnit == null || string.IsNullOrEmpty(selectedVariantName))
            return;

        TrainingSlot slot = FindSlot(activeVariantUnit);
        if (slot == null || slot.IsTraining)
        {
            CloseModal();
            return;
        }

        string pathogenName = selectedVariantName.Substring(activeVariantUnit.Name.Length + 3);
        if (StartTraining(slot, pathogenName))
            CloseModal();
    }

    private TrainingSlot FindSlot(UnitDefinition unit)
    {
        foreach (TrainingSlot slot in trainingSlots)
        {
            if (slot.Unit == unit)
                return slot;
        }

        return null;
    }

    private void CreateInfoColumn(RectTransform parent, string heading, string content, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, bool isPositive)
    {
        RectTransform columnRect = CreateRect(heading + "Column", parent);
        SetRect(columnRect, anchorMin, anchorMax, new Vector2(0f, 0f), position, size);
        Image columnBackground = columnRect.gameObject.AddComponent<Image>();
        columnBackground.color = new Color(IconBackgroundColor.r, IconBackgroundColor.g, IconBackgroundColor.b, 0.86f);
        columnBackground.raycastTarget = false;
        CreateText(heading + "Header", columnRect, heading.ToUpperInvariant(), 12f, isPositive ? AccentColor : new Color(1f, 0.58f, 0.46f, 1f),
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -8f), new Vector2(-28f, 22f));
        CreateText(heading + "Text", columnRect, "• " + content.Replace("; ", "\n• "), 14f, MainTextColor,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -34f), new Vector2(-28f, 104f));
    }

    private RectTransform CreateModalPanel(string panelName, float width, float height)
    {
        GameObject panelObject = CreateUiObject(panelName, modalRoot.transform, true);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        SetRect(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(width, height));
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = ModalPanelColor;
        panelImage.raycastTarget = true;
        CreateAccentBar("ModalTopAccent", panelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 3f), AccentColor);
        return panelRect;
    }

    private void CreateModalHeader(RectTransform parent, string title, string backLabel, Action backAction)
    {
        CreateText("ModalTitle", parent, title, 18f, MainTextColor, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(30f, -28f), new Vector2(-190f, 38f));
        Button backButton = CreateButton("ModalBackButton", parent, out Image backBackground,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -20f),
            new Vector2(118f, 38f), IconBackgroundColor);
        backButton.targetGraphic = backBackground;
        backButton.transition = Selectable.Transition.ColorTint;
        backButton.colors = CreateButtonColors(IconBackgroundColor, IconHighlightColor);
        backButton.onClick.AddListener(() => backAction());
        CreateText("ModalBackLabel", backButton.transform, backLabel, 11f, MainTextColor,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    private void ClearModalContents()
    {
        for (int index = modalRoot.transform.childCount - 1; index >= 0; index--)
            Destroy(modalRoot.transform.GetChild(index).gameObject);
        modalPanelRect = null;
        variantConfirmButton = null;
        activeVariantUnit = null;
        selectedVariantName = null;
        variantRowBackgrounds.Clear();
    }

    private void CloseModal()
    {
        if (modalRoot != null)
        {
            ClearModalContents();
            modalRoot.SetActive(false);
        }
    }

    private static void AddRightClickHandler(GameObject target, Action onRightClick)
    {
        EventTrigger trigger = target.AddComponent<EventTrigger>();
        EventTrigger.Entry pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        pointerClick.callback.AddListener(eventData =>
        {
            PointerEventData pointerEvent = eventData as PointerEventData;
            if (pointerEvent != null && pointerEvent.button == PointerEventData.InputButton.Right)
                onRightClick();
        });
        trigger.triggers.Add(pointerClick);
    }

    private static Button CreateButton(string objectName, RectTransform parent, out Image background,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent, true);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetRect(buttonRect, anchorMin, anchorMax, pivot, anchoredPosition, size);
        background = buttonObject.GetComponent<Image>();
        background.color = color;
        background.raycastTarget = true;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        return button;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, bool withImage)
    {
        GameObject uiObject = withImage
            ? new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = CreateUiObject(objectName, parent, false);
        return rectObject.GetComponent<RectTransform>();
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
        text.font = Resources.Load<TMP_FontAsset>(BankGothicFontResourcePath) ?? TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(8f, fontSize - 3f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateAccentBar(string objectName, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject barObject = CreateUiObject(objectName, parent, true);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        SetRect(barRect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image barImage = barObject.GetComponent<Image>();
        barImage.color = color;
        barImage.raycastTarget = false;
    }

    private static ColorBlock CreateButtonColors(Color normalColor, Color highlightedColor)
    {
        return new ColorBlock
        {
            normalColor = normalColor,
            highlightedColor = highlightedColor,
            pressedColor = new Color(highlightedColor.r, highlightedColor.g, highlightedColor.b, 1f),
            selectedColor = highlightedColor,
            disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.55f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
    }

    private static string FormatTrainingDuration(int seconds)
    {
        return seconds >= 60 ? string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60) : seconds + "s";
    }

    private static string FormatCountdown(float seconds)
    {
        int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return remainingSeconds >= 60
            ? string.Format("{0:00}:{1:00}", remainingSeconds / 60, remainingSeconds % 60)
            : remainingSeconds.ToString("00") + "s";
    }

    private sealed class TrainingSlot
    {
        public readonly UnitDefinition Unit;
        public Button Button;
        public RectTransform IconRect;
        public RectTransform OverlayRect;
        public TextMeshProUGUI TimerLabel;
        public TextMeshProUGUI NameLabel;
        public TextMeshProUGUI StatusLabel;
        public bool IsTraining;
        public bool HasCompleted;
        public float TrainingDuration;
        public float TimeRemaining;
        public string ActiveVariantName;
        public int ReservedNodeIndex = -1;

        public TrainingSlot(UnitDefinition unit)
        {
            Unit = unit;
        }
    }

    private sealed class UnitDefinition
    {
        public readonly string Name;
        public readonly int TrainingSeconds;
        public readonly string Speed;
        public readonly string Damage;
        public readonly string Health;
        public readonly string Description;
        public readonly string Pros;
        public readonly string Cons;

        public UnitDefinition(string name, int trainingSeconds, string speed, string damage, string health,
            string description, string pros, string secondaryPro, string cons, string secondaryCon)
        {
            Name = name;
            TrainingSeconds = trainingSeconds;
            Speed = speed;
            Damage = damage;
            Health = health;
            Description = description;
            Pros = pros + "; " + secondaryPro;
            Cons = cons + "; " + secondaryCon;
        }
    }
}
