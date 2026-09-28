using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Destroys a food instance after its NavMeshAgent reaches the Anus.
/// </summary>
public sealed class DigestiveFoodAgent : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;
    private float arrivalDistance;
    private bool isConfigured;

    /// <summary>Configures the food instance to clean itself up at its destination.</summary>
    /// <param name="agent">The NavMeshAgent moving this food instance.</param>
    /// <param name="destinationArrivalDistance">Distance at which arrival is considered complete.</param>
    public void Initialize(NavMeshAgent agent, float destinationArrivalDistance)
    {
        navMeshAgent = agent;
        arrivalDistance = Mathf.Max(0f, destinationArrivalDistance);
        isConfigured = navMeshAgent != null;
    }

    private void Update()
    {
        if (!isConfigured || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        if (!navMeshAgent.pathPending && navMeshAgent.hasPath &&
            navMeshAgent.pathStatus == NavMeshPathStatus.PathComplete &&
            navMeshAgent.remainingDistance <= Mathf.Max(arrivalDistance, navMeshAgent.stoppingDistance))
        {
            Destroy(gameObject);
        }
    }
}
