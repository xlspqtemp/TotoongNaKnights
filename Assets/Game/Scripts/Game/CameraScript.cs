using UnityEngine;

public class CameraScript : MonoBehaviour
{
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

    private void Start()
    {
        HideAllSystemControls();
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
            transform.position = floor1.position;
            HideAllSystemControls();
            SetLymphaticOrdersVisible(true);
            PlayLayerSwitchSound();
        }
        else if (Input.GetKeyDown(KeyCode.G))
        {
            transform.position = floor2.position;
            HideAllSystemControls();
            SetTacticalOrdersVisible(true);
            PlayLayerSwitchSound();
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            transform.position = floor3.position;
            HideAllSystemControls();
            SetDigestiveOrdersVisible(true);
            PlayLayerSwitchSound();
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            transform.position = floor4.position;
            HideAllSystemControls();
            SetRespiratoryOrdersVisible(true);
            PlayLayerSwitchSound();
        }

        UpdateMovementAudio(Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f);
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
            return;

        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    private void PlayLayerSwitchSound()
    {
        if (movementAudioSource != null && layerSwitchSfx != null)
            movementAudioSource.PlayOneShot(layerSwitchSfx);
    }

    private void UpdateMovementAudio(bool panInputActive)
    {
        Vector2 scrollInput = Input.mouseScrollDelta;
        bool zoomInputActive = Mathf.Abs(scrollInput.y) > 0.01f;
        bool newPanAction = panInputActive && !wasPanInputActive;
        bool newZoomAction = zoomInputActive && !wasZoomInputActive;

        if ((newPanAction || newZoomAction) && movementAudioSource != null && movementSfx != null)
            movementAudioSource.PlayOneShot(movementSfx);

        wasPanInputActive = panInputActive;
        wasZoomInputActive = zoomInputActive;
    }
}
