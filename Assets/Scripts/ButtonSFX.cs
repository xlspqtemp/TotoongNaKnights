using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using ToolkitButton = UnityEngine.UIElements.Button;

/// <summary>Plays assigned hover and click sounds for UGUI or named UI Toolkit buttons.</summary>
[RequireComponent(typeof(AudioSource))]
public class ButtonSFX : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip hoverClip;
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

    /// <summary>Plays the click sound when a pointer clicks this button.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClick();
    }

    /// <summary>Plays the hover sound when a pointer enters this button.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverClip != null && audioSource != null)
            audioSource.PlayOneShot(hoverClip);
    }

    /// <summary>Configures sounds and mixer routing for buttons created at runtime.</summary>
    public void ConfigureFeedback(AudioClip assignedClickClip, AudioClip assignedHoverClip, AudioMixerGroup outputGroup)
    {
        clickClip = assignedClickClip;
        hoverClip = assignedHoverClip;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null && outputGroup != null)
            audioSource.outputAudioMixerGroup = outputGroup;
    }

    /// <summary>Plays this button's click sound.</summary>
    public void PlayClick()
    {
        if (clickClip != null && audioSource != null)
            audioSource.PlayOneShot(clickClip);
    }
}
