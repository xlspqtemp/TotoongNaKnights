using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Routes a respiratory air instance to one lung and back to Airway before destroying it.
/// </summary>
public sealed class RespiratoryAirAgent : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;
    private Vector3 airwayPosition;
    private float arrivalDistance;
    private bool isReturningToAirway;

    /// <summary>Starts the air instance's lung-and-return NavMesh route.</summary>
    /// <param name="agent">The NavMeshAgent that moves this air instance.</param>
    /// <param name="lungPosition">The sampled position of its selected lung.</param>
    /// <param name="returnPosition">The sampled position of Airway.</param>
    /// <param name="destinationArrivalDistance">Distance at which each route point is reached.</param>
    /// <returns>True when the lung destination was accepted by the agent.</returns>
    public bool Initialize(NavMeshAgent agent, Vector3 lungPosition, Vector3 returnPosition, float destinationArrivalDistance)
    {
        navMeshAgent = agent;
        airwayPosition = returnPosition;
        arrivalDistance = Mathf.Max(0f, destinationArrivalDistance);
        isReturningToAirway = false;

        return navMeshAgent != null && navMeshAgent.isOnNavMesh && navMeshAgent.SetDestination(lungPosition);
    }

    private void Update()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh ||
            navMeshAgent.pathPending || !navMeshAgent.hasPath ||
            navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete ||
            navMeshAgent.remainingDistance > Mathf.Max(arrivalDistance, navMeshAgent.stoppingDistance))
        {
            return;
        }

        if (isReturningToAirway)
        {
            Destroy(gameObject);
            return;
        }

        isReturningToAirway = true;
        if (!navMeshAgent.SetDestination(airwayPosition))
            Debug.LogWarning($"Air instance '{name}' could not set its return route to Airway.", this);
    }
}
