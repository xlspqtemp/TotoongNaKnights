using UnityEngine;
using UnityEngine.AI;

/// <summary>Routes an immune cell through its assigned body destination, heart, and a random lung in one direction.</summary>
public sealed class CirculatoryCellRoute : MonoBehaviour
{
    private const float DefaultArrivalDistance = 1f;
    private const float DefaultScanCorridorRadius = 3f;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform heart;
    [SerializeField] private Transform assignedDestination;
    [SerializeField] private Transform[] lungPoints;
    [SerializeField, Min(0f)] private float arrivalDistance = DefaultArrivalDistance;
    [SerializeField, Min(0f)] private float scanCorridorRadius = DefaultScanCorridorRadius;

    private int routeStep;
    private int previousLungIndex = -1;
    private bool isConfigured;
    private bool hasDestination;
    private bool isPausedForThreat;

    /// <summary>Configures the repeating destination → heart → lung → heart route.</summary>
    public bool Configure(NavMeshAgent cellAgent, Transform heartTransform, Transform destinationTransform, Transform[] availableLungPoints)
    {
        agent = cellAgent;
        heart = heartTransform;
        assignedDestination = destinationTransform;
        lungPoints = availableLungPoints;
        routeStep = 0;
        previousLungIndex = -1;
        hasDestination = false;
        isConfigured = HasRequiredRouteData();

        if (isConfigured)
        {
            GameplaySpeedNavMeshAgent.Register(agent);
            SetCurrentDestination();
        }

        return isConfigured;
    }

    private void Update()
    {
        if (!isConfigured || isPausedForThreat || agent == null || !agent.enabled || !agent.isOnNavMesh)
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

    /// <summary>Returns true only while the cell is moving outward and the wound lies along its current NavMesh path.</summary>
    public bool IsScanningAccessPoint(Transform accessPoint)
    {
        if (!isConfigured || isPausedForThreat || routeStep != 0 || accessPoint == null ||
            agent == null || !agent.enabled || !agent.isOnNavMesh || !agent.hasPath ||
            agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        Vector3[] corners = agent.path.corners;
        Vector3 segmentStart = agent.transform.position;
        float maximumDistance = Mathf.Max(scanCorridorRadius, agent.radius);

        for (int index = 0; index < corners.Length; index++)
        {
            Vector3 segmentEnd = corners[index];
            if (DistanceToSegment(accessPoint.position, segmentStart, segmentEnd) <= maximumDistance)
            {
                return true;
            }

            segmentStart = segmentEnd;
        }

        return false;
    }

    /// <summary>Pauses waypoint advancement while the cell responds to another threat.</summary>
    public void PauseForThreat()
    {
        isPausedForThreat = true;
    }

    /// <summary>Resumes the existing waypoint loop after the threat response ends.</summary>
    public void ResumeAfterThreat()
    {
        if (!isPausedForThreat)
        {
            return;
        }

        isPausedForThreat = false;
        SetCurrentDestination();
    }

    private bool HasRequiredRouteData()
    {
        if (agent == null || heart == null || assignedDestination == null || lungPoints == null)
        {
            return false;
        }

        foreach (Transform point in lungPoints)
        {
            if (point != null)
            {
                return true;
            }
        }

        return false;
    }

    private void SetCurrentDestination()
    {
        if (!isConfigured || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            hasDestination = false;
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

        hasDestination = destination != null && agent.SetDestination(destination.position);
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

        bool avoidPreviousLung = validCount > 0;
        if (!avoidPreviousLung)
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

        int selectedIndex = Random.Range(0, validCount);
        for (int index = 0; index < lungPoints.Length; index++)
        {
            Transform point = lungPoints[index];
            if (point == null || (avoidPreviousLung && lungPoints.Length > 1 && index == previousLungIndex))
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                previousLungIndex = index;
                return point;
            }

            selectedIndex--;
        }

        return null;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= Mathf.Epsilon)
        {
            return Vector3.Distance(point, start);
        }

        float projection = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSquared);
        return Vector3.Distance(point, start + segment * projection);
    }
}
