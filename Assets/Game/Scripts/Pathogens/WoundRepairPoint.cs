using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays wound repair and infection progress at an access point selected by a wound event.</summary>
public sealed class WoundRepairPoint : MonoBehaviour
{
    private const string NickedOrScrapedEventName = "Nicked by a sharp object or tripped and scraped knees";
    private const string TrippedAndScratchedEventName = "Tripped on a Rock and Got Scratches";
    private const string AccessPointsContainerName = "Access Points";
    private const int MaximumInfectionPoints = 100;
    private const float InfectionGrowthIntervalSeconds = 10f;
    private const float InfectionGrowthPerInterval = 5f;
    private const float InfectionReductionPerContact = 5f;
    private const float ImmuneContactIntervalSeconds = 2f;
    private const float RepairProgressPerSecond = 1f;
    private const float FloatingHeight = 3f;
    private const float CanvasWorldScale = 0.02f;
    private const float CanvasPixelsPerUnit = 10f;
    private const float StatusFontSize = 42f;
    private const float LabelFontSize = 30f;
    private const float ButtonFontSize = 36f;

    private static readonly Vector2 CanvasSize = new Vector2(640f, 450f);
    private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 StatusSize = new Vector2(600f, 58f);
    private static readonly Vector2 RepairLabelSize = new Vector2(570f, 40f);
    private static readonly Vector2 BarSize = new Vector2(520f, 28f);
    private static readonly Vector2 ButtonSize = new Vector2(300f, 70f);
    private static readonly Vector2 ButtonLabelSize = new Vector2(280f, 64f);
    private static readonly List<WoundRepairPoint> ActivePoints = new List<WoundRepairPoint>();
    private static readonly HashSet<string> HandledEventKeys = new HashSet<string>();
    private static bool isListeningForEvents;

    /// <summary>Raised when the count of active skin breaches changes.</summary>
    public static event System.Action<bool> OnSkinBreachStateChanged;

    /// <summary>Returns whether any registered access point currently has an unrepaired breach.</summary>
    public static bool HasActiveBreaches
    {
        get
        {
            foreach (WoundRepairPoint woundPoint in ActivePoints)
            {
                if (woundPoint != null && woundPoint.isActiveAndEnabled && woundPoint.isBreached)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Returns one active wound's associated wellness event occurrence, if available.</summary>
    public static string GetActiveWellnessEventKey()
    {
        foreach (WoundRepairPoint woundPoint in ActivePoints)
        {
            if (woundPoint != null && woundPoint.isActiveAndEnabled && woundPoint.isBreached &&
                !string.IsNullOrWhiteSpace(woundPoint.wellnessEventKey))
            {
                return woundPoint.wellnessEventKey;
            }
        }

        return null;
    }

    private Canvas floatingCanvas;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI repairStatusLabel;
    private TextMeshProUGUI infectionStatusLabel;
    private TextMeshProUGUI repairButtonLabel;
    private Image repairProgressFill;
    private Image infectionProgressFill;
    private Button repairButton;
    private readonly Dictionary<int, float> nextContactReductionTime = new Dictionary<int, float>();
    private bool isBreached;
    private bool isRepairing;
    private bool hasCapturedCameraRotation;
    private float repairProgress;
    private float infectionPoints;
    private float infectionGrowthTimer;
    private string wellnessEventKey;

    /// <summary>Returns the active wellness event occurrence assigned to this wound.</summary>
    public string WellnessEventKey => wellnessEventKey;

    private bool IsAccessPoint
    {
        get { return transform.parent != null && transform.parent.name == AccessPointsContainerName; }
    }

    private void Awake()
    {
        if (IsAccessPoint)
        {
            CreateFloatingPrompt();
        }
    }

    private void OnEnable()
    {
        if (!IsAccessPoint)
        {
            return;
        }

        if (ActivePoints.Count == 0)
        {
            HandledEventKeys.Clear();
        }

        if (!ActivePoints.Contains(this))
        {
            ActivePoints.Add(this);
        }

        if (!isListeningForEvents)
        {
            RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
            isListeningForEvents = true;
        }

        if (isBreached)
        {
            NotifySkinBreachStateChanged();
        }
    }

    private void OnDisable()
    {
        if (IsAccessPoint)
        {
            bool hadBreach = isBreached;
            ActivePoints.Remove(this);
            if (ActivePoints.Count == 0 && isListeningForEvents)
            {
                RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;
                isListeningForEvents = false;
            }

            if (hadBreach)
            {
                NotifySkinBreachStateChanged();
            }
        }

        if (floatingCanvas != null)
        {
            floatingCanvas.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (floatingCanvas == null || !floatingCanvas.gameObject.activeSelf)
        {
            return;
        }

        floatingCanvas.transform.position = transform.position + Vector3.up * FloatingHeight;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            floatingCanvas.worldCamera = mainCamera;
            if (!hasCapturedCameraRotation)
            {
                floatingCanvas.transform.rotation = Quaternion.LookRotation(mainCamera.transform.position - floatingCanvas.transform.position, mainCamera.transform.up);
                hasCapturedCameraRotation = true;
            }
        }
    }

    private void Update()
    {
        if (!isBreached)
        {
            return;
        }

        infectionGrowthTimer += Time.deltaTime;
        while (infectionGrowthTimer >= InfectionGrowthIntervalSeconds && isBreached)
        {
            infectionGrowthTimer -= InfectionGrowthIntervalSeconds;
            infectionPoints = Mathf.Min(MaximumInfectionPoints, infectionPoints + InfectionGrowthPerInterval);
        }

        if (isRepairing)
        {
            repairProgress = Mathf.Min(100f, repairProgress + RepairProgressPerSecond * Time.deltaTime);
            if (repairProgress >= 100f)
            {
                CompleteRepair();
                return;
            }
        }

        RefreshProgressUI();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isBreached || other == null)
        {
            return;
        }

        CirculatoryCellRoute route = other.GetComponentInParent<CirculatoryCellRoute>();
        if (route == null ||
            (other.GetComponentInParent<MacrophageScript>() == null && other.GetComponentInParent<NeutrophilScript>() == null) ||
            !route.IsScanningAccessPoint(transform))
        {
            return;
        }

        int cellId = route.GetInstanceID();
        float currentTime = Time.time;
        if (nextContactReductionTime.TryGetValue(cellId, out float nextAllowedTime) && currentTime < nextAllowedTime)
        {
            return;
        }

        nextContactReductionTime[cellId] = currentTime + ImmuneContactIntervalSeconds;
        infectionPoints = Mathf.Max(0f, infectionPoints - InfectionReductionPerContact);
        RefreshProgressUI();
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        string eventId;
        if (string.Equals(eventData.eventName, NickedOrScrapedEventName, System.StringComparison.OrdinalIgnoreCase))
        {
            eventId = "NickedOrScraped";
        }
        else if (string.Equals(eventData.eventName, TrippedAndScratchedEventName, System.StringComparison.OrdinalIgnoreCase))
        {
            eventId = "TrippedAndScratched";
        }
        else
        {
            return;
        }

        List<WoundRepairPoint> availablePoints = new List<WoundRepairPoint>();
        foreach (WoundRepairPoint woundPoint in ActivePoints)
        {
            if (woundPoint != null && woundPoint.isActiveAndEnabled && !woundPoint.isBreached)
            {
                availablePoints.Add(woundPoint);
            }
        }

        if (availablePoints.Count == 0)
        {
            return;
        }

        string eventKey = WellnessManager.BuildEventKey(eventId, eventData.day, eventData.hour);
        if (string.IsNullOrWhiteSpace(eventKey) || HandledEventKeys.Contains(eventKey))
        {
            return;
        }

        int selectedIndex = Random.Range(0, availablePoints.Count);
        HandledEventKeys.Add(eventKey);
        availablePoints[selectedIndex].ShowBreach(eventKey);
    }

    private void CreateFloatingPrompt()
    {
        GameObject canvasObject = new GameObject("Wound Status Floating UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        floatingCanvas = canvasObject.GetComponent<Canvas>();
        floatingCanvas.renderMode = RenderMode.WorldSpace;
        floatingCanvas.overrideSorting = true;
        floatingCanvas.sortingOrder = 100;
        floatingCanvas.worldCamera = Camera.main;
        floatingCanvas.transform.position = transform.position + Vector3.up * FloatingHeight;
        floatingCanvas.transform.localScale = Vector3.one * CanvasWorldScale;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = CanvasPixelsPerUnit;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = CanvasSize;

        GameObject panelObject = CreateUiChild("Panel", canvasRect, typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        StretchToParent(panelRect);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.055f, 0.075f, 0.095f, 0.94f);

        statusLabel = CreateText("Status", panelRect, StatusSize, new Vector2(0f, 184f), "Access Point has been wounded!", StatusFontSize);
        statusLabel.color = new Color(1f, 0.82f, 0.68f, 1f);
        repairStatusLabel = CreateText("Repair Status", panelRect, RepairLabelSize, new Vector2(0f, 125f), "Repair Status: 0%", LabelFontSize);
        repairProgressFill = CreateProgressBar("Repair Progress Bar", panelRect, new Vector2(0f, 82f), new Color(0.24f, 0.82f, 0.43f, 1f));
        infectionStatusLabel = CreateText("Bacterial Infection Status", panelRect, RepairLabelSize, new Vector2(0f, 24f), "Bacterial Infection: 0/100", LabelFontSize);
        infectionProgressFill = CreateProgressBar("Bacterial Infection Bar", panelRect, new Vector2(0f, -19f), new Color(0.86f, 0.24f, 0.2f, 1f));

        GameObject buttonObject = CreateUiChild("Repair Button", panelRect, typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = CenterAnchor;
        buttonRect.anchorMax = CenterAnchor;
        buttonRect.pivot = CenterAnchor;
        buttonRect.sizeDelta = ButtonSize;
        buttonRect.anchoredPosition = new Vector2(0f, -105f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.55f, 0.35f, 1f);
        repairButton = buttonObject.GetComponent<Button>();
        repairButton.targetGraphic = buttonImage;
        repairButton.onClick.AddListener(BeginRepair);
        repairButtonLabel = CreateText("Button Label", buttonRect, ButtonLabelSize, Vector2.zero, "Repair", ButtonFontSize);
        repairButtonLabel.raycastTarget = false;

        canvasObject.SetActive(false);
    }

    private void ShowBreach(string eventKey)
    {
        if (isBreached || floatingCanvas == null)
        {
            return;
        }

        isBreached = true;
        isRepairing = false;
        repairProgress = 0f;
        infectionPoints = MaximumInfectionPoints;
        infectionGrowthTimer = 0f;
        nextContactReductionTime.Clear();
        wellnessEventKey = eventKey;
        LayerSelectionHUD.EndThreatGlow(eventKey);
        statusLabel.text = $"{gameObject.name} has been wounded!";
        repairButton.interactable = true;
        repairButtonLabel.text = "Repair";
        RefreshProgressUI();
        floatingCanvas.gameObject.SetActive(true);
        NotifySkinBreachStateChanged();
    }

    private void BeginRepair()
    {
        if (!isBreached || isRepairing)
        {
            return;
        }

        isRepairing = true;
        repairButton.interactable = false;
        repairButtonLabel.text = "Repairing...";
    }

    private void CompleteRepair()
    {
        isBreached = false;
        isRepairing = false;
        floatingCanvas.gameObject.SetActive(false);

        WellnessManager wellnessManager = WellnessManager.Instance;
        if (wellnessManager != null && !string.IsNullOrWhiteSpace(wellnessEventKey))
        {
            wellnessManager.TryAwardEventPoints(wellnessEventKey, "WoundRepaired", 2f);
            wellnessManager.ResolveEventQTE(wellnessEventKey);
        }

        NotifySkinBreachStateChanged();
    }

    private void RefreshProgressUI()
    {
        if (repairStatusLabel != null)
        {
            repairStatusLabel.text = $"Repair Status: {repairProgress:0}%";
        }

        if (repairProgressFill != null)
        {
            repairProgressFill.fillAmount = Mathf.Clamp01(repairProgress / 100f);
        }

        if (infectionStatusLabel != null)
        {
            infectionStatusLabel.text = $"Bacterial Infection: {infectionPoints:0}/{MaximumInfectionPoints}";
        }

        if (infectionProgressFill != null)
        {
            infectionProgressFill.fillAmount = Mathf.Clamp01(infectionPoints / MaximumInfectionPoints);
        }
    }

    private static void NotifySkinBreachStateChanged()
    {
        OnSkinBreachStateChanged?.Invoke(HasActiveBreaches);
    }

    private static Image CreateProgressBar(string objectName, RectTransform parent, Vector2 position, Color fillColor)
    {
        GameObject backgroundObject = CreateUiChild(objectName, parent, typeof(Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = CenterAnchor;
        backgroundRect.anchorMax = CenterAnchor;
        backgroundRect.pivot = CenterAnchor;
        backgroundRect.sizeDelta = BarSize;
        backgroundRect.anchoredPosition = position;
        backgroundObject.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.17f, 1f);

        GameObject fillObject = CreateUiChild("Fill", backgroundRect, typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        StretchToParent(fillRect);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        return fillImage;
    }

    private static GameObject CreateUiChild(string objectName, Transform parent, params System.Type[] componentTypes)
    {
        System.Type[] requiredTypes = new System.Type[componentTypes.Length + 2];
        requiredTypes[0] = typeof(RectTransform);
        requiredTypes[1] = typeof(CanvasRenderer);
        for (int index = 0; index < componentTypes.Length; index++)
        {
            requiredTypes[index + 2] = componentTypes[index];
        }

        GameObject child = new GameObject(objectName, requiredTypes);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static TextMeshProUGUI CreateText(string objectName, RectTransform parent, Vector2 size, Vector2 position, string initialText, float fontSize)
    {
        GameObject textObject = CreateUiChild(objectName, parent, typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = CenterAnchor;
        textRect.anchorMax = CenterAnchor;
        textRect.pivot = CenterAnchor;
        textRect.sizeDelta = size;
        textRect.anchoredPosition = position;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
