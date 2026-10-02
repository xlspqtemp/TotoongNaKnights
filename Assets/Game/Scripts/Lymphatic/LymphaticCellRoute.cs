using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Moves a lymphatic cell to the circulatory system, then loops it through a limb and a lung.
/// </summary>
public sealed class LymphaticCellRoute : MonoBehaviour
{
    private const float DefaultArrivalDistance = 1f;
    private const float DefaultNavMeshSampleRadius = 10f;

    [SerializeField] private float arrivalDistance = DefaultArrivalDistance;
    [SerializeField] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private NavMeshAgent agent;
    private Transform lymphaticExitPoint;
    private Transform circulatoryEntryPoint;
    private Transform heart;
    private Transform[] limbDestinations;
    private Transform[] lungDestinations;
    private Transform[] circulatoryRoute;
    private int currentDestinationIndex;
    private bool isInCirculatoryMode;
    private bool isPausedForThreat;
    private Transform currentRouteDestination;

    /// <summary>
    /// Initializes the cell's lymphatic exit and circulatory destinations.
    /// </summary>
    public void Configure(
        NavMeshAgent cellAgent,
        Transform exitPoint,
        Transform circulatoryEntry,
        Transform heartPoint,
        Transform[] limbPoints,
        Transform[] lungPoints)
    {
        agent = cellAgent;
        lymphaticExitPoint = exitPoint;
        circulatoryEntryPoint = circulatoryEntry;
        heart = heartPoint;
        limbDestinations = limbPoints;
        lungDestinations = lungPoints;
        isInCirculatoryMode = false;
        circulatoryRoute = null;
        currentDestinationIndex = 0;
        SetDestination(lymphaticExitPoint);
    }

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void Update()
    {
        if (isPausedForThreat || agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending)
        {
            return;
        }

        if (agent.remainingDistance > arrivalDistance)
        {
            return;
        }

        if (!isInCirculatoryMode)
        {
            BeginCirculatoryMode();
            return;
        }

        AdvanceCirculatoryRoute();
    }

    /// <summary>Pauses waypoint advancement while the cell responds to a pathogen.</summary>
    public void PauseForThreat()
    {
        isPausedForThreat = true;
    }

    /// <summary>Resumes the saved lymphatic or circulatory waypoint after the pathogen is cleared or lost.</summary>
    public void ResumeAfterThreat()
    {
        if (!isPausedForThreat)
        {
            return;
        }

        isPausedForThreat = false;
        SetDestination(currentRouteDestination);
    }

    private void BeginCirculatoryMode()
    {
        if (!TryTeleportToCirculatoryEntry())
        {
            return;
        }

        isInCirculatoryMode = true;
        BuildCirculatoryRoute();
        SetCurrentCirculatoryDestination();
    }

    private bool TryTeleportToCirculatoryEntry()
    {
        if (circulatoryEntryPoint == null || agent == null)
        {
            Debug.LogError("LymphaticCellRoute requires a circulatory entry point and NavMeshAgent.", this);
            return false;
        }

        int areaMask = agent.areaMask;
        if (!NavMesh.SamplePosition(circulatoryEntryPoint.position, out NavMeshHit entryHit, navMeshSampleRadius, areaMask))
        {
            Debug.LogError("LymphaticCellRoute could not find a circulatory NavMesh position near its entry point.", this);
            return false;
        }

        agent.ResetPath();
        if (!agent.Warp(entryHit.position))
        {
            Debug.LogError("LymphaticCellRoute could not teleport the cell onto the circulatory NavMesh.", this);
            return false;
        }

        return true;
    }

    private void AdvanceCirculatoryRoute()
    {
        currentDestinationIndex++;
        if (circulatoryRoute == null || currentDestinationIndex >= circulatoryRoute.Length)
        {
            BuildCirculatoryRoute();
        }

        SetCurrentCirculatoryDestination();
    }

    private void BuildCirculatoryRoute()
    {
        Transform limb = PickDestination(limbDestinations);
        Transform lung = PickDestination(lungDestinations);
        if (heart == null || limb == null || lung == null)
        {
            circulatoryRoute = null;
            Debug.LogError("LymphaticCellRoute requires a heart, at least one limb, and at least one lung destination.", this);
            return;
        }

        circulatoryRoute = new[] { heart, limb, heart, lung };
        currentDestinationIndex = 0;
    }

    private static Transform PickDestination(Transform[] destinations)
    {
        if (destinations == null || destinations.Length == 0)
        {
            return null;
        }

        int validDestinationCount = 0;
        foreach (Transform destination in destinations)
        {
            if (destination != null)
            {
                validDestinationCount++;
            }
        }

        if (validDestinationCount == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, validDestinationCount);
        foreach (Transform destination in destinations)
        {
            if (destination == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return destination;
            }

            selectedIndex--;
        }

        return null;
    }

    private void SetCurrentCirculatoryDestination()
    {
        if (circulatoryRoute == null || currentDestinationIndex < 0 || currentDestinationIndex >= circulatoryRoute.Length)
        {
            return;
        }

        SetDestination(circulatoryRoute[currentDestinationIndex]);
    }

    private void SetDestination(Transform destination)
    {
        currentRouteDestination = destination;
        if (agent != null && agent.enabled && agent.isOnNavMesh && destination != null)
        {
            agent.SetDestination(destination.position);
        }
    }
}
