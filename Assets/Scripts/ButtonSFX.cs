using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using ToolkitButton = UnityEngine.UIElements.Button;

/// <summary>
/// Plays an assigned sound when a UGUI or named UI Toolkit button is clicked.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ButtonSFX : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private string uiToolkitButtonName;

    private AudioSource audioSource;
    private ToolkitButton uiToolkitButton;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(uiToolkitButtonName))
            return;

        UIDocument document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
            return;

        uiToolkitButton = document.rootVisualElement.Q<ToolkitButton>(uiToolkitButtonName);
        if (uiToolkitButton != null)
            uiToolkitButton.clicked += PlayClick;
    }

    private void OnDisable()
    {
        if (uiToolkitButton != null)
        {
            uiToolkitButton.clicked -= PlayClick;
            uiToolkitButton = null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClick();
    }

    public void PlayClick()
    {
        if (clickClip != null && audioSource != null)
            audioSource.PlayOneShot(clickClip);
    }
}
