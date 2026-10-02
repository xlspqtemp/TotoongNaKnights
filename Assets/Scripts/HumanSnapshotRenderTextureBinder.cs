using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects the isolated snapshot camera to a transparent runtime RenderTexture and RawImage.</summary>
public sealed class HumanSnapshotRenderTextureBinder : MonoBehaviour
{
    private const int RenderTextureDepthBits = 24;
    private const string RenderTextureName = "Human Snapshot Render Texture";

    [SerializeField] private Camera previewCamera;
    [SerializeField] private RawImage targetImage;
    [SerializeField, Min(64)] private int textureWidth = 512;
    [SerializeField, Min(64)] private int textureHeight = 896;

    private RenderTexture renderTexture;

    private void Awake()
    {
        if (previewCamera == null)
            previewCamera = GetComponent<Camera>();

        if (previewCamera == null || targetImage == null)
        {
            Debug.LogError("HumanSnapshotRenderTextureBinder requires a preview Camera and target RawImage.", this);
            return;
        }

        renderTexture = new RenderTexture(textureWidth, textureHeight, RenderTextureDepthBits, RenderTextureFormat.ARGB32)
        {
            name = RenderTextureName,
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1
        };
        renderTexture.Create();

        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Color.clear;
        previewCamera.targetTexture = renderTexture;
        targetImage.texture = renderTexture;
        targetImage.color = Color.white;
        targetImage.raycastTarget = true;
    }

    private void OnDestroy()
    {
        if (previewCamera != null && previewCamera.targetTexture == renderTexture)
            previewCamera.targetTexture = null;

        if (targetImage != null && targetImage.texture == renderTexture)
            targetImage.texture = null;

        if (renderTexture == null)
            return;

        renderTexture.Release();
        Destroy(renderTexture);
    }
}
