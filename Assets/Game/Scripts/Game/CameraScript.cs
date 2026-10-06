using UnityEngine;
using UnityEngine.EventSystems;

public class CameraScript : MonoBehaviour
{
    private const int LayerCount = 4;
    private const int MousePanButton = 2;
    private const float DefaultPanSpeed = 5f;

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
    [SerializeField] private CanvasGroup digestiveOrdersCanvasGroup;
    [SerializeField] private CanvasGroup respiratoryOrdersCanvasGroup;

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

        // World panning uses middle-mouse drag; scroll-wheel zoom and HUD layer selection remain unchanged.
        UpdateMovementAudio(cameraMoved);
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

        if (destination != null)
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
