using System.Collections;
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
    private const float RepairAttemptTimeoutSeconds = 100f;
    private const float WoundInfectionSpawnRetrySeconds = 1f;
    private const float FibrinNetInitialAlpha = 0.1f;
    private const float WellnessShieldDurationSeconds = 15f;
    private const float FibrinNetSuccessHoldSeconds = 2f;
    private const float FibrinNetFadeOutSeconds = 0.75f;
    private const float FibrinNetModelScale = 3f;
    private const float FibrinNetModelRotationX = 270f;
    private const float RepairProgressCanvasScale = 0.04f;
    private const float FloatingHeight = 3f;
    private const string FibrinNetResourceName = "FibrinNet";
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    [SerializeField] private float panelScale = 0.04f;
    [SerializeField] private bool useCameraBillboard = true;
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
    private static readonly Vector2 RepairProgressCanvasSize = new Vector2(260f, 84f);
    private static readonly Vector2 RepairProgressLabelSize = new Vector2(240f, 28f);
    private static readonly Vector2 RepairProgressBarSize = new Vector2(220f, 18f);
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
    private TextMeshProUGUI repairProgressLabel;
    private Image infectionProgressFill;
    private Button repairButton;
    private Canvas repairProgressCanvas;
    [SerializeField] private GameObject fibrinNetPrefab;
    private GameObject fibrinNetInstance;
    private Material[] fibrinNetMaterials;
    private Coroutine fibrinNetCleanupRoutine;
    private Coroutine infectionClearRoutine;
    private InfectionSpawner.InfectionMarker woundInfectionMarker;
    private readonly Dictionary<int, float> nextContactReductionTime = new Dictionary<int, float>();
    private bool isBreached;
    private bool isRepairing;
    private bool isWaitingForInfectionClear;
    private bool breachTriggersQte;
    private float breachElapsed;
    private float repairProgress;
    private float repairAttemptElapsed;
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

        if (repairProgressCanvas != null)
        {
            repairProgressCanvas.gameObject.SetActive(false);
        }

        ClearFibrinNet();
    }

    private void LateUpdate()
    {
        bool showMainPrompt = floatingCanvas != null && floatingCanvas.gameObject.activeSelf;
        bool showRepairProgress = repairProgressCanvas != null && repairProgressCanvas.gameObject.activeSelf;
        if (!showMainPrompt && !showRepairProgress)
        {
            return;
        }

        if (showMainPrompt)
        {
            floatingCanvas.transform.position = transform.position + Vector3.up * FloatingHeight;
        }

        if (showRepairProgress)
        {
            repairProgressCanvas.transform.position = transform.position + Vector3.up * FloatingHeight;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null || (mainCamera.cullingMask & (1 << gameObject.layer)) == 0)
        {
            foreach (Camera candidateCamera in Camera.allCameras)
            {
                if (candidateCamera != null && (candidateCamera.cullingMask & (1 << gameObject.layer)) != 0)
                {
                    mainCamera = candidateCamera;
                    break;
                }
            }
        }

        if (mainCamera != null)
        {
            if (showMainPrompt)
            {
                floatingCanvas.worldCamera = mainCamera;
                if (useCameraBillboard)
                {
                    floatingCanvas.transform.rotation = mainCamera.transform.rotation;
                }
            }

            if (showRepairProgress)
            {
                repairProgressCanvas.worldCamera = mainCamera;
                repairProgressCanvas.transform.rotation = mainCamera.transform.rotation;
            }
        }
    }

    private void Update()
    {
        if (!isBreached)
        {
            return;
        }

        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        infectionGrowthTimer += gameplayDeltaTime;
        while (infectionGrowthTimer >= InfectionGrowthIntervalSeconds && isBreached)
        {
            infectionGrowthTimer -= InfectionGrowthIntervalSeconds;
            infectionPoints = Mathf.Min(MaximumInfectionPoints, infectionPoints + InfectionGrowthPerInterval);
        }

        if (isWaitingForInfectionClear)
        {
            RefreshProgressUI();
            return;
        }

        if (!isRepairing)
        {
            breachElapsed += gameplayDeltaTime;
            if (breachElapsed >= RepairAttemptTimeoutSeconds)
            {
                LeaveWoundUntreated();
                return;
            }
        }

        if (isRepairing)
        {
            repairAttemptElapsed += gameplayDeltaTime;
            repairProgress = Mathf.Min(100f, repairProgress + RepairProgressPerSecond * gameplayDeltaTime);
            UpdateFibrinNetAlpha(Mathf.Lerp(FibrinNetInitialAlpha, 1f, repairProgress / 100f));
            if (repairProgress >= 100f)
            {
                CompleteRepair();
                return;
            }

            if (repairAttemptElapsed >= RepairAttemptTimeoutSeconds)
            {
                LeaveWoundUntreated();
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
        availablePoints[selectedIndex].ShowBreach(eventKey, eventData.triggersQTE);
    }

    private void CreateFloatingPrompt()
    {
        GameObject canvasObject = new GameObject("Wound Status Floating UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = gameObject.layer;
        floatingCanvas = canvasObject.GetComponent<Canvas>();
        floatingCanvas.renderMode = RenderMode.WorldSpace;
        floatingCanvas.overrideSorting = true;
        floatingCanvas.sortingOrder = 100;
        floatingCanvas.worldCamera = Camera.main;
        floatingCanvas.transform.position = transform.position + Vector3.up * FloatingHeight;
        floatingCanvas.transform.localScale = Vector3.one * panelScale;

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
        CreateRepairProgressOverlay();
    }

    private void CreateRepairProgressOverlay()
    {
        GameObject canvasObject = new GameObject("Wound Repair Progress UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = gameObject.layer;
        repairProgressCanvas = canvasObject.GetComponent<Canvas>();
        repairProgressCanvas.renderMode = RenderMode.WorldSpace;
        repairProgressCanvas.overrideSorting = true;
        repairProgressCanvas.sortingOrder = 101;
        repairProgressCanvas.worldCamera = Camera.main;
        canvasObject.transform.position = transform.position + Vector3.up * FloatingHeight;
        canvasObject.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * RepairProgressCanvasScale;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = CanvasPixelsPerUnit;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = RepairProgressCanvasSize;

        GameObject panelObject = CreateUiChild("Repair Progress Panel", canvasRect, typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        StretchToParent(panelRect);
        panelObject.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.065f, 0.82f);

        repairProgressLabel = CreateText("Repair Progress Label", panelRect, RepairProgressLabelSize, new Vector2(0f, 22f), "Repair: 0%", 20f);
        repairProgressFill = CreateCompactProgressBar(panelRect);
        canvasObject.SetActive(false);
    }

    private static Image CreateCompactProgressBar(RectTransform parent)
    {
        GameObject backgroundObject = CreateUiChild("Repair Progress Track", parent, typeof(Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = CenterAnchor;
        backgroundRect.anchorMax = CenterAnchor;
        backgroundRect.pivot = CenterAnchor;
        backgroundRect.sizeDelta = RepairProgressBarSize;
        backgroundRect.anchoredPosition = new Vector2(0f, -14f);
        backgroundObject.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.14f, 1f);

        GameObject fillObject = CreateUiChild("Repair Progress Fill", backgroundRect, typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        StretchToParent(fillRect);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.24f, 0.82f, 0.43f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        return fillImage;
    }

    private void ShowBreach(string eventKey, bool triggersQte)
    {
        if (isBreached || floatingCanvas == null)
        {
            return;
        }

        isBreached = true;
        isRepairing = false;
        isWaitingForInfectionClear = false;
        breachTriggersQte = triggersQte;
        breachElapsed = 0f;
        repairProgress = 0f;
        repairAttemptElapsed = 0f;
        ClearFibrinNet();
        infectionPoints = MaximumInfectionPoints;
        infectionGrowthTimer = 0f;
        nextContactReductionTime.Clear();
        wellnessEventKey = eventKey;
        if (triggersQte)
            QTETracker.Register(QTETracker.CirculatoryLayerIndex, eventKey);
        LayerSelectionHUD.EndThreatGlow(eventKey);
        statusLabel.text = $"{gameObject.name} has been wounded!";
        repairButton.interactable = true;
        repairButtonLabel.text = "Repair";
        RefreshProgressUI();
        floatingCanvas.gameObject.SetActive(true);
        repairProgressCanvas.gameObject.SetActive(false);
        NotifySkinBreachStateChanged();
    }

    private void BeginRepair()
    {
        if (!isBreached || isRepairing)
        {
            return;
        }

        isRepairing = true;
        repairAttemptElapsed = 0f;
        floatingCanvas.gameObject.SetActive(false);
        repairProgressCanvas.gameObject.SetActive(true);
        if (fibrinNetInstance == null)
        {
            SpawnFibrinNet();
        }

        repairButton.interactable = false;
        repairButtonLabel.text = "Repairing...";
    }

    private void LeaveWoundUntreated()
    {
        if (isWaitingForInfectionClear)
        {
            return;
        }

        isRepairing = false;
        isWaitingForInfectionClear = true;
        breachElapsed = 0f;
        repairProgress = 0f;
        repairAttemptElapsed = 0f;
        ClearFibrinNet();

        if (floatingCanvas != null)
        {
            floatingCanvas.gameObject.SetActive(false);
        }

        if (repairProgressCanvas != null)
        {
            repairProgressCanvas.gameObject.SetActive(false);
        }

        if (repairButton != null)
        {
            repairButton.interactable = false;
        }

        QTETracker.Unregister(QTETracker.CirculatoryLayerIndex, wellnessEventKey);
        if (WellnessManager.Instance != null && !string.IsNullOrWhiteSpace(wellnessEventKey))
        {
            WellnessManager.Instance.ResolveEventQTE(wellnessEventKey);
        }

        Debug.Log("[Wound] Wound left untreated. Staph infection has entered the bloodstream.", this);
        if (infectionClearRoutine != null)
        {
            StopCoroutine(infectionClearRoutine);
        }

        infectionClearRoutine = StartCoroutine(SpawnAndWaitForWoundInfection());
    }

    private IEnumerator SpawnAndWaitForWoundInfection()
    {
        InfectionSpawner infectionSpawner = null;
        while (isBreached && isWaitingForInfectionClear && woundInfectionMarker == null)
        {
            if (infectionSpawner == null)
            {
                infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
            }

            if (infectionSpawner == null)
            {
                yield return new WaitForSecondsRealtime(WoundInfectionSpawnRetrySeconds);
                continue;
            }

            woundInfectionMarker = infectionSpawner.ForceSpawnWoundBacterialInfection(transform.position);
            if (woundInfectionMarker == null)
            {
                yield return new WaitForSecondsRealtime(WoundInfectionSpawnRetrySeconds);
            }
        }

        if (woundInfectionMarker == null)
        {
            infectionClearRoutine = null;
            yield break;
        }

        while (isBreached && isWaitingForInfectionClear && infectionSpawner != null &&
               infectionSpawner.IsInfectionActive(woundInfectionMarker))
        {
            yield return null;
        }

        if (!isBreached || !isWaitingForInfectionClear)
        {
            infectionClearRoutine = null;
            yield break;
        }

        woundInfectionMarker = null;
        isWaitingForInfectionClear = false;
        breachElapsed = 0f;
        repairAttemptElapsed = 0f;
        repairProgress = 0f;
        infectionPoints = MaximumInfectionPoints;
        infectionGrowthTimer = 0f;
        nextContactReductionTime.Clear();

        if (repairButton != null)
        {
            repairButton.interactable = true;
        }

        if (repairButtonLabel != null)
        {
            repairButtonLabel.text = "Repair";
        }

        if (statusLabel != null)
        {
            statusLabel.text = $"{gameObject.name} has been wounded! Infection cleared.";
        }

        RefreshProgressUI();
        if (floatingCanvas != null)
        {
            floatingCanvas.gameObject.SetActive(true);
        }

        if (repairProgressCanvas != null)
        {
            repairProgressCanvas.gameObject.SetActive(false);
        }

        if (breachTriggersQte)
        {
            QTETracker.Register(QTETracker.CirculatoryLayerIndex, wellnessEventKey);
        }

        NotifySkinBreachStateChanged();
        infectionClearRoutine = null;
        Debug.Log($"[Wound] Infection cleared at {gameObject.name}. Repair is available again.", this);
    }

    private void SpawnFibrinNet()
    {
        GameObject prefabToInstantiate = fibrinNetPrefab != null
            ? fibrinNetPrefab
            : Resources.Load<GameObject>(FibrinNetResourceName);
        if (prefabToInstantiate == null)
        {
            Debug.LogError($"Missing a Fibrin Net asset reference and Resources/{FibrinNetResourceName}.prefab; cannot display the wound repair net.", this);
            return;
        }

        fibrinNetInstance = Instantiate(prefabToInstantiate, transform.position, transform.rotation);
        fibrinNetInstance.name = "FibrinNet (Wound Repair)";
        fibrinNetInstance.transform.rotation = Quaternion.Euler(FibrinNetModelRotationX, transform.eulerAngles.y, 0f);
        fibrinNetInstance.transform.localScale = Vector3.one * FibrinNetModelScale;
        Renderer[] renderers = fibrinNetInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds netBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                netBounds.Encapsulate(renderers[index].bounds);
            }

            fibrinNetInstance.transform.position += transform.position - netBounds.center;
        }

        Transform[] netTransforms = fibrinNetInstance.GetComponentsInChildren<Transform>(true);
        foreach (Transform netTransform in netTransforms)
        {
            netTransform.gameObject.layer = gameObject.layer;
        }

        List<Material> materials = new List<Material>();
        foreach (Renderer targetRenderer in renderers)
        {
            Material[] rendererMaterials = targetRenderer.materials;
            foreach (Material material in rendererMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                ConfigureTransparentMaterial(material);
                materials.Add(material);
            }
        }

        fibrinNetMaterials = materials.ToArray();
        UpdateFibrinNetAlpha(FibrinNetInitialAlpha);
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private void UpdateFibrinNetAlpha(float alpha)
    {
        if (fibrinNetMaterials == null)
        {
            return;
        }

        float clampedAlpha = Mathf.Clamp01(alpha);
        foreach (Material material in fibrinNetMaterials)
        {
            if (material == null)
            {
                continue;
            }

            int colorPropertyId = material.HasProperty(BaseColorPropertyId)
                ? BaseColorPropertyId
                : material.HasProperty(ColorPropertyId) ? ColorPropertyId : 0;
            if (colorPropertyId == 0)
            {
                continue;
            }

            Color materialColor = material.GetColor(colorPropertyId);
            materialColor.a = clampedAlpha;
            material.SetColor(colorPropertyId, materialColor);
        }
    }

    private IEnumerator KeepThenFadeFibrinNet()
    {
        yield return new WaitForSecondsRealtime(FibrinNetSuccessHoldSeconds);

        float fadeElapsed = 0f;
        while (fadeElapsed < FibrinNetFadeOutSeconds)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            UpdateFibrinNetAlpha(1f - Mathf.Clamp01(fadeElapsed / FibrinNetFadeOutSeconds));
            yield return null;
        }

        UpdateFibrinNetAlpha(0f);
        DestroyFibrinNetInstances();
        fibrinNetCleanupRoutine = null;
    }

    private void ClearFibrinNet()
    {
        if (fibrinNetCleanupRoutine != null)
        {
            StopCoroutine(fibrinNetCleanupRoutine);
            fibrinNetCleanupRoutine = null;
        }

        UpdateFibrinNetAlpha(0f);
        DestroyFibrinNetInstances();
    }

    private void DestroyFibrinNetInstances()
    {
        if (fibrinNetMaterials != null)
        {
            foreach (Material material in fibrinNetMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            fibrinNetMaterials = null;
        }

        if (fibrinNetInstance != null)
        {
            Destroy(fibrinNetInstance);
            fibrinNetInstance = null;
        }
    }
    private void CompleteRepair()
    {
        isBreached = false;
        isRepairing = false;
        repairProgress = 100f;
        repairAttemptElapsed = 0f;
        UpdateFibrinNetAlpha(1f);
        if (fibrinNetInstance != null)
        {
            fibrinNetCleanupRoutine = StartCoroutine(KeepThenFadeFibrinNet());
        }

        floatingCanvas.gameObject.SetActive(false);
        repairProgressCanvas.gameObject.SetActive(false);

        WellnessManager wellnessManager = WellnessManager.Instance;
        if (wellnessManager != null && !string.IsNullOrWhiteSpace(wellnessEventKey))
        {
            wellnessManager.TryAwardEventPoints(wellnessEventKey, "WoundRepaired", 2f);
            wellnessManager.ResolveEventQTE(wellnessEventKey);
        }

        if (wellnessManager != null)
        {
            wellnessManager.ActivateWellnessShield(WellnessShieldDurationSeconds);
        }

        QTETracker.Unregister(QTETracker.CirculatoryLayerIndex, wellnessEventKey);
        NotifySkinBreachStateChanged();
    }

    private void RefreshProgressUI()
    {
        if (repairStatusLabel != null)
        {
            repairStatusLabel.text = $"Repair Status: {repairProgress:0}%";
        }

        if (repairProgressLabel != null)
        {
            repairProgressLabel.text = $"Repair: {repairProgress:0}%";
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
        child.layer = parent.gameObject.layer;
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
