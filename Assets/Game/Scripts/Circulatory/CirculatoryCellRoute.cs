using UnityEngine;
using UnityEngine.AI;

public class CirculatoryCellRoute : MonoBehaviour
{
    private const float DefaultArrivalDistance = 1f;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform heart;
    [SerializeField] private Transform destinationLung;
    [SerializeField] private float arrivalDistance = DefaultArrivalDistance;

    private Transform[] route;
    private int currentDestinationIndex;

    /// <summary>
    /// Configures the cell's repeating route from its spawn point through the heart and assigned lung.
    /// </summary>
    public void Configure(NavMeshAgent cellAgent, Transform cellSpawnPoint, Transform heartTransform, Transform lungTransform)
    {
        agent = cellAgent;
        spawnPoint = cellSpawnPoint;
        heart = heartTransform;
        destinationLung = lungTransform;
        route = new[] { spawnPoint, heart, destinationLung, heart };
        currentDestinationIndex = 1;
    }

    private void Start()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (route == null && spawnPoint != null && heart != null && destinationLung != null)
        {
            route = new[] { spawnPoint, heart, destinationLung, heart };
            currentDestinationIndex = 1;
        }

        SetCurrentDestination();
    }

    private void Update()
    {
        if (route == null || route.Length == 0 || agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= arrivalDistance)
        {
            currentDestinationIndex = (currentDestinationIndex + 1) % route.Length;
            SetCurrentDestination();
        }
    }

    private void SetCurrentDestination()
    {
        if (route == null || route.Length == 0 || agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        Transform destination = route[currentDestinationIndex];
        if (destination != null)
        {
            agent.SetDestination(destination.position);
        }
    }
}
