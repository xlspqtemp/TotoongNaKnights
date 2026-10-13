using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays queued viral adaptive-response resolution messages on the HUD.</summary>
[RequireComponent(typeof(CanvasGroup), typeof(AudioSource))]
public sealed class AdaptiveResponsePopup : MonoBehaviour
{
    private const float FadeInDuration = 0.3f;
    private const float HoldDuration = 3f;
    private const float FadeOutDuration = 0.5f;
    private const float MinimumWellnessGap = 30f;
    private const float HudSfxVolume = 0.65f;
    private const string BankGothicResourcePath = "BankGothicMediumSDF";
    private static readonly Vector2 PopupSize = new Vector2(700f, 90f);
    private static readonly Color BackgroundColor = new Color(0.025f, 0.045f, 0.065f, 0.96f);
    private static readonly Color AccentColor = new Color(0.15f, 0.78f, 0.8f, 1f);

    [SerializeField] private AudioClip resolvedSoundClip;

    private readonly Queue<string> pendingDisplayNames = new Queue<string>();
    private CanvasGroup popupCanvasGroup;
    private AudioSource audioSource;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private float animationElapsed;
    private PopupPhase phase;

    private enum PopupPhase
    {
        Hidden,
        FadingIn,
        Holding,
        FadingOut
    }

    /// <summary>Builds the HUD popup, positions it below the wellness bar, and configures its resolve sound.</summary>
    public void Initialize(Canvas hudCanvas, float wellnessGap)
    {
        if (hudCanvas == null || !(hudCanvas.transform is RectTransform canvasRect))
            return;

        popupCanvasGroup = GetComponent<CanvasGroup>();
        audioSource = GetComponent<AudioSource>();
        if (popupCanvasGroup == null || audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        popupCanvasGroup.alpha = 0f;
        popupCanvasGroup.interactable = false;
        popupCanvasGroup.blocksRaycasts = false;

        RectTransform popupRect = transform as RectTransform;
        popupRect.anchorMin = new Vector2(0.5f, 1f);
        popupRect.anchorMax = new Vector2(0.5f, 1f);
        popupRect.pivot = new Vector2(0.5f, 1f);
        popupRect.sizeDelta = PopupSize;
        PositionBelowWellnessBar(popupRect, canvasRect, wellnessGap);
        CreatePopupVisuals(popupRect);
        gameObject.SetActive(false);
    }

    /// <summary>Queues an adaptive-response message and plays its HUD sound once when shown.</summary>
    public void Show(string displayName)
    {
        pendingDisplayNames.Enqueue(string.IsNullOrWhiteSpace(displayName) ? "Viral infection" : displayName);
        if (phase == PopupPhase.Hidden)
            BeginNextPopup();
    }

    private void Update()
    {
        if (phase == PopupPhase.Hidden || popupCanvasGroup == null)
            return;

        animationElapsed += Time.unscaledDeltaTime;
        switch (phase)
        {
            case PopupPhase.FadingIn:
                popupCanvasGroup.alpha = Mathf.Clamp01(animationElapsed / FadeInDuration);
                if (animationElapsed >= FadeInDuration)
                {
                    phase = PopupPhase.Holding;
                    animationElapsed = 0f;
                }
                break;
            case PopupPhase.Holding:
                popupCanvasGroup.alpha = 1f;
                if (animationElapsed >= HoldDuration)
                {
                    phase = PopupPhase.FadingOut;
                    animationElapsed = 0f;
                }
                break;
            case PopupPhase.FadingOut:
                popupCanvasGroup.alpha = 1f - Mathf.Clamp01(animationElapsed / FadeOutDuration);
                if (animationElapsed >= FadeOutDuration)
                {
                    popupCanvasGroup.alpha = 0f;
                    phase = PopupPhase.Hidden;
                    BeginNextPopup();
                }
                break;
        }
    }

    private void BeginNextPopup()
    {
        if (pendingDisplayNames.Count == 0)
        {
            phase = PopupPhase.Hidden;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            return;
        }

        string displayName = pendingDisplayNames.Dequeue();
        titleLabel.text = "ADAPTIVE RESPONSE CLEARED";
        subtitleLabel.text = $"{displayName} resolved by the body. Good work, soldier.";
        popupCanvasGroup.alpha = 0f;
        animationElapsed = 0f;
        phase = PopupPhase.FadingIn;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        if (resolvedSoundClip != null && audioSource != null)
            audioSource.PlayOneShot(resolvedSoundClip, HudSfxVolume);
    }

    private void CreatePopupVisuals(RectTransform popupRect)
    {
        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = BackgroundColor;
        backgroundImage.raycastTarget = false;

        GameObject accentObject = new GameObject("Cyan Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentObject.transform.SetParent(transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(5f, 0f);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        titleLabel = CreateLabel("Title", new Vector2(0f, 0.5f), Vector2.one,
            new Vector2(18f, 0f), new Vector2(-14f, -4f), 18f, FontStyles.Bold, AccentColor,
            TextAlignmentOptions.MidlineLeft, false);
        subtitleLabel = CreateLabel("Subtitle", Vector2.zero, new Vector2(1f, 0.5f),
            new Vector2(18f, 4f), new Vector2(-14f, 0f), 14f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.MidlineLeft, true);
    }

    private TextMeshProUGUI CreateLabel(string objectName, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles fontStyle, Color color,
        TextAlignmentOptions alignment, bool wrap)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = anchorMin;
        labelRect.anchorMax = anchorMax;
        labelRect.offsetMin = offsetMin;
        labelRect.offsetMax = offsetMax;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>(BankGothicResourcePath) ?? TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void PositionBelowWellnessBar(RectTransform popupRect, RectTransform canvasRect, float wellnessGap)
    {
        float wellnessBottomLocalY = float.PositiveInfinity;
        RectTransform wellnessBarRect = canvasRect.Find("GameProgressBar") as RectTransform;
        RectTransform wellnessValueRect = canvasRect.Find("ProgressValueLabel") as RectTransform;
        if (wellnessBarRect != null)
            wellnessBottomLocalY = GetBottomEdgeCanvasLocalY(wellnessBarRect, canvasRect);
        if (wellnessValueRect != null)
            wellnessBottomLocalY = Mathf.Min(wellnessBottomLocalY, GetBottomEdgeCanvasLocalY(wellnessValueRect, canvasRect));

        float verticalGap = Mathf.Max(MinimumWellnessGap, wellnessGap);
        float yFromCanvasTop = verticalGap;
        if (!float.IsPositiveInfinity(wellnessBottomLocalY))
            yFromCanvasTop = wellnessBottomLocalY - canvasRect.rect.yMax - verticalGap;
        popupRect.anchoredPosition = new Vector2(0f, yFromCanvasTop);
    }

    private static float GetBottomEdgeCanvasLocalY(RectTransform targetRect, RectTransform canvasRect)
    {
        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        float bottomLeftY = canvasRect.InverseTransformPoint(corners[0]).y;
        float bottomRightY = canvasRect.InverseTransformPoint(corners[3]).y;
        return Mathf.Min(bottomLeftY, bottomRightY);
    }
}
