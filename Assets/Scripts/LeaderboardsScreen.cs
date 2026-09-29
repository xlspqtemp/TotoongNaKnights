using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the static placeholder leaderboards panel over the existing main menu.
/// </summary>
public static class LeaderboardsScreen
{
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const int HeaderFontSize = 42;
    private const int EntryNameFontSize = 30;
    private const int MessageFontSize = 24;
    private const int DetailsFontSize = 18;
    private const int ButtonFontSize = 20;

    private static readonly Color BackgroundColor = new Color(0.008f, 0.018f, 0.028f, 1f);
    private static readonly Color TextureTint = new Color(0.18f, 0.66f, 0.72f, 0.42f);
    private static readonly Color PanelColor = new Color(0.025f, 0.055f, 0.07f, 0.97f);
    private static readonly Color RowColor = new Color(0.035f, 0.078f, 0.092f, 0.98f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.77f, 0.79f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 0.97f, 1f);

    private static GameObject overlayRoot;
    private static Sprite roundedSprite;

    /// <summary>
    /// Displays the placeholder leaderboard over the supplied menu canvas.
    /// </summary>
    public static void Show(Transform parent)
    {
        if (overlayRoot != null)
        {
            overlayRoot.transform.SetAsLastSibling();
            return;
        }

        roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        overlayRoot = new GameObject("LeaderboardsOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(parent, false);
        overlayRoot.transform.SetAsLastSibling();
        SetRect(overlayRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        CreateImage(overlayRoot.transform, "DarkBackground", BackgroundColor,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        CreateBackgroundTexture(overlayRoot.transform);

        GameObject header = CreateRoundedPanel(overlayRoot.transform, "LeaderboardHeader",
            new Vector2(0f, 420f), new Vector2(1600f, 118f), AccentColor, PanelColor);
        CreateText(header.transform, "HeaderTitle", "LEADERBOARDS", HeaderFontSize,
            FontStyle.Bold, TextColor, new Vector2(-120f, 0f), new Vector2(1240f, 72f));
        CreateButton(header.transform, "CloseButton", "CLOSE", new Vector2(670f, 0f),
            new Vector2(168f, 66f), Close);

        GameObject entry = CreateRoundedPanel(overlayRoot.transform, "PlaceholderEntry",
            new Vector2(0f, 115f), new Vector2(1540f, 340f), AccentColor, RowColor);
        CreateText(entry.transform, "PlayerName", "No name", EntryNameFontSize,
            FontStyle.Bold, TextColor, new Vector2(-620f, 96f), new Vector2(260f, 52f));
        CreateText(entry.transform, "PlaceholderMessage", "Keep playing the game soldier!",
            MessageFontSize, FontStyle.Normal, MutedTextColor, new Vector2(0f, 28f),
            new Vector2(1280f, 48f));

        GameObject progressTrack = CreateImage(entry.transform, "ProgressTrack",
            new Color(0.015f, 0.032f, 0.042f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -42f),
            new Vector2(1160f, 26f), false);
        CreateImage(progressTrack.transform, "ProgressFill", AccentColor,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(2f, 0f), new Vector2(360f, 16f), false);
        CreateText(entry.transform, "ProgressDetails", "0/14 days - 0", DetailsFontSize,
            FontStyle.Normal, MutedTextColor, new Vector2(0f, -96f), new Vector2(1160f, 36f));
    }

    private static void CreateBackgroundTexture(Transform parent)
    {
        GameObject textureObject = new GameObject("ExistingGameBackgroundTexture",
            typeof(RectTransform), typeof(RawImage));
        textureObject.transform.SetParent(parent, false);
        SetRect(textureObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RawImage rawImage = textureObject.GetComponent<RawImage>();
        rawImage.texture = Resources.Load<Texture2D>("Game background");
        rawImage.color = TextureTint;
        rawImage.raycastTarget = false;
    }

    private static GameObject CreateRoundedPanel(Transform parent, string objectName,
        Vector2 position, Vector2 size, Color borderColor, Color fillColor)
    {
        GameObject border = CreateImage(parent, objectName + "AccentBorder", borderColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            position, size, false);
        GameObject fill = CreateImage(border.transform, objectName, fillColor,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(-8f, -8f), false);
        return fill;
    }

    private static void CreateButton(Transform parent, string objectName, string label,
        Vector2 position, Vector2 size, UnityAction onClick)
    {
        GameObject buttonObject = CreateImage(parent, objectName, AccentColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            position, size, true);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = AccentColor,
            highlightedColor = new Color(0.38f, 0.94f, 0.88f, 1f),
            pressedColor = new Color(0.08f, 0.55f, 0.52f, 1f),
            selectedColor = AccentColor,
            disabledColor = MutedTextColor,
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        button.onClick.AddListener(onClick);
        CreateText(buttonObject.transform, "Label", label, ButtonFontSize,
            FontStyle.Bold, BackgroundColor, Vector2.zero, Vector2.one, null, true);
    }

    private static GameObject CreateImage(Transform parent, string objectName, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size,
        bool raycastTarget)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetRect(imageObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, position, size);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        if (roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }
        return imageObject;
    }

    private static void CreateText(Transform parent, string objectName, string content,
        int fontSize, FontStyle fontStyle, Color color, Vector2 position, Vector2 size,
        Vector2? anchor = null, bool stretch = false)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        SetRect(textRect, stretch ? Vector2.zero : resolvedAnchor,
            stretch ? Vector2.one : resolvedAnchor, stretch ? new Vector2(0.5f, 0.5f) : resolvedAnchor,
            position, size);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private static void Close()
    {
        if (overlayRoot == null)
        {
            return;
        }

        Object.Destroy(overlayRoot);
        overlayRoot = null;
    }
}
