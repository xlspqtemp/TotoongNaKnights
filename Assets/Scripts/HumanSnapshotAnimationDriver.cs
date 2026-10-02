using UnityEngine;

/// <summary>Maps supported routine and random events to one-shot Human Snapshot animation states.</summary>
public sealed class HumanSnapshotAnimationDriver : MonoBehaviour
{
    private const float CrossfadeDuration = 0.25f;
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

    private int lastRequestedStateHash = IdleState;

    private void Awake()
    {
        if (characterAnimator == null)
            characterAnimator = GetComponent<Animator>();

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

    private void HandleGameplaySpeedChanged()
    {
        ApplyGameplaySpeed();
    }

    private void ApplyGameplaySpeed()
    {
        if (characterAnimator != null)
            characterAnimator.speed = GameplaySpeed.Multiplier;
    }
}
