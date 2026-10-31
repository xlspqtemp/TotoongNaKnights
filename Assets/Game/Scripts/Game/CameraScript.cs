using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CameraScript : MonoBehaviour
{
    private const string LayerTransitionOverlayObjectName = "Layer Transition Overlay";
    private const string DigestiveBreachIconObjectName = "DigestiveBreachIcon";
    private const string RespiratoryBreachIconObjectName = "RespiratoryBreachIcon";
    private const string DigestiveBarrierHitIconObjectName = "DigestiveBarrierHitIcon";
    private const string RespiratoryBarrierHitIconObjectName = "RespiratoryBarrierHitIcon";
    private const string HudLayerRowsPath = "/Event Handlers/HUDCanvas/SystemLayerButtons/LayerSelectionRows";
    private const string BarrierHitIconResourcePath = "UI/Barrier taking hit";
    private const float BarrierHitFlashDurationSeconds = 5f;
    private const float BarrierHitFlashIntervalSeconds = 0.3f;
    private const float BarrierHitIconSize = 24f;
    private const float BarrierHitIconRightOffset = -126f;
    private const string BreachIconResourcePath = "UI/Breach icon";
    private static Sprite breachIconSprite;
    private static Sprite barrierHitIconSprite;

    private const int LayerCount = 4;
    private const int DigestiveLayerNumber = 3;
    private const int RespiratoryLayerNumber = 4;
    private const float DayCounterLayerSelectionGap = 12f;
    private const int MousePanButton = 1;
    private const float DefaultPanSpeed = 5f;
    private const float DefaultInfectionFocusDuration = 0.4f;
    private const float DefaultInfectionFocusOrthographicSize = 10f;
    private const float MinimumInfectionFocusDuration = 0.01f;
    private const float FocusLayersVerticalOffset = 100f;
    private static readonly FieldInfo ZoomControllerCurrentZoomField =
        typeof(CameraScript_Zoom).GetField("_currentZoom", BindingFlags.Instance | BindingFlags.NonPublic);

    public float panSpeed = DefaultPanSpeed;

    /// <summary>Gets the currently selected body-system layer number.</summary>
    public int SelectedLayer => selectedLayer;


    /* 08/19, (4)Transform variables allotted for all four(4) systems */
    public Transform floor1;
    public Transform floor2;
    public Transform floor3;
    public Transform floor4;

    [Header("System Controls")]
    [SerializeField] private CanvasGroup lymphaticOrdersCanvasGroup;
    [SerializeField] private CanvasGroup tacticalOrdersCanvasGroup;
    [SerializeField] private bool showTacticalOrdersOnCirculatoryLayer;
    [SerializeField] private Vector3 defaultLayerFocusOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Layer HUD Layout")]
    [SerializeField] private GameObject consoleLogPanel;
    [SerializeField] private GameObject pendingInfectionsPanel;
    [SerializeField] private GameObject humanSnapshotPanel;
    private PendingInfectionsPanel pendingInfectionsPanelController;
    private RectTransform layersPanelRect;
    [SerializeField] private Vector2 focusLayersPosition;
    [SerializeField] private Vector2 defaultLayersPosition;

    [Header("Infection Focus")]
    [SerializeField, Min(MinimumInfectionFocusDuration)] private float infectionFocusDuration = DefaultInfectionFocusDuration;
    [SerializeField, Min(0f)] private float infectionFocusOrthographicSize = DefaultInfectionFocusOrthographicSize;

    [Header("System Controls")]
    [SerializeField] private CanvasGroup digestiveOrdersCanvasGroup;
    [SerializeField] private CanvasGroup respiratoryOrdersCanvasGroup;
    [SerializeField] private Image digestiveBreachIcon;
    [SerializeField] private Image respiratoryBreachIcon;
    [SerializeField] private AudioClip breachSfx;

    [Header("Tutorial Framing")]
    [SerializeField] private Vector3 fullBodyFramePosition = new Vector3(3000f, 0f, -1000f);
    [SerializeField, Min(1f)] private float fullBodyFrameZoom = 250f;
    [SerializeField] private bool calculateFullBodyFrameFromRendererBounds = true;
    [SerializeField] private Vector3[] layerFramePositions =
    {
        new Vector3(0f, 0f, -1000f),
        new Vector3(2000f, 0f, -1000f),
        new Vector3(4000f, 0f, -1000f),
        new Vector3(6000f, 0f, -1000f)
    };
    [SerializeField] private float[] layerFrameZooms = { 150f, 150f, 150f, 150f };
    [SerializeField] private bool calculateLayerFrameZoomFromRendererBounds = true;
    [SerializeField, Min(0f)] private float frameCameraDistance = 80f;
    [SerializeField, Min(0f)] private float frameTweenDuration;
    [SerializeField, Min(1f)] private float framingPadding = 1.15f;

    [Header("Camera Movement Audio")]
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioClip movementSfx;
    [SerializeField] private AudioClip layerSwitchSfx;

    private bool wasPanInputActive;
    private bool wasZoomInputActive;
    private bool mousePanStartedOverWorld;
    private Vector3 previousMousePosition;
    private Camera sceneCamera;
    private LayerTransitionUI layerTransitionUI;
    private AudioSource breachAlarmAudioSource;
    private int selectedLayer = 1;
    private Coroutine infectionFocusCoroutine;
    private Coroutine frameCoroutine;
    private CameraScript_Zoom focusZoomController;
    private CameraScript_Zoom frameZoomController;
    private bool focusZoomControllerWasEnabled;
    private bool frameZoomControllerWasEnabled;
    private bool suppressCameraPositionOnLayerSelection;
    private bool layerHudVisibilityDeferred;
    private int nextDeployedSquadFocusIndex;
    private BarrierHealth digestiveBarrierHealth;
    private BarrierHealth respiratoryBarrierHealth;
    private Image digestiveBarrierHitIcon;
    private Image respiratoryBarrierHitIcon;
    private float digestiveBarrierHitFlashUntil;
    private float respiratoryBarrierHitFlashUntil;
    private BarrierHealth activeBreachAlarmBarrier;
    private Transform followTarget;
    private Vector3 followOffset;

    private void OnDestroy()
    {
        if (digestiveBarrierHealth != null)
            digestiveBarrierHealth.OnBarrierDamaged -= HandleDigestiveBarrierDamaged;
        if (respiratoryBarrierHealth != null)
            respiratoryBarrierHealth.OnBarrierDamaged -= HandleRespiratoryBarrierDamaged;
    }

    private void Awake()
    {
        sceneCamera = GetComponentInChildren<Camera>();
        layerTransitionUI = GetComponent<LayerTransitionUI>();
        if (layerTransitionUI == null)
            layerTransitionUI = gameObject.AddComponent<LayerTransitionUI>();
        breachAlarmAudioSource = gameObject.AddComponent<AudioSource>();
        breachAlarmAudioSource.playOnAwake = false;
        breachAlarmAudioSource.loop = true;
        breachAlarmAudioSource.spatialBlend = 0f;
        if (movementAudioSource != null)
            breachAlarmAudioSource.outputAudioMixerGroup = movementAudioSource.outputAudioMixerGroup;
    }

    private void Start()
    {
        GameObject layerButtonsObj = GameObject.Find("SystemLayerButtons");
        if (layerButtonsObj != null)
        {
            layersPanelRect = layerButtonsObj.GetComponent<RectTransform>();
            if (layersPanelRect == null)
                layersPanelRect = layerButtonsObj.AddComponent<RectTransform>();

            defaultLayersPosition = layersPanelRect.anchoredPosition;
            focusLayersPosition = new Vector2(focusLayersPosition.x, focusLayersPosition.y - FocusLayersVerticalOffset);
        }
        else
        {
            Debug.LogWarning("[Camera] SystemLayerButtons not found for layer repositioning.");
        }

        PendingInfectionsPanel livePendingInfectionsPanel = FindFirstObjectByType<PendingInfectionsPanel>();
        if (livePendingInfectionsPanel != null)
        {
            pendingInfectionsPanelController = livePendingInfectionsPanel;
            pendingInfectionsPanel = livePendingInfectionsPanel.gameObject;
        }
        SetPendingInfectionsPanelVisible(false);

        SetSelectedLayer(1, false);
        InitializeBreachIcons();
        RefreshBreachIcons();
    }

    private void Update()
    {
        RefreshBreachIcons();
        RefreshBarrierHitIcons();
        RefreshBreachAlarm();
        HandleDeployedSquadFocusInput();

        if (infectionFocusCoroutine != null && Time.timeScale > 0f && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
        {
            StopInfectionFocus();
        }

        if (Input.GetMouseButtonDown(MousePanButton))
        {
            mousePanStartedOverWorld = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            previousMousePosition = Input.mousePosition;
        }

        bool panInputActive = Input.GetMouseButton(MousePanButton) && mousePanStartedOverWorld;
        bool cameraMoved = false;
        if (panInputActive)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            Vector3 mouseDelta = currentMousePosition - previousMousePosition;
            previousMousePosition = currentMousePosition;

            if (mouseDelta.sqrMagnitude > 0.01f && Screen.height > 0)
            {
                if (infectionFocusCoroutine != null && Time.timeScale > 0f)
                    StopInfectionFocus();

                float worldUnitsPerPixel = sceneCamera != null && sceneCamera.orthographic
                    ? (sceneCamera.orthographicSize * 2f) / Screen.height
                    : 0.01f;
                float panSpeedScale = Mathf.Max(0f, panSpeed) / DefaultPanSpeed;
                Vector3 cameraRightOnGround = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                Vector3 cameraUpOnGround = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
                Vector3 movement = (-cameraRightOnGround * mouseDelta.x - cameraUpOnGround * mouseDelta.y) * worldUnitsPerPixel * panSpeedScale;
                transform.position += movement;
                cameraMoved = true;
            }
        }
        else if (!Input.GetMouseButton(MousePanButton))
        {
            mousePanStartedOverWorld = false;
        }

        // World panning uses right-mouse drag; scroll-wheel zoom and HUD layer selection remain unchanged.
        UpdateMovementAudio(cameraMoved);
    }

    private void InitializeBreachIcons()
    {
        GameObject digestiveRow = GameObject.Find(HudLayerRowsPath + "/LayerButtonRow_Digestive");
        GameObject respiratoryRow = GameObject.Find(HudLayerRowsPath + "/LayerButtonRow_Respiratory");
        digestiveBreachIcon = GetOrCreateBreachIcon(digestiveRow, DigestiveBreachIconObjectName, digestiveBreachIcon);
        respiratoryBreachIcon = GetOrCreateBreachIcon(respiratoryRow, RespiratoryBreachIconObjectName, respiratoryBreachIcon);
        digestiveBarrierHitIcon = GetOrCreateBarrierHitIcon(digestiveRow, DigestiveBarrierHitIconObjectName, digestiveBarrierHitIcon);
        respiratoryBarrierHitIcon = GetOrCreateBarrierHitIcon(respiratoryRow, RespiratoryBarrierHitIconObjectName, respiratoryBarrierHitIcon);
        RefreshBarrierHealthReferences();
    }

    private static Image GetOrCreateBreachIcon(GameObject layerRow, string iconObjectName, Image breachIcon)
    {
        if (layerRow == null)
            return breachIcon;

        if (breachIcon == null)
        {
            Transform existingIcon = layerRow.transform.Find(iconObjectName);
            breachIcon = existingIcon != null ? existingIcon.GetComponent<Image>() : null;
        }

        if (breachIcon == null)
        {
            GameObject iconObject = new GameObject(iconObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(layerRow.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-101f, 0f);
            iconRect.sizeDelta = new Vector2(20f, 20f);
            breachIcon = iconObject.GetComponent<Image>();
        }

        if (breachIcon.sprite == null)
            breachIcon.sprite = LoadBreachIconSprite();
        breachIcon.preserveAspect = true;
        breachIcon.raycastTarget = false;
        breachIcon.gameObject.SetActive(false);
        return breachIcon;
    }

    private static Image GetOrCreateBarrierHitIcon(GameObject layerRow, string iconObjectName, Image barrierHitIcon)
    {
        if (layerRow == null)
            return barrierHitIcon;

        if (barrierHitIcon == null)
        {
            Transform existingIcon = layerRow.transform.Find(iconObjectName);
            barrierHitIcon = existingIcon != null ? existingIcon.GetComponent<Image>() : null;
        }

        if (barrierHitIcon == null)
        {
            GameObject iconObject = new GameObject(iconObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(layerRow.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(BarrierHitIconRightOffset, 0f);
            iconRect.sizeDelta = new Vector2(BarrierHitIconSize, BarrierHitIconSize);
            barrierHitIcon = iconObject.GetComponent<Image>();
        }

        if (barrierHitIcon.sprite == null)
            barrierHitIcon.sprite = LoadBarrierHitIconSprite();
        barrierHitIcon.preserveAspect = true;
        barrierHitIcon.raycastTarget = false;
        barrierHitIcon.gameObject.SetActive(false);
        return barrierHitIcon;
    }

    private static Sprite LoadBarrierHitIconSprite()
    {
        if (barrierHitIconSprite != null)
            return barrierHitIconSprite;

        Texture2D texture = Resources.Load<Texture2D>(BarrierHitIconResourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"Barrier hit icon texture could not be loaded from Resources/{BarrierHitIconResourcePath}.png.");
            return null;
        }

        barrierHitIconSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        barrierHitIconSprite.name = "Barrier Taking Hit Sprite";
        return barrierHitIconSprite;
    }

    private void RefreshBarrierHealthReferences()
    {
        BarrierHealth digestive = FindBarrierHealth("Digestive");
        if (digestiveBarrierHealth != digestive)
        {
            if (digestiveBarrierHealth != null)
                digestiveBarrierHealth.OnBarrierDamaged -= HandleDigestiveBarrierDamaged;
            digestiveBarrierHealth = digestive;
            if (digestiveBarrierHealth != null)
                digestiveBarrierHealth.OnBarrierDamaged += HandleDigestiveBarrierDamaged;
        }

        BarrierHealth respiratory = FindBarrierHealth("Respiratory");
        if (respiratoryBarrierHealth != respiratory)
        {
            if (respiratoryBarrierHealth != null)
                respiratoryBarrierHealth.OnBarrierDamaged -= HandleRespiratoryBarrierDamaged;
            respiratoryBarrierHealth = respiratory;
            if (respiratoryBarrierHealth != null)
                respiratoryBarrierHealth.OnBarrierDamaged += HandleRespiratoryBarrierDamaged;
        }
    }

    private static BarrierHealth FindBarrierHealth(string layerName)
    {
        GameObject layerObject = GameObject.Find("/Layers/" + layerName);
        if (layerObject == null)
            layerObject = GameObject.Find("/" + layerName);
        return layerObject != null ? layerObject.GetComponent<BarrierHealth>() : null;
    }

    private void HandleDigestiveBarrierDamaged(float appliedDamage)
    {
        if (appliedDamage > 0f)
            digestiveBarrierHitFlashUntil = Time.unscaledTime + BarrierHitFlashDurationSeconds;
    }

    private void HandleRespiratoryBarrierDamaged(float appliedDamage)
    {
        if (appliedDamage > 0f)
            respiratoryBarrierHitFlashUntil = Time.unscaledTime + BarrierHitFlashDurationSeconds;
    }

    private void RefreshBarrierHitIcons()
    {
        if (digestiveBarrierHitIcon == null || respiratoryBarrierHitIcon == null)
            InitializeBreachIcons();

        UpdateBarrierHitIcon(digestiveBarrierHitIcon, digestiveBarrierHitFlashUntil);
        UpdateBarrierHitIcon(respiratoryBarrierHitIcon, respiratoryBarrierHitFlashUntil);
    }

    private static void UpdateBarrierHitIcon(Image icon, float flashUntil)
    {
        if (icon == null)
            return;

        float remaining = flashUntil - Time.unscaledTime;
        bool isFlashing = remaining > 0f;
        bool isVisible = isFlashing &&
            Mathf.FloorToInt((BarrierHitFlashDurationSeconds - remaining) / BarrierHitFlashIntervalSeconds) % 2 == 0;
        if (icon.gameObject.activeSelf != isVisible)
            icon.gameObject.SetActive(isVisible);
    }

    private static Sprite LoadBreachIconSprite()
    {
        if (breachIconSprite != null)
            return breachIconSprite;

        Texture2D breachTexture = Resources.Load<Texture2D>(BreachIconResourcePath);
        if (breachTexture == null)
        {
            Debug.LogWarning($"Breach icon texture could not be loaded from Resources/{BreachIconResourcePath}.");
            return null;
        }

        breachIconSprite = Sprite.Create(
            breachTexture,
            new Rect(0f, 0f, breachTexture.width, breachTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return breachIconSprite;
    }

    private void RefreshBreachIcons()
    {
        if (digestiveBreachIcon == null || respiratoryBreachIcon == null ||
            digestiveBarrierHitIcon == null || respiratoryBarrierHitIcon == null)
        {
            InitializeBreachIcons();
        }

        RefreshBarrierHealthReferences();
        UpdateBreachIcon(digestiveBreachIcon, digestiveBarrierHealth);
        UpdateBreachIcon(respiratoryBreachIcon, respiratoryBarrierHealth);
    }

    private static void UpdateBreachIcon(Image breachIcon, BarrierHealth barrierHealth)
    {
        if (breachIcon == null)
            return;

        bool isBreached = barrierHealth != null && barrierHealth.IsBreached;
        breachIcon.gameObject.SetActive(isBreached);
        if (!isBreached)
            return;

        Color iconColor = breachIcon.color;
        iconColor.a = Mathf.PingPong(Time.time, 1f);
        breachIcon.color = iconColor;
    }

    private void RefreshBreachAlarm()
    {
        if (digestiveBarrierHealth != null && digestiveBarrierHealth.BreachSfx == null)
            digestiveBarrierHealth.breachSfx = breachSfx;
        if (respiratoryBarrierHealth != null && respiratoryBarrierHealth.BreachSfx == null)
            respiratoryBarrierHealth.breachSfx = breachSfx;

        BarrierHealth selectedBarrier = selectedLayer == 3
            ? digestiveBarrierHealth
            : selectedLayer == 4 ? respiratoryBarrierHealth : null;
        if (selectedBarrier == null || !selectedBarrier.IsBreached || selectedBarrier.BreachSfx == null)
        {
            StopBreachAlarm();
            return;
        }

        if (activeBreachAlarmBarrier == selectedBarrier && breachAlarmAudioSource.isPlaying)
            return;

        StopBreachAlarm();
        breachAlarmAudioSource.clip = selectedBarrier.BreachSfx;
        breachAlarmAudioSource.loop = true;
        breachAlarmAudioSource.Play();
        activeBreachAlarmBarrier = selectedBarrier;
    }

    private void StopBreachAlarm()
    {
        if (breachAlarmAudioSource != null && breachAlarmAudioSource.isPlaying)
            breachAlarmAudioSource.Stop();
        activeBreachAlarmBarrier = null;
    }

    private void HandleDeployedSquadFocusInput()
    {
        if (Time.timeScale <= 0f || !Input.GetMouseButtonDown(2))
            return;

        InfectionSpawner infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (infectionSpawner == null || infectionSpawner.IsAwaitingInfectionTargetSelection)
            return;

        var squadPositions = infectionSpawner.GetDeployedSquadPositions();
        if (squadPositions.Count == 0)
        {
            nextDeployedSquadFocusIndex = 0;
            return;
        }

        nextDeployedSquadFocusIndex %= squadPositions.Count;
        FocusOnWorldPosition(squadPositions[nextDeployedSquadFocusIndex]);
        nextDeployedSquadFocusIndex = (nextDeployedSquadFocusIndex + 1) % squadPositions.Count;
    }

    /// <summary>Frames the combined body map with a zoomed-out orthographic view.</summary>
    public void FrameFullBody()
    {
        SelectLayer(2);
        Bounds bodyBounds;
        Vector3 center = fullBodyFramePosition;
        float targetZoom = fullBodyFrameZoom;
        if (calculateFullBodyFrameFromRendererBounds && TryGetCombinedLayerBounds(out bodyBounds))
        {
            center = bodyBounds.center;
            targetZoom = Mathf.Max(targetZoom, CalculateOrthographicFrameSize(bodyBounds));
        }

        FrameCameraToPosition(center, targetZoom);
    }

    /// <summary>Switches to and frames one body layer. Layer indices match SelectLayer (1-4).</summary>
    public void FrameLayer(int layerIndex)
    {
        if (layerIndex < 1 || layerIndex > LayerCount)
        {
            Debug.LogWarning($"CameraScript cannot frame invalid layer index {layerIndex}. Expected 1-{LayerCount}.", this);
            return;
        }

        SelectLayer(layerIndex);
        Transform layerRoot = GetLayerRoot(layerIndex);
        Vector3 fallbackPosition = layerRoot != null ? layerRoot.position : GetLayerFramePosition(layerIndex);
        Vector3 center = GetLayerFramePosition(layerIndex, fallbackPosition);
        float targetZoom = GetLayerFrameZoom(layerIndex);
        if (calculateLayerFrameZoomFromRendererBounds && layerRoot != null && TryGetRendererBounds(layerRoot, out Bounds layerBounds))
            targetZoom = Mathf.Max(targetZoom, CalculateOrthographicFrameSize(layerBounds));

        FrameCameraToPosition(center, targetZoom);
    }

    private Transform GetLayerRoot(int layerIndex)
    {
        switch (layerIndex)
        {
            case 1: return floor1;
            case 2: return floor2;
            case 3: return floor3;
            case 4: return floor4;
            default: return null;
        }
    }

    private Vector3 GetLayerFramePosition(int layerIndex, Vector3 fallbackPosition = default)
    {
        int arrayIndex = layerIndex - 1;
        if (layerFramePositions != null && arrayIndex >= 0 && arrayIndex < layerFramePositions.Length)
        {
            Vector3 configuredPosition = layerFramePositions[arrayIndex];
            if (configuredPosition != Vector3.zero)
                return configuredPosition;
        }

        return fallbackPosition;
    }

    private float GetLayerFrameZoom(int layerIndex)
    {
        int arrayIndex = layerIndex - 1;
        if (layerFrameZooms != null && arrayIndex >= 0 && arrayIndex < layerFrameZooms.Length && layerFrameZooms[arrayIndex] > 0f)
            return layerFrameZooms[arrayIndex];

        return fullBodyFrameZoom;
    }

    private bool TryGetCombinedLayerBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        for (int layerIndex = 1; layerIndex <= LayerCount; layerIndex++)
        {
            Transform layerRoot = GetLayerRoot(layerIndex);
            if (!TryGetRendererBounds(layerRoot, out Bounds layerBounds))
                continue;

            if (!hasBounds)
            {
                combinedBounds = layerBounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(layerBounds);
            }
        }

        return hasBounds;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private float CalculateOrthographicFrameSize(Bounds bounds)
    {
        if (sceneCamera == null || !sceneCamera.orthographic)
            return fullBodyFrameZoom;

        Vector3 extents = bounds.extents;
        Vector3 cameraRight = transform.right;
        Vector3 cameraUp = transform.up;
        float projectedHalfWidth = Mathf.Abs(cameraRight.x) * extents.x + Mathf.Abs(cameraRight.y) * extents.y + Mathf.Abs(cameraRight.z) * extents.z;
        float projectedHalfHeight = Mathf.Abs(cameraUp.x) * extents.x + Mathf.Abs(cameraUp.y) * extents.y + Mathf.Abs(cameraUp.z) * extents.z;
        float aspect = Mathf.Max(0.1f, sceneCamera.aspect);
        return Mathf.Max(projectedHalfHeight, projectedHalfWidth / aspect) * Mathf.Max(1f, framingPadding);
    }

    private void FrameCameraToPosition(Vector3 targetPosition, float targetZoom)
    {
        StopInfectionFocus();
        StopCameraFrame();
        sceneCamera = sceneCamera != null ? sceneCamera : GetComponentInChildren<Camera>();
        frameZoomController = GetComponent<CameraScript_Zoom>();
        frameZoomControllerWasEnabled = frameZoomController != null && frameZoomController.enabled;
        if (frameZoomController != null)
            frameZoomController.enabled = false;

        Vector3 targetCameraPosition = targetPosition - transform.forward * frameCameraDistance;
        float duration = Mathf.Max(0f, frameTweenDuration);
        if (duration <= MinimumInfectionFocusDuration)
        {
            ApplyFramePose(targetCameraPosition, targetZoom);
            FinishCameraFrame(targetZoom);
            return;
        }

        frameCoroutine = StartCoroutine(AnimateCameraFrame(targetCameraPosition, targetZoom, duration));
    }

    private IEnumerator AnimateCameraFrame(Vector3 targetPosition, float targetZoom, float duration)
    {
        Vector3 startPosition = transform.position;
        float startZoom = sceneCamera != null ? sceneCamera.orthographicSize : targetZoom;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            ApplyFramePose(Vector3.Lerp(startPosition, targetPosition, easedProgress), Mathf.Lerp(startZoom, targetZoom, easedProgress));
            yield return null;
        }

        ApplyFramePose(targetPosition, targetZoom);
        frameCoroutine = null;
        FinishCameraFrame(targetZoom);
    }

    private void ApplyFramePose(Vector3 cameraPosition, float orthographicSize)
    {
        transform.position = cameraPosition;
        if (sceneCamera != null && sceneCamera.orthographic)
            sceneCamera.orthographicSize = orthographicSize;
    }

    private void FinishCameraFrame(float orthographicSize)
    {
        if (frameZoomController != null)
        {
            frameZoomController.maxZoom = Mathf.Max(frameZoomController.maxZoom, orthographicSize);
            if (ZoomControllerCurrentZoomField != null)
                ZoomControllerCurrentZoomField.SetValue(frameZoomController, orthographicSize);
            frameZoomController.enabled = frameZoomControllerWasEnabled;
        }

        frameZoomController = null;
    }

    private void StopCameraFrame()
    {
        if (frameCoroutine != null)
        {
            StopCoroutine(frameCoroutine);
            frameCoroutine = null;
        }

        if (frameZoomController != null)
        {
            if (sceneCamera != null && sceneCamera.orthographic && ZoomControllerCurrentZoomField != null)
                ZoomControllerCurrentZoomField.SetValue(frameZoomController, sceneCamera.orthographicSize);
            frameZoomController.enabled = frameZoomControllerWasEnabled;
            frameZoomController = null;
        }
    }

    /// <summary>Locks the camera onto a moving unit and applies a close environment-aware zoom.</summary>
    public void SetFollowTarget(Transform target, float orthographicSize)
    {
        if (target == null)
            return;

        StopInfectionFocus();
        followTarget = target;
        followOffset = Vector3.zero;
        if (sceneCamera == null)
            sceneCamera = GetComponentInChildren<Camera>();
        if (sceneCamera != null && sceneCamera.orthographic)
        {
            focusZoomController = GetComponent<CameraScript_Zoom>();
            float minimumZoom = focusZoomController != null ? focusZoomController.minZoom : 2f;
            float maximumZoom = focusZoomController != null ? focusZoomController.maxZoom : 50f;
            float targetZoom = Mathf.Clamp(orthographicSize, minimumZoom, maximumZoom);
            sceneCamera.orthographicSize = targetZoom;
            if (focusZoomController != null && ZoomControllerCurrentZoomField != null)
                ZoomControllerCurrentZoomField.SetValue(focusZoomController, targetZoom);
        }
    }

    /// <summary>Releases camera follow at the camera's current position and zoom.</summary>
    public void ClearFollowTarget()
    {
        followTarget = null;
    }

    private void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + followOffset;
    }

    /// <summary>Smoothly pans and zooms the camera to an infection world position.</summary>
    public void FocusOnWorldPosition(Vector3 worldPosition)
    {
        StopInfectionFocus();
        infectionFocusCoroutine = StartCoroutine(AnimateInfectionFocus(worldPosition));
    }

    private IEnumerator AnimateInfectionFocus(Vector3 worldPosition)
    {
        focusZoomController = GetComponent<CameraScript_Zoom>();
        focusZoomControllerWasEnabled = focusZoomController != null && focusZoomController.enabled;
        if (focusZoomController != null)
            focusZoomController.enabled = false;

        Vector3 startPosition = transform.position;
        float startZoom = sceneCamera != null ? sceneCamera.orthographicSize : 0f;
        float targetZoom = startZoom;
        if (sceneCamera != null && sceneCamera.orthographic)
        {
            float minimumZoom = focusZoomController != null ? focusZoomController.minZoom : 0f;
            float maximumZoom = focusZoomController != null ? focusZoomController.maxZoom : infectionFocusOrthographicSize;
            targetZoom = Mathf.Clamp(Mathf.Min(startZoom, infectionFocusOrthographicSize), minimumZoom, maximumZoom);
        }

        float duration = Mathf.Max(MinimumInfectionFocusDuration, infectionFocusDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            transform.position = Vector3.Lerp(startPosition, worldPosition, easedProgress);
            if (sceneCamera != null && sceneCamera.orthographic)
                sceneCamera.orthographicSize = Mathf.Lerp(startZoom, targetZoom, easedProgress);
            yield return null;
        }

        transform.position = worldPosition;
        if (sceneCamera != null && sceneCamera.orthographic)
            sceneCamera.orthographicSize = targetZoom;
        infectionFocusCoroutine = null;
        RestoreFocusZoomController();
    }

    private void StopInfectionFocus()
    {
        if (infectionFocusCoroutine != null)
        {
            StopCoroutine(infectionFocusCoroutine);
            infectionFocusCoroutine = null;
        }

        RestoreFocusZoomController();
    }

    private void RestoreFocusZoomController()
    {
        if (focusZoomController == null)
            return;

        if (sceneCamera != null && sceneCamera.orthographic && ZoomControllerCurrentZoomField != null)
            ZoomControllerCurrentZoomField.SetValue(focusZoomController, sceneCamera.orthographicSize);

        focusZoomController.enabled = focusZoomControllerWasEnabled;
        focusZoomController = null;
    }

    /// <summary>Selects a body-system layer while keeping the camera at its current position.</summary>
    public void SelectLayerWithoutCameraMove(int layerNumber)
    {
        bool previousSuppressionState = suppressCameraPositionOnLayerSelection;
        suppressCameraPositionOnLayerSelection = true;
        try
        {
            SelectLayer(layerNumber);
        }
        finally
        {
            suppressCameraPositionOnLayerSelection = previousSuppressionState;
        }
    }

    /// <summary>
    /// Selects the requested body-system layer and plays the layer-switch sound.
    /// </summary>
    public void SelectLayer(int layerNumber)
    {
        SetSelectedLayer(layerNumber, true);
    }

    /// <summary>
    /// Advances to the next body-system layer and wraps from layer four to layer one.
    /// </summary>
    public void SelectNextLayer()
    {
        int nextLayer = selectedLayer % LayerCount + 1;
        SetSelectedLayer(nextLayer, true);
    }

    private void SetSelectedLayer(int layerNumber, bool playSwitchSound)
    {
        if (layerNumber < 1 || layerNumber > LayerCount)
        {
            return;
        }

        if (playSwitchSound && layerTransitionUI != null)
        {
            layerHudVisibilityDeferred = true;
            SetLayerHudPanelsVisible(false);
            SetPendingInfectionsPanelVisible(false);
            layerTransitionUI.PlayTransition(() =>
            {
                SetSelectedLayer(layerNumber, false);
                PlayLayerSwitchSound();
                StartCoroutine(ShowLayerHudAfterTransition());
            });
            return;
        }

        StopInfectionFocus();
        if (followTarget != null)
            ClearFollowTarget();
        selectedLayer = layerNumber;
        HideAllSystemControls();

        bool showLayerHud = layerNumber == 1 || layerNumber == 2;
        SetLayerHudPanelsVisible(showLayerHud && !layerHudVisibilityDeferred);
        SetPendingInfectionsPanelVisible(layerNumber == 2 && !layerHudVisibilityDeferred);
        if (layersPanelRect != null)
        {
            if (layerNumber == DigestiveLayerNumber || layerNumber == RespiratoryLayerNumber)
            {
                RectTransform dayCounterRect = layersPanelRect.parent != null
                    ? layersPanelRect.parent.Find("DayCounterPanel") as RectTransform
                    : null;
                if (dayCounterRect != null)
                {
                    layersPanelRect.anchoredPosition = new Vector2(
                        dayCounterRect.anchoredPosition.x,
                        dayCounterRect.anchoredPosition.y - dayCounterRect.rect.height - DayCounterLayerSelectionGap);
                }
                else
                {
                    layersPanelRect.anchoredPosition = defaultLayersPosition;
                }
            }
            else
            {
                layersPanelRect.anchoredPosition = showLayerHud ? defaultLayersPosition : focusLayersPosition;
            }
        }

        Transform destination = null;
        switch (selectedLayer)
        {
            case 1:
                destination = floor1;
                SetLymphaticOrdersVisible(true);
                break;
            case 2:
                destination = floor2;
                SetTacticalOrdersVisible(showTacticalOrdersOnCirculatoryLayer);
                break;
            case 3:
                destination = floor3;
                SetDigestiveOrdersVisible(true);
                break;
            case 4:
                destination = floor4;
                SetRespiratoryOrdersVisible(true);
                break;
        }

        if (destination != null && !suppressCameraPositionOnLayerSelection)
        {
            Vector3 focusOffset = selectedLayer == 1 ? defaultLayerFocusOffset : Vector3.zero;
            transform.position = destination.position + focusOffset;
        }

        LayerSelectionHUD.SetSelectedLayer(selectedLayer);

        LymphaticTrainingConsoleUI trainingConsole = FindFirstObjectByType<LymphaticTrainingConsoleUI>();
        if (trainingConsole != null)
            trainingConsole.SetLayerAvailable(selectedLayer == 1);
        LymphaticBatchConsoleUI batchConsole = FindFirstObjectByType<LymphaticBatchConsoleUI>();
        if (batchConsole != null)
            batchConsole.SetLayerAvailable(selectedLayer == 2);

        InfectionSpawner infectionSpawner = FindFirstObjectByType<InfectionSpawner>();
        if (infectionSpawner != null)
        {
            infectionSpawner.SetWbcHudVisibility(layerNumber == 2);
        }

        if (playSwitchSound)
        {
            PlayLayerSwitchSound();
        }
    }

    private void HideAllSystemControls()
    {
        SetLymphaticOrdersVisible(false);
        SetTacticalOrdersVisible(false);
        SetDigestiveOrdersVisible(false);
        SetRespiratoryOrdersVisible(false);
    }

    private IEnumerator ShowLayerHudAfterTransition()
    {
        GameObject overlayObject = GameObject.Find(LayerTransitionOverlayObjectName);
        Canvas overlayCanvas = overlayObject != null ? overlayObject.GetComponent<Canvas>() : null;
        while (overlayCanvas != null && overlayCanvas.enabled)
            yield return null;

        layerHudVisibilityDeferred = false;
        SetLayerHudPanelsVisible(selectedLayer == 1 || selectedLayer == 2);
        SetPendingInfectionsPanelVisible(selectedLayer == 2);
    }

    private void SetLayerHudPanelsVisible(bool isVisible)
    {
        if (consoleLogPanel != null)
            consoleLogPanel.SetActive(isVisible);
        if (humanSnapshotPanel != null)
            humanSnapshotPanel.SetActive(isVisible);
    }

    private void SetPendingInfectionsPanelVisible(bool isVisible)
    {
        if (pendingInfectionsPanelController == null && pendingInfectionsPanel != null)
            pendingInfectionsPanelController = pendingInfectionsPanel.GetComponent<PendingInfectionsPanel>();
        if (pendingInfectionsPanelController == null)
            pendingInfectionsPanelController = FindFirstObjectByType<PendingInfectionsPanel>();
        if (pendingInfectionsPanelController != null)
            pendingInfectionsPanelController.SetLayerVisible(isVisible);
    }

    private void SetLymphaticOrdersVisible(bool isVisible)
    {
        SetCanvasGroupVisible(lymphaticOrdersCanvasGroup, isVisible);
        LymphaticTrainingConsoleUI trainingConsole = FindFirstObjectByType<LymphaticTrainingConsoleUI>();
        if (trainingConsole != null)
            trainingConsole.SetLayerAvailable(isVisible);
    }

    private void SetTacticalOrdersVisible(bool isVisible)
    {
        SetCanvasGroupVisible(tacticalOrdersCanvasGroup, false);
    }

    private void SetDigestiveOrdersVisible(bool isVisible)
    {
        SetCanvasGroupVisible(digestiveOrdersCanvasGroup, isVisible);
    }

    private void SetRespiratoryOrdersVisible(bool isVisible)
    {
        SetCanvasGroupVisible(respiratoryOrdersCanvasGroup, isVisible);
    }

    private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool isVisible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    private void PlayLayerSwitchSound()
    {
        if (movementAudioSource != null && layerSwitchSfx != null)
        {
            movementAudioSource.PlayOneShot(layerSwitchSfx);
        }
    }

    private void UpdateMovementAudio(bool panInputActive)
    {
        Vector2 scrollInput = Input.mouseScrollDelta;
        bool zoomInputActive = Mathf.Abs(scrollInput.y) > 0.01f;
        bool newPanAction = panInputActive && !wasPanInputActive;
        bool newZoomAction = zoomInputActive && !wasZoomInputActive;

        if ((newPanAction || newZoomAction) && movementAudioSource != null && movementSfx != null)
        {
            movementAudioSource.PlayOneShot(movementSfx);
        }

        wasPanInputActive = panInputActive;
        wasZoomInputActive = zoomInputActive;
    }
}
