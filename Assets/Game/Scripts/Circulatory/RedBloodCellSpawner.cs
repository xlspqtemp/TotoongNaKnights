using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Spawns and routes red blood cells from the heart at a capped interval.</summary>
public sealed class RedBloodCellSpawner : MonoBehaviour
{
    private const int HardMaximumActiveCells = 100;
    private const float DefaultSpawnIntervalSeconds = 1f;
    private const float DefaultNavMeshSampleRadius = 10f;

    [SerializeField] private GameObject redBloodCellPrefab;
    [SerializeField] private Transform heart;
    [SerializeField] private Transform[] limbTargets;
    [SerializeField] private Transform[] lungTargets;
    [SerializeField, Min(0.1f)] private float spawnIntervalSeconds = DefaultSpawnIntervalSeconds;
    [SerializeField, Range(0, HardMaximumActiveCells)] private int maximumActiveCells = HardMaximumActiveCells;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private readonly List<GameObject> spawnedCells = new List<GameObject>();

    private void Start()
    {
        if (!HasRequiredSpawnData())
        {
            Debug.LogError("RedBloodCellSpawner requires a prefab, heart, at least one limb target, and at least one lung target.", this);
            return;
        }

        StartCoroutine(SpawnCellsOverTime());
    }

    private IEnumerator SpawnCellsOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, spawnIntervalSeconds));
            PruneDestroyedCells();

            int activeCellLimit = Mathf.Clamp(maximumActiveCells, 0, HardMaximumActiveCells);
            if (spawnedCells.Count < activeCellLimit)
            {
                SpawnCell();
            }
        }
    }

    private bool HasRequiredSpawnData()
    {
        return redBloodCellPrefab != null && heart != null && HasAnyTarget(limbTargets) && HasAnyTarget(lungTargets);
    }

    private void SpawnCell()
    {
        if (!NavMesh.SamplePosition(heart.position, out NavMeshHit hit, Mathf.Max(0.1f, navMeshSampleRadius), NavMesh.AllAreas))
        {
            Debug.LogWarning("RedBloodCellSpawner could not find a NavMesh position near the heart; this spawn was skipped.", this);
            return;
        }

        GameObject cell = Instantiate(redBloodCellPrefab, hit.position, Quaternion.identity, transform);
        NavMeshAgent agent = cell.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.Warp(hit.position))
        {
            Debug.LogError("The Red Blood Cell prefab needs an enabled NavMeshAgent that can be placed on the circulatory NavMesh.", cell);
            Destroy(cell);
            return;
        }

        RedBloodCellRoute route = cell.GetComponent<RedBloodCellRoute>();
        if (route == null)
        {
            route = cell.AddComponent<RedBloodCellRoute>();
        }

        if (route == null || !route.Configure(agent, heart, limbTargets, lungTargets))
        {
            Debug.LogError("RedBloodCellSpawner could not configure the red blood cell route.", cell);
            Destroy(cell);
            return;
        }

        spawnedCells.Add(cell);
    }

    private void PruneDestroyedCells()
    {
        for (int index = spawnedCells.Count - 1; index >= 0; index--)
        {
            if (spawnedCells[index] == null)
            {
                spawnedCells.RemoveAt(index);
            }
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
