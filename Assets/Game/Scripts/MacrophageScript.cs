using UnityEngine;
using UnityEngine.AI;

public class MacrophageScript : MonoBehaviour
{
    private const string AnimatorMovingParameter = "IsMoving";
    private const string AnimatorArrivalParameter = "Arrival";
    private const string AnimatorAttackParameter = "Attack";
    private const string AnimatorCrossPunchParameter = "CrossPunch";
    private const string AnimatorTauntParameter = "Taunt";
    private const string LeftHoloarmName = "L holoarm";
    private const string RightHoloarmName = "R holoarm";
    private const string IntegratedHoloarmName = "Right holoarm";
    private const string JumpAttackStateName = "Base Layer.Jump attack";
    private const string MutantSwingStateName = "Base Layer.Mutant swiping";
    private const string CrossPunchStateName = "Base Layer.Cross punch";
    private const string TauntStateName = "Base Layer.Taunt";
    private const float AnimatorMovementThreshold = 0.1f;
    private const float HoloarmFallbackDurationSeconds = 4f;
    private static readonly int JumpAttackStateHash = Animator.StringToHash("Jump attack");
    private static readonly int MutantSwingStateHash = Animator.StringToHash("Mutant swiping");
    private static readonly int CrossPunchStateHash = Animator.StringToHash("Cross punch");
    private static readonly int TauntStateHash = Animator.StringToHash("Taunt");

    public NavMeshAgent agent;
    public Transform target;

    public float radius = 5f;
    public float damage = 20f;
    public int health = 300;

    [SerializeField] private Animator animator;

    private Animator cachedAnimator;
    private RuntimeAnimatorController cachedAnimatorController;
    private AnimatorControllerParameter[] cachedAnimatorParameters;
    private GameObject leftHoloarm;
    private GameObject rightHoloarm;
    private GameObject integratedHoloarm;
    private float holoarmFallbackDeactivateTime;
    private int attackAnimationIndex;

    /*private void OnTriggerEnter(Collider other)
    {
        // Make sure your Lava Block GameObject has the Tag "Lava"
        if (other.CompareTag("Lava"))
        {
            temp = 10f;
        }
    }*/

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        EnsureAnimator();
        CacheHoloarms();

/*
        InvokeRepeating(nameof(AttackBacteria), 1f, 1f);
        Destroy(gameObject, health);
*/
    }

    private void Update()
    {
        UpdateMovementAnimation();
        UpdateHoloarmVisibility();
    }

    /// <summary>Triggers the macrophage jump-attack arrival animation and materializes its holoarms.</summary>
    public void TriggerArrivalAnimation()
    {
        EnsureAnimator();
        if (animator == null)
            return;

        SetMovingAnimation(false);
        if (HasAnimatorParameter(AnimatorArrivalParameter, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(AnimatorArrivalParameter);
            RequestHoloarmsForCombat();
        }
    }

    /// <summary>Triggers three Attack swipes for each CrossPunch animation on this macrophage.</summary>
    public void TriggerAttackAnimation()
    {
        EnsureAnimator();
        bool useCrossPunch = attackAnimationIndex == 3;
        attackAnimationIndex = (attackAnimationIndex + 1) % 4;
        if (animator == null)
            return;

        SetMovingAnimation(false);
        string parameterName = useCrossPunch ? AnimatorCrossPunchParameter : AnimatorAttackParameter;
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterName);
            RequestHoloarmsForCombat();
        }
    }

    /// <summary>Triggers the macrophage taunt when it clears a pathogen.</summary>
    public void TriggerTauntAnimation()
    {
        EnsureAnimator();
        if (animator == null)
            return;

        SetMovingAnimation(false);
        if (HasAnimatorParameter(AnimatorTauntParameter, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(AnimatorTauntParameter);
            RequestHoloarmsForCombat();
        }
    }

    private void UpdateMovementAnimation()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        EnsureAnimator();
        if (animator == null)
            return;

        bool hasUsableAgent = agent != null && agent.enabled && agent.isOnNavMesh;
        bool hasActivePath = hasUsableAgent && (agent.pathPending || agent.hasPath);
        Vector3 velocity = hasUsableAgent ? agent.velocity : Vector3.zero;
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        bool isMoving = hasActivePath && !agent.isStopped &&
            planarVelocity.sqrMagnitude > AnimatorMovementThreshold * AnimatorMovementThreshold;
        SetMovingAnimation(isMoving);
    }

    private void CacheHoloarms()
    {
        if (integratedHoloarm != null || (leftHoloarm != null && rightHoloarm != null))
            return;

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform childTransform in childTransforms)
        {
            if (childTransform.name == LeftHoloarmName)
                leftHoloarm = childTransform.gameObject;
            else if (childTransform.name == RightHoloarmName)
                rightHoloarm = childTransform.gameObject;
            else if (childTransform.name == IntegratedHoloarmName)
                integratedHoloarm = childTransform.gameObject;
        }
    }

    private void RequestHoloarmsForCombat()
    {
        holoarmFallbackDeactivateTime = Time.time + HoloarmFallbackDurationSeconds;
        SetHoloarmsActive(true);
    }

    private void SetHoloarmsActive(bool isActive)
    {
        CacheHoloarms();
        if (leftHoloarm != null && leftHoloarm.activeSelf != isActive)
            leftHoloarm.SetActive(isActive);
        if (rightHoloarm != null && rightHoloarm.activeSelf != isActive)
            rightHoloarm.SetActive(isActive);
        if (integratedHoloarm != null && integratedHoloarm.activeSelf != isActive)
            integratedHoloarm.SetActive(isActive);
    }

    private void UpdateHoloarmVisibility()
    {
        EnsureAnimator();
        if (animator == null || animator.layerCount == 0)
        {
            SetHoloarmsActive(false);
            return;
        }

        bool isCombatAnimationActive = IsCombatAnimationState(animator.GetCurrentAnimatorStateInfo(0));
        if (animator.IsInTransition(0))
            isCombatAnimationActive |= IsCombatAnimationState(animator.GetNextAnimatorStateInfo(0));

        SetHoloarmsActive(isCombatAnimationActive || Time.time < holoarmFallbackDeactivateTime);
    }

    private static bool IsCombatAnimationState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.shortNameHash == JumpAttackStateHash ||
            stateInfo.shortNameHash == MutantSwingStateHash ||
            stateInfo.shortNameHash == CrossPunchStateHash ||
            stateInfo.shortNameHash == TauntStateHash ||
            stateInfo.IsName(JumpAttackStateName) ||
            stateInfo.IsName(MutantSwingStateName) ||
            stateInfo.IsName(CrossPunchStateName) ||
            stateInfo.IsName(TauntStateName);
    }

    private void SetMovingAnimation(bool isMoving)
    {
        if (animator != null && HasAnimatorParameter(AnimatorMovingParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(AnimatorMovingParameter, isMoving);
    }

    private void EnsureAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            cachedAnimator = null;
            cachedAnimatorController = null;
            cachedAnimatorParameters = null;
            return;
        }

        if (!animator.enabled)
            animator.enabled = true;

        RuntimeAnimatorController currentController = animator.runtimeAnimatorController;
        if (animator != cachedAnimator || currentController != cachedAnimatorController)
        {
            cachedAnimator = animator;
            cachedAnimatorController = currentController;
            cachedAnimatorParameters = currentController != null ? animator.parameters : null;
        }
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || cachedAnimatorParameters == null)
            return false;

        foreach (AnimatorControllerParameter parameter in cachedAnimatorParameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }

/*
    void Track()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Bacteria");
        float closestDist = Mathf.Infinity;
        Transform mainTarget = null;

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < closestDist)
            {
                closestDist = distance;
                mainTarget = enemy.transform;
            }
        }
        target = mainTarget;
    }
    void AttackBacteria()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (var hit in hits)
        {
            BacteriaScript_A bacteria = hit.GetComponentInParent<BacteriaScript_A>();

            if (bacteria != null && bacteria.bacteriaHP > 0)
            {
                bacteria.TakeDamage(damage);
            }
        }
    }
    */
}