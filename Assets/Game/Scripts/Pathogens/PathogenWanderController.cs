using UnityEngine;
using UnityEngine.AI;

/// <summary>Applies pathogen category visuals and bounded wandering around the pathogen's spawn point.</summary>
[DisallowMultipleComponent]
public sealed class PathogenWanderController : MonoBehaviour
{
    private const float DefaultWanderRadius = 5f;
    private const float DefaultWanderSpeed = 1.25f;
    private const float DefaultArrivalDistance = 0.2f;
    private const float DefaultTargetScanIntervalSeconds = 0.4f;
    private const float DefaultTurnSpeedDegreesPerSecond = 540f;
    private const int DestinationSampleAttempts = 16;
    private const float NavMeshSampleDistance = 1.25f;
    private const float BoundaryTolerance = 0.1f;
    private const float MovementAnimationVelocityThreshold = 0.05f;
    private const string MovementAnimationStateName = "Walk";
    private static readonly int MovementAnimationStateHash = Animator.StringToHash(MovementAnimationStateName);

    private static readonly Color BacterialTint = new Color(0.48f, 0.025f, 0.04f, 1f);
    private static readonly Color ViralTint = new Color(0.045f, 0.24f, 0.95f, 1f);
    private static readonly Color FungalTint = new Color(0.48f, 0.95f, 0.42f, 1f);
    private static readonly Color AllergenTint = new Color(0.96f, 0.97f, 1f, 1f);
    private static readonly Color ParasiteTint = new Color(1f, 0.78f, 0.04f, 1f);

    [SerializeField, Min(0.1f)] private float wanderRadius = DefaultWanderRadius;
    [SerializeField, Min(0.1f)] private float wanderSpeed = DefaultWanderSpeed;
    [SerializeField, Min(0f)] private float arrivalDistance = DefaultArrivalDistance;
    [SerializeField, Min(0.1f)] private float targetScanIntervalSeconds = DefaultTargetScanIntervalSeconds;
    [SerializeField, Min(1f)] private float turnSpeedDegreesPerSecond = DefaultTurnSpeedDegreesPerSecond;

    private NavMeshAgent navMeshAgent;
    private Animator pathogenAnimator;
    private InfectionSpawner infectionSpawner;
    private Vector3 spawnCenter;
    private Transform currentAttacker;
    private InfectionPathogenType pathogenType = InfectionPathogenType.Bacterial;
    private float nextAttackerScanTime;
    private float nextWanderTime;
    private bool pursuitOverride;
    private bool movementAnimationPlaying;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        pathogenAnimator = GetComponentInChildren<Animator>(true);
        if (pathogenAnimator != null)
            pathogenAnimator.speed = 0f;
        infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        spawnCenter = transform.position;
        ConfigureAgent();
    }

    private void Start()
    {
        ApplyPathogenColor();
        nextWanderTime = Time.time;
    }

    private void Update()
    {
        if (Time.time >= nextAttackerScanTime)
        {
            nextAttackerScanTime = Time.time + Mathf.Max(0.1f, targetScanIntervalSeconds);
            currentAttacker = FindNearestWorkingBatchMember();
        }

        if (currentAttacker != null)
        {
            HoldAndFace(currentAttacker.position);
            SetMovementAnimation(false);
            return;
        }

        if (pursuitOverride)
        {
            FaceMovementDirection();
            UpdateMovementAnimation();
            return;
        }

        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            SetMovementAnimation(false);
            return;
        }

        if (navMeshAgent.isStopped)
            navMeshAgent.isStopped = false;

        Vector3 currentOffset = Vector3.ProjectOnPlane(transform.position - spawnCenter, Vector3.up);
        if (currentOffset.sqrMagnitude > (wanderRadius + BoundaryTolerance) * (wanderRadius + BoundaryTolerance))
        {
            navMeshAgent.ResetPath();
            navMeshAgent.SetDestination(spawnCenter);
            FaceMovementDirection();
            UpdateMovementAnimation();
            return;
        }

        if (Time.time >= nextWanderTime && !navMeshAgent.pathPending &&
            (!navMeshAgent.hasPath || navMeshAgent.remainingDistance <= Mathf.Max(arrivalDistance, navMeshAgent.stoppingDistance)))
        {
            navMeshAgent.ResetPath();
            if (TrySetWanderDestination())
                nextWanderTime = Time.time + Random.Range(0.4f, 1.8f);
            else
                nextWanderTime = Time.time + 0.5f;
        }

        FaceMovementDirection();
        UpdateMovementAnimation();
    }

    /// <summary>Sets the pathogen category and the center used to constrain its wandering radius.</summary>
    /// <param name="type">The infection's pathogen category.</param>
    /// <param name="center">The world-space position around which this pathogen may wander.</param>
    public void Initialize(InfectionPathogenType type, Vector3 center)
    {
        pathogenType = type;
        spawnCenter = center;
        ConfigureAgent();
        SnapToNavMeshSpawnPoint();
        ApplyPathogenColor();
    }

    /// <summary>Pauses autonomous wandering while a bacteria responder is pursuing this pathogen.</summary>
    /// <param name="isPursued">Whether another agent currently controls this pathogen's NavMesh destination.</param>
    public void SetPursuitOverride(bool isPursued)
    {
        pursuitOverride = isPursued;
        if (!pursuitOverride)
            nextWanderTime = Time.time;
    }

    private void ConfigureAgent()
    {
        if (navMeshAgent == null)
            return;

        navMeshAgent.speed = wanderSpeed;
        navMeshAgent.updateRotation = false;
    }

    private void SnapToNavMeshSpawnPoint()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || navMeshAgent.isOnNavMesh ||
            !NavMesh.SamplePosition(spawnCenter, out NavMeshHit hit, NavMeshSampleDistance, navMeshAgent.areaMask))
        {
            return;
        }

        navMeshAgent.Warp(hit.position);
        spawnCenter = hit.position;
    }

    private void ApplyPathogenColor()
    {
        ToonMaterialSwapper.ApplyToPathogen(gameObject, GetPathogenColor(pathogenType));
    }

    private static Color GetPathogenColor(InfectionPathogenType type)
    {
        switch (type)
        {
            case InfectionPathogenType.Viral:
                return ViralTint;
            case InfectionPathogenType.Fungal:
                return FungalTint;
            case InfectionPathogenType.Allergen:
                return AllergenTint;
            case InfectionPathogenType.Parasite:
                return ParasiteTint;
            default:
                return BacterialTint;
        }
    }

    private Transform FindNearestWorkingBatchMember()
    {
        Transform nearestMember = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        if (infectionSpawner == null)
            infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (infectionSpawner != null)
        {
            nearestMember = infectionSpawner.FindNearestArrivedWbcForPathogen(gameObject);
            if (nearestMember != null)
                nearestDistanceSquared = (nearestMember.position - transform.position).sqrMagnitude;
        }

        LymphaticBatchGroup[] batches = FindObjectsByType<LymphaticBatchGroup>(FindObjectsSortMode.None);

        foreach (LymphaticBatchGroup batch in batches)
        {
            if (batch == null || batch.GroupLeader != batch || batch.Status != LymphaticBatchGroup.BatchStatus.Working)
                continue;

            InfectionSpawner.InfectionMarker infection = batch.TargetInfection;
            if (infection == null || infection.threatVisuals == null || !infection.threatVisuals.Contains(gameObject))
                continue;

            foreach (GameObject member in batch.Members)
            {
                if (member == null || !member.activeInHierarchy)
                    continue;

                Health health = member.GetComponent<Health>();
                if (health != null && health.IsDead)
                    continue;

                float distanceSquared = (member.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestMember = member.transform;
                }
            }
        }

        return nearestMember;
    }

    private bool TrySetWanderDestination()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return false;

        float allowedRadius = Mathf.Max(0.1f, wanderRadius);
        float allowedRadiusSquared = allowedRadius * allowedRadius;
        NavMeshPath candidatePath = new NavMeshPath();

        for (int attempt = 0; attempt < DestinationSampleAttempts; attempt++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * allowedRadius;
            Vector3 candidate = spawnCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSampleDistance, navMeshAgent.areaMask))
                continue;

            Vector3 hitOffset = Vector3.ProjectOnPlane(hit.position - spawnCenter, Vector3.up);
            if (hitOffset.sqrMagnitude > allowedRadiusSquared)
                continue;

            if (!navMeshAgent.CalculatePath(hit.position, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete ||
                !PathStaysInsideRadius(candidatePath, allowedRadiusSquared))
            {
                continue;
            }

            if (navMeshAgent.SetDestination(hit.position))
                return true;
        }

        return false;
    }

    private bool PathStaysInsideRadius(NavMeshPath path, float allowedRadiusSquared)
    {
        foreach (Vector3 corner in path.corners)
        {
            Vector3 offset = Vector3.ProjectOnPlane(corner - spawnCenter, Vector3.up);
            if (offset.sqrMagnitude > allowedRadiusSquared)
                return false;
        }

        return true;
    }

    private void UpdateMovementAnimation()
    {
        bool isMoving = navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh &&
            !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > MovementAnimationVelocityThreshold * MovementAnimationVelocityThreshold;
        SetMovementAnimation(isMoving);
    }

    private void SetMovementAnimation(bool shouldAnimate)
    {
        if (pathogenAnimator == null || pathogenAnimator.runtimeAnimatorController == null ||
            movementAnimationPlaying == shouldAnimate)
        {
            return;
        }

        if (shouldAnimate)
        {
            if (!pathogenAnimator.HasState(0, MovementAnimationStateHash))
                return;

            pathogenAnimator.Play(MovementAnimationStateHash, 0, 0f);
            pathogenAnimator.speed = 1f;
            movementAnimationPlaying = true;
            return;
        }

        pathogenAnimator.speed = 0f;
        movementAnimationPlaying = false;
    }

    private void HoldAndFace(Vector3 targetPosition)
    {
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            if (navMeshAgent.hasPath)
                navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }

        FacePosition(targetPosition);
    }

    private void FaceMovementDirection()
    {
        if (navMeshAgent == null)
            return;

        Vector3 direction = Vector3.ProjectOnPlane(navMeshAgent.velocity, Vector3.up);
        if (direction.sqrMagnitude > 0.01f)
            FaceDirection(direction);
    }

    private void FacePosition(Vector3 position)
    {
        Vector3 direction = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.01f)
            FaceDirection(direction);
    }

    private void FaceDirection(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
            Mathf.Max(1f, turnSpeedDegreesPerSecond) * Time.deltaTime);
    }
}
