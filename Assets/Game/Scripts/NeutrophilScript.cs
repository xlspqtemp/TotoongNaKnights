using UnityEngine;
using UnityEngine.AI;

public class NeutrophilScript : MonoBehaviour
{
    private const string MovingParameter = "IsMoving";
    private const string AttackParameter = "Attack";
    private const float MovementThreshold = 0.1f;
    private const float RotationSpeed = 720f;
    private const float TargetSearchInterval = 0.5f;
    private const float AttackCooldown = 2.2f;

    public NavMeshAgent agent;
    public Transform target;

    public float radius = 5f;
    public float damage = 1f;
    public int health = 180;

    [SerializeField] private Animator animator;

    private bool isCirculatoryCell;
    private float nextTargetSearchTime;
    private float nextAttackTime;

    private void Start()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        isCirculatoryCell = GetComponent<CirculatoryCellRoute>() != null;

        if (agent != null)
        {
            agent.updateRotation = false;
        }
    }

    private void Update()
    {
        if (agent == null)
        {
            return;
        }

        UpdateMovementPresentation();

        if (isCirculatoryCell)
        {
            AttackNearbyBacteria();
            return;
        }

        UpdateTarget();
        if (target == null)
        {
            if (agent.isOnNavMesh && agent.hasPath)
            {
                agent.ResetPath();
            }

            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget > radius && agent.isOnNavMesh)
        {
            agent.SetDestination(target.position);
        }
        else if (distanceToTarget <= radius)
        {
            if (agent.isOnNavMesh && agent.hasPath)
            {
                agent.ResetPath();
            }

            TryAttack(target);
        }
    }

    private void UpdateMovementPresentation()
    {
        Vector3 velocity = agent.velocity;
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        bool isMoving = planarVelocity.sqrMagnitude > MovementThreshold * MovementThreshold;

        if (animator != null)
        {
            animator.SetBool(MovingParameter, isMoving);
        }

        if (!isMoving)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            RotationSpeed * Time.deltaTime);
    }

    private void UpdateTarget()
    {
        if (target != null && target.gameObject.activeInHierarchy)
        {
            return;
        }

        target = null;
        if (Time.time < nextTargetSearchTime)
        {
            return;
        }

        nextTargetSearchTime = Time.time + TargetSearchInterval;
        GameObject[] bacteria = GameObject.FindGameObjectsWithTag("Bacteria");
        float closestDistance = Mathf.Infinity;

        foreach (GameObject bacterium in bacteria)
        {
            float distance = Vector3.Distance(transform.position, bacterium.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                target = bacterium.transform;
            }
        }
    }

    private void AttackNearbyBacteria()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        Transform closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            Transform candidate = hit.transform;
            while (candidate != null && !candidate.CompareTag("Bacteria"))
            {
                candidate = candidate.parent;
            }

            if (candidate == null)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, candidate.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = candidate;
            }
        }

        if (closestTarget != null)
        {
            TryAttack(closestTarget);
        }
    }

    private void TryAttack(Transform bacteria)
    {
        if (bacteria == null || Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + AttackCooldown;
        if (animator != null)
        {
            animator.SetTrigger(AttackParameter);
        }

        bacteria.gameObject.SendMessageUpwards(
            "TakeDamage",
            damage,
            SendMessageOptions.DontRequireReceiver);
    }
}
