using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Spawns one red blood cell for each assigned destination over a fixed duration.</summary>
public sealed class RedBloodLoopSpawner : MonoBehaviour
{
    private const int MaximumRouteCells = 25;
    private const float DefaultSpawnIntervalSeconds = 1f;
    private const float DefaultSpawnDurationSeconds = 25f;
    private const float DefaultNavMeshSampleRadius = 10f;
    private const float SpawnBoundsHalfExtent = 0.5f;
    // Route waypoints are collected from the spawn point's parent container.
    private const string DestinationTargetName = "Destination - Target";
    private const string LungPointName = "Point - Lung";

    [SerializeField] private GameObject redBloodPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform heart;

    private Transform[] destinationTargets;
    private Transform[] lungPoints;
    private bool hasReportedFirstSpawn;
    [SerializeField, Min(0.1f)] private float spawnIntervalSeconds = DefaultSpawnIntervalSeconds;
    [SerializeField, Min(0.1f)] private float spawnDurationSeconds = DefaultSpawnDurationSeconds;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private void Start()
    {
        ResolveWaypoints();
        if (!HasRequiredSpawnData())
        {
            Debug.LogError("RedBloodLoopSpawner requires a prefab, spawn point, heart, destination targets, and lung points under the active waypoint container.", this);
            return;
        }

        Debug.Log($"Red blood spawner ready: {destinationTargets.Length} unique targets, {lungPoints.Length} lung points, one spawn per {spawnIntervalSeconds:0.##} seconds for {spawnDurationSeconds:0.##} seconds.", this);
        StartCoroutine(SpawnCellsOverTime());
    }

    private void ResolveWaypoints()
    {
        Transform waypointContainer = spawnPoint != null ? spawnPoint.parent : null;
        if (waypointContainer == null)
        {
            Debug.LogError("RedBloodLoopSpawner requires the spawner transform to be parented under the circulatory waypoint container.", this);
            destinationTargets = new Transform[0];
            lungPoints = new Transform[0];
            return;
        }

        Transform[] children = waypointContainer.GetComponentsInChildren<Transform>(true);
        destinationTargets = FindNamedWaypoints(children, DestinationTargetName);
        lungPoints = FindNamedWaypoints(children, LungPointName);
        ShuffleDestinationTargets();
    }

    private void ShuffleDestinationTargets()
    {
        for (int index = destinationTargets.Length - 1; index > 0; index--)
        {
            int randomIndex = Random.Range(0, index + 1);
            Transform target = destinationTargets[index];
            destinationTargets[index] = destinationTargets[randomIndex];
            destinationTargets[randomIndex] = target;
        }
    }

    private static Transform[] FindNamedWaypoints(Transform[] candidates, string waypointName)
    {
        int matchingCount = 0;
        foreach (Transform candidate in candidates)
        {
            if (candidate.name == waypointName)
            {
                matchingCount++;
            }
        }

        Transform[] matches = new Transform[matchingCount];
        int matchIndex = 0;
        foreach (Transform candidate in candidates)
        {
            if (candidate.name == waypointName)
            {
                matches[matchIndex] = candidate;
                matchIndex++;
            }
        }

        return matches;
    }

    private IEnumerator SpawnCellsOverTime()
    {
        int routeCount = Mathf.Min(destinationTargets.Length, MaximumRouteCells);
        float elapsedSeconds = 0f;
        float intervalSeconds = Mathf.Max(0.1f, spawnIntervalSeconds);
        float durationSeconds = Mathf.Max(0.1f, spawnDurationSeconds);

        for (int routeIndex = 0; routeIndex < routeCount && elapsedSeconds < durationSeconds; routeIndex++)
        {
            float waitSeconds = Mathf.Min(intervalSeconds, durationSeconds - elapsedSeconds);
            yield return new WaitForSeconds(waitSeconds);
            elapsedSeconds += waitSeconds;
            SpawnCell(destinationTargets[routeIndex]);
        }
    }

    private bool HasRequiredSpawnData()
    {
        return redBloodPrefab != null
            && spawnPoint != null
            && heart != null
            && HasAnyTarget(destinationTargets)
            && HasAnyTarget(lungPoints);
    }

    private void SpawnCell(Transform assignedDestination)
    {
        if (assignedDestination == null)
        {
            Debug.LogWarning("RedBloodLoopSpawner skipped an unassigned destination target.", this);
            return;
        }

        Vector3 localOffset = new Vector3(
            Random.Range(-SpawnBoundsHalfExtent, SpawnBoundsHalfExtent),
            Random.Range(-SpawnBoundsHalfExtent, SpawnBoundsHalfExtent),
            Random.Range(-SpawnBoundsHalfExtent, SpawnBoundsHalfExtent));
        Vector3 candidatePosition = spawnPoint.TransformPoint(localOffset);

        if (!NavMesh.SamplePosition(candidatePosition, out NavMeshHit hit, Mathf.Max(0.1f, navMeshSampleRadius), NavMesh.AllAreas))
        {
            Debug.LogWarning("RedBloodLoopSpawner could not find a NavMesh position near the randomized spawn point; this cell was skipped.", this);
            return;
        }

        GameObject cell = Instantiate(redBloodPrefab, hit.position, spawnPoint.rotation);
        NavMeshAgent agent = cell.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.Warp(hit.position))
        {
            Debug.LogError("The Red Blood prefab needs an enabled NavMeshAgent that can be placed on the circulatory NavMesh.", cell);
            Destroy(cell);
            return;
        }

        RedBloodLoopRoute route = cell.GetComponent<RedBloodLoopRoute>();
        if (route == null)
        {
            route = cell.AddComponent<RedBloodLoopRoute>();
        }

        if (route == null || !route.Configure(agent, heart, assignedDestination, lungPoints))
        {
            Debug.LogError("RedBloodLoopSpawner could not configure the red blood cell's route.", cell);
            Destroy(cell);
            return;
        }

        if (!hasReportedFirstSpawn)
        {
            hasReportedFirstSpawn = true;
            Debug.Log($"Red blood spawner created the first routed cell for '{assignedDestination.name}'.", this);
        }
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
