using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game manual overlay using the existing manual textures.
/// </summary>
public static class ManualScreen
{
    private const string ResourceFolder = "Manual/";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const string ManualIconResource = "Manual/Manual icon";
    private const string MapArtworkResource = "Manual/MapIntro";
    private const string GameplayTitleResource = "Manual/Gameplay";
    private const string GameplayDetailsResource = "Manual/GameInt";
    private const float MenuIconSize = 112f;
    private const float MenuIconInset = 64f;
    private const float HeaderFontSize = 38f;
    private const float ButtonFontSize = 20f;
    private const float ListLabelFontSize = 19f;
    private const float VirusLabelFontSize = 16f;
    private const float BackButtonWidth = 154f;
    private const float BackButtonHeight = 58f;
    private const float ListPanelWidth = 486f;
    private const float ListPanelHeight = 850f;
    private const float DetailPanelWidth = 1128f;
    private const float DetailPanelHeight = 850f;
    private const float PanelCenterY = -8f;
    private const float CharacterRowHeight = 156f;
    private const float VirusRowHeight = 70f;
    private const float VirusThumbnailSize = 50f;
    private const float CharacterThumbnailSize = 100f;
    private const float GameplayImageWidth = 1540f;
    private const float GameplayImageHeight = 850f;

    private static readonly Color BackgroundColor = new Color(0.008f, 0.019f, 0.032f, 1f);
    private static readonly Color PanelColor = new Color(0.018f, 0.045f, 0.062f, 0.98f);
    private static readonly Color RowColor = new Color(0.035f, 0.086f, 0.105f, 0.98f);
    private static readonly Color SelectedRowColor = new Color(0.055f, 0.28f, 0.32f, 1f);
    private static readonly Color AccentColor = new Color(0.12f, 0.82f, 0.84f, 1f);
    private static readonly Color AccentDimColor = new Color(0.07f, 0.34f, 0.42f, 1f);
    private static readonly Color TextColor = new Color(0.91f, 0.97f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.61f, 0.76f, 0.8f, 1f);
    private static readonly Color HitboxNormalColor = new Color(0.08f, 0.72f, 0.78f, 0.015f);
    private static readonly Color HitboxHoverColor = new Color(0.08f, 0.72f, 0.78f, 0.2f);
    private static readonly Color HitboxPressedColor = new Color(0.08f, 0.72f, 0.78f, 0.32f);

    private static readonly ManualItem[] CharacterItems =
    {
        new ManualItem("B-cell", "B-cell"),
        new ManualItem("RBC", "RBC"),
        new ManualItem("WBC", "WBC"),
        new ManualItem("KTC", "KTC")
    };

    private static readonly ManualItem[] VirusItems =
    {
        new ManualItem("ChicPox", "ChicPox"),
        new ManualItem("Common Cold", "Common Cold"),
        new ManualItem("Dengue", "Dengue"),
        new ManualItem("Digestive", "Digestive"),
        new ManualItem("E.coli", "E.coli"),
        new ManualItem("Flu", "Flu"),
        new ManualItem("pneumonia", "pneumonia"),
        new ManualItem("Respiratory", "Respiratory"),
        new ManualItem("salmonella", "salmonella"),
        new ManualItem("Sore throat", "Sore throat"),
        new ManualItem("Viruses and Bacteria", "Viruses and Bacteria")
    };

    private static GameObject overlayRoot;
    private static GameObject pageRoot;
    private static RawImage detailImage;
    private static Image[] itemRowImages;
    private static int selectedItemIndex;
    private static int gameplayPageIndex;

    /// <summary>
    /// Adds the existing Manual icon as a clickable entry on the supplied menu canvas.
    /// </summary>
    public static void AddMenuEntry(Transform canvas, UnityAction openManual)
    {
        if (canvas == null || canvas.Find("ManualEntryButton") != null)
        {
            return;
        }

        Texture2D iconTexture = Resources.Load<Texture2D>(ManualIconResource);
        if (iconTexture == null)
        {
            Debug.LogWarning("Manual icon texture was not found at Resources/" + ManualIconResource + ".png");
            return;
        }

        GameObject iconObject = new GameObject("ManualEntryButton", typeof(RectTransform), typeof(RawImage), typeof(Button));
        iconObject.transform.SetParent(canvas, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetRect(iconRect, Vector2.one, Vector2.one, Vector2.one,
            new Vector2(-MenuIconInset, -MenuIconInset), new Vector2(MenuIconSize, MenuIconSize));

        RawImage iconImage = iconObject.GetComponent<RawImage>();
        iconImage.texture = iconTexture;
        iconImage.raycastTarget = true;

        Button button = iconObject.GetComponent<Button>();
        ConfigureButton(button, iconImage, openManual);
    }

    /// <summary>
    /// Shows the manual over the supplied menu canvas without changing scenes or gameplay state.
    /// </summary>
    public static void Show(Transform canvas)
    {
        if (canvas == null)
        {
            return;
        }

        if (overlayRoot != null)
        {
            overlayRoot.transform.SetAsLastSibling();
            return;
        }

        overlayRoot = new GameObject("ManualOverlay", typeof(RectTransform), typeof(Image));
        overlayRoot.transform.SetParent(canvas, false);
        overlayRoot.transform.SetAsLastSibling();
        SetRect(overlayRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image background = overlayRoot.GetComponent<Image>();
        background.color = BackgroundColor;
        background.raycastTarget = true;

        gameplayPageIndex = 0;
        BuildManualHome();
    }

    private static void BuildManualHome()
    {
        PreparePage("MANUAL");
        CreateBackButton(pageRoot.transform, Close);
        CreateText(pageRoot.transform, "ManualTitle", "MANUAL", HeaderFontSize, FontStyle.Bold,
            AccentColor, TextAnchor.MiddleCenter, new Vector2(0f, 398f), new Vector2(920f, 64f));
        GameObject categoryPanel = CreatePanel(pageRoot.transform, "ManualCategoryPanel",
            new Vector2(0f, -12f), new Vector2(860f, 730f));

        CreateButton(categoryPanel.transform, "CharacterCategoryButton", "CHARACTER",
            new Vector2(0f, 238f), new Vector2(610f, 104f), BuildCharacterPage);
        CreateButton(categoryPanel.transform, "MapCategoryButton", "MAP",
            new Vector2(0f, 78f), new Vector2(610f, 104f), BuildMapPage);
        CreateButton(categoryPanel.transform, "VirusCategoryButton", "VIRUSES & BACTERIA",
            new Vector2(0f, -82f), new Vector2(610f, 104f), BuildVirusPage);
        CreateButton(categoryPanel.transform, "GameplayCategoryButton", "GAMEPLAY",
            new Vector2(0f, -242f), new Vector2(610f, 104f), BuildGameplayPage);
    }

    private static void BuildCharacterPage()
    {
        BuildSelectionPage("CHARACTER DATA", CharacterItems, CharacterRowHeight, CharacterThumbnailSize,
            ListLabelFontSize);
    }

    private static void BuildVirusPage()
    {
        BuildSelectionPage("VIRUSES & BACTERIA", VirusItems, VirusRowHeight, VirusThumbnailSize,
            VirusLabelFontSize);
    }

    private static void BuildSelectionPage(string heading, ManualItem[] items, float rowHeight,
        float thumbnailSize, float labelFontSize)
    {
        PreparePage(heading);
        CreateBackButton(pageRoot.transform, BuildManualHome);
        CreateText(pageRoot.transform, "ManualSectionTitle", heading, HeaderFontSize, FontStyle.Bold,
            AccentColor, TextAnchor.MiddleCenter, new Vector2(0f, 472f), new Vector2(920f, 58f));

        GameObject listPanel = CreatePanel(pageRoot.transform, "ManualSelectionList",
            new Vector2(-650f, PanelCenterY), new Vector2(ListPanelWidth, ListPanelHeight));
        GameObject detailPanel = CreatePanel(pageRoot.transform, "ManualDetailPanel",
            new Vector2(300f, PanelCenterY), new Vector2(DetailPanelWidth, DetailPanelHeight));

        detailImage = CreateAspectImage(detailPanel.transform, "ManualDetailImage",
            Resources.Load<Texture2D>(ResourceFolder + items[0].ResourceName), Vector2.zero, Vector2.one,
            new Vector2(-18f, -18f)).GetComponent<RawImage>();

        selectedItemIndex = 0;
        itemRowImages = new Image[items.Length];
        float listTop = ListPanelHeight * 0.5f - 32f;
        for (int i = 0; i < items.Length; i++)
        {
            int itemIndex = i;
            float rowY = listTop - rowHeight * (i + 0.5f);
            itemRowImages[i] = CreateSelectionRow(listPanel.transform, items[i], itemIndex,
                rowY, rowHeight, thumbnailSize, labelFontSize,
                i == selectedItemIndex, () => SelectItem(itemIndex, items));
        }
    }

    private static void BuildMapPage()
    {
        PreparePage("MAP");
        CreateBackButton(pageRoot.transform, BuildManualHome);
        CreateText(pageRoot.transform, "MapSectionTitle", "MAP", HeaderFontSize, FontStyle.Bold,
            AccentColor, TextAnchor.MiddleCenter, new Vector2(0f, 472f), new Vector2(720f, 58f));
        GameObject mapPanel = CreatePanel(pageRoot.transform, "ManualMapPanel",
            new Vector2(0f, -16f), new Vector2(1660f, 860f));
        CreateAspectImage(mapPanel.transform, "MapIntroImage",
            Resources.Load<Texture2D>(MapArtworkResource), Vector2.zero, Vector2.one,
            new Vector2(-18f, -18f));
    }

    private static void BuildGameplayPage()
    {
        PreparePage("GAMEPLAY");
        CreateBackButton(pageRoot.transform, BuildManualHome);

        string resourceName = gameplayPageIndex == 0 ? GameplayTitleResource : GameplayDetailsResource;
        string pageLabel = gameplayPageIndex == 0 ? "1 / 2 - OVERVIEW" : "2 / 2 - GAMEPLAY GUIDE";
        GameObject imagePanel = CreatePanel(pageRoot.transform, "GameplayImagePanel",
            new Vector2(0f, 18f), new Vector2(GameplayImageWidth, GameplayImageHeight));
        CreateAspectImage(imagePanel.transform, "GameplayPageImage",
            Resources.Load<Texture2D>(resourceName), Vector2.zero, Vector2.one,
            new Vector2(-18f, -18f));
        CreateText(pageRoot.transform, "GameplayPageIndicator", pageLabel, 18f, FontStyle.Bold,
            MutedTextColor, TextAnchor.MiddleCenter, new Vector2(0f, -448f), new Vector2(460f, 36f));

        if (gameplayPageIndex == 0)
        {
            CreateButton(pageRoot.transform, "GameplayNextButton", "NEXT  >",
                new Vector2(788f, -458f), new Vector2(190f, 58f), () =>
                {
                    gameplayPageIndex = 1;
                    BuildGameplayPage();
                });
        }
        else
        {
            CreateButton(pageRoot.transform, "GameplayPreviousButton", "<  PREVIOUS",
                new Vector2(788f, -458f), new Vector2(210f, 58f), () =>
                {
                    gameplayPageIndex = 0;
                    BuildGameplayPage();
                });
        }
    }

    private static void PreparePage(string pageName)
    {
        if (pageRoot != null)
        {
            UnityEngine.Object.Destroy(pageRoot);
        }

        pageRoot = new GameObject(pageName + "Page", typeof(RectTransform));
        pageRoot.transform.SetParent(overlayRoot.transform, false);
        pageRoot.transform.SetAsLastSibling();
        SetRect(pageRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        detailImage = null;
        itemRowImages = null;
    }

    private static GameObject CreateAspectImage(Transform parent, string objectName, Texture2D texture,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(RawImage),
            typeof(AspectRatioFitter));
        imageObject.transform.SetParent(parent, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        SetRect(imageRect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, sizeDelta);

        RawImage rawImage = imageObject.GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        AspectRatioFitter fitter = imageObject.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = texture == null ? 1f : (float)texture.width / texture.height;
        return imageObject;
    }

    private static GameObject CreatePanel(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        GameObject borderObject = CreateImage(parent, objectName + "AccentBorder", AccentDimColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, false);
        GameObject panelObject = CreateImage(borderObject.transform, objectName, PanelColor,
            Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8f, -8f), false);
        return panelObject;
    }

    private static Image CreateSelectionRow(Transform parent, ManualItem item, int itemIndex,
        float anchoredY, float rowHeight, float thumbnailSize, float labelFontSize,
        bool isSelected, UnityAction onClick)
    {
        GameObject rowObject = new GameObject("ManualItem_" + itemIndex,
            typeof(RectTransform), typeof(Image), typeof(Button));
        rowObject.transform.SetParent(parent, false);
        SetRect(rowObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, anchoredY), new Vector2(ListPanelWidth - 28f, rowHeight - 8f));

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = isSelected ? SelectedRowColor : RowColor;
        rowImage.raycastTarget = true;
        Button button = rowObject.GetComponent<Button>();
        ConfigureButton(button, rowImage, onClick);
        ColorBlock colors = button.colors;
        colors.normalColor = rowImage.color;
        colors.highlightedColor = new Color(0.09f, 0.38f, 0.43f, 1f);
        colors.pressedColor = SelectedRowColor;
        colors.selectedColor = SelectedRowColor;
        button.colors = colors;

        Texture2D thumbnailTexture = Resources.Load<Texture2D>(ResourceFolder + item.ResourceName);
        GameObject thumbnailObject = new GameObject("Thumbnail", typeof(RectTransform), typeof(RawImage));
        thumbnailObject.transform.SetParent(rowObject.transform, false);
        RectTransform thumbnailRect = thumbnailObject.GetComponent<RectTransform>();
        SetRect(thumbnailRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(thumbnailSize, thumbnailSize));
        RawImage thumbnail = thumbnailObject.GetComponent<RawImage>();
        thumbnail.texture = thumbnailTexture;
        thumbnail.raycastTarget = false;

        CreateText(rowObject.transform, "ItemLabel", item.DisplayName, labelFontSize, FontStyle.Bold,
            TextColor, TextAnchor.MiddleLeft, new Vector2(thumbnailSize + 26f, 0f),
            new Vector2(ListPanelWidth - thumbnailSize - 68f, rowHeight - 10f),
            new Vector2(0f, 0.5f));
        return rowImage;
    }

    private static void SelectItem(int index, ManualItem[] items)
    {
        selectedItemIndex = index;
        if (itemRowImages != null)
        {
            for (int i = 0; i < itemRowImages.Length; i++)
            {
                itemRowImages[i].color = i == selectedItemIndex ? SelectedRowColor : RowColor;
                Button rowButton = itemRowImages[i].GetComponent<Button>();
                ColorBlock colors = rowButton.colors;
                colors.normalColor = itemRowImages[i].color;
                rowButton.colors = colors;
            }
        }

        if (detailImage != null)
        {
            detailImage.texture = Resources.Load<Texture2D>(ResourceFolder + items[index].ResourceName);
            AspectRatioFitter fitter = detailImage.GetComponent<AspectRatioFitter>();
            if (fitter != null && detailImage.texture != null)
            {
                fitter.aspectRatio = (float)detailImage.texture.width / detailImage.texture.height;
            }
        }
    }

    private static void CreateBackButton(Transform parent, UnityAction onClick)
    {
        CreateButton(parent, "ManualBackButton", "<  BACK",
            new Vector2(-810f, 474f), new Vector2(BackButtonWidth, BackButtonHeight), onClick);
    }

    private static void CreateButton(Transform parent, string objectName, string label,
        Vector2 position, Vector2 size, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = AccentDimColor;
        buttonImage.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        ConfigureButton(button, buttonImage, onClick);
        CreateText(buttonObject.transform, "Label", label, ButtonFontSize, FontStyle.Bold,
            TextColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, null, true);
    }

    private static GameObject CreateImage(Transform parent, string objectName, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, bool raycastTarget)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetRect(imageObject.GetComponent<RectTransform>(), anchorMin, anchorMax,
            new Vector2(0.5f, 0.5f), position, size);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return imageObject;
    }

    private static void CreateText(Transform parent, string objectName, string content, float fontSize,
        FontStyle fontStyle, Color color, TextAnchor alignment, Vector2 anchoredPosition, Vector2 sizeDelta,
        Vector2? anchor = null, bool stretch = false)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        SetRect(textRect, stretch ? Vector2.zero : resolvedAnchor,
            stretch ? Vector2.one : resolvedAnchor,
            stretch ? new Vector2(0.5f, 0.5f) : resolvedAnchor, anchoredPosition, sizeDelta);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        text.text = content;
        text.fontSize = Mathf.RoundToInt(fontSize);
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
    }

    private static void ConfigureButton(Button button, Graphic targetGraphic, UnityAction onClick)
    {
        button.targetGraphic = targetGraphic;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = targetGraphic.color,
            highlightedColor = AccentColor,
            pressedColor = AccentDimColor,
            selectedColor = AccentColor,
            disabledColor = MutedTextColor,
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        button.onClick.AddListener(onClick);
    }

    private static void Close()
    {
        if (overlayRoot == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(overlayRoot);
        overlayRoot = null;
        pageRoot = null;
        detailImage = null;
        itemRowImages = null;
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

    private sealed class ManualItem
    {
        public ManualItem(string displayName, string resourceName)
        {
            DisplayName = displayName;
            ResourceName = resourceName;
        }

        public string DisplayName { get; }
        public string ResourceName { get; }
    }
}
