using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Displays the selected difficulty levels in the lower-right corner of the game HUD.
/// </summary>
public static class DifficultyHUD
{
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const string GameSceneName = "Game";
    private const string HudCanvasName = "HUDCanvas";
    private const int PanelWidth = 286;
    private const int PanelHeight = 128;
    private const int PanelRightInset = 32;
    private const int PanelTopInset = 82;
    private const int HeadingFontSize = 16;
    private const int LevelFontSize = 15;

    private static readonly Color PanelColor = new Color(0.025f, 0.05f, 0.067f, 0.9f);
    private static readonly Color HeadingColor = new Color(0.3f, 0.88f, 0.82f, 1f);
    private static readonly Color LevelColor = new Color(0.92f, 0.96f, 0.96f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameSceneName)
        {
            return;
        }

        GameObject hudCanvasObject = GameObject.Find(HudCanvasName);
        Canvas hudCanvas = hudCanvasObject != null ? hudCanvasObject.GetComponent<Canvas>() : null;
        if (hudCanvas == null || hudCanvas.transform.Find("DifficultyIndicator") != null)
        {
            return;
        }

        CreateIndicator(hudCanvas.transform);
    }

    private static void CreateIndicator(Transform parent)
    {
        GameObject panel = new GameObject("DifficultyIndicator", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-PanelRightInset, -PanelTopInset);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.localScale = Vector3.one;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        CreateText(panel.transform, "DifficultyIndicatorTitle", "DIFFICULTY", HeadingFontSize,
            HeadingColor, new Vector2(16f, -12f), new Vector2(PanelWidth - 32f, 24f),
            new Vector2(0f, 1f), new Vector2(0f, 1f));
        CreateText(panel.transform, "EnemyDifficultyValue", "Infection Severity  " + DifficultySettings.EnemyLevel,
            LevelFontSize, LevelColor, new Vector2(16f, -42f), new Vector2(PanelWidth - 32f, 20f),
            new Vector2(0f, 1f), new Vector2(0f, 1f));
        CreateText(panel.transform, "DefenseDifficultyValue", "Immune System  " + DifficultySettings.DefenseLevel,
            LevelFontSize, LevelColor, new Vector2(16f, -67f), new Vector2(PanelWidth - 32f, 20f),
            new Vector2(0f, 1f), new Vector2(0f, 1f));
        CreateText(panel.transform, "RandomEventsDifficultyValue", "Lifestyle  " + DifficultySettings.RandomEventsLevel,
            LevelFontSize, LevelColor, new Vector2(16f, -92f), new Vector2(PanelWidth - 32f, 20f),
            new Vector2(0f, 1f), new Vector2(0f, 1f));
    }

    private static void CreateText(Transform parent, string objectName, string content, int fontSize,
        Color color, Vector2 position, Vector2 size, Vector2 anchor, Vector2 pivot)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = anchor;
        textRect.anchorMax = anchor;
        textRect.pivot = pivot;
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;
        textRect.localScale = Vector3.one;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }
}
