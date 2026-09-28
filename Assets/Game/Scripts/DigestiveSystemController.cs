using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Spawns food for scheduled meals, digestive events, and player commands, then routes it from Mouth to Anus.
/// </summary>
public sealed class DigestiveSystemController : MonoBehaviour
{
    private const int FoodPerBatch = 5;
    private const float SpawnIntervalSeconds = 0.5f;
    private const float ManualCooldownSeconds = 5f;
    private const float NavMeshSampleRadius = 50f;
    private const float ArrivalDistance = 1f;
    private const string OrdersContainerPath = "HUDCanvas/DigestiveOrdersContainer";

    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private GameObject contaminatedFoodPrefab;
    [SerializeField] private Transform mouthPoint;
    [SerializeField] private Transform anusPoint;
    [SerializeField] private DayCounterUI dayCounter;

    private readonly Queue<FoodBatchRequest> pendingBatches = new Queue<FoodBatchRequest>();
    private RoutineSystem routineSystem;
    private Button eatButton;
    private Button vomitButton;
    private TextMeshProUGUI eatLabel;
    private TextMeshProUGUI vomitLabel;
    private Coroutine batchQueueRoutine;
    private FoodBatchRequest currentBatch;
    private float eatCooldownUntil;
    private float vomitCooldownUntil;
    private int lastContaminatedEventDay = int.MinValue;
    private int lastContaminatedEventHour = -1;

    private bool IsSleeping => routineSystem != null && routineSystem.CurrentActivity == RoutineActivity.Sleeping;
    private bool IsBatchRunning => batchQueueRoutine != null || pendingBatches.Count > 0;

    private sealed class FoodBatchRequest
    {
        public GameObject prefab;
        public bool isScheduledMeal;
        public int day;
        public int hour;
        public readonly List<GameObject> spawnedFood = new List<GameObject>();
    }

    private void OnEnable()
    {
        RoutineSystem.OnRoutineActivityChanged += HandleRoutineActivityChanged;
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
    }

    private void Start()
    {
        if (mouthPoint == null)
            mouthPoint = transform.Find("Mouth");
        if (anusPoint == null)
            anusPoint = transform.Find("Anus");
        if (dayCounter == null)
            dayCounter = FindFirstObjectByType<DayCounterUI>();
        routineSystem = RoutineSystem.Instance;

        GameObject ordersContainer = GameObject.Find(OrdersContainerPath);
        if (ordersContainer != null)
        {
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
            }
        }

        if (eatButton != null)
            eatButton.onClick.AddListener(HandleEatPressed);
        else
            Debug.LogError("DigestiveOrdersContainer is missing its EatButton.", this);

        if (vomitButton != null)
            vomitButton.onClick.AddListener(HandleVomitPressed);
        else
            Debug.LogError("DigestiveOrdersContainer is missing its VomitButton.", this);

        if (mouthPoint == null || anusPoint == null)
            Debug.LogError("DigestiveSystemController requires Mouth and Anus transforms.", this);
        if (foodPrefab == null || contaminatedFoodPrefab == null)
            Debug.LogError("DigestiveSystemController requires both food prefab references.", this);
        if (dayCounter == null)
            Debug.LogError("DigestiveSystemController could not find the DayCounterUI.", this);

        RefreshButtonStates();
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

        RefreshButtonStates();
    }

    private void HandleRoutineActivityChanged(RoutineActivity activity)
    {
        if (activity == RoutineActivity.EatingBreakfast ||
            activity == RoutineActivity.EatingLunch ||
            activity == RoutineActivity.EatingDinner)
        {
            int day = dayCounter != null ? dayCounter.CurrentDay : -1;
            int hour = dayCounter != null ? dayCounter.CurrentHour : -1;
            if (day != lastContaminatedEventDay || hour != lastContaminatedEventHour)
                QueueFoodBatch(foodPrefab, true, day, hour);
        }

        RefreshButtonStates();
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData == null || IsSleeping ||
            (eventData.eventName != "Ate expired/spoiled food" && eventData.eventName != "Junk food binge"))
        {
            return;
        }

        lastContaminatedEventDay = eventData.day;
        lastContaminatedEventHour = eventData.hour;

        FoodBatchRequest scheduledMeal = FindScheduledMealBatch(eventData.day, eventData.hour);
        if (scheduledMeal != null)
        {
            scheduledMeal.prefab = contaminatedFoodPrefab;
            foreach (GameObject spawnedFood in scheduledMeal.spawnedFood)
            {
                if (spawnedFood != null)
                    Destroy(spawnedFood);
            }
            scheduledMeal.spawnedFood.Clear();
            return;
        }

        QueueFoodBatch(contaminatedFoodPrefab, false, eventData.day, eventData.hour);
    }

    private void HandleEatPressed()
    {
        if (IsSleeping || IsBatchRunning || Time.unscaledTime < eatCooldownUntil)
            return;

        eatCooldownUntil = Time.unscaledTime + ManualCooldownSeconds;
        QueueFoodBatch(foodPrefab, false, dayCounter != null ? dayCounter.CurrentDay : -1,
            dayCounter != null ? dayCounter.CurrentHour : -1);
        RefreshButtonStates();
    }

    private void HandleVomitPressed()
    {
        if (IsSleeping || Time.unscaledTime < vomitCooldownUntil)
            return;

        vomitCooldownUntil = Time.unscaledTime + ManualCooldownSeconds;
        StopPendingBatches();
        DestroyFoodOnDigestiveSystem();
        RefreshButtonStates();
    }

    private void QueueFoodBatch(GameObject prefab, bool isScheduledMeal, int day, int hour)
    {
        if (prefab == null || mouthPoint == null || anusPoint == null)
            return;

        pendingBatches.Enqueue(new FoodBatchRequest
        {
            prefab = prefab,
            isScheduledMeal = isScheduledMeal,
            day = day,
            hour = hour
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
            for (int index = 0; index < FoodPerBatch; index++)
            {
                if (currentBatch.prefab == null)
                    break;

                GameObject spawnedFood = SpawnFood(currentBatch.prefab);
                if (spawnedFood != null)
                    currentBatch.spawnedFood.Add(spawnedFood);

                if (index < FoodPerBatch - 1)
                    yield return new WaitForSeconds(SpawnIntervalSeconds);
            }

            currentBatch = null;
        }

        batchQueueRoutine = null;
        RefreshButtonStates();
    }

    private GameObject SpawnFood(GameObject prefab)
    {
        NavMeshAgent prefabAgent = prefab.GetComponent<NavMeshAgent>();
        if (prefabAgent == null)
        {
            Debug.LogError($"Food prefab '{prefab.name}' requires a NavMeshAgent.", prefab);
            return null;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = prefabAgent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(mouthPoint.position, out NavMeshHit spawnHit, NavMeshSampleRadius, filter))
        {
            Debug.LogWarning("No Digestive System NavMesh position was found near Mouth.", this);
            return null;
        }

        if (!NavMesh.SamplePosition(anusPoint.position, out NavMeshHit destinationHit, NavMeshSampleRadius, filter))
        {
            Debug.LogWarning("No Digestive System NavMesh position was found near Anus.", this);
            return null;
        }

        GameObject food = Instantiate(prefab, spawnHit.position, mouthPoint.rotation, transform);
        NavMeshAgent agent = food.GetComponent<NavMeshAgent>();
        if (agent == null || (!agent.isOnNavMesh && !agent.Warp(spawnHit.position)))
        {
            Debug.LogWarning($"Spawned food '{food.name}' could not attach to the Digestive System NavMesh.", food);
            Destroy(food);
            return null;
        }

        if (!agent.SetDestination(destinationHit.position))
        {
            Debug.LogWarning($"Spawned food '{food.name}' could not set its Anus destination.", food);
            Destroy(food);
            return null;
        }

        DigestiveFoodAgent foodAgent = food.GetComponent<DigestiveFoodAgent>();
        if (foodAgent == null)
            foodAgent = food.AddComponent<DigestiveFoodAgent>();
        foodAgent.Initialize(agent, ArrivalDistance);
        return food;
    }

    private FoodBatchRequest FindScheduledMealBatch(int day, int hour)
    {
        if (currentBatch != null && currentBatch.isScheduledMeal &&
            currentBatch.day == day && currentBatch.hour == hour)
        {
            return currentBatch;
        }

        foreach (FoodBatchRequest pendingBatch in pendingBatches)
        {
            if (pendingBatch.isScheduledMeal && pendingBatch.day == day && pendingBatch.hour == hour)
                return pendingBatch;
        }

        return null;
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

    private void DestroyFoodOnDigestiveSystem()
    {
        Transform[] digestiveChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in digestiveChildren)
        {
            if (candidate == transform)
                continue;

            bool hasFoodAgent = candidate.GetComponent<DigestiveFoodAgent>() != null;
            bool matchesFoodPrefab = HasPrefabName(candidate.name, foodPrefab) ||
                                     HasPrefabName(candidate.name, contaminatedFoodPrefab);
            if (hasFoodAgent || matchesFoodPrefab)
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
        float eatCooldownRemaining = Mathf.Max(0f, eatCooldownUntil - Time.unscaledTime);
        float vomitCooldownRemaining = Mathf.Max(0f, vomitCooldownUntil - Time.unscaledTime);

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
