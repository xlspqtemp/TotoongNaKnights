using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Spawns paired immune cells and assigns them a randomized one-way circulatory loop.</summary>
public sealed class CirculatorySystemController : MonoBehaviour
{
    private const int DefaultPairsToSpawn = 5;
    private const float DefaultSpawnIntervalSeconds = 1f;
    private const float DefaultNavMeshSampleRadius = 10f;
    private const float NeutrophilSpawnSpacing = 1.25f;
 
    private const string DestinationTargetName = "Destination - Target";
    private const string LungPointName = "Point - Lung";

    [SerializeField] private GameObject neutrophilPrefab;
    [SerializeField] private GameObject macrophagePrefab;
    [SerializeField] private Transform destinationPointContainer;
    [SerializeField] private Transform heart;
    [SerializeField, Min(0)] private int pairsToSpawn = DefaultPairsToSpawn;
    [SerializeField, Min(0.1f)] private float spawnIntervalSeconds = DefaultSpawnIntervalSeconds;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private Transform[] destinationPoints;
    private Transform[] lungPoints;

    private void Start()
    {
        ResolveWaypoints();
        if (!HasRequiredSpawnData())
        {
            Debug.LogError("CirculatorySystemController requires both cell prefabs, the heart, and destination and lung waypoints under its waypoint container.", this);
            return;
        }

        StartCoroutine(SpawnPairsOverTime());
    }


    private void ResolveWaypoints()
    {
        if (destinationPointContainer == null)
        {
            destinationPoints = new Transform[0];
            lungPoints = new Transform[0];
            return;
        }

        Transform[] candidates = destinationPointContainer.GetComponentsInChildren<Transform>(true);
        destinationPoints = FindNamedWaypoints(candidates, DestinationTargetName);
        lungPoints = FindNamedWaypoints(candidates, LungPointName);
    }

    private static Transform[] FindNamedWaypoints(Transform[] candidates, string waypointName)
    {
        int matchingCount = 0;
        foreach (Transform candidate in candidates)
        {
            if (candidate != null && candidate.name == waypointName)
            {
                matchingCount++;
            }
        }

        Transform[] matches = new Transform[matchingCount];
        int matchIndex = 0;
        foreach (Transform candidate in candidates)
        {
            if (candidate != null && candidate.name == waypointName)
            {
                matches[matchIndex] = candidate;
                matchIndex++;
            }
        }

        return matches;
    }

    private IEnumerator SpawnPairsOverTime()
    {
        int pairCount = Mathf.Max(0, pairsToSpawn);
        float intervalSeconds = Mathf.Max(0.1f, spawnIntervalSeconds);

        for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            SpawnRandomPair();
            if (pairIndex + 1 < pairCount)
            {
                yield return new WaitForSeconds(intervalSeconds);
            }
        }
    }

    private void SpawnRandomPair()
    {
        Transform destinationPoint = SelectRandomDestinationPoint();
        if (destinationPoint == null)
        {
            Debug.LogWarning("CirculatorySystemController could not find a valid external destination point.", this);
            return;
        }

        SpawnCellPairMember(neutrophilPrefab, destinationPoint, heart.position);
        SpawnCellPairMember(macrophagePrefab, destinationPoint, heart.position);
    }

    /// <summary>Spawns a requested immune-cell pair at a tactical spawn point while keeping a randomized route target.</summary>
    public bool SpawnPairAt(Transform spawnPoint)
    {
        Transform destinationPoint = SelectRandomDestinationPoint();
        if (spawnPoint == null || destinationPoint == null || !HasRequiredSpawnData())
        {
            return false;
        }

        bool spawnedNeutrophil = SpawnCellPairMember(neutrophilPrefab, destinationPoint, spawnPoint.position);
        bool spawnedMacrophage = SpawnCellPairMember(macrophagePrefab, destinationPoint, spawnPoint.position);
        return spawnedNeutrophil && spawnedMacrophage;
    }

    /// <summary>Spawns a group of neutrophils at the requested circulatory anchor using the configured WBC prefab and NavMesh setup.</summary>
    public GameObject[] SpawnNeutrophilsAt(Transform spawnPoint, int count)
    {
        if (count <= 0 || neutrophilPrefab == null)
        {
            return new GameObject[0];
        }

        ResolveWaypoints();
        if (heart == null || destinationPointContainer == null || !HasAnyTarget(destinationPoints) || !HasAnyTarget(lungPoints))
        {
            Debug.LogWarning("CirculatorySystemController cannot spawn a WBC squad because heart or route waypoint references are missing.", this);
            return new GameObject[0];
        }

        Transform resolvedSpawnPoint = spawnPoint != null ? spawnPoint : heart;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        float spacing = Mathf.Max(1.1f, NeutrophilSpawnSpacing);
        List<GameObject> spawnedUnits = new List<GameObject>(count);
        for (int cellIndex = 0; cellIndex < count; cellIndex++)
        {
            Transform destinationPoint = SelectRandomDestinationPoint();
            if (destinationPoint == null)
                continue;

            int row = cellIndex / columns;
            int column = cellIndex % columns;
            float offsetX = (column - (columns - 1) * 0.5f) * spacing;
            float offsetZ = (row - (Mathf.CeilToInt((float)count / columns) - 1) * 0.5f) * spacing;
            Vector3 requestedPosition = resolvedSpawnPoint.position + resolvedSpawnPoint.right * offsetX + resolvedSpawnPoint.forward * offsetZ;
            if (!SpawnCellPairMember(neutrophilPrefab, destinationPoint, requestedPosition, out GameObject spawnedCell))
                continue;

            NavMeshAgent agent = spawnedCell.GetComponent<NavMeshAgent>();
            CirculatoryCellRoute route = spawnedCell.GetComponent<CirculatoryCellRoute>();
            if (agent == null || route == null)
            {
                spawnedCell.SetActive(false);
                continue;
            }

            agent.ResetPath();
            agent.isStopped = true;
            route.enabled = false;
            spawnedUnits.Add(spawnedCell);
        }

        return spawnedUnits.ToArray();
    }

    private bool SpawnCellPairMember(GameObject cellPrefab, Transform destinationPoint, Vector3 requestedPosition)
    {
        return SpawnCellPairMember(cellPrefab, destinationPoint, requestedPosition, out _);
    }

    private bool SpawnCellPairMember(GameObject cellPrefab, Transform destinationPoint, Vector3 requestedPosition, out GameObject spawnedCell)
    {
        spawnedCell = null;
        if (cellPrefab == null || destinationPoint == null)
        {
            return false;
        }

        NavMeshAgent prefabAgent = cellPrefab.GetComponent<NavMeshAgent>();
        if (prefabAgent == null)
        {
            Debug.LogError($"Immune cell prefab '{cellPrefab.name}' requires a NavMeshAgent.", cellPrefab);
            return false;
        }
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = prefabAgent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit spawnHit, Mathf.Max(0.1f, navMeshSampleRadius), filter))
        {
            Debug.LogWarning("CirculatorySystemController could not find a NavMesh position near the requested immune-cell spawn point; an immune cell was skipped.", this);
            return false;
        }

        GameObject cell = Instantiate(cellPrefab, spawnHit.position, Quaternion.identity);
        spawnedCell = cell;
        NavMeshAgent agent = cell.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.Warp(spawnHit.position))
        {
            Debug.LogError($"Immune cell '{cellPrefab.name}' could not be placed on the circulatory NavMesh.", cell);
            Destroy(cell);
            return false;
        }

        SelectableUnit selectableUnit = cell.GetComponent<SelectableUnit>();
        if (selectableUnit == null)
        {
            selectableUnit = cell.AddComponent<SelectableUnit>();
        }
        selectableUnit.Initialize(cellPrefab.name);

        CirculatoryCellRoute route = cell.GetComponent<CirculatoryCellRoute>();
        if (route == null)
        {
            route = cell.AddComponent<CirculatoryCellRoute>();
        }

        if (!route.Configure(agent, heart, destinationPoint, lungPoints))
        {
            Debug.LogError($"CirculatorySystemController could not configure the route for '{cellPrefab.name}'.", cell);
            Destroy(cell);
            return false;
        }

        return true;
    }

    private Transform SelectRandomDestinationPoint()
    {
        if (destinationPoints == null)
        {
            return null;
        }

        int validCount = 0;
        foreach (Transform point in destinationPoints)
        {
            if (point != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, validCount);
        foreach (Transform point in destinationPoints)
        {
            if (point == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return point;
            }

            selectedIndex--;
        }

        return null;
    }

    private bool HasRequiredSpawnData()
    {
        return neutrophilPrefab != null && macrophagePrefab != null && heart != null && destinationPointContainer != null &&
            HasAnyTarget(destinationPoints) && HasAnyTarget(lungPoints);
    }

    private static bool HasAnyTarget(Transform[] targets)
    {
        if (targets == null)
        {
            return false;
        }

        foreach (Transform target in targets)
        {
            if (target != null)
            {
                return true;
            }
        }

        return false;
    }
}
