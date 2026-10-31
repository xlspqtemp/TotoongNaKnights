using UnityEngine;

/// <summary>Tracks the remaining protective barrier for a body system.</summary>
public sealed class BarrierHealth : MonoBehaviour
{
    private const float MaximumBarrier = 100f;
    private const string DigestiveOrdersContainerPath = "/Event Handlers/HUDCanvas/DigestiveOrdersContainer";
    private const string RespiratoryOrdersContainerPath = "/Event Handlers/HUDCanvas/RespiratoryOrdersContainer";

    public AudioClip breachSfx;
    [SerializeField] private AudioSource breachAudioSource;

    public float CurrentBarrier = MaximumBarrier;
    public bool IsBreached { get; private set; }

    /// <summary>Raised when positive damage actually reduces this barrier's current value.</summary>
    public event System.Action<float> OnBarrierDamaged;

    public AudioClip BreachSfx => breachSfx;

    private void Awake()
    {
        CurrentBarrier = Mathf.Clamp(CurrentBarrier, 0f, MaximumBarrier);
        if (breachAudioSource == null)
            breachAudioSource = GetComponent<AudioSource>();
        if (breachAudioSource == null)
            breachAudioSource = gameObject.AddComponent<AudioSource>();

        breachAudioSource.playOnAwake = false;
        breachAudioSource.loop = true;
        breachAudioSource.spatialBlend = 0f;
        RefreshBreachState();
    }

    private void Update()
    {
        RefreshBreachState();
        RefreshBreachAudio();
    }

    private void OnDisable()
    {
        if (breachAudioSource != null && breachAudioSource.isPlaying)
            breachAudioSource.Stop();
    }

    /// <summary>Reduces the barrier by the requested amount and updates its breach state.</summary>
    public void TakeDamage(float amount)
    {
        float previousBarrier = CurrentBarrier;
        if (amount > 0f)
            CurrentBarrier = Mathf.Clamp(CurrentBarrier - amount, 0f, MaximumBarrier);

        float appliedDamage = previousBarrier - CurrentBarrier;
        if (appliedDamage > 0f)
            OnBarrierDamaged?.Invoke(appliedDamage);

        RefreshBreachState();
    }

    /// <summary>Restores the barrier without exceeding its maximum value.</summary>
    public void RestoreBarrier(float amount)
    {
        if (amount > 0f)
            CurrentBarrier = Mathf.Clamp(CurrentBarrier + amount, 0f, MaximumBarrier);

        RefreshBreachState();
    }

    private void RefreshBreachState()
    {
        bool isCurrentlyBreached = CurrentBarrier <= 0f;
        if (isCurrentlyBreached && !IsBreached)
        {
            IsBreached = true;
            Debug.LogWarning($"[Barrier] {name} Barrier has been breached! Next day pathogens will be severely mutated.", this);
        }
        else if (!isCurrentlyBreached)
        {
            IsBreached = false;
        }
    }

    private void RefreshBreachAudio()
    {
        bool shouldPlay = IsBreached && breachSfx != null && IsLayerVisible();
        if (!shouldPlay)
        {
            if (breachAudioSource.isPlaying)
                breachAudioSource.Stop();
            return;
        }

        if (breachAudioSource.clip != breachSfx)
        {
            breachAudioSource.Stop();
            breachAudioSource.clip = breachSfx;
        }

        if (!breachAudioSource.isPlaying)
            breachAudioSource.Play();
    }

    private bool IsLayerVisible()
    {
        string ordersContainerPath = gameObject.name == "Digestive"
            ? DigestiveOrdersContainerPath
            : gameObject.name == "Respiratory" ? RespiratoryOrdersContainerPath : null;
        if (ordersContainerPath == null)
            return false;

        GameObject ordersContainer = GameObject.Find(ordersContainerPath);
        CanvasGroup canvasGroup = ordersContainer != null ? ordersContainer.GetComponent<CanvasGroup>() : null;
        return canvasGroup != null && canvasGroup.alpha > 0.5f;
    }
}
