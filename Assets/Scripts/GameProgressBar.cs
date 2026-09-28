using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Synchronizes a horizontal progress image with the current in-game day.
/// </summary>
public class GameProgressBar : MonoBehaviour
{
    [SerializeField] private DayCounterUI dayCounter;
    [SerializeField] private int totalGameDays = 30;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        if (dayCounter != null)
            dayCounter.OnDayAdvanced += HandleDayAdvanced;
    }

    private void Start()
    {
        UpdateProgress(dayCounter != null ? dayCounter.CurrentDay : 0);
    }

    private void OnDisable()
    {
        if (dayCounter != null)
            dayCounter.OnDayAdvanced -= HandleDayAdvanced;
    }

    private void HandleDayAdvanced(int currentDay)
    {
        UpdateProgress(currentDay);
    }

    private void UpdateProgress(int currentDay)
    {
        if (fillImage == null)
            return;

        fillImage.fillAmount = totalGameDays > 0
            ? Mathf.Clamp01((float)currentDay / totalGameDays)
            : 0f;
    }
}
