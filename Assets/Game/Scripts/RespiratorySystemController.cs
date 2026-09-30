using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Spawns respiratory air from Airway, routes it through a lung and back, and handles allergen and cough behavior.
/// </summary>
public sealed class RespiratorySystemController : MonoBehaviour
{
    private const float SpawnIntervalSeconds = 1f;
    private const float ContaminatedAirDurationSeconds = 5f;
    private const float CoughCooldownSeconds = 5f;
    private const float NavMeshSampleRadius = 50f;
    private const float ArrivalDistance = 1f;
    private const string DustQTEId = "Inhaled dust/allergen";
    private const string ColdQTEId = "Caught a cold from a sick classmate/coworker";

    [SerializeField] private Transform airwaySpawnPoint;
    [SerializeField] private Transform leftLung;
    [SerializeField] private Transform rightLung;
    [SerializeField] private GameObject airPrefab;
    [SerializeField] private GameObject contaminatedAirPrefab;
    [SerializeField] private Button coughButton;
    [SerializeField] private TextMeshProUGUI coughButtonLabel;

    private float coughCooldownRemaining;
    private float contaminatedAirRemaining;
    private Coroutine spawnRoutine;

    private void OnEnable()
    {
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
    }

    private void Start()
    {
        if (coughButton != null)
            coughButton.onClick.AddListener(HandleCoughPressed);
        else
            Debug.LogError("RespiratorySystemController requires a Cough button.", this);

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
            coughButton.onClick.RemoveListener(HandleCoughPressed);
    }

    private void Update()
    {
        float gameplayDelta = GameplaySpeed.DeltaTime;
        coughCooldownRemaining = Mathf.Max(0f, coughCooldownRemaining - gameplayDelta);
        contaminatedAirRemaining = Mathf.Max(0f, contaminatedAirRemaining - gameplayDelta);
        RefreshCoughButton();
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData != null && eventData.eventName == DustQTEId)
            contaminatedAirRemaining = Mathf.Max(contaminatedAirRemaining, ContaminatedAirDurationSeconds);
    }

    private IEnumerator RunSpawnLoop()
    {
        while (true)
        {
            GameObject prefab = contaminatedAirRemaining > 0f ? contaminatedAirPrefab : airPrefab;
            SpawnAir(prefab);
            yield return GameplaySpeed.WaitForGameplaySeconds(SpawnIntervalSeconds);
        }
    }

    private void SpawnAir(GameObject prefab)
    {
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

        GameplaySpeedNavMeshAgent.Register(agent);
        RespiratoryAirAgent airAgent = air.GetComponent<RespiratoryAirAgent>();
        if (airAgent == null)
            airAgent = air.AddComponent<RespiratoryAirAgent>();

        if (!airAgent.Initialize(agent, lungHit.position, airwayHit.position, ArrivalDistance))
        {
            Debug.LogWarning($"Spawned air '{air.name}' could not start its route.", air);
            Destroy(air);
        }
    }

    private void HandleCoughPressed()
    {
        if (coughButton == null || !coughButton.interactable)
            return;

        coughCooldownRemaining = CoughCooldownSeconds;
        GameplaySpeed.ResolveQTE(DustQTEId);
        GameplaySpeed.ResolveQTE(ColdQTEId);
        Transform[] respiratoryChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in respiratoryChildren)
        {
            if (candidate == transform)
                continue;

            bool hasAirAgent = candidate.GetComponent<RespiratoryAirAgent>() != null;
            bool matchesAirPrefab = HasPrefabName(candidate.name, airPrefab) ||
                                    HasPrefabName(candidate.name, contaminatedAirPrefab);
            if (hasAirAgent || matchesAirPrefab)
                Destroy(candidate.gameObject);
        }

        RefreshCoughButton();
    }

    private static bool HasPrefabName(string objectName, GameObject prefab)
    {
        if (prefab == null)
            return false;

        return objectName == prefab.name ||
               objectName == prefab.name + "(Clone)" ||
               objectName.StartsWith(prefab.name + " (");
    }

    private void RefreshCoughButton()
    {
        if (coughButton == null)
            return;

        float remainingCooldown = coughCooldownRemaining;
        bool isOnCooldown = remainingCooldown > 0f;
        coughButton.interactable = !isOnCooldown;
        if (coughButtonLabel != null)
            coughButtonLabel.text = isOnCooldown ? $"COUGH ({Mathf.CeilToInt(remainingCooldown)}s)" : "COUGH";
    }
}
