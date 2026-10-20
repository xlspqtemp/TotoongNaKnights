using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Creates and plays the full-screen video overlay used during body-layer transitions.</summary>
public sealed class LayerTransitionUI : MonoBehaviour
{
    private const string VideoResourcePath = "Layer transition";
    private const string OverlayObjectName = "Layer Transition Overlay";
    private const int RenderTextureWidth = 1920;
    private const int RenderTextureHeight = 1080;
    private const int OverlaySortingOrder = 32767;
    private const double TransitionVideoDurationSeconds = 5.0;
    private const float FadeInDurationSeconds = 0.15f;
    private const float FadeOutDurationSeconds = 0.2f;
    private const float PreparationTimeoutSeconds = 5f;

    private Canvas overlayCanvas;
    private CanvasGroup overlayCanvasGroup;
    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private Coroutine transitionCoroutine;
    private Action onScreenCovered;

    private void Awake()
    {
        CreateOverlay();
    }

    /// <summary>Plays the layer-transition video, limited to five seconds with short fades.</summary>
    public void PlayTransition()
    {
        PlayTransition(null);
    }

    /// <summary>Plays the layer-transition video and invokes the callback once the screen is fully covered.</summary>
    public void PlayTransition(Action screenCoveredCallback)
    {
        if (overlayCanvas == null || overlayCanvasGroup == null || videoPlayer == null || videoPlayer.clip == null)
        {
            screenCoveredCallback?.Invoke();
            return;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
            videoPlayer.Stop();
        }

        onScreenCovered = screenCoveredCallback;
        overlayCanvas.enabled = true;
        overlayCanvasGroup.alpha = 0f;
        transitionCoroutine = StartCoroutine(RunTransition());
    }

    private void CreateOverlay()
    {
        VideoClip videoClip = Resources.Load<VideoClip>(VideoResourcePath);
        if (videoClip == null)
        {
            Debug.LogError($"[LayerTransitionUI] Could not load Resources/{VideoResourcePath}.mp4.", this);
            return;
        }

        GameObject canvasObject = new GameObject(OverlayObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = OverlaySortingOrder;
        overlayCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvas.enabled = false;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(RenderTextureWidth, RenderTextureHeight);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("Layer Transition Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(VideoPlayer));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        RawImage rawImage = imageObject.GetComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        renderTexture = new RenderTexture(RenderTextureWidth, RenderTextureHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "LayerTransitionRuntimeTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        renderTexture.Create();
        rawImage.texture = renderTexture;

        videoPlayer = imageObject.GetComponent<VideoPlayer>();
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        for (ushort trackIndex = 0; trackIndex < videoClip.audioTrackCount; trackIndex++)
            videoPlayer.EnableAudioTrack(trackIndex, true);
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        videoPlayer.Prepare();
        videoPlayer.errorReceived += HandleVideoError;
    }

    private IEnumerator RunTransition()
    {
        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            float preparationElapsed = 0f;
            while (!videoPlayer.isPrepared && preparationElapsed < PreparationTimeoutSeconds)
            {
                preparationElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (!videoPlayer.isPrepared)
        {
            onScreenCovered?.Invoke();
            onScreenCovered = null;
            overlayCanvasGroup.alpha = 0f;
            overlayCanvas.enabled = false;
            transitionCoroutine = null;
            yield break;
        }

        videoPlayer.time = 0.0;
        videoPlayer.Play();
        yield return FadeCanvasGroup(0f, 1f, FadeInDurationSeconds);
        onScreenCovered?.Invoke();
        onScreenCovered = null;

        float playbackSafetyElapsed = 0f;
        bool playbackWasStarted = false;
        while (videoPlayer.time < TransitionVideoDurationSeconds && playbackSafetyElapsed < PreparationTimeoutSeconds + (float)TransitionVideoDurationSeconds)
        {
            if (videoPlayer.isPlaying)
                playbackWasStarted = true;
            else if (playbackWasStarted || playbackSafetyElapsed > 2f)
                break;

            playbackSafetyElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        videoPlayer.Stop();
        yield return FadeCanvasGroup(1f, 0f, FadeOutDurationSeconds);

        overlayCanvasGroup.alpha = 0f;
        overlayCanvas.enabled = false;
        transitionCoroutine = null;
    }

    private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        overlayCanvasGroup.alpha = startAlpha;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        overlayCanvasGroup.alpha = endAlpha;
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[LayerTransitionUI] Video playback error: {message}", this);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= HandleVideoError;
            videoPlayer.Stop();
            videoPlayer.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (overlayCanvas != null)
            Destroy(overlayCanvas.gameObject);
    }
}
