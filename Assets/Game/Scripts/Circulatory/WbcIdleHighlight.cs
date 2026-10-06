using UnityEngine;

/// <summary>Displays a pulsing cyan halo around an idle WBC unit.</summary>
public sealed class WbcIdleHighlight : MonoBehaviour
{
    private const int RingSegmentCount = 48;
    private const string RingObjectName = "WBC Idle Highlight Ring";

    private static Material sharedRingMaterial;

    [SerializeField] private Color highlightColor = new Color(0f, 1f, 1f, 1f);
    [SerializeField, Min(0.1f)] private float pulseSpeed = 2f;
    [SerializeField, Min(0.1f)] private float ringRadius = 0.7f;
    [SerializeField, Min(0.005f)] private float minimumLineWidth = 0.025f;
    [SerializeField, Min(0.005f)] private float maximumLineWidth = 0.055f;

    private LineRenderer ring;
    private bool highlighted;
    private float pulsePhase;

    private void Awake()
    {
        CreateRing();
        ApplyPulse(0.5f);
    }

    private void Update()
    {
        if (!highlighted || ring == null || !ring.enabled)
            return;

        pulsePhase += GameplaySpeed.DeltaTime * pulseSpeed;
        ApplyPulse((Mathf.Sin(pulsePhase) + 1f) * 0.5f);
    }

    private void OnDisable()
    {
        if (ring != null)
            ring.enabled = false;
    }

    /// <summary>Sets the highlight color used by this unit's idle halo.</summary>
    public void SetHighlightColor(Color color)
    {
        highlightColor = color;
        if (highlighted)
            ApplyPulse((Mathf.Sin(pulsePhase) + 1f) * 0.5f);
    }

    /// <summary>Enables or disables the pulsing idle halo.</summary>
    public void SetHighlighted(bool shouldHighlight)
    {
        highlighted = shouldHighlight;
        pulsePhase = 0f;
        if (ring == null)
            CreateRing();
        if (ring == null)
            return;

        ring.enabled = shouldHighlight && isActiveAndEnabled && ring.sharedMaterial != null;
        if (ring.enabled)
            ApplyPulse(0.5f);
    }

    private void CreateRing()
    {
        if (ring != null)
            return;

        GameObject ringObject = new GameObject(RingObjectName, typeof(LineRenderer));
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        ringObject.transform.localRotation = Quaternion.identity;
        ringObject.transform.localScale = Vector3.one;

        ring = ringObject.GetComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = RingSegmentCount;
        ring.alignment = LineAlignment.View;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.sharedMaterial = GetSharedRingMaterial();
        ring.enabled = false;
        UpdateRingPoints();
    }

    private void UpdateRingPoints()
    {
        if (ring == null)
            return;

        float safeRadius = Mathf.Max(0.1f, ringRadius);
        for (int index = 0; index < RingSegmentCount; index++)
        {
            float angle = index * (Mathf.PI * 2f / RingSegmentCount);
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * safeRadius, 0f, Mathf.Sin(angle) * safeRadius));
        }
    }

    private void ApplyPulse(float pulse)
    {
        if (ring == null)
            return;

        UpdateRingPoints();
        float width = Mathf.Lerp(Mathf.Min(minimumLineWidth, maximumLineWidth), Mathf.Max(minimumLineWidth, maximumLineWidth), pulse);
        Color color = highlightColor;
        color.a *= Mathf.Lerp(0.4f, 1f, pulse);
        ring.startWidth = width;
        ring.endWidth = width;
        ring.startColor = color;
        ring.endColor = color;
    }

    private static Material GetSharedRingMaterial()
    {
        if (sharedRingMaterial != null)
            return sharedRingMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        sharedRingMaterial = new Material(shader)
        {
            name = "WBC Idle Highlight Shared Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedRingMaterial;
    }
}
