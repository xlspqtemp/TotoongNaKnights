using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Routes a looping video clip through a runtime RenderTexture to a camera-space RawImage background.
/// </summary>
public class GameBackgroundVideo : MonoBehaviour
{
    private const int RenderTextureWidth = 1280;
    private const int RenderTextureHeight = 720;

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage backgroundImage;

    private RenderTexture renderTexture;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<RawImage>();
        }

        renderTexture = new RenderTexture(RenderTextureWidth, RenderTextureHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "GameBackgroundVideoRuntimeTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        renderTexture.Create();

        videoPlayer.targetTexture = renderTexture;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        backgroundImage.texture = renderTexture;

        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.Prepare();
        videoPlayer.Play();
    }

    private void OnVideoPrepared(VideoPlayer preparedVideoPlayer)
    {
        preparedVideoPlayer.Play();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[GameBackgroundVideo] Video playback error: {message}");
    }


    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
