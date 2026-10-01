using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the clickable four-layer selector on the game HUD and displays its selected state.
/// </summary>
internal static class LayerSelectionHUD
{
    private const string GameSceneName = "Game";
    private const string HudCanvasName = "HUDCanvas";
    private const string SelectorObjectName = "LayerSelectionButton";
    private const string IconObjectName = "LayerStateIcon";
    private const string LabelObjectName = "LayerSelectionLabel";
    private const string LymphaticIconResourcePath = "UI/Lymphatic";
    private const string CirculatoryIconResourcePath = "UI/Circulatory";
    private const string DigestiveIconResourcePath = "UI/Digestive";
    private const string RespiratoryIconResourcePath = "UI/Respiratory";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const int LayerCount = 4;
    private const int LayerPanelWidth = 286;
    private const int LayerPanelHeight = 76;
    private const int LayerPanelRightInset = 32;
    private const int LayerPanelTopInset = 222;
    private const int IconSize = 56;
    private const int LabelFontSize = 17;

    private static readonly Color PanelColor = new Color(0.025f, 0.05f, 0.067f, 0.9f);
    private static readonly Color LabelColor = new Color(0.92f, 0.96f, 0.96f, 1f);

    private static Image layerIcon;
    private static Text layerLabel;
    private static Button layerButton;
    private static Sprite[] layerSprites;

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

        GameObject canvasObject = GameObject.Find(HudCanvasName);
        Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            Debug.LogWarning("Layer selector could not find HUDCanvas in the Game scene.");
            return;
        }

        Transform existingSelector = canvas.transform.Find(SelectorObjectName);
        if (existingSelector != null)
        {
            BindExistingSelector(existingSelector);
        }
        else
        {
            CreateSelector(canvas.transform);
        }

        SetSelectedLayer(1);
    }

    private static void CreateSelector(Transform parent)
    {
        GameObject selectorObject = new GameObject(SelectorObjectName, typeof(RectTransform), typeof(Image), typeof(Button));
        selectorObject.transform.SetParent(parent, false);

        RectTransform selectorRect = selectorObject.GetComponent<RectTransform>();
        selectorRect.anchorMin = new Vector2(1f, 1f);
        selectorRect.anchorMax = new Vector2(1f, 1f);
        selectorRect.pivot = new Vector2(1f, 1f);
        selectorRect.anchoredPosition = new Vector2(-LayerPanelRightInset, -LayerPanelTopInset);
        selectorRect.sizeDelta = new Vector2(LayerPanelWidth, LayerPanelHeight);
        selectorRect.localScale = Vector3.one;

        Image background = selectorObject.GetComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = true;

        layerButton = selectorObject.GetComponent<Button>();
        layerButton.targetGraphic = background;
        layerButton.onClick.AddListener(OnLayerButtonClicked);

        CreateIcon(selectorObject.transform);
        CreateLabel(selectorObject.transform);
        LoadLayerSprites();
    }

    private static void BindExistingSelector(Transform selectorTransform)
    {
        layerButton = selectorTransform.GetComponent<Button>();
        layerIcon = selectorTransform.Find(IconObjectName)?.GetComponent<Image>();
        layerLabel = selectorTransform.Find(LabelObjectName)?.GetComponent<Text>();

        if (layerButton != null)
        {
            layerButton.onClick.RemoveListener(OnLayerButtonClicked);
            layerButton.onClick.AddListener(OnLayerButtonClicked);
        }

        LoadLayerSprites();
    }

    private static void CreateIcon(Transform parent)
    {
        GameObject iconObject = new GameObject(IconObjectName, typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(parent, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(42f, 0f);
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        iconRect.localScale = Vector3.one;

        layerIcon = iconObject.GetComponent<Image>();
        layerIcon.preserveAspect = true;
        layerIcon.raycastTarget = false;
    }

    private static void CreateLabel(Transform parent)
    {
        GameObject labelObject = new GameObject(LabelObjectName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(82f, -13f);
        labelRect.sizeDelta = new Vector2(188f, 50f);
        labelRect.localScale = Vector3.one;

        layerLabel = labelObject.GetComponent<Text>();
        layerLabel.font = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        layerLabel.fontSize = LabelFontSize;
        layerLabel.fontStyle = FontStyle.Bold;
        layerLabel.color = LabelColor;
        layerLabel.alignment = TextAnchor.MiddleLeft;
        layerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        layerLabel.verticalOverflow = VerticalWrapMode.Overflow;
        layerLabel.raycastTarget = false;
    }

    private static void LoadLayerSprites()
    {
        layerSprites = new[]
        {
            Resources.Load<Sprite>(LymphaticIconResourcePath),
            Resources.Load<Sprite>(CirculatoryIconResourcePath),
            Resources.Load<Sprite>(DigestiveIconResourcePath),
            Resources.Load<Sprite>(RespiratoryIconResourcePath)
        };

        for (int layerIndex = 0; layerIndex < layerSprites.Length; layerIndex++)
        {
            if (layerSprites[layerIndex] == null)
            {
                Debug.LogWarning("Layer selector is missing the " + GetLayerName(layerIndex + 1) + " icon sprite.");
            }
        }
    }

    private static void OnLayerButtonClicked()
    {
        CameraScript cameraController = Object.FindFirstObjectByType<CameraScript>();
        if (cameraController == null)
        {
            Debug.LogWarning("Layer selector could not find CameraScript in the Game scene.");
            return;
        }

        cameraController.SelectNextLayer();
    }

    internal static void SetSelectedLayer(int layerNumber)
    {
        if (layerNumber < 1 || layerNumber > LayerCount)
        {
            return;
        }

        if (layerIcon != null && layerSprites != null && layerSprites.Length >= layerNumber)
        {
            layerIcon.sprite = layerSprites[layerNumber - 1];
        }

        if (layerLabel != null)
        {
            layerLabel.text = "Layer " + layerNumber + ": " + GetLayerName(layerNumber);
        }
    }

    private static string GetLayerName(int layerNumber)
    {
        switch (layerNumber)
        {
            case 1:
                return "Lymphatic";
            case 2:
                return "Circulatory";
            case 3:
                return "Digestive";
            case 4:
                return "Respiratory";
            default:
                return string.Empty;
        }
    }
}
