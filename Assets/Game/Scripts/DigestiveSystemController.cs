using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Spawns scheduled food and fluid batches, then routes each item through the digestive pathway.
/// </summary>
public sealed class DigestiveSystemController : MonoBehaviour
{
    private const int ItemsPerBatch = 5;
    private const float SpawnIntervalSeconds = 0.5f;
    private const float ManualCooldownSeconds = 5f;
    private const float DefaultTransitSpeed = 20f;
    private const string OrdersContainerPath = "HUDCanvas/DigestiveOrdersContainer";
    private const string SpoiledFoodQTEId = "Ate expired/spoiled food";
    private const string ExpiredFoodEventName = "Ate expired/spoiled food";
    private const string JunkFoodEventName = "Junk food binge";
    private const string AccidentallyAteSpoiledFoodEventName = "Accidentally Ate Spoiled Food";
    private const string ContaminatedWaterEventName = "Accidentally Drank Contaminated Water";
    private const float BarrierDamagePerRandomEvent = 20f;
    private const float BarrierRestorePerAction = 20f;
    private const float BarrierMaximum = 100f;
    private const string BarrierGaugeObjectName = "DigestiveBarrierGauge";
    private const float BarrierGaugeWidth = 600f;
    private const float BarrierGaugeHeight = 600f;
    private const float BarrierGaugeVerticalOffset = -338f;
    private const float DigestiveQteVerticalOffset = -357f;
    private static Sprite barrierGaugeSprite;

    [SerializeField] private Image barrierGaugeImage;
    [SerializeField] private TextMeshProUGUI barrierTitleLabel;
    [SerializeField] private TextMeshProUGUI barrierPercentLabel;

    private static readonly string[] DefaultPathwayObjectPaths =
    {
        "/Digestive/Pathway/Stomach",
        "/Digestive/Pathway/Intestine Entry",
        "/Digestive/Pathway/Intestine",
        "/Digestive/Pathway/Intestine (1)",
        "/Digestive/Pathway/Intestine",
        "/Digestive/Pathway/Intestine Entry",
        "/Digestive/Pathway/Large Intestine",
        "/Digestive/Pathway/Large Intestine (1)",
        "/Digestive/Pathway/Anus"
    };

    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private GameObject contaminatedFoodPrefab;
    [SerializeField] private GameObject fluidPrefab;
    [SerializeField] private GameObject contaminatedFluidPrefab;
    [SerializeField] private Transform mouthPoint;
    [SerializeField] private Transform anusPoint;
    [SerializeField] private Transform[] pathwayWaypoints;
    [SerializeField] private DayCounterUI dayCounter;

    private readonly Queue<DigestiveBatchRequest> pendingBatches = new Queue<DigestiveBatchRequest>();
    private RoutineSystem routineSystem;
    private Button eatButton;
    private Button vomitButton;
    private TextMeshProUGUI eatLabel;
    private TextMeshProUGUI vomitLabel;
    private Coroutine batchQueueRoutine;
    private DigestiveBatchRequest currentBatch;
    private float eatCooldownRemaining;
    private float vomitCooldownRemaining;
    private int lastContaminatedFoodEventDay = int.MinValue;
    private int lastContaminatedFoodEventHour = -1;
    private int lastContaminatedWaterEventDay = int.MinValue;
    private int lastContaminatedWaterEventHour = -1;
    private BarrierHealth barrierHealth;

    private bool IsSleeping => routineSystem != null && routineSystem.CurrentActivity == RoutineActivity.Sleeping;
    private bool IsBatchRunning => batchQueueRoutine != null || pendingBatches.Count > 0;

    private sealed class DigestiveBatchRequest
    {
        public GameObject prefab;
        public bool isScheduledMeal;
        public bool isScheduledWater;
        public int day;
        public int hour;
        public string eventKey;
        public readonly List<GameObject> spawnedItems = new List<GameObject>();
    }

    private void OnEnable()
    {
        RoutineSystem.OnRoutineActivityChanged += HandleRoutineActivityChanged;
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
    }

    private void Start()
    {
        if (mouthPoint == null)
            mouthPoint = GameObject.Find("/Digestive/Pathway/Mouth")?.transform;
        if (anusPoint == null)
            anusPoint = GameObject.Find("/Digestive/Pathway/Anus")?.transform;
        if (pathwayWaypoints == null || pathwayWaypoints.Length == 0)
            pathwayWaypoints = FindDefaultPathwayWaypoints();
        if (anusPoint == null && pathwayWaypoints != null && pathwayWaypoints.Length > 0)
            anusPoint = pathwayWaypoints[pathwayWaypoints.Length - 1];
        if (dayCounter == null)
            dayCounter = FindFirstObjectByType<DayCounterUI>();
        routineSystem = RoutineSystem.Instance;
        InitializeBarrierHealth();

        GameObject ordersContainer = GameObject.Find(OrdersContainerPath);
        if (ordersContainer != null)
        {
            CenterOrdersContainer(ordersContainer.GetComponent<RectTransform>());
            InitializeBarrierGauge(ordersContainer.GetComponent<RectTransform>());
            Transform eatTransform = ordersContainer.transform.Find("EatButton");
            Transform vomitTransform = ordersContainer.transform.Find("VomitButton");
            if (eatTransform != null)
            {
                eatButton = eatTransform.GetComponent<Button>();
                eatLabel = eatTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (vomitTransform != null)
            {
                vomitButton = vomitTransform.GetComponent<Button>();
                vomitLabel = vomitTransform.GetComponentInChildren<TextMeshProUGUI>(true);
                CenterQteButton(vomitButton);
            }
        }

        if (eatButton != null)
            eatButton.gameObject.SetActive(false);

        if (vomitButton != null)
            vomitButton.onClick.AddListener(HandleVomitPressed);
        else
            Debug.LogError("DigestiveOrdersContainer is missing its VomitButton.", this);

        if (mouthPoint == null || !HasCompletePathway())
            Debug.LogError("DigestiveSystemController requires Mouth and all digestive pathway waypoints through Anus.", this);
        if (foodPrefab == null || contaminatedFoodPrefab == null || fluidPrefab == null || contaminatedFluidPrefab == null)
            Debug.LogError("DigestiveSystemController requires food, contaminated food, fluid, and contaminated fluid prefab references.", this);
        if (dayCounter == null)
            Debug.LogError("DigestiveSystemController could not find the DayCounterUI.", this);

        RefreshButtonStates();
    }

    private void InitializeBarrierHealth()
    {
        GameObject digestiveRoot = GameObject.Find("/Digestive");
        if (digestiveRoot == null)
        {
            Debug.LogError("DigestiveSystemController could not find the Digestive layer root for BarrierHealth.", this);
            return;
        }

        barrierHealth = digestiveRoot.GetComponent<BarrierHealth>();
        if (barrierHealth == null)
            barrierHealth = digestiveRoot.AddComponent<BarrierHealth>();
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

    private static void CenterQteButton(Button button)
    {
        if (button == null)
            return;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect == null)
            return;

        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, DigestiveQteVerticalOffset);
    }

    private static void CenterOrdersContainer(RectTransform panelRect)
    {
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

    private void OnDisable()
    {
        RoutineSystem.OnRoutineActivityChanged -= HandleRoutineActivityChanged;
        RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;
    }

    private void OnDestroy()
    {
        if (eatButton != null)
            eatButton.onClick.RemoveListener(HandleEatPressed);
        if (vomitButton != null)
            vomitButton.onClick.RemoveListener(HandleVomitPressed);
    }

    private void Update()
    {
        if (routineSystem == null)
            routineSystem = RoutineSystem.Instance;

        float gameplayDelta = GameplaySpeed.DeltaTime;
        eatCooldownRemaining = Mathf.Max(0f, eatCooldownRemaining - gameplayDelta);
        vomitCooldownRemaining = Mathf.Max(0f, vomitCooldownRemaining - gameplayDelta);
        RefreshButtonStates();
        RefreshBarrierGauge();
    }

    private void HandleRoutineActivityChanged(RoutineActivity activity)
    {
        int day = dayCounter != null ? dayCounter.CurrentDay : -1;
        int hour = dayCounter != null ? dayCounter.CurrentHour : -1;

        if (activity == RoutineActivity.EatingBreakfast ||
            activity == RoutineActivity.EatingLunch ||
            activity == RoutineActivity.EatingDinner)
        {
            if (FindScheduledMealBatch(day, hour) == null)
            {
                bool contaminated = day == lastContaminatedFoodEventDay && hour == lastContaminatedFoodEventHour;
                QueueBatch(contaminated ? contaminatedFoodPrefab : foodPrefab, true, false, day, hour);
            }
        }
        else if (activity == RoutineActivity.DrinkingWater)
        {
            if (FindScheduledWaterBatch(day, hour) == null)
            {
                bool contaminated = day == lastContaminatedWaterEventDay && hour == lastContaminatedWaterEventHour;
                QueueBatch(contaminated ? contaminatedFluidPrefab : fluidPrefab, false, true, day, hour);
            }
        }

        RefreshButtonStates();
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData == null || IsSleeping)
            return;

        if (eventData.eventName == ExpiredFoodEventName ||
            eventData.eventName == JunkFoodEventName ||
            eventData.eventName == AccidentallyAteSpoiledFoodEventName)
        {
            HandleContaminatedFoodEvent(eventData);
        }
        else if (eventData.eventName == ContaminatedWaterEventName)
        {
            HandleContaminatedWaterEvent(eventData);
        }
    }

    private void HandleContaminatedFoodEvent(RandomEventData eventData)
    {
        barrierHealth?.TakeDamage(BarrierDamagePerRandomEvent);
        lastContaminatedFoodEventDay = eventData.day;
        lastContaminatedFoodEventHour = eventData.hour;
        string eventId = eventData.eventName == ExpiredFoodEventName
            ? "AteExpiredFood"
            : eventData.eventName == JunkFoodEventName
                ? "JunkFoodBinge"
                : "AccidentallyAteSpoiledFood";
        string eventKey = WellnessManager.BuildEventKey(eventId, eventData.day, eventData.hour);
        if (eventData.triggersQTE)
            QTETracker.Register(QTETracker.DigestiveLayerIndex, eventKey);

        DigestiveBatchRequest scheduledMeal = FindScheduledMealBatch(eventData.day, eventData.hour);
        if (scheduledMeal != null)
        {
            ReplaceBatchPrefab(scheduledMeal, contaminatedFoodPrefab, eventKey);
            return;
        }

        QueueBatch(contaminatedFoodPrefab, true, false, eventData.day, eventData.hour, eventKey);
    }

    private void HandleContaminatedWaterEvent(RandomEventData eventData)
    {
        barrierHealth?.TakeDamage(BarrierDamagePerRandomEvent);
        lastContaminatedWaterEventDay = eventData.day;
        lastContaminatedWaterEventHour = eventData.hour;
        string eventKey = WellnessManager.BuildEventKey("ContaminatedWater", eventData.day, eventData.hour);

        DigestiveBatchRequest scheduledWater = FindScheduledWaterBatch(eventData.day, eventData.hour);
        if (scheduledWater != null)
        {
            ReplaceBatchPrefab(scheduledWater, contaminatedFluidPrefab, eventKey);
            return;
        }

        QueueBatch(contaminatedFluidPrefab, false, true, eventData.day, eventData.hour, eventKey);
    }

    private static void ReplaceBatchPrefab(DigestiveBatchRequest batch, GameObject replacementPrefab, string eventKey)
    {
        batch.prefab = replacementPrefab;
        batch.eventKey = eventKey;
        foreach (GameObject spawnedItem in batch.spawnedItems)
        {
            if (spawnedItem != null)
                Destroy(spawnedItem);
        }
        batch.spawnedItems.Clear();
    }

    private void HandleEatPressed()
    {
        if (IsSleeping || IsBatchRunning || eatCooldownRemaining > 0f)
            return;

        eatCooldownRemaining = ManualCooldownSeconds;
        barrierHealth?.RestoreBarrier(BarrierRestorePerAction);
        QueueBatch(foodPrefab, false, false, dayCounter != null ? dayCounter.CurrentDay : -1,
            dayCounter != null ? dayCounter.CurrentHour : -1);
        RefreshButtonStates();
    }

    private void HandleVomitPressed()
    {
        if (IsSleeping || vomitCooldownRemaining > 0f)
            return;

        vomitCooldownRemaining = ManualCooldownSeconds;
        barrierHealth?.RestoreBarrier(BarrierRestorePerAction);
        HashSet<string> eventKeys = FindActiveContaminationEventKeys();
        GameplaySpeed.ResolveQTE(SpoiledFoodQTEId);
        StopPendingBatches();
        DestroyDigestiveItems();

        WellnessManager wellnessManager = WellnessManager.Instance;
        if (wellnessManager != null)
        {
            foreach (string eventKey in eventKeys)
            {
                wellnessManager.TryAwardEventResponse(eventKey, "DigestiveResponse", 3f, 5f);
                wellnessManager.ResolveEventQTE(eventKey);
                QTETracker.Unregister(QTETracker.DigestiveLayerIndex, eventKey);
            }
        }

        RefreshButtonStates();
    }

    private HashSet<string> FindActiveContaminationEventKeys()
    {
        HashSet<string> eventKeys = new HashSet<string>();
        if (currentBatch != null && !string.IsNullOrWhiteSpace(currentBatch.eventKey))
            eventKeys.Add(currentBatch.eventKey);

        foreach (DigestiveBatchRequest pendingBatch in pendingBatches)
        {
            if (!string.IsNullOrWhiteSpace(pendingBatch.eventKey))
                eventKeys.Add(pendingBatch.eventKey);
        }

        DigestiveFoodAgent[] activeFoodAgents = GetComponentsInChildren<DigestiveFoodAgent>(true);
        foreach (DigestiveFoodAgent foodAgent in activeFoodAgents)
        {
            if (foodAgent != null && !string.IsNullOrWhiteSpace(foodAgent.WellnessEventKey))
                eventKeys.Add(foodAgent.WellnessEventKey);
        }

        return eventKeys;
    }

    private void QueueBatch(GameObject prefab, bool isScheduledMeal, bool isScheduledWater, int day, int hour, string eventKey = null)
    {
        if (prefab == null || mouthPoint == null || !HasCompletePathway())
            return;

        pendingBatches.Enqueue(new DigestiveBatchRequest
        {
            prefab = prefab,
            isScheduledMeal = isScheduledMeal,
            isScheduledWater = isScheduledWater,
            day = day,
            hour = hour,
            eventKey = eventKey
        });

        if (batchQueueRoutine == null)
            batchQueueRoutine = StartCoroutine(ProcessBatchQueue());

        RefreshButtonStates();
    }

    private IEnumerator ProcessBatchQueue()
    {
        while (pendingBatches.Count > 0)
        {
            currentBatch = pendingBatches.Dequeue();
            for (int index = 0; index < ItemsPerBatch; index++)
            {
                if (currentBatch.prefab == null)
                    break;

                GameObject spawnedItem = SpawnDigestiveItem(currentBatch.prefab, currentBatch.eventKey);
                if (spawnedItem != null)
                    currentBatch.spawnedItems.Add(spawnedItem);

                if (index < ItemsPerBatch - 1)
                    yield return GameplaySpeed.WaitForGameplaySeconds(SpawnIntervalSeconds);
            }

            currentBatch = null;
        }

        batchQueueRoutine = null;
        RefreshButtonStates();
    }

    private GameObject SpawnDigestiveItem(GameObject prefab, string eventKey)
    {
        GameObject item = Instantiate(prefab, mouthPoint.position, mouthPoint.rotation, transform);
        NavMeshAgent navMeshAgent = item.GetComponent<NavMeshAgent>();
        float movementSpeed = navMeshAgent != null ? Mathf.Max(0.01f, navMeshAgent.speed) : DefaultTransitSpeed;
        if (navMeshAgent != null)
            navMeshAgent.enabled = false;

        DigestiveFoodAgent pathFollower = item.GetComponent<DigestiveFoodAgent>();
        if (pathFollower == null)
            pathFollower = item.AddComponent<DigestiveFoodAgent>();
        pathFollower.Initialize(pathwayWaypoints, movementSpeed, eventKey);
        return item;
    }

    private Transform[] FindDefaultPathwayWaypoints()
    {
        Transform[] foundWaypoints = new Transform[DefaultPathwayObjectPaths.Length];
        for (int index = 0; index < DefaultPathwayObjectPaths.Length; index++)
        {
            GameObject waypointObject = GameObject.Find(DefaultPathwayObjectPaths[index]);
            if (waypointObject == null)
                return Array.Empty<Transform>();
            foundWaypoints[index] = waypointObject.transform;
        }

        return foundWaypoints;
    }

    private bool HasCompletePathway()
    {
        if (pathwayWaypoints == null || pathwayWaypoints.Length != DefaultPathwayObjectPaths.Length)
            return false;

        for (int index = 0; index < pathwayWaypoints.Length; index++)
        {
            if (pathwayWaypoints[index] == null)
                return false;
        }

        return anusPoint == null || pathwayWaypoints[pathwayWaypoints.Length - 1] == anusPoint;
    }

    private DigestiveBatchRequest FindScheduledMealBatch(int day, int hour)
    {
        return FindScheduledRoutineBatch(day, hour, true);
    }

    private DigestiveBatchRequest FindScheduledWaterBatch(int day, int hour)
    {
        return FindScheduledRoutineBatch(day, hour, false);
    }

    private DigestiveBatchRequest FindScheduledRoutineBatch(int day, int hour, bool isMeal)
    {
        if (currentBatch != null && MatchesScheduledRoutine(currentBatch, day, hour, isMeal))
            return currentBatch;

        foreach (DigestiveBatchRequest pendingBatch in pendingBatches)
        {
            if (MatchesScheduledRoutine(pendingBatch, day, hour, isMeal))
                return pendingBatch;
        }

        return null;
    }

    private static bool MatchesScheduledRoutine(DigestiveBatchRequest batch, int day, int hour, bool isMeal)
    {
        return (isMeal ? batch.isScheduledMeal : batch.isScheduledWater) && batch.day == day && batch.hour == hour;
    }

    private static void DestroySpawnedItems(DigestiveBatchRequest batch)
    {
        foreach (GameObject spawnedItem in batch.spawnedItems)
        {
            if (spawnedItem != null)
                Destroy(spawnedItem);
        }
        batch.spawnedItems.Clear();
    }

    private void StopPendingBatches()
    {
        pendingBatches.Clear();
        currentBatch = null;
        if (batchQueueRoutine != null)
        {
            StopCoroutine(batchQueueRoutine);
            batchQueueRoutine = null;
        }
    }

    private void DestroyDigestiveItems()
    {
        Transform[] digestiveChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in digestiveChildren)
        {
            if (candidate == transform)
                continue;

            bool hasPathFollower = candidate.GetComponent<DigestiveFoodAgent>() != null;
            bool matchesPrefab = HasPrefabName(candidate.name, foodPrefab) ||
                                 HasPrefabName(candidate.name, contaminatedFoodPrefab) ||
                                 HasPrefabName(candidate.name, fluidPrefab) ||
                                 HasPrefabName(candidate.name, contaminatedFluidPrefab);
            if (hasPathFollower || matchesPrefab)
                Destroy(candidate.gameObject);
        }
    }

    private static bool HasPrefabName(string objectName, GameObject prefab)
    {
        if (prefab == null)
            return false;

        return objectName == prefab.name ||
               objectName == prefab.name + "(Clone)" ||
               objectName.StartsWith(prefab.name + " (", StringComparison.Ordinal);
    }

    private void RefreshButtonStates()
    {
        bool sleeping = IsSleeping;
        float eatCooldownRemaining = this.eatCooldownRemaining;
        float vomitCooldownRemaining = this.vomitCooldownRemaining;

        if (eatButton != null)
        {
            eatButton.interactable = !sleeping && !IsBatchRunning && eatCooldownRemaining <= 0f;
            if (eatLabel != null)
            {
                eatLabel.text = sleeping ? "Eat (Sleeping)" :
                    eatCooldownRemaining > 0f ? $"Eat ({Mathf.CeilToInt(eatCooldownRemaining)}s)" :
                    IsBatchRunning ? "Eating..." : "Eat";
            }
        }

        if (vomitButton != null)
        {
            vomitButton.interactable = !sleeping && vomitCooldownRemaining <= 0f;
            if (vomitLabel != null)
            {
                vomitLabel.text = sleeping ? "Vomit (Sleeping)" :
                    vomitCooldownRemaining > 0f ? $"Vomit ({Mathf.CeilToInt(vomitCooldownRemaining)}s)" : "Vomit";
            }
        }
    }
}
