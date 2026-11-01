using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Tracks virus infiltrations for one lung and dispatches a timed immune-cell pair.</summary>
public sealed class LungInfectionResponse : MonoBehaviour
{
    private const float SecondsPerInfiltration = 10f;
    private const float OxygenBoostDurationSeconds = 15f;
    internal const string OxygenBoostIconObjectName = "Oxygen Boost Perk Timer";
    internal const string OxygenBoostIconResourcePath = "UI/Oxygen Boost perk";
    internal const float OxygenBoostIconSize = 46f;
    internal const float OxygenBoostIconHorizontalOffset = -198f;
    internal const float OxygenBoostIconVerticalOffset = -34f;
    internal const string OxygenBoostOverlayObjectName = "Respiratory Oxygen Boost Video Overlay";
    internal const string OxygenBoostOverlayVideoResourcePath = "Oxygen boost overlay";
    internal const string OxygenBoostOverlayMaterialResourcePath = "UI/Wellness Shield Additive";
    internal const int OxygenBoostOverlayTextureWidth = 1920;
    internal const int OxygenBoostOverlayTextureHeight = 1080;
    internal const int TransparentRenderQueue = 3000;
    private const float FloatingHeight = 12f;
    [SerializeField] private float panelScale = 0.04f;
    [SerializeField] private bool useCameraBillboard = true;
    private const float NavMeshSampleRadius = 50f;
    private const float CanvasPixelsPerUnit = 12f;
    private const float StatusFontSize = 58f;
    private const float ButtonFontSize = 48f;
    private static readonly Vector2 CanvasSize = new Vector2(1100f, 430f);
    private static readonly Vector2 StatusSize = new Vector2(1020f, 170f);
    private static readonly Vector2 StatusPosition = new Vector2(0f, 90f);
    private static readonly Vector2 ButtonSize = new Vector2(600f, 120f);
    private static readonly Vector2 ButtonPosition = new Vector2(0f, -115f);
    private static readonly Vector2 ButtonLabelSize = new Vector2(570f, 105f);
    private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);

    [SerializeField] private GameObject tCellPrefab;
    [SerializeField] private GameObject macrophagePrefab;

    private Canvas floatingCanvas;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI sendButtonLabel;
    private Button sendImmuneCellsButton;
    private GameObject spawnedTCell;
    private GameObject spawnedMacrophage;
    private Coroutine responseRoutine;
    private readonly HashSet<string> pendingWellnessEventKeys = new HashSet<string>();
    private int infiltrationCount;
    private float remainingResponseSeconds;
    private bool responseInProgress;

    private void Awake()
    {
        CreateFloatingPrompt();
    }

    private void OnDestroy()
    {
        if (sendImmuneCellsButton != null)
        {
            sendImmuneCellsButton.onClick.RemoveListener(BeginRepair);
        }

        if (responseRoutine != null)
        {
            StopCoroutine(responseRoutine);
        }

        DestroySpawnedImmuneCells();
        if (floatingCanvas != null)
        {
            Destroy(floatingCanvas.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (floatingCanvas == null)
        {
            return;
        }

        floatingCanvas.transform.position = transform.position + Vector3.up * FloatingHeight;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            floatingCanvas.worldCamera = mainCamera;
            if (floatingCanvas.gameObject.activeSelf && useCameraBillboard)
            {
                floatingCanvas.transform.rotation = mainCamera.transform.rotation;
            }
        }
    }

    /// <summary>Registers one contaminated-air arrival at this lung and adds ten seconds to an active repair.</summary>
    /// <param name="eventKey">The wellness event occurrence associated with the contaminated air, if any.</param>
    public void RegisterInfiltration(string eventKey)
    {
        infiltrationCount++;
        if (!string.IsNullOrWhiteSpace(eventKey))
        {
            pendingWellnessEventKeys.Add(eventKey);
            QTETracker.Register(QTETracker.RespiratoryLayerIndex, eventKey);
            WellnessManager.Instance?.MarkEventReachedTarget(eventKey);
        }
        if (responseInProgress)
        {
            remainingResponseSeconds += SecondsPerInfiltration;
        }

        if (floatingCanvas != null)
        {
            floatingCanvas.gameObject.SetActive(true);
        }

        RefreshPrompt();
    }

    private void CreateFloatingPrompt()
    {
        GameObject canvasObject = new GameObject("Lung Virus Response UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        floatingCanvas = canvasObject.GetComponent<Canvas>();
        floatingCanvas.renderMode = RenderMode.WorldSpace;
        floatingCanvas.overrideSorting = true;
        floatingCanvas.sortingOrder = 110;
        floatingCanvas.worldCamera = Camera.main;
        canvasObject.transform.position = transform.position + Vector3.up * FloatingHeight;
        canvasObject.transform.localScale = Vector3.one * panelScale;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = CanvasPixelsPerUnit;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = CanvasSize;

        GameObject panelObject = CreateUiChild("Panel", canvasRect, typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        StretchToParent(panelRect);
        panelObject.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.085f, 0.96f);

        statusLabel = CreateText("Infection Status", panelRect, StatusSize, StatusPosition, string.Empty, StatusFontSize);
        statusLabel.color = new Color(1f, 0.86f, 0.7f, 1f);

        GameObject buttonObject = CreateUiChild("Repair Button", panelRect, typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureCenteredRect(buttonRect, ButtonSize, ButtonPosition);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.13f, 0.48f, 0.33f, 1f);
        sendImmuneCellsButton = buttonObject.GetComponent<Button>();
        sendImmuneCellsButton.targetGraphic = buttonImage;
        sendImmuneCellsButton.onClick.AddListener(BeginRepair);
        sendButtonLabel = CreateText("Button Label", buttonRect, ButtonLabelSize, Vector2.zero, "Repair", ButtonFontSize);
        sendButtonLabel.raycastTarget = false;

        canvasObject.SetActive(false);
    }

    private void BeginRepair()
    {
        if (infiltrationCount <= 0 || responseInProgress)
        {
            return;
        }

        if (tCellPrefab == null || macrophagePrefab == null)
        {
            Debug.LogError($"LungInfectionResponse on '{name}' requires T cell and macrophage prefabs.", this);
            return;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning($"No NavMesh position was found near {name}; immune cells could not be dispatched.", this);
            return;
        }

        spawnedTCell = SpawnImmuneCell(tCellPrefab, hit.position);
        spawnedMacrophage = SpawnImmuneCell(macrophagePrefab, hit.position);
        if (spawnedTCell == null || spawnedMacrophage == null)
        {
            DestroySpawnedImmuneCells();
            return;
        }

        responseInProgress = true;
        remainingResponseSeconds = infiltrationCount * SecondsPerInfiltration;
        sendImmuneCellsButton.interactable = false;
        responseRoutine = StartCoroutine(RunImmuneResponse());
        RefreshPrompt();
    }

    private GameObject SpawnImmuneCell(GameObject prefab, Vector3 spawnPosition)
    {
        GameObject immuneCell = Instantiate(prefab, spawnPosition, Quaternion.identity);
        NavMeshAgent agent = immuneCell.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.Warp(spawnPosition))
        {
            Debug.LogWarning($"Immune-cell prefab '{prefab.name}' could not attach to the lung NavMesh.", immuneCell);
            Destroy(immuneCell);
            return null;
        }

        return immuneCell;
    }

    private IEnumerator RunImmuneResponse()
    {
        while (remainingResponseSeconds > 0f)
        {
            remainingResponseSeconds -= GameplaySpeed.DeltaTime;
            RefreshPrompt();
            yield return null;
        }

        responseRoutine = null;
        responseInProgress = false;
        infiltrationCount = 0;
        remainingResponseSeconds = 0f;
        bool completedQte = pendingWellnessEventKeys.Count > 0;
        WellnessManager wellnessManager = WellnessManager.Instance;
        if (wellnessManager != null)
        {
            foreach (string eventKey in pendingWellnessEventKeys)
            {
                wellnessManager.TryAwardEventResponse(eventKey, "RespiratoryResponse", 3f, 5f);
                wellnessManager.ResolveEventQTE(eventKey);
                QTETracker.Unregister(QTETracker.RespiratoryLayerIndex, eventKey);
            }
        }
        pendingWellnessEventKeys.Clear();
        if (completedQte)
            ActivateOxygenBoost();
        DestroySpawnedImmuneCells();
        if (floatingCanvas != null)
        {
            floatingCanvas.gameObject.SetActive(false);
        }

        sendImmuneCellsButton.interactable = true;
    }

    private void ActivateOxygenBoost()
    {
        GameObject hudCanvasObject = GameObject.Find("HUDCanvas");
        Canvas hudCanvas = hudCanvasObject != null ? hudCanvasObject.GetComponent<Canvas>() : null;
        if (hudCanvas == null)
        {
            Debug.LogWarning("[RESPIRATORY] HUDCanvas was not found; the Oxygen Boost perk could not be displayed or timed.", this);
            return;
        }

        RespiratoryOxygenBoostHud oxygenBoostHud = hudCanvas.GetComponent<RespiratoryOxygenBoostHud>();
        if (oxygenBoostHud == null)
            oxygenBoostHud = hudCanvas.gameObject.AddComponent<RespiratoryOxygenBoostHud>();

        oxygenBoostHud.Activate(OxygenBoostDurationSeconds);
    }

    private void RefreshPrompt()
    {
        if (statusLabel == null || sendButtonLabel == null)
        {
            return;
        }

        string lungName = name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Left lung" : "Right lung";
        if (responseInProgress)
        {
            int seconds = Mathf.CeilToInt(remainingResponseSeconds);
            int minutes = seconds / 60;
            int remainingSeconds = seconds % 60;
            statusLabel.text = $"{lungName} is infected.\nContaminated air: {infiltrationCount}";
            sendButtonLabel.text = $"Repairing  {minutes:00}:{remainingSeconds:00}";
            sendImmuneCellsButton.interactable = false;
        }
        else
        {
            statusLabel.text = $"{lungName} is infected.\nContaminated air: {infiltrationCount}";
            sendButtonLabel.text = "Repair";
            sendImmuneCellsButton.interactable = true;
        }
    }

    private void DestroySpawnedImmuneCells()
    {
        if (spawnedTCell != null)
        {
            Destroy(spawnedTCell);
            spawnedTCell = null;
        }

        if (spawnedMacrophage != null)
        {
            Destroy(spawnedMacrophage);
            spawnedMacrophage = null;
        }
    }

    private static GameObject CreateUiChild(string objectName, Transform parent, params System.Type[] componentTypes)
    {
        System.Type[] requiredTypes = new System.Type[componentTypes.Length + 2];
        requiredTypes[0] = typeof(RectTransform);
        requiredTypes[1] = typeof(CanvasRenderer);
        for (int index = 0; index < componentTypes.Length; index++)
        {
            requiredTypes[index + 2] = componentTypes[index];
        }

        GameObject child = new GameObject(objectName, requiredTypes);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static TextMeshProUGUI CreateText(string objectName, RectTransform parent, Vector2 size, Vector2 position, string initialText, float fontSize)
    {
        GameObject textObject = CreateUiChild(objectName, parent, typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        ConfigureCenteredRect(textRect, size, position);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureCenteredRect(RectTransform rectTransform, Vector2 size, Vector2 position)
    {
        rectTransform.anchorMin = CenterAnchor;
        rectTransform.anchorMax = CenterAnchor;
        rectTransform.pivot = CenterAnchor;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}

/// <summary>Displays and times the Respiratory Oxygen Boost icon on the main HUD.</summary>
internal sealed class RespiratoryOxygenBoostHud : MonoBehaviour
{
    private const string ProgressBarObjectName = "GameProgressBar";
    private const string PausePanelObjectName = "PausePanel";

    private Image oxygenBoostImage;
    private Sprite oxygenBoostSprite;
    private Coroutine oxygenBoostRoutine;
    private RawImage oxygenBoostOverlayImage;
    private VideoPlayer oxygenBoostOverlayVideoPlayer;
    private RenderTexture oxygenBoostOverlayTexture;
    private Material runtimeOxygenBoostOverlayMaterial;

    private void Awake()
    {
        InitializeOxygenBoostOverlay();
    }

    /// <summary>Starts or refreshes the Oxygen Boost icon countdown and WBC speed buff.</summary>
    public void Activate(float durationSeconds)
    {
        if (durationSeconds <= 0f)
            return;

        CreateOxygenBoostIcon();
        PlayOxygenBoostOverlay();
        if (oxygenBoostRoutine != null)
            StopCoroutine(oxygenBoostRoutine);

        if (oxygenBoostImage != null)
        {
            oxygenBoostImage.fillAmount = 1f;
            oxygenBoostImage.gameObject.SetActive(true);
        }

        InfectionSpawner.SetOxygenBuffActive(true);
        oxygenBoostRoutine = StartCoroutine(RunOxygenBoost(durationSeconds));
    }

    private void CreateOxygenBoostIcon()
    {
        if (oxygenBoostImage != null)
            return;

        Texture2D iconTexture = Resources.Load<Texture2D>(LungInfectionResponse.OxygenBoostIconResourcePath);
        if (iconTexture == null)
        {
            Debug.LogWarning($"[RESPIRATORY] Icon texture was not found at Resources/{LungInfectionResponse.OxygenBoostIconResourcePath}.png.", this);
            return;
        }

        oxygenBoostSprite = Sprite.Create(
            iconTexture,
            new Rect(0f, 0f, iconTexture.width, iconTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        oxygenBoostSprite.name = LungInfectionResponse.OxygenBoostIconObjectName + " Sprite";

        GameObject iconObject = new GameObject(
            LungInfectionResponse.OxygenBoostIconObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        iconObject.transform.SetParent(transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(LungInfectionResponse.OxygenBoostIconSize, LungInfectionResponse.OxygenBoostIconSize);
        iconRect.anchoredPosition = new Vector2(
            LungInfectionResponse.OxygenBoostIconHorizontalOffset,
            LungInfectionResponse.OxygenBoostIconVerticalOffset);

        oxygenBoostImage = iconObject.GetComponent<Image>();
        oxygenBoostImage.sprite = oxygenBoostSprite;
        oxygenBoostImage.color = Color.white;
        oxygenBoostImage.type = Image.Type.Filled;
        oxygenBoostImage.fillMethod = Image.FillMethod.Horizontal;
        oxygenBoostImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        oxygenBoostImage.fillAmount = 1f;
        oxygenBoostImage.raycastTarget = false;
        oxygenBoostImage.preserveAspect = true;

        Transform pausePanel = transform.Find(PausePanelObjectName);
        if (pausePanel != null)
            iconObject.transform.SetSiblingIndex(pausePanel.GetSiblingIndex());
        else
        {
            Transform progressBar = transform.Find(ProgressBarObjectName);
            if (progressBar != null)
                iconObject.transform.SetSiblingIndex(progressBar.GetSiblingIndex() + 1);
            else
                iconObject.transform.SetAsLastSibling();
        }

        iconObject.SetActive(false);
    }

    private IEnumerator RunOxygenBoost(float durationSeconds)
    {
        float elapsedSeconds = 0f;
        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds = Mathf.Min(durationSeconds, elapsedSeconds + Time.unscaledDeltaTime);
            if (oxygenBoostImage != null)
                oxygenBoostImage.fillAmount = 1f - elapsedSeconds / durationSeconds;

            yield return null;
        }

        if (oxygenBoostImage != null)
        {
            oxygenBoostImage.fillAmount = 0f;
            oxygenBoostImage.gameObject.SetActive(false);
        }

        StopOxygenBoostOverlay();
        InfectionSpawner.SetOxygenBuffActive(false);
        oxygenBoostRoutine = null;
    }

    private bool InitializeOxygenBoostOverlay()
    {
        Transform overlayTransform = transform.Find(LungInfectionResponse.OxygenBoostOverlayObjectName);
        if (overlayTransform == null)
        {
            Debug.LogWarning($"[RESPIRATORY] {LungInfectionResponse.OxygenBoostOverlayObjectName} was not found under HUDCanvas.", this);
            return false;
        }

        VideoClip videoClip = Resources.Load<VideoClip>(LungInfectionResponse.OxygenBoostOverlayVideoResourcePath);
        Material additiveMaterial = Resources.Load<Material>(LungInfectionResponse.OxygenBoostOverlayMaterialResourcePath);
        oxygenBoostOverlayImage = overlayTransform.GetComponent<RawImage>();
        oxygenBoostOverlayVideoPlayer = overlayTransform.GetComponent<VideoPlayer>();
        if (videoClip == null || additiveMaterial == null || oxygenBoostOverlayImage == null || oxygenBoostOverlayVideoPlayer == null)
        {
            Debug.LogError("[RESPIRATORY] Oxygen Boost overlay requires its video clip, additive material, RawImage, and VideoPlayer.", this);
            return false;
        }

        if (oxygenBoostOverlayTexture != null)
        {
            oxygenBoostOverlayTexture.Release();
            Destroy(oxygenBoostOverlayTexture);
        }
        if (runtimeOxygenBoostOverlayMaterial != null)
            Destroy(runtimeOxygenBoostOverlayMaterial);

        oxygenBoostOverlayTexture = new RenderTexture(
            LungInfectionResponse.OxygenBoostOverlayTextureWidth,
            LungInfectionResponse.OxygenBoostOverlayTextureHeight,
            0)
        {
            name = "RespiratoryOxygenBoostOverlayRenderTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        if (!oxygenBoostOverlayTexture.Create())
        {
            Debug.LogError("[RESPIRATORY] Could not create the Oxygen Boost overlay RenderTexture.", this);
            Destroy(oxygenBoostOverlayTexture);
            oxygenBoostOverlayTexture = null;
            return false;
        }

        runtimeOxygenBoostOverlayMaterial = new Material(additiveMaterial)
        {
            name = additiveMaterial.name + " (Oxygen Boost Runtime)"
        };
        runtimeOxygenBoostOverlayMaterial.SetTexture("_MainTex", oxygenBoostOverlayTexture);
        runtimeOxygenBoostOverlayMaterial.SetColor("_Color", Color.white);
        runtimeOxygenBoostOverlayMaterial.renderQueue = LungInfectionResponse.TransparentRenderQueue;

        oxygenBoostOverlayImage.texture = oxygenBoostOverlayTexture;
        oxygenBoostOverlayImage.material = runtimeOxygenBoostOverlayMaterial;
        oxygenBoostOverlayImage.color = Color.white;
        oxygenBoostOverlayImage.raycastTarget = false;
        oxygenBoostOverlayImage.enabled = false;

        oxygenBoostOverlayVideoPlayer.Stop();
        oxygenBoostOverlayVideoPlayer.source = VideoSource.VideoClip;
        oxygenBoostOverlayVideoPlayer.clip = videoClip;
        oxygenBoostOverlayVideoPlayer.playOnAwake = false;
        oxygenBoostOverlayVideoPlayer.waitForFirstFrame = true;
        oxygenBoostOverlayVideoPlayer.isLooping = true;
        oxygenBoostOverlayVideoPlayer.skipOnDrop = true;
        oxygenBoostOverlayVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        oxygenBoostOverlayVideoPlayer.targetTexture = oxygenBoostOverlayTexture;
        oxygenBoostOverlayVideoPlayer.aspectRatio = VideoAspectRatio.Stretch;
        oxygenBoostOverlayVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        oxygenBoostOverlayVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        oxygenBoostOverlayVideoPlayer.Prepare();
        return true;
    }

    private void PlayOxygenBoostOverlay()
    {
        if ((oxygenBoostOverlayImage == null || oxygenBoostOverlayVideoPlayer == null) && !InitializeOxygenBoostOverlay())
            return;

        if (oxygenBoostOverlayVideoPlayer.isPlaying)
            oxygenBoostOverlayVideoPlayer.Stop();

        oxygenBoostOverlayImage.color = Color.white;
        oxygenBoostOverlayImage.raycastTarget = false;
        oxygenBoostOverlayImage.enabled = true;
        oxygenBoostOverlayImage.gameObject.SetActive(true);
        oxygenBoostOverlayVideoPlayer.Play();
    }

    private void StopOxygenBoostOverlay()
    {
        if (oxygenBoostOverlayVideoPlayer != null)
            oxygenBoostOverlayVideoPlayer.Stop();
        if (oxygenBoostOverlayImage != null)
        {
            oxygenBoostOverlayImage.enabled = false;
            oxygenBoostOverlayImage.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (oxygenBoostRoutine != null)
            StopCoroutine(oxygenBoostRoutine);

        StopOxygenBoostOverlay();
        if (oxygenBoostOverlayVideoPlayer != null)
            oxygenBoostOverlayVideoPlayer.targetTexture = null;
        if (oxygenBoostOverlayTexture != null)
        {
            oxygenBoostOverlayTexture.Release();
            Destroy(oxygenBoostOverlayTexture);
        }
        if (runtimeOxygenBoostOverlayMaterial != null)
            Destroy(runtimeOxygenBoostOverlayMaterial);

        if (InfectionSpawner.isOxygenBuffActive)
            InfectionSpawner.SetOxygenBuffActive(false);

        if (oxygenBoostSprite != null)
            Destroy(oxygenBoostSprite);
    }
}
