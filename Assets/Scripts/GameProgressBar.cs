using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays wellness on the existing top progress bar.</summary>
public class GameProgressBar : MonoBehaviour
{
    private static readonly Color HealthyFillColor = new Color(0f, 0.8980392f, 1f, 1f);
    private static readonly Color WarningFillColor = new Color(1f, 0.7568628f, 0.027451f, 1f);
    private static readonly Color CriticalFillColor = new Color(1f, 0.2313726f, 0.1882353f, 1f);

    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI valueLabel;

    private WellnessManager wellnessManager;

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
            wellnessManager.OnWellnessChanged -= HandleWellnessChanged;

        wellnessManager = null;
    }

    private void BindToWellnessManager()
    {
        WellnessManager currentManager = WellnessManager.Instance;
        if (currentManager == null || wellnessManager == currentManager)
            return;

        if (wellnessManager != null)
            wellnessManager.OnWellnessChanged -= HandleWellnessChanged;

        wellnessManager = currentManager;
        wellnessManager.OnWellnessChanged += HandleWellnessChanged;
        HandleWellnessChanged(wellnessManager.CurrentWellness, wellnessManager.MaxWellness);
    }

    private void HandleWellnessChanged(float current, float maximum)
    {
        float safeMaximum = Mathf.Max(1f, maximum);
        float clampedValue = Mathf.Clamp(current, 0f, safeMaximum);
        float normalizedValue = clampedValue / safeMaximum;

        if (fillImage != null)
        {
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = normalizedValue;
            fillImage.color = clampedValue <= (wellnessManager != null ? wellnessManager.CriticalThreshold : 25f)
                ? CriticalFillColor
                : clampedValue < (wellnessManager != null ? wellnessManager.WinThreshold : 50f)
                    ? WarningFillColor
                    : HealthyFillColor;
        }

        if (valueLabel != null)
            valueLabel.text = Mathf.RoundToInt(clampedValue).ToString();
    }
}
