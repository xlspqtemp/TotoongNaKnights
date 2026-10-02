using UnityEngine;
using UnityEngine.AI;

/// <summary>Finds reachable bacteria, pursues one target, clears it after contact, then resumes the cell's route.</summary>
public sealed class BacteriaCellResponder : MonoBehaviour
{
    private const float DefaultScanIntervalSeconds = 0.5f;
    private const float DefaultContactDistance = 2.25f;

    [SerializeField, Min(0.1f)] private float scanIntervalSeconds = DefaultScanIntervalSeconds;
    [SerializeField, Min(0.1f)] private float contactDistance = DefaultContactDistance;

    private NavMeshAgent agent;
    private CirculatoryCellRoute circulatoryRoute;
    private LymphaticCellRoute lymphaticRoute;
    private BacteriaAgent target;
    private NavMeshPath candidatePath;
    private float nextScanTime;
    private bool routePaused;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        candidatePath = new NavMeshPath();
    }

    private void Update()
    {
        if (WellnessManager.Instance != null && WellnessManager.Instance.HasRunEnded)
        {
            return;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (target == null || !target.IsAvailable)
        {
            ClearTargetAndResumeRoute();
            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + Mathf.Max(0.1f, scanIntervalSeconds);
                BacteriaAgent nearestBacteria = FindNearestReachableBacteria();
                if (nearestBacteria != null)
                {
                    Pursue(nearestBacteria);
                }
            }

            return;
        }

        float targetDistance = Vector3.Distance(transform.position, target.transform.position);
        if (targetDistance <= Mathf.Max(contactDistance, agent.radius + 0.5f))
        {
            if (agent.hasPath)
            {
                agent.ResetPath();
            }

            target.RegisterContact(this, Time.deltaTime);
        }
        else
        {
            target.StopContact(this);
        }
    }

    private BacteriaAgent FindNearestReachableBacteria()
    {
        BacteriaAgent nearestBacteria = null;
        float shortestPathDistance = float.PositiveInfinity;
        int bacteriaCount = BacteriaAgent.ActiveCount;

        for (int index = 0; index < bacteriaCount; index++)
        {
            BacteriaAgent bacteria = BacteriaAgent.GetActiveAt(index);
            if (bacteria == null)
            {
                continue;
            }

            candidatePath.ClearCorners();
            if (!NavMesh.CalculatePath(transform.position, bacteria.transform.position, agent.areaMask, candidatePath) ||
                candidatePath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float pathDistance = CalculatePathDistance(candidatePath);
            if (pathDistance < shortestPathDistance)
            {
                shortestPathDistance = pathDistance;
                nearestBacteria = bacteria;
            }
        }

        return nearestBacteria;
    }

    private void Pursue(BacteriaAgent bacteria)
    {
        target = bacteria;
        PauseRoute();
        if (!agent.SetDestination(bacteria.transform.position))
        {
            ClearTargetAndResumeRoute();
            nextScanTime = Time.time + Mathf.Max(0.1f, scanIntervalSeconds);
        }
    }

    private void PauseRoute()
    {
        if (routePaused)
        {
            return;
        }

        circulatoryRoute = GetComponent<CirculatoryCellRoute>();
        lymphaticRoute = GetComponent<LymphaticCellRoute>();
        if (circulatoryRoute != null)
        {
            circulatoryRoute.PauseForThreat();
        }
        else if (lymphaticRoute != null)
        {
            lymphaticRoute.PauseForThreat();
        }
        else
        {
            agent.isStopped = true;
        }

        routePaused = true;
    }

    private void ClearTargetAndResumeRoute()
    {
        if (target != null)
        {
            target.StopContact(this);
            target = null;
        }

        if (!routePaused)
        {
            return;
        }

        circulatoryRoute = GetComponent<CirculatoryCellRoute>();
        lymphaticRoute = GetComponent<LymphaticCellRoute>();
        if (circulatoryRoute != null)
        {
            circulatoryRoute.ResumeAfterThreat();
        }
        else if (lymphaticRoute != null)
        {
            lymphaticRoute.ResumeAfterThreat();
        }
        else if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        routePaused = false;
    }

    private static float CalculatePathDistance(NavMeshPath path)
    {
        Vector3[] corners = path.corners;
        float distance = 0f;
        for (int index = 1; index < corners.Length; index++)
        {
            distance += Vector3.Distance(corners[index - 1], corners[index]);
        }

        return distance;
    }

    private void OnDisable()
    {
        ClearTargetAndResumeRoute();
    }
}
