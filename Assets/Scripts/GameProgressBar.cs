using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays wellness and its temporary event-delta indicator on the existing HUD bar.</summary>
public class GameProgressBar : MonoBehaviour
{
    private const float ChangeIndicatorDuration = 1.5f;
    private const float ChangeIndicatorFadeDuration = 0.45f;

    private static readonly Color HealthyFillColor = new Color(0f, 0.8980392f, 1f, 1f);
    private static readonly Color WarningFillColor = new Color(1f, 0.7568628f, 0.027451f, 1f);
    private static readonly Color CriticalFillColor = new Color(1f, 0.2313726f, 0.1882353f, 1f);
    private static readonly Color GainIndicatorColor = new Color(0.25f, 1f, 0.42f, 1f);
    private static readonly Color LossIndicatorColor = new Color(1f, 0.28f, 0.25f, 1f);

    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI valueLabel;
    [SerializeField] private TextMeshProUGUI changeIndicator;
    [SerializeField] private CanvasGroup changeIndicatorCanvasGroup;

    private WellnessManager wellnessManager;
    private Coroutine changeIndicatorCoroutine;

    private void OnEnable()
    {
        BindToWellnessManager();
    }

    private void Start()
    {
        BindToWellnessManager();
    }

    private void OnDisable()
    {
        if (wellnessManager != null)
        {
            wellnessManager.OnWellnessChanged -= HandleWellnessChanged;
            wellnessManager.OnWellnessDeltaApplied -= HandleWellnessDeltaApplied;
        }

        if (changeIndicatorCoroutine != null)
            StopCoroutine(changeIndicatorCoroutine);

        wellnessManager = null;
        changeIndicatorCoroutine = null;
    }

    private void BindToWellnessManager()
    {
        WellnessManager currentManager = WellnessManager.Instance;
        if (currentManager == null || wellnessManager == currentManager)
            return;

        if (wellnessManager != null)
        {
            wellnessManager.OnWellnessChanged -= HandleWellnessChanged;
            wellnessManager.OnWellnessDeltaApplied -= HandleWellnessDeltaApplied;
        }

        wellnessManager = currentManager;
        wellnessManager.OnWellnessChanged += HandleWellnessChanged;
        wellnessManager.OnWellnessDeltaApplied += HandleWellnessDeltaApplied;
        HandleWellnessChanged(wellnessManager.CurrentWellness, wellnessManager.MaxWellness);
    }

    private void HandleWellnessChanged(float current, float maximum)
    {
        float safeMaximum = Mathf.Max(1f, maximum);
        float clampedValue = Mathf.Clamp(current, 0f, safeMaximum);

        if (fillImage != null)
        {
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = clampedValue / safeMaximum;
            fillImage.color = clampedValue <= (wellnessManager != null ? wellnessManager.CriticalThreshold : 25f)
                ? CriticalFillColor
                : clampedValue < (wellnessManager != null ? wellnessManager.WinThreshold : 50f)
                    ? WarningFillColor
                    : HealthyFillColor;
        }

        if (valueLabel != null)
            valueLabel.text = Mathf.RoundToInt(clampedValue).ToString();
    }

    private void HandleWellnessDeltaApplied(float actualDelta)
    {
        if (Mathf.Approximately(actualDelta, 0f) || changeIndicator == null || changeIndicatorCanvasGroup == null)
            return;

        if (changeIndicatorCoroutine != null)
            StopCoroutine(changeIndicatorCoroutine);

        changeIndicator.text = $"{(actualDelta > 0f ? "+" : string.Empty)}{actualDelta:0.##}";
        changeIndicator.color = actualDelta > 0f ? GainIndicatorColor : LossIndicatorColor;
        changeIndicatorCanvasGroup.alpha = 1f;
        changeIndicatorCoroutine = StartCoroutine(FadeChangeIndicator());
    }

    private IEnumerator FadeChangeIndicator()
    {
        float holdDuration = ChangeIndicatorDuration - ChangeIndicatorFadeDuration;
        float elapsed = 0f;

        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float fadeElapsed = 0f;
        while (fadeElapsed < ChangeIndicatorFadeDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            changeIndicatorCanvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / ChangeIndicatorFadeDuration);
            yield return null;
        }

        changeIndicatorCanvasGroup.alpha = 0f;
        changeIndicatorCoroutine = null;
    }
}
