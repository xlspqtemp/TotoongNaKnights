using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns B cells and T cells in the lymphatic system and configures their circulatory routes.
/// </summary>
public sealed class LymphaticSystemController : MonoBehaviour
{
    private const float DefaultNavMeshSampleRadius = 10f;

    [SerializeField] private GameObject bCellPrefab;
    [SerializeField] private GameObject tCellPrefab;
    [SerializeField] private Transform lymphaticSpawnPoint;
    [SerializeField] private Transform lymphaticExitPoint;
    [SerializeField] private Transform circulatoryEntryPoint;
    [SerializeField] private Transform heart;
    [SerializeField] private Transform[] limbDestinations;
    [SerializeField] private Transform[] lungDestinations;
    [SerializeField] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    /// <summary>
    /// Spawns one B cell at the lymphatic spawn point.
    /// </summary>
    public bool SpawnBCell()
    {
        return SpawnCell(bCellPrefab);
    }

    /// <summary>
    /// Spawns one T cell at the lymphatic spawn point.
    /// </summary>
    public bool SpawnTCell()
    {
        return SpawnCell(tCellPrefab);
    }

    private bool SpawnCell(GameObject cellPrefab)
    {
        if (!HasRequiredRouteData() || cellPrefab == null)
        {
            Debug.LogError("LymphaticSystemController requires a cell prefab and all route references.", this);
            return false;
        }

        if (!NavMesh.SamplePosition(lymphaticSpawnPoint.position, out NavMeshHit spawnHit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            Debug.LogError("LymphaticSystemController could not find a lymphatic NavMesh position near its spawn point.", this);
            return false;
        }

        GameObject cell = Instantiate(cellPrefab, spawnHit.position, Quaternion.identity, transform);
        NavMeshAgent agent = cell.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"{cellPrefab.name} requires a NavMeshAgent component.", cell);
            Destroy(cell);
            return false;
        }

        if (!agent.isOnNavMesh && !agent.Warp(spawnHit.position))
        {
            Debug.LogError($"{cellPrefab.name} could not be placed on the lymphatic NavMesh.", cell);
            Destroy(cell);
            return false;
        }

        LymphaticCellRoute route = cell.GetComponent<LymphaticCellRoute>();
        if (route == null)
        {
            route = cell.AddComponent<LymphaticCellRoute>();
        }

        if (route == null)
        {
            Debug.LogError($"{cellPrefab.name} could not be given a LymphaticCellRoute component.", cell);
            Destroy(cell);
            return false;
        }

        route.Configure(agent, lymphaticExitPoint, circulatoryEntryPoint, heart, limbDestinations, lungDestinations);
        return true;
    }

    private bool HasRequiredRouteData()
    {
        return lymphaticSpawnPoint != null
            && lymphaticExitPoint != null
            && circulatoryEntryPoint != null
            && heart != null
            && HasAtLeastOneDestination(limbDestinations)
            && HasAtLeastOneDestination(lungDestinations);
    }

    private static bool HasAtLeastOneDestination(Transform[] destinations)
    {
        if (destinations == null)
        {
            return false;
        }

        foreach (Transform destination in destinations)
        {
            if (destination != null)
            {
                return true;
            }
        }

        return false;
    }
}
