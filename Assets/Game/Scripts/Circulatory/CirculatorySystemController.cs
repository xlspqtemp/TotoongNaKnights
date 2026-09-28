using UnityEngine;
using UnityEngine.AI;

public class CirculatorySystemController : MonoBehaviour
{
    private const int DefaultPairsPerSpawnPoint = 1;
    private const float DefaultNavMeshSampleRadius = 10f;

    [SerializeField] private GameObject neutrophilPrefab;
    [SerializeField] private GameObject macrophagePrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform heart;
    [SerializeField] private Transform leftLung;
    [SerializeField] private Transform rightLung;
    [SerializeField] private int pairsPerSpawnPoint = DefaultPairsPerSpawnPoint;
    [SerializeField] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private void Start()
    {
        SpawnInitialCells();
    }

    private void SpawnInitialCells()
    {
        if (!HasRequiredSpawnData())
        {
            Debug.LogError("CirculatorySystemController requires both prefabs, the heart, and both lungs.", this);
            return;
        }

        int spawnCount = Mathf.Max(0, pairsPerSpawnPoint);
        if (spawnPoints == null)
        {
            return;
        }

        foreach (Transform spawnPoint in spawnPoints)
        {
            for (int pairIndex = 0; pairIndex < spawnCount; pairIndex++)
            {
                SpawnPairAt(spawnPoint);
            }
        }
    }

    /// <summary>
    /// Spawns one neutrophil and one macrophage at the requested anatomical spawn point.
    /// </summary>
    public bool SpawnPairAt(Transform spawnPoint)
    {
        if (spawnPoint == null || !HasRequiredSpawnData())
        {
            Debug.LogError("CirculatorySystemController cannot spawn a pair because its references are incomplete.", this);
            return false;
        }

        Transform destinationLung = IsLeftSide(spawnPoint.name) ? leftLung : rightLung;
        Vector3 spawnPosition = GetNavMeshPosition(spawnPoint.position);
        SpawnCell(neutrophilPrefab, spawnPosition, spawnPoint, destinationLung);
        SpawnCell(macrophagePrefab, spawnPosition, spawnPoint, destinationLung);
        return true;
    }

    private bool HasRequiredSpawnData()
    {
        return neutrophilPrefab != null && macrophagePrefab != null && heart != null && leftLung != null && rightLung != null;
    }

    private void SpawnCell(GameObject cellPrefab, Vector3 spawnPosition, Transform spawnPoint, Transform destinationLung)
    {
        GameObject cell = Instantiate(cellPrefab, spawnPosition, Quaternion.identity, transform);
        NavMeshAgent agent = cell.GetComponent<NavMeshAgent>();
        CirculatoryCellRoute route = cell.GetComponent<CirculatoryCellRoute>();
        if (route == null)
        {
            route = cell.AddComponent<CirculatoryCellRoute>();
        }

        if (agent == null || route == null)
        {
            Debug.LogError($"{cellPrefab.name} requires a NavMeshAgent component.", cell);
            return;
        }

        route.Configure(agent, spawnPoint, heart, destinationLung);
    }

    private Vector3 GetNavMeshPosition(Vector3 requestedPosition)
    {
        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return requestedPosition;
    }

    private static bool IsLeftSide(string markerName)
    {
        return markerName.ToLowerInvariant().Contains("left");
    }
}
