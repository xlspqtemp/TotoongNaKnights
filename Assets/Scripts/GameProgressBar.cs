using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays wellness as a segmented HUD bar and handles its visual feedback.</summary>
public class GameProgressBar : MonoBehaviour
{
    private const float ChangeIndicatorDuration = 1.5f;
    private const float ChangeIndicatorFadeDuration = 0.45f;
    private const string BankGothicResourcePath = "BankGothicMediumSDF";
    private const int RoundedSpriteResolution = 32;
    private const float RoundedSpriteCornerRadius = 8f;
    private const float RoundedSpritePixelCenter = 0.5f;

    private static readonly Color DefaultHealthyFillColor = new Color(0f, 0.85f, 1f, 1f);
    private static readonly Color DefaultWarningFillColor = new Color(1f, 0.72f, 0.02f, 1f);
    private static readonly Color DefaultCriticalFillColor = new Color(1f, 0.2f, 0.33f, 1f);
    private static readonly Color DefaultUnlitSegmentColor = new Color(0.039f, 0.098f, 0.16f, 1f);
    private static readonly Color DefaultFrameColor = new Color(0f, 0.85f, 1f, 1f);
    private static readonly Color DefaultGainFlashColor = new Color(0.85f, 1f, 1f, 1f);
    private static readonly Color DefaultLossFlashColor = new Color(1f, 0.2f, 0.33f, 1f);
    private static readonly Color DefaultLabelColor = new Color(0.65f, 0.86f, 0.91f, 1f);

    [Header("Wellness References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI valueLabel;
    [SerializeField] private TextMeshProUGUI changeIndicator;
    [SerializeField] private CanvasGroup changeIndicatorCanvasGroup;

    [Header("Segment Layout")]
    [SerializeField, Min(1)] private int segmentCount = 10;
    [SerializeField, Min(0f)] private float segmentGap = 2f;
    [SerializeField, Min(1f)] private float segmentHeight = 9f;
    [SerializeField, Min(0f)] private float segmentHorizontalInset = 8f;
    [SerializeField, Min(0f)] private float frameThickness = 1.5f;
    [SerializeField, Min(1f)] private float cornerBracketLength = 6f;
    [SerializeField, Min(1f)] private float cornerBracketThickness = 1.5f;

    [Header("Segment Colors")]
    [SerializeField] private Color healthyFillColor = DefaultHealthyFillColor;
    [SerializeField] private Color warningFillColor = DefaultWarningFillColor;
    [SerializeField] private Color criticalFillColor = DefaultCriticalFillColor;
    [SerializeField] private Color unlitSegmentColor = DefaultUnlitSegmentColor;
    [SerializeField] private Color frameColor = DefaultFrameColor;
    [SerializeField] private Color gainFlashColor = DefaultGainFlashColor;
    [SerializeField] private Color lossFlashColor = DefaultLossFlashColor;
    [SerializeField] private Color labelColor = DefaultLabelColor;
    [SerializeField, Min(0f)] private float warningThreshold = 50f;
    [SerializeField, Min(0f)] private float criticalThreshold = 25f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float fillTransitionDuration = 0.22f;
    [SerializeField, Min(0f)] private float segmentStaggerDelay = 0.025f;
    [SerializeField, Min(0f)] private float gainFlashDuration = 0.3f;
    [SerializeField, Min(0f)] private float lossFlashDuration = 0.3f;

    [Header("Labels")]
    [SerializeField, Min(1f)] private float wellnessLabelFontSize = 13f;
    [SerializeField, Min(1f)] private float valueLabelFontSize = 16f;

    private readonly List<WellnessSegment> segments = new List<WellnessSegment>();
    private WellnessManager wellnessManager;
    private Coroutine changeIndicatorCoroutine;
    private TextMeshProUGUI wellnessLabel;
    private static Sprite sharedRoundedSprite;
    private Sprite roundedSprite;
    private float targetWellness;
    private float targetMaximum;
    private float previousWellnessForDelta;
    private float segmentTransitionElapsed;
    private Color currentFillColor = DefaultHealthyFillColor;
    private bool hasInitialWellnessValue;

    private sealed class WellnessSegment
    {
        public Image backgroundImage;
        public Image fillImage;
        public float currentFill;
        public float startFill;
        public float targetFill;
        public float transitionDelay;
        public float flashRemaining;
        public float flashDuration;
        public Color flashColor;
    }

    private void OnEnable()
    {
        BuildSegmentedVisuals();
        BindToWellnessManager();
    }

    private void Start()
    {
        BuildSegmentedVisuals();
        BindToWellnessManager();
    }

    private void Update()
    {
        UpdateSegmentAnimations(Time.unscaledDeltaTime);
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

    private void BuildSegmentedVisuals()
    {
        if (segments.Count > 0 || fillImage == null)
            return;

        roundedSprite = GetRoundedSprite();
        segmentCount = Mathf.Max(1, segmentCount);
        RectTransform barRect = transform as RectTransform;
        if (barRect == null)
            return;

        fillImage.sprite = roundedSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = unlitSegmentColor;
        fillImage.raycastTarget = false;
        RectTransform trackRect = fillImage.rectTransform;
        trackRect.anchorMin = Vector2.zero;
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = new Vector2(frameThickness, frameThickness);



        trackRect.offsetMax = new Vector2(-frameThickness, -frameThickness);
        fillImage.transform.SetAsFirstSibling();

        CreateFrameLines(barRect);
        CreateCornerBrackets(barRect);
        CreateSegments(barRect);
        CreateWellnessLabel(barRect);
        ConfigureValueLabel(barRect);
    }

    private static Sprite GetRoundedSprite()
    {
        if (sharedRoundedSprite != null)
            return sharedRoundedSprite;

        Texture2D texture = new Texture2D(RoundedSpriteResolution, RoundedSpriteResolution, TextureFormat.RGBA32, false);
        texture.name = "Generated Wellness Rounded Sprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < RoundedSpriteResolution; y++)
        {
            for (int x = 0; x < RoundedSpriteResolution; x++)
            {
                float sampleX = x + RoundedSpritePixelCenter;
                float sampleY = y + RoundedSpritePixelCenter;
                float nearestX = Mathf.Clamp(sampleX, RoundedSpriteCornerRadius, RoundedSpriteResolution - RoundedSpriteCornerRadius);
                float nearestY = Mathf.Clamp(sampleY, RoundedSpriteCornerRadius, RoundedSpriteResolution - RoundedSpriteCornerRadius);
                float distance = Vector2.Distance(new Vector2(sampleX, sampleY), new Vector2(nearestX, nearestY));
                float alpha = Mathf.Clamp01(RoundedSpriteCornerRadius + RoundedSpritePixelCenter - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        sharedRoundedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, RoundedSpriteResolution, RoundedSpriteResolution),
            new Vector2(0.5f, 0.5f),
            RoundedSpriteResolution,
            0,
            SpriteMeshType.FullRect,
            new Vector4(RoundedSpriteCornerRadius, RoundedSpriteCornerRadius, RoundedSpriteCornerRadius, RoundedSpriteCornerRadius));
        sharedRoundedSprite.name = "Generated Wellness Rounded Sprite";
        sharedRoundedSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedRoundedSprite;
    }

    private void CreateFrameLines(RectTransform barRect)
    {
        float width = barRect.rect.width;
        float height = barRect.rect.height;
        CreateBarImage("Wellness Frame Top", new Vector2(0f, height * 0.5f), new Vector2(width, frameThickness), frameColor, false);
        CreateBarImage("Wellness Frame Bottom", new Vector2(0f, -height * 0.5f), new Vector2(width, frameThickness), frameColor, false);
        CreateBarImage("Wellness Frame Left", new Vector2(-width * 0.5f, 0f), new Vector2(frameThickness, height), frameColor, false);
        CreateBarImage("Wellness Frame Right", new Vector2(width * 0.5f, 0f), new Vector2(frameThickness, height), frameColor, false);
    }

    private void CreateCornerBrackets(RectTransform barRect)
    {
        float halfWidth = barRect.rect.width * 0.5f;
        float halfHeight = barRect.rect.height * 0.5f;
        float horizontalCenter = halfWidth - cornerBracketLength * 0.5f;
        float verticalCenter = halfHeight - cornerBracketLength * 0.5f;
        float left = -horizontalCenter;
        float right = horizontalCenter;
        float top = verticalCenter;
        float bottom = -verticalCenter;

        CreateBarImage("Wellness Bracket TL Horizontal", new Vector2(left + cornerBracketLength * 0.5f, top), new Vector2(cornerBracketLength, cornerBracketThickness), frameColor, false);
        CreateBarImage("Wellness Bracket TL Vertical", new Vector2(left, top - cornerBracketLength * 0.5f), new Vector2(cornerBracketThickness, cornerBracketLength), frameColor, false);
        CreateBarImage("Wellness Bracket TR Horizontal", new Vector2(right - cornerBracketLength * 0.5f, top), new Vector2(cornerBracketLength, cornerBracketThickness), frameColor, false);
        CreateBarImage("Wellness Bracket TR Vertical", new Vector2(right, top - cornerBracketLength * 0.5f), new Vector2(cornerBracketThickness, cornerBracketLength), frameColor, false);
        CreateBarImage("Wellness Bracket BL Horizontal", new Vector2(left + cornerBracketLength * 0.5f, bottom), new Vector2(cornerBracketLength, cornerBracketThickness), frameColor, false);
        CreateBarImage("Wellness Bracket BL Vertical", new Vector2(left, bottom + cornerBracketLength * 0.5f), new Vector2(cornerBracketThickness, cornerBracketLength), frameColor, false);
        CreateBarImage("Wellness Bracket BR Horizontal", new Vector2(right - cornerBracketLength * 0.5f, bottom), new Vector2(cornerBracketLength, cornerBracketThickness), frameColor, false);
        CreateBarImage("Wellness Bracket BR Vertical", new Vector2(right, bottom + cornerBracketLength * 0.5f), new Vector2(cornerBracketThickness, cornerBracketLength), frameColor, false);
    }

    private void CreateSegments(RectTransform barRect)
    {
        float availableWidth = Mathf.Max(0f, barRect.rect.width - segmentHorizontalInset * 2f);
        float totalGapWidth = segmentGap * (segmentCount - 1);
        float segmentWidth = Mathf.Max(1f, (availableWidth - totalGapWidth) / segmentCount);
        float totalSegmentsWidth = segmentWidth * segmentCount + totalGapWidth;
        float leftEdge = -totalSegmentsWidth * 0.5f;

        for (int index = 0; index < segmentCount; index++)
        {
            float centerX = leftEdge + segmentWidth * 0.5f + index * (segmentWidth + segmentGap);
            GameObject segmentObject = new GameObject($"Wellness Segment {index + 1}", typeof(RectTransform));
            segmentObject.transform.SetParent(transform, false);
            RectTransform segmentRect = segmentObject.GetComponent<RectTransform>();
            segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
            segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
            segmentRect.pivot = new Vector2(0.5f, 0.5f);
            segmentRect.anchoredPosition = new Vector2(centerX, 0f);
            segmentRect.sizeDelta = new Vector2(segmentWidth, segmentHeight);

            Image backgroundImage = CreateImageChild(segmentObject.transform, "Unlit", unlitSegmentColor);
            Image litImage = CreateImageChild(segmentObject.transform, "Lit", healthyFillColor);
            WellnessSegment segment = new WellnessSegment
            {
                backgroundImage = backgroundImage,
                fillImage = litImage,
                flashColor = Color.white
            };
            segments.Add(segment);
            UpdateSegmentColor(segment);
        }
    }

    private Image CreateImageChild(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void CreateBarImage(string objectName, Vector2 position, Vector2 size, Color color, bool rounded)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = position;
        imageRect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = rounded ? roundedSprite : null;
        image.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
    }

    private void CreateWellnessLabel(RectTransform barRect)
    {
        if (wellnessLabel != null)
            return;

        GameObject labelObject = new GameObject("Wellness Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 14f);
        labelRect.sizeDelta = new Vector2(Mathf.Max(120f, barRect.rect.width * 0.5f), 18f);
        wellnessLabel = labelObject.GetComponent<TextMeshProUGUI>();
        wellnessLabel.font = Resources.Load<TMP_FontAsset>(BankGothicResourcePath) ?? TMP_Settings.defaultFontAsset;
        wellnessLabel.fontSize = wellnessLabelFontSize;
        wellnessLabel.fontStyle = FontStyles.Bold;
        wellnessLabel.color = labelColor;
        wellnessLabel.alignment = TextAlignmentOptions.Left;
        wellnessLabel.textWrappingMode = TextWrappingModes.NoWrap;
        wellnessLabel.text = "WELLNESS";
        wellnessLabel.raycastTarget = false;
    }

    private void ConfigureValueLabel(RectTransform barRect)
    {
        if (valueLabel == null)
            return;

        RectTransform valueRect = valueLabel.rectTransform;
        valueRect.anchorMin = new Vector2(0.5f, 1f);
        valueRect.anchorMax = new Vector2(0.5f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.sizeDelta = new Vector2(140f, 20f);
        valueRect.anchoredPosition = new Vector2(barRect.anchoredPosition.x + barRect.rect.width * 0.5f,
            barRect.anchoredPosition.y + 14f);
        valueLabel.font = Resources.Load<TMP_FontAsset>(BankGothicResourcePath) ?? TMP_Settings.defaultFontAsset;
        valueLabel.fontSize = valueLabelFontSize;
        valueLabel.fontStyle = FontStyles.Bold;
        valueLabel.alignment = TextAlignmentOptions.Right;
        valueLabel.textWrappingMode = TextWrappingModes.NoWrap;
        valueLabel.color = labelColor;
        valueLabel.raycastTarget = false;
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
        previousWellnessForDelta = hasInitialWellnessValue ? targetWellness : clampedValue;
        float previousMaximum = hasInitialWellnessValue ? targetMaximum : safeMaximum;
        float oldSegmentValue = previousMaximum > 0f ? previousWellnessForDelta / previousMaximum * segments.Count : 0f;

        targetWellness = clampedValue;
        targetMaximum = safeMaximum;
        float wellnessPercentage = clampedValue / safeMaximum * 100f;
        currentFillColor = GetWellnessFillColor(wellnessPercentage);

        if (valueLabel != null)
            valueLabel.text = $"{Mathf.RoundToInt(clampedValue)} / {Mathf.RoundToInt(safeMaximum)}";

        if (segments.Count == 0)
            return;

        float newSegmentValue = clampedValue / safeMaximum * segments.Count;
        if (!hasInitialWellnessValue)
        {
            for (int index = 0; index < segments.Count; index++)
            {
                float amount = Mathf.Clamp01(newSegmentValue - index);
                segments[index].currentFill = amount;
                segments[index].startFill = amount;
                segments[index].targetFill = amount;
                UpdateSegmentColor(segments[index]);
            }
            hasInitialWellnessValue = true;
            return;
        }

        ConfigureSegmentTransition(oldSegmentValue, newSegmentValue);
        hasInitialWellnessValue = true;
    }

    private void ConfigureSegmentTransition(float oldSegmentValue, float newSegmentValue)
    {
        List<int> changedSegments = new List<int>();
        for (int index = 0; index < segments.Count; index++)
        {
            WellnessSegment segment = segments[index];
            segment.startFill = segment.currentFill;
            segment.targetFill = Mathf.Clamp01(newSegmentValue - index);
            if (!Mathf.Approximately(segment.startFill, segment.targetFill))
                changedSegments.Add(index);
        }

        if (newSegmentValue < oldSegmentValue)
            changedSegments.Reverse();

        for (int order = 0; order < changedSegments.Count; order++)
            segments[changedSegments[order]].transitionDelay = order * segmentStaggerDelay;

        segmentTransitionElapsed = 0f;
    }

    private void HandleWellnessDeltaApplied(float actualDelta)
    {
        if (Mathf.Approximately(actualDelta, 0f))
            return;

        FlashAffectedSegments(actualDelta);

        if (changeIndicator == null || changeIndicatorCanvasGroup == null)
            return;

        if (changeIndicatorCoroutine != null)
            StopCoroutine(changeIndicatorCoroutine);

        changeIndicator.text = $"{(actualDelta > 0f ? "+" : string.Empty)}{actualDelta:0.##}";
        changeIndicator.color = actualDelta > 0f ? new Color(0.25f, 1f, 0.42f, 1f) : new Color(1f, 0.28f, 0.25f, 1f);
        changeIndicatorCanvasGroup.alpha = 1f;
        changeIndicatorCoroutine = StartCoroutine(FadeChangeIndicator());
    }

    private void FlashAffectedSegments(float actualDelta)
    {
        if (segments.Count == 0 || targetMaximum <= 0f)
            return;

        float lowerWellness = Mathf.Min(previousWellnessForDelta, targetWellness);
        float upperWellness = Mathf.Max(previousWellnessForDelta, targetWellness);
        float segmentWidthWellness = targetMaximum / segments.Count;
        float duration = actualDelta > 0f ? gainFlashDuration : lossFlashDuration;
        Color flashColor = actualDelta > 0f ? gainFlashColor : lossFlashColor;

        for (int index = 0; index < segments.Count; index++)
        {
            float segmentStart = index * segmentWidthWellness;
            float segmentEnd = (index + 1) * segmentWidthWellness;
            if (upperWellness <= segmentStart || lowerWellness >= segmentEnd)
                continue;

            segments[index].flashRemaining = duration;
            segments[index].flashDuration = duration;
            segments[index].flashColor = flashColor;
            UpdateSegmentColor(segments[index]);
        }
    }

    private void UpdateSegmentAnimations(float unscaledDeltaTime)
    {
        if (segments.Count == 0)
            return;

        segmentTransitionElapsed += unscaledDeltaTime;
        for (int index = 0; index < segments.Count; index++)
        {
            WellnessSegment segment = segments[index];
            float segmentElapsed = segmentTransitionElapsed - segment.transitionDelay;
            float transitionProgress = fillTransitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(segmentElapsed / fillTransitionDuration);
            float easedProgress = transitionProgress * transitionProgress * (3f - 2f * transitionProgress);
            segment.currentFill = Mathf.Lerp(segment.startFill, segment.targetFill, easedProgress);
            if (segment.flashRemaining > 0f)
                segment.flashRemaining = Mathf.Max(0f, segment.flashRemaining - unscaledDeltaTime);
            UpdateSegmentColor(segment);
        }
    }

    private Color GetWellnessFillColor(float wellness)
    {
        if (wellness < criticalThreshold)
            return criticalFillColor;
        if (wellness < warningThreshold)
            return warningFillColor;
        return healthyFillColor;
    }

    private void UpdateSegmentColor(WellnessSegment segment)
    {
        segment.backgroundImage.color = unlitSegmentColor;
        Color fillColor = currentFillColor;
        if (segment.flashRemaining > 0f && segment.flashDuration > 0f)
        {
            float flashIntensity = Mathf.Clamp01(segment.flashRemaining / segment.flashDuration);
            fillColor = Color.Lerp(fillColor, segment.flashColor, flashIntensity);
        }
        fillColor.a = Mathf.Clamp01(segment.currentFill);
        segment.fillImage.color = fillColor;
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
