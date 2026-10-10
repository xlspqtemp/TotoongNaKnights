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

    [SerializeField] private Transform airwaySpawnPoint;
    [SerializeField] private Transform leftLung;
    [SerializeField] private Transform rightLung;
    [SerializeField] private GameObject airPrefab;
    [SerializeField] private GameObject contaminatedAirPrefab;
    [SerializeField] private Button coughButton;
    [SerializeField] private TextMeshProUGUI coughButtonLabel;
    [SerializeField] private Button testContaminatedAirButton;

    private sealed class ContaminationWindow
    {
        public string eventKey;
        public float expiresAt;
    }

    private readonly List<ContaminationWindow> contaminationWindows = new List<ContaminationWindow>();
    private float coughCooldownRemaining;
    private Coroutine spawnRoutine;

    private void OnEnable()
    {
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
    }

    private void Start()
    {
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
        if (coughButton == null || !coughButton.interactable)
        {
            return;
        }

        coughCooldownRemaining = CoughCooldownSeconds;
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
