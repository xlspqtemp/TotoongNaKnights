using UnityEngine;
using UnityEngine.AI;

/// <summary>Keeps NavMeshAgents at authored speed because global time scale accelerates their simulation.</summary>
public sealed class GameplaySpeedNavMeshAgent : MonoBehaviour
{
    private NavMeshAgent agent;
    private float authoredSpeed;

    /// <summary>Ensures an agent receives gameplay acceleration exactly once.</summary>
    public static void Register(NavMeshAgent navMeshAgent)
    {
        if (navMeshAgent != null && navMeshAgent.GetComponent<GameplaySpeedNavMeshAgent>() == null)
            navMeshAgent.gameObject.AddComponent<GameplaySpeedNavMeshAgent>();
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            authoredSpeed = agent.speed;
    }

    private void OnEnable()
    {
        GameplaySpeed.OnSpeedChanged += ApplySpeed;
        ApplySpeed();
    }

    private void OnDisable()
    {
        GameplaySpeed.OnSpeedChanged -= ApplySpeed;
    }

    private void ApplySpeed()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            if (authoredSpeed <= 0f)
                authoredSpeed = agent.speed;
            agent.speed = authoredSpeed;
        }
    }
}
