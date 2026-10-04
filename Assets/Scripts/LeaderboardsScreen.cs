using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds and displays the online wellness leaderboard over the main menu.
/// </summary>
public static class LeaderboardsScreen
{
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const int HeaderFontSize = 42;
    private const int ColumnHeaderFontSize = 19;
    private const int EntryFontSize = 18;
    private const int StatusFontSize = 24;
    private const int ButtonFontSize = 20;
    private const int EntryCount = 20;
    private const float RowStartY = 260f;
    private const float RowSpacing = 35f;
    private const float RowHeight = 32f;

    private static readonly Color BackgroundColor = new Color(0.008f, 0.018f, 0.028f, 1f);
    private static readonly Color TextureTint = new Color(0.18f, 0.66f, 0.72f, 0.42f);
    private static readonly Color PanelColor = new Color(0.025f, 0.055f, 0.07f, 0.97f);
    private static readonly Color RowColor = new Color(0.035f, 0.078f, 0.092f, 0.98f);
    private static readonly Color AccentColor = new Color(0.18f, 0.83f, 0.78f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.77f, 0.79f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.97f, 0.97f, 1f);

    private static GameObject overlayRoot;
    private static Sprite roundedSprite;
    private static LeaderboardManager leaderboardManager;

    /// <summary>
    /// Displays the online leaderboard over the supplied menu canvas.
    /// </summary>
    public static void Show(Transform parent)
    {
        if (overlayRoot != null)
        {
            overlayRoot.transform.SetAsLastSibling();
            if (leaderboardManager != null)
            {
                leaderboardManager.LoadScores();
            }
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

        CreateColumnHeader(overlayRoot.transform, "RankHeader", "RANK", new Vector2(-660f, 302f), new Vector2(110f, 30f));
        CreateColumnHeader(overlayRoot.transform, "PlayerHeader", "PLAYER", new Vector2(-410f, 302f), new Vector2(460f, 30f));
        CreateColumnHeader(overlayRoot.transform, "WellnessHeader", "WELLNESS SCORE", new Vector2(70f, 302f), new Vector2(220f, 30f));
        CreateColumnHeader(overlayRoot.transform, "DifficultyHeader", "DIFFICULTY", new Vector2(490f, 302f), new Vector2(640f, 30f));

        Text[] ranks = new Text[EntryCount];
        Text[] playerNames = new Text[EntryCount];
        Text[] scores = new Text[EntryCount];
        Text[] difficulties = new Text[EntryCount];
        Image[] backgrounds = new Image[EntryCount];
        CreateRows(overlayRoot.transform, ranks, playerNames, scores, difficulties, backgrounds);

        Text statusText = CreateText(overlayRoot.transform, "LeaderboardStatus", "Loading...", StatusFontSize,
            FontStyle.Normal, MutedTextColor, new Vector2(0f, -45f), new Vector2(1100f, 48f));
        Button retryButton = CreateButton(overlayRoot.transform, "RetryButton", "RETRY",
            new Vector2(0f, -108f), new Vector2(190f, 58f), null);
        retryButton.onClick.RemoveAllListeners();
        retryButton.gameObject.SetActive(false);

        leaderboardManager = overlayRoot.AddComponent<LeaderboardManager>();
        leaderboardManager.Initialize(statusText, retryButton, ranks, playerNames, scores, difficulties, backgrounds);
    }

    private static void CreateRows(Transform parent, Text[] ranks, Text[] playerNames, Text[] scores,
        Text[] difficulties, Image[] backgrounds)
    {
        for (int i = 0; i < EntryCount; i++)
        {
            float y = RowStartY - RowSpacing * i;
            GameObject row = CreateImage(parent, "LeaderboardRow" + i, RowColor,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(1540f, RowHeight), false);
            backgrounds[i] = row.GetComponent<Image>();

            ranks[i] = CreateText(row.transform, "Rank", string.Empty, EntryFontSize,
                FontStyle.Normal, TextColor, new Vector2(-660f, 0f), new Vector2(110f, RowHeight));
            playerNames[i] = CreateText(row.transform, "PlayerName", string.Empty, EntryFontSize,
                FontStyle.Normal, TextColor, new Vector2(-410f, 0f), new Vector2(460f, RowHeight));
            scores[i] = CreateText(row.transform, "WellnessScore", string.Empty, EntryFontSize,
                FontStyle.Bold, TextColor, new Vector2(70f, 0f), new Vector2(220f, RowHeight));
            difficulties[i] = CreateText(row.transform, "Difficulty", string.Empty, EntryFontSize,
                FontStyle.Normal, TextColor, new Vector2(490f, 0f), new Vector2(640f, RowHeight));
        }
    }

    private static void CreateColumnHeader(Transform parent, string objectName, string label,
        Vector2 position, Vector2 size)
    {
        CreateText(parent, objectName, label, ColumnHeaderFontSize, FontStyle.Bold,
            AccentColor, position, size);
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

    private static Button CreateButton(Transform parent, string objectName, string label,
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
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }
        CreateText(buttonObject.transform, "Label", label, ButtonFontSize,
            FontStyle.Bold, BackgroundColor, Vector2.zero, Vector2.one, null, true);
        return button;
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

    private static Text CreateText(Transform parent, string objectName, string content,
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
        return text;
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
        leaderboardManager = null;
    }
}
