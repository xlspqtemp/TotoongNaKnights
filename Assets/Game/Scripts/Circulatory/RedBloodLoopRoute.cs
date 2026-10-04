using UnityEngine;
using UnityEngine.AI;

/// <summary>Routes a red blood cell through its exclusive destination and a randomized lung loop.</summary>
public sealed class RedBloodLoopRoute : MonoBehaviour
{
    private const float DefaultArrivalDistance = 1f;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform heart;
    [SerializeField] private Transform assignedDestination;
    [SerializeField] private Transform[] lungPoints;
    [SerializeField, Min(0f)] private float arrivalDistance = DefaultArrivalDistance;

    private int routeStep;
    private int previousLungIndex = -1;
    private bool isConfigured;
    private bool hasDestination;

    /// <summary>Assigns this cell its permanent destination and configures its repeating heart/lung route.</summary>
    /// <param name="cellAgent">The NavMeshAgent that moves the cell.</param>
    /// <param name="heartTransform">The heart waypoint.</param>
    /// <param name="destinationTransform">The exclusive destination waypoint for this cell.</param>
    /// <param name="availableLungPoints">The lung waypoints from which a route point is chosen at random.</param>
    /// <returns>True when all route references are usable.</returns>
    public bool Configure(NavMeshAgent cellAgent, Transform heartTransform, Transform destinationTransform, Transform[] availableLungPoints)
    {
        agent = cellAgent;
        heart = heartTransform;
        assignedDestination = destinationTransform;
        lungPoints = availableLungPoints;
        routeStep = 0;
        previousLungIndex = -1;
        isConfigured = HasRequiredRouteData();
        hasDestination = false;

        if (isConfigured)
        {
            SetCurrentDestination();
        }

        return isConfigured;
    }

    private void Update()
    {
        if (!isConfigured || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (!hasDestination)
        {
            SetCurrentDestination();
            return;
        }

        if (agent.pathPending || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            return;
        }

        if (agent.remainingDistance > Mathf.Max(arrivalDistance, agent.stoppingDistance))
        {
            return;
        }

        routeStep = (routeStep + 1) % 4;
        hasDestination = false;
        SetCurrentDestination();
    }

    private bool HasRequiredRouteData()
    {
        return agent != null
            && heart != null
            && assignedDestination != null
            && HasAnyTarget(lungPoints);
    }

    private void SetCurrentDestination()
    {
        if (!isConfigured || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Transform destination;
        switch (routeStep)
        {
            case 0:
                destination = assignedDestination;
                break;
            case 1:
            case 3:
                destination = heart;
                break;
            default:
                destination = SelectRandomLungPoint();
                break;
        }

        if (destination != null)
        {
            hasDestination = agent.SetDestination(destination.position);
        }
    }

    private Transform SelectRandomLungPoint()
    {
        int validCount = 0;
        for (int index = 0; index < lungPoints.Length; index++)
        {
            if (lungPoints[index] != null && (lungPoints.Length == 1 || index != previousLungIndex))
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            for (int index = 0; index < lungPoints.Length; index++)
            {
                if (lungPoints[index] != null)
                {
                    validCount++;
                }
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int selectedValidIndex = Random.Range(0, validCount);
        for (int index = 0; index < lungPoints.Length; index++)
        {
            if (lungPoints[index] == null || (lungPoints.Length > 1 && index == previousLungIndex))
            {
                continue;
            }

            if (selectedValidIndex == 0)
            {
                previousLungIndex = index;
                return lungPoints[index];
            }

            selectedValidIndex--;
        }

        for (int index = 0; index < lungPoints.Length; index++)
        {
            if (lungPoints[index] != null)
            {
                previousLungIndex = index;
                return lungPoints[index];
            }
        }

        return null;
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
