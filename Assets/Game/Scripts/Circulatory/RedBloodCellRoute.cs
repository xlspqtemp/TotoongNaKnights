using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Routes one red blood cell through a randomized limb and lung loop.</summary>
public sealed class RedBloodCellRoute : MonoBehaviour
{
    private const float DefaultArrivalDistance = 1f;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform heart;
    [SerializeField] private Transform[] limbTargets;
    [SerializeField] private Transform[] lungTargets;
    [SerializeField, Min(0f)] private float arrivalDistance = DefaultArrivalDistance;

    private int routeStep;
    private int previousLimbIndex = -1;
    private int previousLungIndex = -1;
    private bool isConfigured;
    private bool hasDestination;

    /// <summary>Configures the route from the heart through a random limb and lung, returning to the heart between destinations.</summary>
    /// <param name="cellAgent">The NavMeshAgent that moves this red blood cell.</param>
    /// <param name="heartTransform">The heart waypoint.</param>
    /// <param name="limbTransforms">The available hand and foot waypoints.</param>
    /// <param name="lungTransforms">The available lung waypoints.</param>
    /// <returns>True when all route references are usable.</returns>
    public bool Configure(NavMeshAgent cellAgent, Transform heartTransform, Transform[] limbTransforms, Transform[] lungTransforms)
    {
        agent = cellAgent;
        heart = heartTransform;
        limbTargets = limbTransforms;
        lungTargets = lungTransforms;
        routeStep = 0;
        previousLimbIndex = -1;
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
        return agent != null && heart != null && HasAnyTarget(limbTargets) && HasAnyTarget(lungTargets);
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
                destination = SelectRandomTarget(limbTargets, ref previousLimbIndex);
                break;
            case 1:
            case 3:
                destination = heart;
                break;
            default:
                destination = SelectRandomTarget(lungTargets, ref previousLungIndex);
                break;
        }

        if (destination != null)
        {
            hasDestination = agent.SetDestination(destination.position);
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

    private static Transform SelectRandomTarget(Transform[] targets, ref int previousIndex)
    {
        List<int> validIndices = new List<int>();
        for (int index = 0; index < targets.Length; index++)
        {
            if (targets[index] != null && (targets.Length == 1 || index != previousIndex))
            {
                validIndices.Add(index);
            }
        }

        if (validIndices.Count == 0)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null)
                {
                    validIndices.Add(index);
                }
            }
        }

        if (validIndices.Count == 0)
        {
            return null;
        }

        previousIndex = validIndices[Random.Range(0, validIndices.Count)];
        return targets[previousIndex];
    }
}
