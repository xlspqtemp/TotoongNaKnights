using UnityEngine;

public class CameraScript : MonoBehaviour
{
    private const int LayerCount = 4;

    public float panSpeed = 5f;

    /* 08/19, (4)Transform variables allotted for all four(4) systems */
    public Transform floor1;
    public Transform floor2;
    public Transform floor3;
    public Transform floor4;

    [Header("System Controls")]
    [SerializeField] private CanvasGroup lymphaticOrdersCanvasGroup;
    [SerializeField] private CanvasGroup tacticalOrdersCanvasGroup;
    [SerializeField] private CanvasGroup digestiveOrdersCanvasGroup;
    [SerializeField] private CanvasGroup respiratoryOrdersCanvasGroup;

    [Header("Camera Movement Audio")]
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioClip movementSfx;
    [SerializeField] private AudioClip layerSwitchSfx;

    private bool wasPanInputActive;
    private bool wasZoomInputActive;
    private int selectedLayer = 1;

    private void Start()
    {
        SetSelectedLayer(1, false);
    }

    private void Update()
    {
        float vertical = Input.GetAxis("Horizontal");
        float horizontal = Input.GetAxis("Vertical");

        Vector3 isoHorizontal = new Vector3(-1, 0, 1);
        Vector3 isoVertical = new Vector3(1, 0, 1);
        Vector3 movement = (isoHorizontal * horizontal) + (isoVertical * vertical);

        const float cameraMoveSpeed = 100f;
        transform.position += movement * cameraMoveSpeed * Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.F))
        {
            SetSelectedLayer(1, true);
        }
        else if (Input.GetKeyDown(KeyCode.G))
        {
            SetSelectedLayer(2, true);
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            SetSelectedLayer(3, true);
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            SetSelectedLayer(4, true);
        }

        UpdateMovementAudio(Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f);
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
                SetTacticalOrdersVisible(true);
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
            transform.position = destination.position;
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
