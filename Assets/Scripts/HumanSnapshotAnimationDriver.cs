using UnityEngine;

/// <summary>Maps supported routine and random events to one-shot Human Snapshot animation states.</summary>
public sealed class HumanSnapshotAnimationDriver : MonoBehaviour
{
    private const float CrossfadeDuration = 0.25f;
    private const float DefaultOrthographicSize = 10.5f;
    private const float SleepingModelRollDegrees = -90f;
    private const float FramingPadding = 1.12f;
    private const float FramingSmoothing = 8f;
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Breathing Idle");
    private static readonly int SleepingState = Animator.StringToHash("Base Layer.Sleeping Idle");
    private static readonly int WakingUpState = Animator.StringToHash("Base Layer.Getting Up");
    private static readonly int EatingState = Animator.StringToHash("Base Layer.Sitting Drinking");
    private static readonly int WalkingState = Animator.StringToHash("Base Layer.Walking");
    private static readonly int LeisureState = Animator.StringToHash("Base Layer.Gaming");
    private static readonly int ExerciseState = Animator.StringToHash("Base Layer.Jumping Jacks");
    private static readonly int SkippedMealState = Animator.StringToHash("Base Layer.Sitting");
    private static readonly int SmokingState = Animator.StringToHash("Base Layer.Smoking");
    private static readonly int WoundState = Animator.StringToHash("Base Layer.Injured Walking");
    private static readonly int CoughState = Animator.StringToHash("Base Layer.Laying Severe Cough");

    [SerializeField] private Animator characterAnimator;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform cameraTargetPivot;

    private int lastRequestedStateHash = IdleState;
    private Renderer[] characterRenderers;
    private Quaternion characterBaseWorldRotation;
    private float minimumOrthographicSize = DefaultOrthographicSize;
    private bool sleepingPresentation;

    /// <summary>Returns the Animator that drives the 3D Human Snapshot model.</summary>
    public Animator CharacterAnimator => characterAnimator;

    private void Awake()
    {
        if (characterAnimator == null)
            characterAnimator = GetComponent<Animator>();

        if (GetComponent<ToonMaterialSwapper>() == null)
            gameObject.AddComponent<ToonMaterialSwapper>();

        if (characterAnimator != null)
        {
            characterBaseWorldRotation = characterAnimator.transform.rotation;
            characterRenderers = characterAnimator.GetComponentsInChildren<Renderer>(true);
        }

        GameObject previewCameraObject = GameObject.Find("HumanSnapshotCamera");
        if (previewCamera == null && previewCameraObject != null)
            previewCamera = previewCameraObject.GetComponent<Camera>();

        GameObject cameraTargetObject = GameObject.Find("HumanSnapshotOrbitPivot");
        if (cameraTargetPivot == null && cameraTargetObject != null)
            cameraTargetPivot = cameraTargetObject.transform;

        if (previewCamera != null && previewCamera.orthographic)
            minimumOrthographicSize = previewCamera.orthographicSize;

        ApplyGameplaySpeed();
    }

    private void OnEnable()
    {
        RoutineSystem.OnRoutineActivityChanged += HandleRoutineActivityChanged;
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
        GameplaySpeed.OnSpeedChanged += HandleGameplaySpeedChanged;
        ApplyGameplaySpeed();
    }

    private void OnDisable()
    {
        RoutineSystem.OnRoutineActivityChanged -= HandleRoutineActivityChanged;
        RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;
        GameplaySpeed.OnSpeedChanged -= HandleGameplaySpeedChanged;
    }

    private void HandleRoutineActivityChanged(RoutineActivity activity)
    {
        sleepingPresentation = activity == RoutineActivity.Sleeping || activity == RoutineActivity.SleepingIn;
        switch (activity)
        {
            case RoutineActivity.Idle:
                PlayState(IdleState);
                break;
            case RoutineActivity.Sleeping:
            case RoutineActivity.SleepingIn:
                PlayState(SleepingState);
                break;
            case RoutineActivity.WakingUp:
                PlayState(WakingUpState);
                break;
            case RoutineActivity.EatingBreakfast:
            case RoutineActivity.EatingLunch:
            case RoutineActivity.EatingDinner:
            case RoutineActivity.EatingJunkFood:
                PlayState(EatingState);
                break;
            case RoutineActivity.WalkingLightActivity:
                PlayState(WalkingState);
                break;
            case RoutineActivity.LeisureTime:
                PlayState(LeisureState);
                break;
            case RoutineActivity.Exercising:
                PlayState(ExerciseState);
                break;
        }
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData == null)
            return;

        sleepingPresentation = false;
        switch (eventData.eventName)
        {
            case "Skipped a meal":
                PlayState(SkippedMealState);
                break;
            case "Smoked cigarette":
                PlayState(SmokingState);
                break;
            case "Nicked by a sharp object or tripped and scraped knees":
            case "Tripped on a Rock and Got Scratches":
                PlayState(WoundState);
                break;
            case "Inhaled dust/allergen":
            case "Caught a cold from a sick classmate/coworker":
                PlayState(CoughState);
                break;
        }
    }

    private void PlayState(int stateHash)
    {
        if (characterAnimator == null || lastRequestedStateHash == stateHash)
            return;

        if (!characterAnimator.HasState(0, stateHash))
        {
            Debug.LogWarning($"Human Snapshot Animator Controller is missing state hash {stateHash}.", this);
            return;
        }

        lastRequestedStateHash = stateHash;
        characterAnimator.CrossFadeInFixedTime(stateHash, CrossfadeDuration, 0);
    }

    private void LateUpdate()
    {
        if (characterAnimator == null || previewCamera == null || cameraTargetPivot == null ||
            characterRenderers == null || characterRenderers.Length == 0)
            return;

        Transform characterTransform = characterAnimator.transform;
        if (sleepingPresentation)
        {
            Vector3 cameraForward = previewCamera.transform.forward;
            Quaternion sleepRoll = Quaternion.AngleAxis(SleepingModelRollDegrees, cameraForward);
            characterTransform.rotation = sleepRoll * characterBaseWorldRotation;
        }
        else
        {
            characterTransform.rotation = characterBaseWorldRotation;
        }

        Bounds combinedBounds = default;
        bool hasBounds = false;
        foreach (Renderer characterRenderer in characterRenderers)
        {
            if (characterRenderer == null || !characterRenderer.enabled || !characterRenderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                combinedBounds = characterRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(characterRenderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        Transform targetParent = cameraTargetPivot.parent;
        Vector3 targetLocalPosition = targetParent != null
            ? targetParent.InverseTransformPoint(combinedBounds.center)
            : combinedBounds.center;
        float blend = 1f - Mathf.Exp(-FramingSmoothing * Time.unscaledDeltaTime);
        cameraTargetPivot.localPosition = Vector3.Lerp(cameraTargetPivot.localPosition, targetLocalPosition, blend);

        if (!previewCamera.orthographic)
            return;

        Vector3 boundsCenterInCamera = previewCamera.transform.InverseTransformPoint(combinedBounds.center);
        float halfWidthInCamera = 0f;
        float halfHeightInCamera = 0f;
        Vector3 min = combinedBounds.min;
        Vector3 max = combinedBounds.max;
        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            Vector3 worldCorner = new Vector3(
                (cornerIndex & 1) == 0 ? min.x : max.x,
                (cornerIndex & 2) == 0 ? min.y : max.y,
                (cornerIndex & 4) == 0 ? min.z : max.z);
            Vector3 cameraCorner = previewCamera.transform.InverseTransformPoint(worldCorner);
            halfWidthInCamera = Mathf.Max(halfWidthInCamera, Mathf.Abs(cameraCorner.x - boundsCenterInCamera.x));
            halfHeightInCamera = Mathf.Max(halfHeightInCamera, Mathf.Abs(cameraCorner.y - boundsCenterInCamera.y));
        }

        float verticalSize = Mathf.Max(halfHeightInCamera, halfWidthInCamera / Mathf.Max(previewCamera.aspect, 0.01f));
        float targetOrthographicSize = Mathf.Max(minimumOrthographicSize, verticalSize * FramingPadding);
        previewCamera.orthographicSize = Mathf.Lerp(previewCamera.orthographicSize, targetOrthographicSize, blend);
    }

    private void HandleGameplaySpeedChanged()
    {
        ApplyGameplaySpeed();
    }

    private void ApplyGameplaySpeed()
    {
        if (characterAnimator != null)
            characterAnimator.speed = Time.timeScale == 0f ? 0f : 1f;
    }
}
