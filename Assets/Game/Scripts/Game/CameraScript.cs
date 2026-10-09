using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraScript : MonoBehaviour
{
    private const int LayerCount = 4;
    private const int MousePanButton = 1;
    private const float DefaultPanSpeed = 5f;
    private const float DefaultInfectionFocusDuration = 0.4f;
    private const float DefaultInfectionFocusOrthographicSize = 10f;
    private const float MinimumInfectionFocusDuration = 0.01f;
    private static readonly FieldInfo ZoomControllerCurrentZoomField =
        typeof(CameraScript_Zoom).GetField("_currentZoom", BindingFlags.Instance | BindingFlags.NonPublic);

    public float panSpeed = DefaultPanSpeed;

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

    [Header("Infection Focus")]
    [SerializeField, Min(MinimumInfectionFocusDuration)] private float infectionFocusDuration = DefaultInfectionFocusDuration;
    [SerializeField, Min(0f)] private float infectionFocusOrthographicSize = DefaultInfectionFocusOrthographicSize;

    [Header("System Controls")]
    [SerializeField] private CanvasGroup digestiveOrdersCanvasGroup;
    [SerializeField] private CanvasGroup respiratoryOrdersCanvasGroup;

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
    private int selectedLayer = 1;
    private Coroutine infectionFocusCoroutine;
    private Coroutine frameCoroutine;
    private CameraScript_Zoom focusZoomController;
    private CameraScript_Zoom frameZoomController;
    private bool focusZoomControllerWasEnabled;
    private bool frameZoomControllerWasEnabled;
    private bool suppressCameraPositionOnLayerSelection;
    private int nextDeployedSquadFocusIndex;

    private void Awake()
    {
        sceneCamera = GetComponentInChildren<Camera>();
    }

    private void Start()
    {
        SetSelectedLayer(1, false);
    }

    private void Update()
    {
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

        StopInfectionFocus();
        selectedLayer = layerNumber;
        HideAllSystemControls();

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

    private void SetLymphaticOrdersVisible(bool isVisible)
    {
        SetCanvasGroupVisible(lymphaticOrdersCanvasGroup, isVisible);
    }

    private void SetTacticalOrdersVisible(bool isVisible)
    {
        SetCanvasGroupVisible(tacticalOrdersCanvasGroup, isVisible);
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
