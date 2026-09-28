using System.Collections;
using UnityEngine;

/// <summary>
/// Displays a reusable, non-interactive notification pulse when the random event system broadcasts an event.
/// </summary>
public class RandomEventNotificationUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform notificationTransform;

    [Header("Animation")]
    [Min(0.01f)]
    [SerializeField] private float blinkDuration = 0.15f;
    [Min(0.01f)]
    [SerializeField] private float fadeOutDuration = 0.65f;
    [Min(1f)]
    [SerializeField] private float scalePunch = 1.15f;
    [Range(0f, 1f)]
    [SerializeField] private float peakAlpha = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float postBlinkAlpha = 0.6f;

    private Coroutine notificationRoutine;
    private Vector3 baseScale;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (notificationTransform == null)
            notificationTransform = transform as RectTransform;

        baseScale = notificationTransform != null ? notificationTransform.localScale : Vector3.one;
        HideImmediately();
    }

    private void OnEnable()
    {
        RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
    }

    private void OnDisable()
    {
        RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;

        if (notificationRoutine != null)
        {
            StopCoroutine(notificationRoutine);
            notificationRoutine = null;
        }
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (notificationRoutine != null)
            StopCoroutine(notificationRoutine);

        notificationRoutine = StartCoroutine(PlayNotification());
    }

    private IEnumerator PlayNotification()
    {
        if (canvasGroup == null || notificationTransform == null)
            yield break;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;
        notificationTransform.localScale = baseScale * Mathf.Max(1f, scalePunch);

        float halfBlinkDuration = Mathf.Max(0.005f, blinkDuration * 0.5f);
        yield return Animate(0f, peakAlpha, halfBlinkDuration, notificationTransform.localScale, baseScale);
        yield return Animate(peakAlpha, postBlinkAlpha, halfBlinkDuration, baseScale, baseScale);
        yield return Animate(postBlinkAlpha, 0f, Mathf.Max(0.01f, fadeOutDuration), baseScale, baseScale);

        HideImmediately();
        notificationRoutine = null;
    }

    private IEnumerator Animate(float startAlpha, float endAlpha, float duration, Vector3 startScale, Vector3 endScale)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, easedProgress);
            notificationTransform.localScale = Vector3.LerpUnclamped(startScale, endScale, easedProgress);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        notificationTransform.localScale = endScale;
    }

    private void HideImmediately()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (notificationTransform != null)
            notificationTransform.localScale = baseScale == Vector3.zero ? Vector3.one : baseScale;
    }
}
