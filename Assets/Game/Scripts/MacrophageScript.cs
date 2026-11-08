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
    private const float AnimatorMovementThreshold = 0.1f;
    private const float HoloarmActiveDurationSeconds = 1f;

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
    private float holoarmDeactivateTime;
    private bool holoarmsActiveForAttack;
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
            ShowHoloarmsForAttack();
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
            ShowHoloarmsForAttack();
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
            animator.SetTrigger(AnimatorTauntParameter);
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
        if (leftHoloarm != null && rightHoloarm != null)
            return;

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform childTransform in childTransforms)
        {
            if (leftHoloarm == null && childTransform.name == LeftHoloarmName)
                leftHoloarm = childTransform.gameObject;
            else if (rightHoloarm == null && childTransform.name == RightHoloarmName)
                rightHoloarm = childTransform.gameObject;

            if (leftHoloarm != null && rightHoloarm != null)
                return;
        }
    }

    private void ShowHoloarmsForAttack()
    {
        CacheHoloarms();
        if (leftHoloarm != null)
            leftHoloarm.SetActive(true);
        if (rightHoloarm != null)
            rightHoloarm.SetActive(true);

        holoarmDeactivateTime = Time.time + HoloarmActiveDurationSeconds;
        holoarmsActiveForAttack = leftHoloarm != null || rightHoloarm != null;
    }

    private void UpdateHoloarmVisibility()
    {
        if (!holoarmsActiveForAttack || Time.time < holoarmDeactivateTime)
            return;

        if (leftHoloarm != null)
            leftHoloarm.SetActive(false);
        if (rightHoloarm != null)
            rightHoloarm.SetActive(false);

        holoarmsActiveForAttack = false;
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