using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Spawns one test unit at each numbered lymphatic spawner and routes it to its assigned duct.</summary>
public sealed class LymphaticTestUnitSpawner : MonoBehaviour
{
    private const int SpawnerCount = 26;
    private const int RightLymphaticDuctFirstSpawner = 12;
    private const int RightLymphaticDuctLastSpawner = 18;
    private const float DefaultNavMeshSampleRadius = 10f;

    [SerializeField] private GameObject testUnitPrefab;
    [SerializeField] private Transform destinationPointsRoot;
    [SerializeField] private Transform thoracicDuct;
    [SerializeField] private Transform rightLymphaticDuct;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("LymphaticTestUnitSpawner requires the Test Unit prefab, destination points root, and both duct destinations.", this);
            return;
        }

        SpawnTestUnits();
    }

    private bool HasRequiredReferences()
    {
        return testUnitPrefab != null
            && destinationPointsRoot != null
            && thoracicDuct != null
            && rightLymphaticDuct != null;
    }

    private void SpawnTestUnits()
    {
        Transform[] childTransforms = destinationPointsRoot.GetComponentsInChildren<Transform>(true);
        Transform[] spawners = new Transform[SpawnerCount];

        foreach (Transform child in childTransforms)
        {
            if (TryGetSpawnerIndex(child.name, out int spawnerIndex) && spawnerIndex < SpawnerCount)
            {
                spawners[spawnerIndex] = child;
            }
        }

        int spawnedCount = 0;
        int reachableCount = 0;
        for (int spawnerIndex = 0; spawnerIndex < SpawnerCount; spawnerIndex++)
        {
            Transform spawner = spawners[spawnerIndex];
            if (spawner == null)
            {
                Debug.LogError($"LymphaticTestUnitSpawner could not find Spawner ({spawnerIndex}) under '{destinationPointsRoot.name}'.", this);
                continue;
            }

            if (SpawnAndRouteUnit(spawner, spawnerIndex))
            {
                spawnedCount++;
                reachableCount++;
            }
        }

        Debug.Log($"Lymphatic test route check complete: spawned and routed {spawnedCount}/{SpawnerCount} units; {reachableCount} destination paths were complete.", this);
    }

    private bool SpawnAndRouteUnit(Transform spawner, int spawnerIndex)
    {
        Transform destination = GetDestinationForSpawner(spawnerIndex);
        if (!NavMesh.SamplePosition(spawner.position, out NavMeshHit spawnHit, Mathf.Max(0.1f, navMeshSampleRadius), NavMesh.AllAreas))
        {
            Debug.LogError($"Test Unit for Spawner ({spawnerIndex}) could not find a NavMesh position within {navMeshSampleRadius:0.##} units.", spawner);
            return false;
        }

        if (!NavMesh.SamplePosition(destination.position, out NavMeshHit destinationHit, Mathf.Max(0.1f, navMeshSampleRadius), NavMesh.AllAreas))
        {
            Debug.LogError($"Destination '{destination.name}' for Spawner ({spawnerIndex}) is not near a NavMesh position.", destination);
            return false;
        }

        GameObject testUnit = Instantiate(testUnitPrefab, spawnHit.position, spawner.rotation);
        testUnit.name = $"Test Unit (Spawner {spawnerIndex})";

        NavMeshAgent agent = testUnit.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.Warp(spawnHit.position))
        {
            Debug.LogError($"Test Unit for Spawner ({spawnerIndex}) needs an enabled NavMeshAgent that can be placed on the lymphatic NavMesh.", testUnit);
            Destroy(testUnit);
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destinationHit.position, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            Vector3 lastReachableCorner = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] : spawnHit.position;
            Debug.LogError($"Test Unit from Spawner ({spawnerIndex}) cannot reach '{destination.name}' on the active NavMesh (path status: {path.status}; last reachable point: {lastReachableCorner}).", testUnit);
            Destroy(testUnit);
            return false;
        }

        if (!agent.SetDestination(destinationHit.position))
        {
            Debug.LogError($"Test Unit from Spawner ({spawnerIndex}) found a complete path but failed to start moving to '{destination.name}'.", testUnit);
            Destroy(testUnit);
            return false;
        }

        return true;
    }

    private Transform GetDestinationForSpawner(int spawnerIndex)
    {
        return spawnerIndex >= RightLymphaticDuctFirstSpawner && spawnerIndex <= RightLymphaticDuctLastSpawner
            ? rightLymphaticDuct
            : thoracicDuct;
    }

    private static bool TryGetSpawnerIndex(string objectName, out int spawnerIndex)
    {
        const string Prefix = "Spawner (";
        spawnerIndex = -1;
        if (!objectName.StartsWith(Prefix, StringComparison.Ordinal) || !objectName.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        string indexText = objectName.Substring(Prefix.Length, objectName.Length - Prefix.Length - 1);
        return int.TryParse(indexText, out spawnerIndex) && spawnerIndex >= 0;
    }
}
