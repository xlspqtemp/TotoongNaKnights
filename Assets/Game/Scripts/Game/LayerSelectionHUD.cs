using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the four explicit body-system controls and keeps the HUD layout responsive.
/// </summary>
internal static class LayerSelectionHUD
{
    private const string GameSceneName = "Game";
    private const string HudCanvasName = "HUDCanvas";
    private const string SelectorObjectName = "LayerSelectionButton";
    private const string ControlsObjectName = "SystemLayerButtons";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const float LayerToggleHeight = 36f;
    private const float LayerToggleGap = 8f;

    private static readonly Color NormalButtonColor = new Color(0.035f, 0.065f, 0.085f, 0.96f);
    private static readonly Color SelectedButtonColor = new Color(0.055f, 0.24f, 0.28f, 1f);
    private static readonly Color AccentColor = new Color(0.12f, 0.78f, 0.88f, 1f);
    private static readonly Color LabelColor = new Color(0.92f, 0.96f, 0.97f, 1f);

    private static Button[] layerButtons;
    private static Image[] layerButtonBackgrounds;
    private static Outline[] layerButtonOutlines;
    private static RectTransform layerRowsRoot;
    private static HashSet<string>[] activeThreatKeys;
    private static readonly Dictionary<string, int> ActiveThreatLayers = new Dictionary<string, int>();
    private static int selectedLayer = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;
        ActiveThreatLayers.Clear();
        activeThreatKeys = null;
        layerButtons = null;
        layerButtonBackgrounds = null;
        layerButtonOutlines = null;
        layerRowsRoot = null;
        if (scene.name != GameSceneName)
            return;

        GameObject canvasObject = GameObject.Find(HudCanvasName);
        Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            Debug.LogWarning("System layer controls could not find HUDCanvas in the Game scene.");
            return;
        }

        Transform existingSelector = canvas.transform.Find(SelectorObjectName);
        if (existingSelector != null)
            Object.Destroy(existingSelector.gameObject);

        Transform existingControls = canvas.transform.Find(ControlsObjectName);
        if (existingControls != null)
            Object.Destroy(existingControls.gameObject);

        RectTransform snapshotPanel = canvas.transform.Find("HumanSnapshotPanel") as RectTransform;
        RectTransform dayPanel = canvas.transform.Find("DayCounterPanel") as RectTransform;
        RectTransform consolePanel = canvas.transform.Find("ConsoleLogPanel") as RectTransform;
        RectTransform controlsRoot = CreateLayerControls(canvas.transform, canvas.transform as RectTransform,
            out RectTransform toggleRect);

        ResponsiveHudLayout responsiveLayout = canvas.GetComponent<ResponsiveHudLayout>();
        if (responsiveLayout == null)
            responsiveLayout = canvas.gameObject.AddComponent<ResponsiveHudLayout>();
        responsiveLayout.Configure(snapshotPanel, controlsRoot, dayPanel, consolePanel, layerRowsRoot, toggleRect);
        SetSelectedLayer(1);
        RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
        GameplayTutorial tutorial = canvas.GetComponent<GameplayTutorial>();
        if (tutorial == null)
            canvas.gameObject.AddComponent<GameplayTutorial>();
    }

    private static RectTransform CreateLayerControls(Transform parent, RectTransform canvasRect, out RectTransform toggleRect)
    {
        GameObject rootObject = new GameObject(ControlsObjectName, typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.localScale = Vector3.one;

        GameObject toggleObject = new GameObject(SelectorObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        toggleObject.transform.SetParent(rootRect, false);
        toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(0f, 1f);
        toggleRect.pivot = new Vector2(0f, 1f);
        toggleRect.anchoredPosition = Vector2.zero;
        toggleRect.sizeDelta = new Vector2(300f, LayerToggleHeight);
        Image toggleBackground = toggleObject.GetComponent<Image>();
        toggleBackground.color = SelectedButtonColor;
        toggleBackground.raycastTarget = true;
        Button toggleButton = toggleObject.GetComponent<Button>();
        toggleButton.targetGraphic = toggleBackground;
        toggleButton.transition = Selectable.Transition.ColorTint;
        toggleButton.colors = CreateButtonColors(SelectedButtonColor);
        CreateLabel("LayerToggleLabel", toggleRect, "Layers  −", 14f, TextAlignmentOptions.MidlineLeft);
        TextMeshProUGUI toggleLabel = toggleRect.Find("LayerToggleLabel").GetComponent<TextMeshProUGUI>();

        layerRowsRoot = CreateRectObject("LayerSelectionRows", rootRect);
        layerRowsRoot.anchoredPosition = new Vector2(0f, -(LayerToggleHeight + LayerToggleGap));
        layerRowsRoot.sizeDelta = new Vector2(300f, 1f);
        CanvasGroup rowsCanvasGroup = layerRowsRoot.gameObject.AddComponent<CanvasGroup>();
        rowsCanvasGroup.alpha = 1f;
        rowsCanvasGroup.interactable = true;
        rowsCanvasGroup.blocksRaycasts = true;
        LayerSelectionDropdownController dropdown = toggleObject.AddComponent<LayerSelectionDropdownController>();
        dropdown.Initialize(layerRowsRoot, rowsCanvasGroup, toggleLabel, toggleButton);

        string[] labels = { "Respiratory", "Digestive", "Circulatory", "Lymphatic" };
        string[] descriptions =
        {
            "The respiratory system brings oxygen into the body and expels carbon dioxide through the process of breathing.",
            "The digestive system breaks down food into nutrients that the body can absorb for energy, growth, and repair while eliminating waste.",
            "The circulatory system uses the heart and blood vessels to pump oxygen, nutrients, and hormones to cells throughout the body.",
            "The lymphatic system maintains fluid balance and defends the body against infections by filtering pathogens and transporting lymph fluid."
        };
        int[] layerNumbers = { 4, 3, 2, 1 };
        layerButtons = new Button[labels.Length];
        layerButtonBackgrounds = new Image[labels.Length];
        layerButtonOutlines = new Outline[labels.Length];
        activeThreatKeys = new HashSet<string>[labels.Length];

        for (int index = 0; index < labels.Length; index++)
        {
            activeThreatKeys[index] = new HashSet<string>();
            RectTransform rowRect = CreateRectObject("LayerButtonRow_" + labels[index], layerRowsRoot);
            Button layerButton = CreateButton("Select" + labels[index] + "Button", rowRect, out Image layerBackground);
            Outline layerOutline = layerButton.gameObject.AddComponent<Outline>();
            layerOutline.effectColor = new Color(1f, 0.08f, 0.1f, 0.9f);
            layerOutline.effectDistance = new Vector2(2f, -2f);
            layerOutline.useGraphicAlpha = true;
            layerOutline.enabled = false;
            CreateLabel("LayerButtonLabel", layerButton.transform as RectTransform, labels[index], 15f, TextAlignmentOptions.MidlineLeft);
            int layerNumber = layerNumbers[index];
            layerButton.onClick.AddListener(() => SelectLayer(layerNumber));

            GameObject infoObject = new GameObject("LayerInfoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            infoObject.transform.SetParent(rowRect, false);
            RectTransform infoRect = infoObject.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(1f, 0.5f);
            infoRect.anchorMax = new Vector2(1f, 0.5f);
            infoRect.pivot = new Vector2(1f, 0.5f);
            infoRect.sizeDelta = new Vector2(30f, 30f);
            infoRect.anchoredPosition = Vector2.zero;

            Image infoBackground = infoObject.GetComponent<Image>();
            infoBackground.color = new Color(0.09f, 0.15f, 0.18f, 1f);
            infoBackground.raycastTarget = true;
            Button infoButton = infoObject.GetComponent<Button>();
            infoButton.targetGraphic = infoBackground;
            infoButton.transition = Selectable.Transition.ColorTint;
            infoButton.colors = CreateButtonColors(infoBackground.color);
            CreateLabel("InfoIndicator", infoRect, "i", 14f, TextAlignmentOptions.Center);

            GameObject tooltipObject = CreateTooltip(canvasRect, descriptions[index]);
            LayerInfoTooltip tooltip = infoObject.AddComponent<LayerInfoTooltip>();
            tooltip.Initialize(canvasRect, infoRect, tooltipObject.GetComponent<RectTransform>());

            layerButtons[index] = layerButton;
            layerButtonBackgrounds[index] = layerBackground;
            layerButtonOutlines[index] = layerOutline;
        }

        rootObject.AddComponent<LayerEventGlowAnimator>();
        return rootRect;
    }

    private static RectTransform CreateRectObject(string objectName, RectTransform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    private static Button CreateButton(string objectName, RectTransform parent, out Image background)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = new Vector2(-38f, 0f);

        background = buttonObject.GetComponent<Image>();
        background.color = NormalButtonColor;
        background.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateButtonColors(NormalButtonColor);
        return button;
    }

    private static ColorBlock CreateButtonColors(Color baseColor)
    {
        return new ColorBlock
        {
            normalColor = baseColor,
            highlightedColor = new Color(0.08f, 0.22f, 0.26f, 1f),
            pressedColor = new Color(0.1f, 0.55f, 0.62f, 1f),
            selectedColor = new Color(0.08f, 0.22f, 0.26f, 1f),
            disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
    }

    private static GameObject CreateTooltip(RectTransform canvasRect, string description)
    {
        GameObject tooltipObject = new GameObject("LayerInfoTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipObject.transform.SetParent(canvasRect, false);
        RectTransform tooltipRect = tooltipObject.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 0.5f);
        tooltipRect.sizeDelta = new Vector2(292f, 100f);

        Image background = tooltipObject.GetComponent<Image>();
        background.color = new Color(0.012f, 0.028f, 0.038f, 0.98f);
        background.raycastTarget = false;

        RectTransform labelRect = CreateRectObject("Description", tooltipRect);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(12f, 9f);
        labelRect.offsetMax = new Vector2(-12f, -9f);
        TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 12f;
        label.color = LabelColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.text = description;
        label.raycastTarget = false;
        tooltipObject.SetActive(false);
        return tooltipObject;
    }

    private static void CreateLabel(string objectName, RectTransform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = objectName == "InfoIndicator" ? AccentColor : LabelColor;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = value;
        label.raycastTarget = false;
    }

    private static void SelectLayer(int layerNumber)
    {
        CameraScript cameraController = Object.FindFirstObjectByType<CameraScript>();
        if (cameraController == null)
        {
            Debug.LogWarning("System layer controls could not find CameraScript in the Game scene.");
            return;
        }

        cameraController.SelectLayer(layerNumber);
    }

    private static void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData == null || activeThreatKeys == null)
            return;

        int layerIndex;
        string eventId;
        if (!TryGetTrackedLayerEvent(eventData, out layerIndex, out eventId))
            return;

        string eventKey = WellnessManager.BuildEventKey(eventId, eventData.day, eventData.hour);
        if (ActiveThreatLayers.ContainsKey(eventKey))
            return;

        ActiveThreatLayers.Add(eventKey, layerIndex);
        activeThreatKeys[layerIndex].Add(eventKey);
        RefreshLayerButtonVisuals(Time.unscaledTime);
    }

    private static bool TryGetTrackedLayerEvent(RandomEventData eventData, out int layerIndex, out string eventId)
    {
        layerIndex = -1;
        eventId = string.Empty;
        switch (eventData.eventType)
        {
            case RandomEventType.Respiratory:
                layerIndex = 0;
                switch (eventData.eventName)
                {
                    case "Inhaled dust/allergen": eventId = "InhaledDustOrAllergen"; return true;
                    case "Caught a cold from a sick classmate/coworker": eventId = "ColdFromSickPerson"; return true;
                    case "Came Into Contact With Someone Who Was Sick": eventId = "ContactWithSickPerson"; return true;
                    case "Smoked cigarette": eventId = "SmokedCigarette"; return true;
                    default: return false;
                }
            case RandomEventType.Digestive:
                layerIndex = 1;
                switch (eventData.eventName)
                {
                    case "Ate expired/spoiled food": eventId = "AteExpiredFood"; return true;
                    case "Junk food binge": eventId = "JunkFoodBinge"; return true;
                    case "Accidentally Drank Contaminated Water": eventId = "ContaminatedWater"; return true;
                    case "Accidentally Ate Spoiled Food": eventId = "AccidentallyAteSpoiledFood"; return true;
                    default: return false;
                }
            default:
                return false;
        }
    }

    /// <summary>Ends the layer alert when an active event is resolved, reaches a target, or expires.</summary>
    internal static void EndThreatGlow(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || !ActiveThreatLayers.TryGetValue(eventKey, out int layerIndex))
            return;

        ActiveThreatLayers.Remove(eventKey);
        if (activeThreatKeys != null && layerIndex >= 0 && layerIndex < activeThreatKeys.Length)
            activeThreatKeys[layerIndex].Remove(eventKey);
        RefreshLayerButtonVisuals(Time.unscaledTime);
    }

    internal static void UpdateThreatGlow(float unscaledTime)
    {
        RefreshLayerButtonVisuals(unscaledTime);
    }

    private static void RefreshLayerButtonVisuals(float unscaledTime)
    {
        if (layerButtonBackgrounds == null || layerButtonOutlines == null || activeThreatKeys == null)
            return;

        float wave = Mathf.SmoothStep(0f, 1f, (Mathf.Sin(unscaledTime * 4.5f) + 1f) * 0.5f);
        for (int index = 0; index < layerButtonBackgrounds.Length; index++)
        {
            Image background = layerButtonBackgrounds[index];
            Outline outline = layerButtonOutlines[index];
            if (background == null || outline == null)
                continue;

            bool hasThreat = activeThreatKeys[index] != null && activeThreatKeys[index].Count > 0;
            if (!hasThreat)
            {
                background.color = new[] { 4, 3, 2, 1 }[index] == selectedLayer
                    ? SelectedButtonColor
                    : NormalButtonColor;
                outline.enabled = false;
                continue;
            }

            Color restingColor = new[] { 4, 3, 2, 1 }[index] == selectedLayer
                ? SelectedButtonColor
                : NormalButtonColor;
            Color warningTint = new Color(0.62f, 0.045f, 0.06f, 1f);
            background.color = Color.Lerp(restingColor, warningTint, 0.22f + wave * 0.36f);
            outline.enabled = true;
            outline.effectColor = new Color(1f, 0.08f, 0.1f, 0.35f + wave * 0.62f);
            outline.effectDistance = new Vector2(1.5f + wave * 2f, -1.5f - wave * 2f);
        }
    }

    internal static void SetSelectedLayer(int layerNumber)
    {
        if (layerNumber < 1 || layerNumber > 4)
            return;

        selectedLayer = layerNumber;
        RefreshLayerButtonVisuals(Time.unscaledTime);
    }
}

/// <summary>Animates independent layer warning glows using unscaled time.</summary>
public sealed class LayerEventGlowAnimator : MonoBehaviour
{
    private void Update()
    {
        LayerSelectionHUD.UpdateThreatGlow(Time.unscaledTime);
    }
}

/// <summary>Positions the snapshot and related HUD panels without overlap as the canvas changes size.</summary>
internal sealed class ResponsiveHudLayout : MonoBehaviour
{
    private const float DayPanelWidth = 204f;
    private const float DayPanelHeight = 64f;
    private const float SnapshotBaseWidth = 320f;
    private const float SnapshotPreviewWidth = 240f;
    private const float SnapshotBaseHeight = 534f;
    private const float LayerToggleHeight = 36f;
    private const float LayerToggleGap = 8f;

    private RectTransform canvasRect;
    private RectTransform snapshotRect;
    private RectTransform controlsRect;
    private RectTransform layerRowsRect;
    private RectTransform layerToggleRect;
    private RectTransform dayPanelRect;
    private RectTransform consoleRect;
    private float previousWidth = -1f;
    private float previousHeight = -1f;

    internal void Configure(RectTransform snapshot, RectTransform controls, RectTransform dayPanel, RectTransform console,
        RectTransform layerRows, RectTransform layerToggle)
    {
        canvasRect = transform as RectTransform;
        snapshotRect = snapshot;
        controlsRect = controls;
        dayPanelRect = dayPanel;
        consoleRect = console;
        layerRowsRect = layerRows;
        layerToggleRect = layerToggle;
        ApplyLayout();
    }

    private void LateUpdate()
    {
        if (canvasRect == null || snapshotRect == null || controlsRect == null)
            return;

        Rect canvasBounds = canvasRect.rect;
        if (Mathf.Abs(canvasBounds.width - previousWidth) > 1f || Mathf.Abs(canvasBounds.height - previousHeight) > 1f)
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (canvasRect == null || snapshotRect == null || controlsRect == null)
            return;

        Rect canvasBounds = canvasRect.rect;
        float canvasWidth = canvasBounds.width;
        float canvasHeight = canvasBounds.height;
        previousWidth = canvasWidth;
        previousHeight = canvasHeight;

        float margin = Mathf.Clamp(Mathf.Min(canvasWidth, canvasHeight) * 0.025f, 14f, 28f);
        if (dayPanelRect != null)
            SetTopLeftRect(dayPanelRect, new Vector2(margin, -margin), new Vector2(DayPanelWidth, DayPanelHeight));

        float consoleHeight = Mathf.Min(440f, Mathf.Max(220f, canvasHeight * 0.42f));
        float consoleWidth = Mathf.Min(460f, Mathf.Max(180f, canvasWidth * 0.28f));
        consoleHeight = Mathf.Min(consoleHeight, Mathf.Max(120f, canvasHeight - margin * 2f));
        if (consoleRect != null)
        {
            consoleRect.anchorMin = Vector2.zero;
            consoleRect.anchorMax = Vector2.zero;
            consoleRect.pivot = Vector2.zero;
            consoleRect.anchoredPosition = new Vector2(margin, margin);
            consoleRect.sizeDelta = new Vector2(consoleWidth, consoleHeight);
            consoleRect.localScale = Vector3.one;
        }

        float consoleTopFromCanvasTop = canvasHeight - margin - consoleHeight;
        float snapshotTop = margin + DayPanelHeight + 12f;
        float controlsTop = snapshotTop;
        float verticalSpace = Mathf.Max(1f, consoleTopFromCanvasTop - snapshotTop - margin);
        float availableWidth = Mathf.Max(1f, canvasWidth - margin * 2f);
        bool controlsBelow = availableWidth < 560f;
        bool twoColumnGrid = availableWidth >= 260f;
        float controlsGap = controlsBelow ? 8f : 12f;
        float controlsWidth = controlsBelow
            ? availableWidth
            : Mathf.Clamp(availableWidth * 0.3f, 250f, 300f);
        float controlsRowHeight = 56f;
        float controlsGapY = 7f;
        float snapshotScale;

        if (!controlsBelow)
        {
            float availableSideHeight = Mathf.Max(1f, consoleTopFromCanvasTop - controlsTop - margin);
            float availableRowsHeight = availableSideHeight - LayerToggleHeight - LayerToggleGap - controlsGapY * 3f;
            float rowHeight = Mathf.Min(56f, availableRowsHeight / 4f);
            if (rowHeight < 34f)
            {
                controlsBelow = true;
                controlsWidth = availableWidth;
            }
            else
            {
                controlsRowHeight = rowHeight;
                float sideLayoutHeight = LayerToggleHeight + LayerToggleGap + 4f * controlsRowHeight + 3f * controlsGapY;
                float snapshotAvailableWidth = availableWidth - controlsWidth - controlsGap;
                snapshotScale = Mathf.Min(1f, snapshotAvailableWidth / SnapshotBaseWidth, verticalSpace / SnapshotBaseHeight);
                // Layout helper is declared after this method.
                PlaceSnapshot(snapshotTop, margin, snapshotScale);
                controlsRect.anchoredPosition = new Vector2(margin + (SnapshotBaseWidth + SnapshotPreviewWidth) * 0.5f * snapshotScale, -controlsTop);
                controlsRect.localScale = Vector3.one;
                ConfigureLayerControlRects(controlsWidth, sideLayoutHeight);
                ArrangeLayerRows(layerRowsRect, controlsWidth, controlsRowHeight, controlsGapY, false);
                return;
            }
        }

        controlsRowHeight = twoColumnGrid ? 52f : 40f;
        controlsGapY = twoColumnGrid ? 8f : 5f;
        float rowCount = twoColumnGrid ? 2f : 4f;
        float controlsHeight = LayerToggleHeight + LayerToggleGap + rowCount * controlsRowHeight + (rowCount - 1f) * controlsGapY;
        float snapshotAvailableHeight = Mathf.Max(1f, verticalSpace - controlsHeight - controlsGap);
        snapshotScale = Mathf.Min(1f, availableWidth / SnapshotBaseWidth, snapshotAvailableHeight / SnapshotBaseHeight);
        PlaceSnapshot(snapshotTop, margin, snapshotScale);
        controlsRect.anchoredPosition = new Vector2(margin, -snapshotTop - SnapshotBaseHeight * snapshotScale - controlsGap);
        controlsRect.localScale = Vector3.one;
        ConfigureLayerControlRects(controlsWidth, controlsHeight);
        ArrangeLayerRows(layerRowsRect, controlsWidth, controlsRowHeight, controlsGapY, twoColumnGrid);
    }
    private void ConfigureLayerControlRects(float width, float totalHeight)
    {
        if (controlsRect != null)
            controlsRect.sizeDelta = new Vector2(width, totalHeight);
        if (layerToggleRect != null)
            layerToggleRect.sizeDelta = new Vector2(width, LayerToggleHeight);
        if (layerRowsRect != null)
        {
            layerRowsRect.anchorMin = new Vector2(0f, 1f);
            layerRowsRect.anchorMax = new Vector2(0f, 1f);
            layerRowsRect.pivot = new Vector2(0f, 1f);
            layerRowsRect.anchoredPosition = new Vector2(0f, -(LayerToggleHeight + LayerToggleGap));
            layerRowsRect.sizeDelta = new Vector2(width, 1f);
        }
    }


    private void PlaceSnapshot(float topOffset, float leftOffset, float scale)
    {
        snapshotRect.anchorMin = new Vector2(0f, 1f);
        snapshotRect.anchorMax = new Vector2(0f, 1f);
        snapshotRect.pivot = new Vector2(0f, 1f);
        snapshotRect.anchoredPosition = new Vector2(leftOffset, -topOffset);
        snapshotRect.sizeDelta = new Vector2(SnapshotBaseWidth, SnapshotBaseHeight);
        snapshotRect.localScale = new Vector3(scale, scale, 1f);
    }


    private static void SetTopLeftRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void ArrangeLayerRows(RectTransform controlsRoot, float width, float rowHeight, float gap, bool grid)
    {
        int columnCount = grid ? 2 : 1;
        float cellWidth = grid ? (width - gap) / 2f : width;
        for (int index = 0; index < controlsRoot.childCount; index++)
        {
            RectTransform rowRect = controlsRoot.GetChild(index) as RectTransform;
            if (rowRect == null)
                continue;

            int row = grid ? index / columnCount : index;
            int column = grid ? index % columnCount : 0;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(column * (cellWidth + gap), -row * (rowHeight + gap));
            rowRect.sizeDelta = new Vector2(cellWidth, rowHeight);
        }
    }
}

/// <summary>Expands or collapses the body-system buttons beneath the persistent Layers toggle.</summary>
public sealed class LayerSelectionDropdownController : MonoBehaviour
{
    private const float TransitionDuration = 0.22f;
    private const float CollapsedSlideDistance = 54f;

    private RectTransform rowsRect;
    private CanvasGroup rowsCanvasGroup;
    private TextMeshProUGUI toggleLabel;
    private Button toggleButton;
    private Vector2 transitionStartPosition;
    private Vector2 expandedPosition;
    private Vector2 transitionTargetPosition;
    private float transitionStartAlpha;
    private float transitionTargetAlpha;
    private float transitionElapsed;
    private bool isExpanded = true;
    private bool isTransitioning;

    internal void Initialize(RectTransform layerRows, CanvasGroup rowsGroup, TextMeshProUGUI label, Button button)
    {
        rowsRect = layerRows;
        rowsCanvasGroup = rowsGroup;
        toggleLabel = label;
        toggleButton = button;
        expandedPosition = rowsRect != null ? rowsRect.anchoredPosition : Vector2.zero;
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
        UpdateToggleLabel();
    }

    private void Toggle()
    {
        if (rowsRect == null || rowsCanvasGroup == null)
            return;

        isExpanded = !isExpanded;
        transitionStartPosition = rowsRect.anchoredPosition;
        transitionStartAlpha = rowsCanvasGroup.alpha;
        transitionTargetPosition = isExpanded ? expandedPosition : expandedPosition + Vector2.up * CollapsedSlideDistance;
        transitionTargetAlpha = isExpanded ? 1f : 0f;
        transitionElapsed = 0f;
        isTransitioning = true;
        rowsCanvasGroup.interactable = false;
        rowsCanvasGroup.blocksRaycasts = false;
        UpdateToggleLabel();
    }

    private void Update()
    {
        if (!isTransitioning || rowsRect == null || rowsCanvasGroup == null)
            return;

        transitionElapsed += Time.unscaledDeltaTime;
        float progress = TransitionDuration > 0f ? Mathf.Clamp01(transitionElapsed / TransitionDuration) : 1f;
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        rowsRect.anchoredPosition = Vector2.Lerp(transitionStartPosition, transitionTargetPosition, easedProgress);
        rowsCanvasGroup.alpha = Mathf.Lerp(transitionStartAlpha, transitionTargetAlpha, easedProgress);
        if (progress < 1f)
            return;

        isTransitioning = false;
        rowsCanvasGroup.interactable = isExpanded;
        rowsCanvasGroup.blocksRaycasts = isExpanded;
    }

    private void UpdateToggleLabel()
    {
        if (toggleLabel != null)
            toggleLabel.text = isExpanded ? "Layers  −" : "Layers  +";
    }
}


/// <summary>Shows a layer description while the corresponding information indicator is hovered.</summary>
internal sealed class LayerInfoTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform canvasRect;
    private RectTransform indicatorRect;
    private RectTransform tooltipRect;

    internal void Initialize(RectTransform targetCanvas, RectTransform indicator, RectTransform tooltip)
    {
        canvasRect = targetCanvas;
        indicatorRect = indicator;
        tooltipRect = tooltip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (canvasRect == null || indicatorRect == null || tooltipRect == null)
            return;

        Rect bounds = canvasRect.rect;
        float tooltipWidth = Mathf.Min(292f, bounds.width - 24f);
        float tooltipHeight = 100f;
        Vector3[] corners = new Vector3[4];
        indicatorRect.GetWorldCorners(corners);
        Vector2 rightCenter = canvasRect.InverseTransformPoint((corners[2] + corners[3]) * 0.5f);
        Vector2 leftCenter = canvasRect.InverseTransformPoint((corners[0] + corners[1]) * 0.5f);
        bool placeRight = bounds.xMax - rightCenter.x >= tooltipWidth + 12f;
        float x = placeRight ? rightCenter.x + 8f : leftCenter.x - 8f;
        float y = Mathf.Clamp(rightCenter.y, bounds.yMin + tooltipHeight * 0.5f + 8f, bounds.yMax - tooltipHeight * 0.5f - 8f);
        tooltipRect.pivot = new Vector2(placeRight ? 0f : 1f, 0.5f);
        tooltipRect.sizeDelta = new Vector2(tooltipWidth, tooltipHeight);
        tooltipRect.anchoredPosition = new Vector2(x, y);
        tooltipRect.gameObject.SetActive(true);
        tooltipRect.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRect != null)
            tooltipRect.gameObject.SetActive(false);
    }
}
