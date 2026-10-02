using UnityEngine;
using UnityEngine.AI;

/// <summary>Routes an air instance through the lungs and resolves contaminated air on arrival.</summary>
public sealed class RespiratoryAirAgent : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;
    private Vector3 airwayPosition;
    private float arrivalDistance;
    private bool isReturningToAirway;
    private bool destroyOnLungArrival;
    private LungInfectionResponse lungInfectionResponse;

    /// <summary>Starts the normal air route to a lung and back to Airway.</summary>
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
        destroyOnLungArrival = false;
        lungInfectionResponse = null;

        return navMeshAgent != null && navMeshAgent.isOnNavMesh && navMeshAgent.SetDestination(lungPosition);
    }

    /// <summary>Routes contaminated air to its destination lung, records the breach, and destroys the air instance there.</summary>
    /// <param name="agent">The NavMeshAgent that moves the contaminated-air instance.</param>
    /// <param name="lungPosition">The sampled position of the selected lung.</param>
    /// <param name="infectionResponse">The infection state associated with the destination lung.</param>
    /// <param name="destinationArrivalDistance">Distance at which arrival is considered complete.</param>
    /// <returns>True when the lung destination was accepted by the agent.</returns>
    public bool InitializeForLungInfection(NavMeshAgent agent, Vector3 lungPosition, LungInfectionResponse infectionResponse, float destinationArrivalDistance)
    {
        navMeshAgent = agent;
        arrivalDistance = Mathf.Max(0f, destinationArrivalDistance);
        isReturningToAirway = false;
        destroyOnLungArrival = true;
        lungInfectionResponse = infectionResponse;

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

        if (destroyOnLungArrival)
        {
            if (lungInfectionResponse != null)
            {
                lungInfectionResponse.RegisterInfiltration();
            }

            Destroy(gameObject);
            return;
        }

        if (isReturningToAirway)
        {
            Destroy(gameObject);
            return;
        }

        isReturningToAirway = true;
        if (!navMeshAgent.SetDestination(airwayPosition))
        {
            Debug.LogWarning($"Air instance '{name}' could not set its return route to Airway.", this);
        }
    }
}
