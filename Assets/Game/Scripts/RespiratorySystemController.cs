using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>Spawns respiratory air, routes it through a lung, and handles allergen, infection, test, and cough behavior.</summary>
public sealed class RespiratorySystemController : MonoBehaviour
{
    private const float SpawnIntervalSeconds = 1f;
    private const float ContaminatedAirDurationSeconds = 5f;
    private const float CoughCooldownSeconds = 5f;
    private const string InhaledDustOrAllergenEventName = "Inhaled dust/allergen";
    private const string ColdFromSickPersonEventName = "Caught a cold from a sick classmate/coworker";
    private const string ContactWithSickPersonEventName = "Came Into Contact With Someone Who Was Sick";
    private const string SmokedCigaretteEventName = "Smoked cigarette";
    private const float NavMeshSampleRadius = 50f;
    private const float ArrivalDistance = 1f;
    private const float BarrierDamagePerRandomEvent = 20f;
    private const float BarrierRestorePerAction = 20f;
    private const float BarrierMaximum = 100f;
    private const string BarrierGaugeObjectName = "RespiratoryBarrierGauge";
    private const string OrdersContainerPath = "HUDCanvas/RespiratoryOrdersContainer";
    private const string BreachOverlayObjectName = "RespiratoryBarrierBreachOverlay";
    private const string BreachWarningObjectName = "RespiratoryBarrierBreachWarning";
    private const string BreachWarningMessage = "BARRIER BREACHED";
    private const string BreachRepairMessage = "BARRIER RESTORING...";
    private const string BreachWarningFontResourcePath = "BankGothicMediumSDF";
    private const float BreachWarningBlinkDuration = 0.6f;
    private const float BarrierGaugeWidth = 600f;
    private const float BarrierGaugeHeight = 600f;
    private const float BarrierGaugeVerticalOffset = -338f;
    private const float RespiratoryQteVerticalOffset = -357f;
    private static Sprite barrierGaugeSprite;

    [SerializeField] private Image barrierGaugeImage;
    [SerializeField] private TextMeshProUGUI barrierTitleLabel;
    [SerializeField] private TextMeshProUGUI barrierPercentLabel;
    [SerializeField] private Image breachOverlay;
    [SerializeField] private TextMeshProUGUI breachWarningText;
    private static Sprite breachOverlaySprite;

    [SerializeField] private Transform airwaySpawnPoint;
    [SerializeField] private Transform leftLung;
    [SerializeField] private Transform rightLung;
    [SerializeField] private GameObject airPrefab;
    [SerializeField] private GameObject contaminatedAirPrefab;
    [SerializeField] private Button coughButton;
    [SerializeField] private TextMeshProUGUI coughButtonLabel;
    [SerializeField] private Button testContaminatedAirButton;
    [SerializeField] private bool showTestButtons = false;

    private sealed class ContaminationWindow
    {
        public string eventKey;
        public float expiresAt;
    }

    private readonly List<ContaminationWindow> contaminationWindows = new List<ContaminationWindow>();
    private float coughCooldownRemaining;
    private Coroutine spawnRoutine;
    private BarrierHealth barrierHealth;
    private bool isBarrierRepairing;

    private void OnEnable()
    {
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
    }

    private void Start()
    {
        CenterQteButton(coughButton);
        SetButtonVisibility(coughButton, true);
        SetButtonVisibility(testContaminatedAirButton, false);
        InitializeBarrierHealth();
        InitializeBreachVisuals();
        InitializeBarrierGauge(coughButton != null ? coughButton.transform.parent as RectTransform : null);

        if (coughButton != null)
        {
            coughButton.onClick.AddListener(HandleCoughPressed);
        }
        else
        {
            Debug.LogError("RespiratorySystemController requires a Cough button.", this);
        }

        if (testContaminatedAirButton != null)
        {
            testContaminatedAirButton.onClick.AddListener(ForceSpawnContaminatedAir);
        }

        if (airwaySpawnPoint == null || leftLung == null || rightLung == null)
        {
            Debug.LogError("RespiratorySystemController requires Airway, Left Lung, and Right Lung points.", this);
            return;
        }

        if (airPrefab == null || contaminatedAirPrefab == null)
        {
            Debug.LogError("RespiratorySystemController requires both air prefabs.", this);
            return;
        }

        spawnRoutine = StartCoroutine(RunSpawnLoop());
        RefreshCoughButton();
    }
    private void InitializeBarrierHealth()
    {
        GameObject respiratoryRoot = GameObject.Find("/Layers/Respiratory");
        if (respiratoryRoot == null)
        {
            Debug.LogError("RespiratorySystemController could not find the Respiratory layer root for BarrierHealth.", this);
            return;
        }

        barrierHealth = respiratoryRoot.GetComponent<BarrierHealth>();
        if (barrierHealth == null)
            barrierHealth = respiratoryRoot.AddComponent<BarrierHealth>();
    }

    private void InitializeBarrierGauge(RectTransform panelRect)
    {
        if (panelRect == null)
            return;

        Transform existingGauge = panelRect.Find(BarrierGaugeObjectName);
        if (barrierGaugeImage == null && existingGauge != null)
            barrierGaugeImage = existingGauge.GetComponent<Image>();

        if (barrierGaugeImage == null)
        {
            GameObject gaugeObject = new GameObject(BarrierGaugeObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gaugeObject.transform.SetParent(panelRect, false);
            barrierGaugeImage = gaugeObject.GetComponent<Image>();
        }

        RectTransform gaugeRect = barrierGaugeImage.rectTransform;
        gaugeRect.anchorMin = new Vector2(0.5f, 0.5f);
        gaugeRect.anchorMax = new Vector2(0.5f, 0.5f);
        gaugeRect.pivot = new Vector2(0.5f, 0.5f);
        gaugeRect.sizeDelta = new Vector2(BarrierGaugeWidth, BarrierGaugeHeight);
        gaugeRect.anchoredPosition = new Vector2(0f, BarrierGaugeVerticalOffset);
        barrierGaugeImage.transform.SetAsFirstSibling();
        barrierGaugeImage.sprite = LoadBarrierGaugeSprite();
        barrierGaugeImage.type = Image.Type.Filled;
        barrierGaugeImage.fillMethod = Image.FillMethod.Radial360;
        barrierGaugeImage.fillOrigin = (int)Image.Origin360.Bottom;
        barrierGaugeImage.preserveAspect = true;
        barrierGaugeImage.raycastTarget = false;

        if (barrierTitleLabel == null)
            barrierTitleLabel = FindOrCreateGaugeLabel(barrierGaugeImage.transform, "BarrierTitle", "BARRIER", 18f, 22f);
        if (barrierPercentLabel == null)
            barrierPercentLabel = FindOrCreateGaugeLabel(barrierGaugeImage.transform, "BarrierPercent", "100%", -18f, 26f);
        barrierTitleLabel.gameObject.SetActive(false);
        barrierPercentLabel.gameObject.SetActive(false);

        RefreshBarrierGauge();
    }

    private static TextMeshProUGUI FindOrCreateGaugeLabel(Transform parent, string labelName, string initialText, float verticalPosition, float fontSize)
    {
        Transform existingLabel = parent.Find(labelName);
        TextMeshProUGUI label = existingLabel != null ? existingLabel.GetComponent<TextMeshProUGUI>() : null;
        if (label == null)
        {
            GameObject labelObject = new GameObject(labelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(200f, 38f);
        labelRect.anchoredPosition = new Vector2(0f, verticalPosition);
        label.text = initialText;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Sprite LoadBarrierGaugeSprite()
    {
        if (barrierGaugeSprite != null)
            return barrierGaugeSprite;

        barrierGaugeSprite = Resources.Load<Sprite>("UI/Barrier meter");
        if (barrierGaugeSprite != null)
            return barrierGaugeSprite;

        Texture2D gaugeTexture = Resources.Load<Texture2D>("UI/Barrier meter");
        if (gaugeTexture != null)
        {
            barrierGaugeSprite = Sprite.Create(
                gaugeTexture,
                new Rect(0f, 0f, gaugeTexture.width, gaugeTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
        else
        {
            Debug.LogWarning("Barrier meter texture could not be loaded from Resources/UI/Barrier meter.png.");
        }

        return barrierGaugeSprite;
    }

    private void RefreshBarrierGauge()
    {
        if (barrierHealth == null || barrierGaugeImage == null)
            return;

        float barrierValue = Mathf.Clamp(barrierHealth.CurrentBarrier, 0f, BarrierMaximum);
        barrierGaugeImage.fillAmount = barrierValue / BarrierMaximum;
        barrierGaugeImage.color = barrierValue >= 50f
            ? Color.cyan
            : barrierValue >= 25f ? Color.yellow : Color.red;

        if (barrierPercentLabel != null)
            barrierPercentLabel.text = $"{Mathf.RoundToInt(barrierValue)}%";
    }

    private void InitializeBreachVisuals()
    {
        Canvas canvas = GameObject.Find("HUDCanvas")?.GetComponent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        if (breachOverlay == null)
        {
            Transform existingOverlay = canvas.transform.Find(BreachOverlayObjectName);
            breachOverlay = existingOverlay != null ? existingOverlay.GetComponent<Image>() : null;
            if (breachOverlay == null)
            {
                GameObject overlayObject = new GameObject(BreachOverlayObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlayObject.transform.SetParent(canvas.transform, false);
                RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                breachOverlay = overlayObject.GetComponent<Image>();
            }

            breachOverlay.sprite = LoadBreachOverlaySprite();
            breachOverlay.raycastTarget = false;
        }

        if (breachWarningText == null)
        {
            Transform existingWarning = canvas.transform.Find(BreachWarningObjectName);
            breachWarningText = existingWarning != null ? existingWarning.GetComponent<TextMeshProUGUI>() : null;
            if (breachWarningText == null)
            {
                GameObject warningObject = new GameObject(BreachWarningObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                warningObject.transform.SetParent(canvas.transform, false);
                RectTransform warningRect = warningObject.GetComponent<RectTransform>();
                warningRect.anchorMin = new Vector2(0.5f, 0.5f);
                warningRect.anchorMax = new Vector2(0.5f, 0.5f);
                warningRect.pivot = new Vector2(0.5f, 0.5f);
                warningRect.anchoredPosition = Vector2.zero;
                warningRect.sizeDelta = new Vector2(720f, 90f);
                breachWarningText = warningObject.GetComponent<TextMeshProUGUI>();
                breachWarningText.text = BreachWarningMessage;
                breachWarningText.font = TMP_Settings.defaultFontAsset;
                breachWarningText.fontSize = 38f;
                breachWarningText.fontStyle = FontStyles.Bold;
                breachWarningText.color = Color.white;
                breachWarningText.alignment = TextAlignmentOptions.Center;
                breachWarningText.raycastTarget = false;
            }
        }

        TMP_FontAsset breachWarningFont = Resources.Load<TMP_FontAsset>(BreachWarningFontResourcePath);
        if (breachWarningText != null && breachWarningFont != null)
            breachWarningText.font = breachWarningFont;

        if (breachOverlay != null)
            breachOverlay.enabled = false;
        if (breachWarningText != null)
            breachWarningText.enabled = false;
    }

    private void RefreshBreachVisuals()
    {
        bool layerVisible = IsBreachLayerVisible();
        bool isBreached = barrierHealth != null && barrierHealth.IsBreached;
        float barrierValue = barrierHealth != null ? barrierHealth.CurrentBarrier : BarrierMaximum;
        if (isBreached || barrierValue >= BarrierMaximum)
            isBarrierRepairing = false;

        bool isRepairing = isBarrierRepairing && barrierValue > 0f && barrierValue < BarrierMaximum;
        bool showBreach = layerVisible && isBreached;
        bool showRepair = layerVisible && !isBreached && isRepairing;

        if (breachOverlay != null)
        {
            breachOverlay.enabled = showBreach;
            if (showBreach)
            {
                Color overlayColor = Color.red;
                overlayColor.a = Mathf.PingPong(Time.time, 0.3f) + 0.1f;
                breachOverlay.color = overlayColor;
            }
        }

        if (breachWarningText == null)
            return;

        if (showRepair)
        {
            breachWarningText.text = BreachRepairMessage;
            breachWarningText.color = Color.yellow;
            breachWarningText.enabled = true;
        }
        else if (showBreach)
        {
            breachWarningText.text = BreachWarningMessage;
            breachWarningText.color = Color.red;
            breachWarningText.enabled = Mathf.PingPong(Time.time, BreachWarningBlinkDuration) < BreachWarningBlinkDuration * 0.5f;
        }
        else
        {
            breachWarningText.enabled = false;
        }
    }

    private static bool IsBreachLayerVisible()
    {
        GameObject ordersContainer = GameObject.Find(OrdersContainerPath);
        CanvasGroup ordersCanvasGroup = ordersContainer != null ? ordersContainer.GetComponent<CanvasGroup>() : null;
        return ordersCanvasGroup != null && ordersCanvasGroup.alpha > 0.5f;
    }

    private static Sprite LoadBreachOverlaySprite()
    {
        if (breachOverlaySprite != null)
            return breachOverlaySprite;

        const int textureSize = 512;
        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int column = -1; column <= textureSize / 48 + 1; column++)
        {
            int centerX = column * 48;
            int verticalOffset = (column & 1) == 0 ? 0 : 28;
            for (int row = -1; row <= textureSize / 56 + 1; row++)
            {
                int centerY = row * 56 + verticalOffset;
                DrawHoneycombEdge(pixels, textureSize, centerX, centerY - 28, centerX + 24, centerY - 14);
                DrawHoneycombEdge(pixels, textureSize, centerX + 24, centerY - 14, centerX + 24, centerY + 14);
                DrawHoneycombEdge(pixels, textureSize, centerX + 24, centerY + 14, centerX, centerY + 28);
                DrawHoneycombEdge(pixels, textureSize, centerX, centerY + 28, centerX - 24, centerY + 14);
                DrawHoneycombEdge(pixels, textureSize, centerX - 24, centerY + 14, centerX - 24, centerY - 14);
                DrawHoneycombEdge(pixels, textureSize, centerX - 24, centerY - 14, centerX, centerY - 28);
            }
        }

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "Runtime Barrier Breach Honeycomb";
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        breachOverlaySprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f);
        return breachOverlaySprite;
    }

    private static void DrawHoneycombEdge(Color32[] pixels, int textureSize, int startX, int startY, int endX, int endY)
    {
        int deltaX = Mathf.Abs(endX - startX);
        int stepX = startX < endX ? 1 : -1;
        int deltaY = -Mathf.Abs(endY - startY);
        int stepY = startY < endY ? 1 : -1;
        int error = deltaX + deltaY;

        while (true)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int pixelX = startX + offsetX;
                    int pixelY = startY + offsetY;
                    if (pixelX >= 0 && pixelX < textureSize && pixelY >= 0 && pixelY < textureSize)
                        pixels[pixelY * textureSize + pixelX] = new Color32(255, 255, 255, 255);
                }
            }

            if (startX == endX && startY == endY)
                break;

            int doubledError = error * 2;
            if (doubledError >= deltaY)
            {
                error += deltaY;
                startX += stepX;
            }
            if (doubledError <= deltaX)
            {
                error += deltaX;
                startY += stepY;
            }
        }
    }

    private static void CenterQteButton(Button button)
    {
        if (button == null)
            return;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, RespiratoryQteVerticalOffset);
        }

        RectTransform panelRect = button.transform.parent as RectTransform;
        if (panelRect == null)
            return;

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBackground = panelRect.GetComponent<Image>();
        if (panelBackground != null)
            panelBackground.enabled = false;
    }


    private static void SetButtonVisibility(Button button, bool isVisible)
    {
        if (button == null)
        {
            return;
        }

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    private void OnDisable()
    {
        RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (coughButton != null)
        {
            coughButton.onClick.RemoveListener(HandleCoughPressed);
        }

        if (testContaminatedAirButton != null)
        {
            testContaminatedAirButton.onClick.RemoveListener(ForceSpawnContaminatedAir);
        }
    }

    private void Update()
    {
        coughCooldownRemaining = Mathf.Max(0f, coughCooldownRemaining - GameplaySpeed.DeltaTime);
        RefreshCoughButton();
        RefreshBarrierGauge();
        RefreshBreachVisuals();
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        string eventId;
        if (eventData.eventName == InhaledDustOrAllergenEventName)
            eventId = "InhaledDustOrAllergen";
        else if (eventData.eventName == ColdFromSickPersonEventName)
            eventId = "ColdFromSickPerson";
        else if (eventData.eventName == ContactWithSickPersonEventName)
            eventId = "ContactWithSickPerson";
        else if (eventData.eventName == SmokedCigaretteEventName)
            eventId = "SmokedCigarette";
        else
            return;

        string eventKey = WellnessManager.BuildEventKey(eventId, eventData.day, eventData.hour);
        barrierHealth?.TakeDamage(BarrierDamagePerRandomEvent);
        contaminationWindows.Add(new ContaminationWindow
        {
            eventKey = eventKey,
            expiresAt = Time.time + ContaminatedAirDurationSeconds
        });
        if (eventData.triggersQTE)
            QTETracker.Register(QTETracker.RespiratoryLayerIndex, eventKey);
    }

    private IEnumerator RunSpawnLoop()
    {
        while (true)
        {
            string eventKey = GetActiveContaminationEventKey();
            GameObject prefab = eventKey != null ? contaminatedAirPrefab : airPrefab;
            SpawnAir(prefab, eventKey);
            yield return new WaitForSeconds(SpawnIntervalSeconds);
        }
    }

    private string GetActiveContaminationEventKey()
    {
        for (int index = contaminationWindows.Count - 1; index >= 0; index--)
        {
            if (Time.time >= contaminationWindows[index].expiresAt)
            {
                LayerSelectionHUD.EndThreatGlow(contaminationWindows[index].eventKey);
                contaminationWindows.RemoveAt(index);
            }
        }

        return contaminationWindows.Count > 0
            ? contaminationWindows[contaminationWindows.Count - 1].eventKey
            : null;
    }

    /// <summary>Immediately spawns one contaminated-air instance for testing lung infection behavior.</summary>
    public void ForceSpawnContaminatedAir()
    {
        if (contaminatedAirPrefab == null || airwaySpawnPoint == null || leftLung == null || rightLung == null)
        {
            Debug.LogWarning("RespiratorySystemController cannot run the contaminated-air test because required references are missing.", this);
            return;
        }

        SpawnAir(contaminatedAirPrefab, null);
    }

    private void SpawnAir(GameObject prefab, string eventKey)
    {
        if (prefab == null)
        {
            return;
        }

        NavMeshAgent prefabAgent = prefab.GetComponent<NavMeshAgent>();
        if (prefabAgent == null)
        {
            Debug.LogError($"Air prefab '{prefab.name}' requires a NavMeshAgent.", prefab);
            return;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = prefabAgent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        Transform lungPoint = UnityEngine.Random.value < 0.5f ? leftLung : rightLung;
        if (!NavMesh.SamplePosition(airwaySpawnPoint.position, out NavMeshHit airwayHit, NavMeshSampleRadius, filter))
        {
            Debug.LogWarning("No Respiratory System NavMesh position was found near Airway.", this);
            return;
        }

        if (!NavMesh.SamplePosition(lungPoint.position, out NavMeshHit lungHit, NavMeshSampleRadius, filter))
        {
            Debug.LogWarning($"No Respiratory System NavMesh position was found near {lungPoint.name}.", this);
            return;
        }

        GameObject air = Instantiate(prefab, airwayHit.position, airwaySpawnPoint.rotation, transform);
        NavMeshAgent agent = air.GetComponent<NavMeshAgent>();
        if (agent == null || (!agent.isOnNavMesh && !agent.Warp(airwayHit.position)))
        {
            Debug.LogWarning($"Spawned air '{air.name}' could not attach to the Respiratory System NavMesh.", air);
            Destroy(air);
            return;
        }

        RespiratoryAirAgent airAgent = air.GetComponent<RespiratoryAirAgent>();
        if (airAgent == null)
        {
            airAgent = air.AddComponent<RespiratoryAirAgent>();
        }

        bool isContaminatedAir = prefab == contaminatedAirPrefab;
        bool routeStarted;
        if (isContaminatedAir)
        {
            LungInfectionResponse infectionResponse = lungPoint.GetComponent<LungInfectionResponse>();
            if (infectionResponse == null)
            {
                Debug.LogError($"Lung point '{lungPoint.name}' requires a LungInfectionResponse component.", lungPoint);
                Destroy(air);
                return;
            }

            routeStarted = airAgent.InitializeForLungInfection(agent, lungHit.position, infectionResponse, ArrivalDistance, eventKey);
        }
        else
        {
            routeStarted = airAgent.Initialize(agent, lungHit.position, airwayHit.position, ArrivalDistance);
        }

        if (!routeStarted)
        {
            Debug.LogWarning($"Spawned air '{air.name}' could not start its route.", air);
            Destroy(air);
        }
    }

    private void HandleCoughPressed()
    {
        if (Time.timeScale <= 0f || coughButton == null || !coughButton.interactable)
        {
            return;
        }

        coughCooldownRemaining = CoughCooldownSeconds;
        barrierHealth?.RestoreBarrier(BarrierRestorePerAction);
        RefreshBarrierGauge();
        isBarrierRepairing = barrierHealth != null && barrierHealth.CurrentBarrier > 0f && barrierHealth.CurrentBarrier < BarrierMaximum;
        RefreshBreachVisuals();
        HashSet<string> eventKeys = new HashSet<string>();
        foreach (ContaminationWindow window in contaminationWindows)
        {
            if (!string.IsNullOrWhiteSpace(window.eventKey))
                eventKeys.Add(window.eventKey);
        }

        Transform[] respiratoryChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in respiratoryChildren)
        {
            if (candidate == transform)
            {
                continue;
            }

            RespiratoryAirAgent airAgent = candidate.GetComponent<RespiratoryAirAgent>();
            if (airAgent != null && !string.IsNullOrWhiteSpace(airAgent.WellnessEventKey))
                eventKeys.Add(airAgent.WellnessEventKey);

            bool hasAirAgent = airAgent != null;
            bool matchesAirPrefab = HasPrefabName(candidate.name, airPrefab) ||
                                    HasPrefabName(candidate.name, contaminatedAirPrefab);
            if (hasAirAgent || matchesAirPrefab)
            {
                Destroy(candidate.gameObject);
            }
        }

        contaminationWindows.Clear();
        WellnessManager wellnessManager = WellnessManager.Instance;
        if (wellnessManager != null)
        {
            foreach (string eventKey in eventKeys)
            {
                if (wellnessManager.TryAwardEventResponse(eventKey, "RespiratoryResponse", 0f, 5f, true))
                {
                    wellnessManager.ResolveEventQTE(eventKey);
                    QTETracker.Unregister(QTETracker.RespiratoryLayerIndex, eventKey);
                }
            }
        }

        RefreshCoughButton();
    }

    private static bool HasPrefabName(string objectName, GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        return objectName == prefab.name ||
               objectName == prefab.name + "(Clone)" ||
               objectName.StartsWith(prefab.name + " (");
    }

    private void RefreshCoughButton()
    {
        if (coughButton == null)
        {
            return;
        }

        float remainingCooldown = coughCooldownRemaining;
        bool isOnCooldown = remainingCooldown > 0f;
        coughButton.interactable = !isOnCooldown;
        if (coughButtonLabel != null)
        {
            coughButtonLabel.text = isOnCooldown ? $"COUGH ({Mathf.CeilToInt(remainingCooldown)}s)" : "COUGH";
        }
    }
}
