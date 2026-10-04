using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Spawns stationary bacteria from each skin breach lane while any wound remains open.</summary>
public sealed class BacteriaOutbreakController : MonoBehaviour
{
    private const float DefaultSpawnIntervalSeconds = 10f;
    private const float DefaultNavMeshSampleRadius = 10f;

    [SerializeField] private GameObject bacteriaPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(0.1f)] private float spawnIntervalSeconds = DefaultSpawnIntervalSeconds;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private Coroutine spawnRoutine;

    private void OnEnable()
    {
        WoundRepairPoint.OnSkinBreachStateChanged += HandleSkinBreachStateChanged;
    }

    private void Start()
    {
        UpdateSpawningState(WoundRepairPoint.HasActiveBreaches);
    }

    private void OnDisable()
    {
        WoundRepairPoint.OnSkinBreachStateChanged -= HandleSkinBreachStateChanged;
        StopSpawning();
    }

    private void HandleSkinBreachStateChanged(bool hasActiveBreaches)
    {
        UpdateSpawningState(hasActiveBreaches || WoundRepairPoint.HasActiveBreaches);
    }

    private void UpdateSpawningState(bool hasActiveBreaches)
    {
        if (hasActiveBreaches)
        {
            if (spawnRoutine == null && isActiveAndEnabled)
            {
                spawnRoutine = StartCoroutine(SpawnAtEveryOpenLane());
            }
        }
        else
        {
            StopSpawning();
        }
    }

    private IEnumerator SpawnAtEveryOpenLane()
    {
        while (WoundRepairPoint.HasActiveBreaches)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, spawnIntervalSeconds));
            if (WellnessManager.Instance != null && WellnessManager.Instance.HasRunEnded)
            {
                break;
            }

            if (!WoundRepairPoint.HasActiveBreaches)
            {
                break;
            }

            SpawnAtAllPoints();
        }

        spawnRoutine = null;
    }

    private void SpawnAtAllPoints()
    {
        if (bacteriaPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("BacteriaOutbreakController needs a bacteria prefab and at least one spawn point.", this);
            return;
        }

        string eventKey = WoundRepairPoint.GetActiveWellnessEventKey();
        for (int index = 0; index < spawnPoints.Length; index++)
        {
            Transform spawnPoint = spawnPoints[index];
            if (spawnPoint == null)
            {
                continue;
            }

            NavMeshAgent prefabAgent = bacteriaPrefab.GetComponent<NavMeshAgent>();
            if (prefabAgent == null)
            {
                Debug.LogError($"Bacteria prefab '{bacteriaPrefab.name}' requires a NavMeshAgent.", bacteriaPrefab);
                return;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = prefabAgent.agentTypeID,
                areaMask = NavMesh.AllAreas
            };

            if (!NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit spawnHit, Mathf.Max(0.1f, navMeshSampleRadius), filter))
            {
                Debug.LogWarning($"No NavMesh position was found near bacteria spawn point '{spawnPoint.name}'.", spawnPoint);
                continue;
            }

            GameObject bacteriaObject = Instantiate(bacteriaPrefab, spawnHit.position, spawnPoint.rotation, transform);
            NavMeshAgent bacteriaNavMeshAgent = bacteriaObject.GetComponent<NavMeshAgent>();
            BacteriaAgent bacteriaAgent = bacteriaObject.GetComponent<BacteriaAgent>();
            if (bacteriaAgent == null)
            {
                bacteriaAgent = bacteriaObject.AddComponent<BacteriaAgent>();
            }

            if (bacteriaNavMeshAgent == null || bacteriaAgent == null ||
                !bacteriaAgent.InitializeStationary(bacteriaNavMeshAgent, spawnHit.position, eventKey))
            {
                Debug.LogWarning($"Bacteria '{bacteriaObject.name}' could not be placed as a stationary NavMesh target.", bacteriaObject);
                Destroy(bacteriaObject);
            }
        }
    }

    private void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }
}
