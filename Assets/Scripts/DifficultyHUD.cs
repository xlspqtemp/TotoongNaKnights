using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Displays all three selected difficulty levels in a compact row beside Pause.</summary>
public static class DifficultyHUD
{
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const string GameSceneName = "Game";
    private const string HudCanvasName = "HUDCanvas";
    private const string IndicatorObjectName = "DifficultyIndicator";
    private static readonly bool ShowDifficultyIndicator = false;
    private const float PanelHeight = 46f;
    private const float MaximumPanelWidth = 520f;
    private const float PauseGap = 12f;
    private const float TextRightPadding = 22f;

    private static readonly Color PanelColor = new Color(0.025f, 0.05f, 0.067f, 0.9f);
    private static readonly Color TextColor = new Color(0.92f, 0.96f, 0.96f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameSceneName)
            return;

        GameObject hudCanvasObject = GameObject.Find(HudCanvasName);
        Canvas hudCanvas = hudCanvasObject != null ? hudCanvasObject.GetComponent<Canvas>() : null;
        if (hudCanvas == null)
            return;

        Transform existingIndicator = hudCanvas.transform.Find(IndicatorObjectName);
        if (!ShowDifficultyIndicator)
        {
            if (existingIndicator != null)
                existingIndicator.gameObject.SetActive(false);
            return;
        }

        RectTransform indicatorRect = existingIndicator as RectTransform;
        if (indicatorRect == null)
            indicatorRect = CreateIndicator(hudCanvas.transform);
        else
            UpdateIndicatorText(indicatorRect);

        DifficultyHudResponsiveLayout responsiveLayout = hudCanvas.GetComponent<DifficultyHudResponsiveLayout>();
        if (responsiveLayout == null)
            responsiveLayout = hudCanvas.gameObject.AddComponent<DifficultyHudResponsiveLayout>();

        RectTransform pauseButtonRect = hudCanvas.transform.Find("PauseButton") as RectTransform;
        responsiveLayout.Configure(hudCanvas.transform as RectTransform, indicatorRect, pauseButtonRect);
    }

    private static RectTransform CreateIndicator(Transform parent)
    {
        GameObject panel = new GameObject(IndicatorObjectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.localScale = Vector3.one;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        GameObject valueObject = new GameObject("DifficultyValues", typeof(RectTransform), typeof(Text));
        valueObject.transform.SetParent(panel.transform, false);
        RectTransform valueRect = valueObject.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(12f, 4f);
        valueRect.offsetMax = new Vector2(-TextRightPadding, -4f);

        Text valueText = valueObject.GetComponent<Text>();
        valueText.font = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        valueText.fontSize = 14;
        valueText.resizeTextForBestFit = true;
        valueText.resizeTextMinSize = 11;
        valueText.resizeTextMaxSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = TextColor;
        valueText.alignment = TextAnchor.MiddleLeft;
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow = VerticalWrapMode.Overflow;
        valueText.raycastTarget = false;
        UpdateIndicatorText(panelRect);
        return panelRect;
    }

    private static void UpdateIndicatorText(RectTransform panelRect)
    {
        if (panelRect == null)
            return;

        Transform valueTransform = panelRect.Find("DifficultyValues");
        Text valueText = valueTransform != null ? valueTransform.GetComponent<Text>() : null;
        if (valueText != null)
        {
            valueText.text = "Infection Severity " + DifficultySettings.EnemyLevel +
                             "  |  Immune System " + DifficultySettings.DefenseLevel +
                             "  |  Lifestyle " + DifficultySettings.RandomEventsLevel;
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 11;
            valueText.resizeTextMaxSize = 14;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}

/// <summary>Tracks the Pause control and sizes the difficulty strip to the available HUD width.</summary>
internal sealed class DifficultyHudResponsiveLayout : MonoBehaviour
{
    private const float PanelHeight = 46f;
    private const float MaximumPanelWidth = 520f;
    private const float PauseGap = 12f;

    private RectTransform canvasRect;
    private RectTransform panelRect;
    private RectTransform pauseButtonRect;
    private float previousWidth = -1f;
    private float previousHeight = -1f;

    internal void Configure(RectTransform canvas, RectTransform panel, RectTransform pauseButton)
    {
        canvasRect = canvas;
        panelRect = panel;
        pauseButtonRect = pauseButton;
        ApplyLayout();
    }

    private void LateUpdate()
    {
        if (canvasRect == null || panelRect == null)
            return;

        Rect canvasBounds = canvasRect.rect;
        if (Mathf.Abs(canvasBounds.width - previousWidth) > 1f || Mathf.Abs(canvasBounds.height - previousHeight) > 1f)
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (canvasRect == null || panelRect == null)
            return;

        Rect bounds = canvasRect.rect;
        previousWidth = bounds.width;
        previousHeight = bounds.height;

        float margin = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) * 0.025f, 14f, 32f);
        float pauseWidth = pauseButtonRect != null ? pauseButtonRect.rect.width : 82f;
        float rightInset = margin;
        float topInset = margin;

        if (pauseButtonRect != null)
        {
            rightInset = Mathf.Max(margin, -pauseButtonRect.anchoredPosition.x + pauseWidth + PauseGap);
            topInset = Mathf.Max(margin, -pauseButtonRect.anchoredPosition.y);
        }

        float availableWidth = Mathf.Max(0f, bounds.width - rightInset - margin);
        float panelWidth = Mathf.Min(MaximumPanelWidth, availableWidth);

        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-rightInset, -topInset);
        panelRect.sizeDelta = new Vector2(panelWidth, PanelHeight);
        panelRect.localScale = Vector3.one;
    }
}
