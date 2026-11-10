using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Applies runtime Unity Toon Shader variants to system-layer meshes, pathogen instances, and platelet drones.</summary>
[DisallowMultipleComponent]
public sealed class ToonMaterialSwapper : MonoBehaviour
{
    private const string ToonShaderName = "Toon/Toon";
    private const string PlateletDronePrefabName = "Platelet drone static fixed 3 fbx";
    private const string LayersRootName = "Layers";
    private const int TransparentRenderQueue = (int)RenderQueue.Transparent;
    private const int OpaqueRenderQueue = (int)RenderQueue.Geometry;
    private const float OpaqueAlphaThreshold = 0.999f;
    private const float BaseColorStep = 0.6f;
    private const float BaseShadeFeather = 0.1f;
    private const float FirstShadeStep = 0.45f;
    private const float FirstShadeFeather = 0.1f;
    private const float SecondShadeStep = 0.1f;
    private const float SecondShadeFeather = 0.1f;
    private const float OutlineWidth = 1.5f;
    private const float RimLightPower = 0.12f;
    private static readonly Color RimLightColor = new Color(0.12f, 0.62f, 0.76f, 1f);
    private static readonly Color OutlineColor = new Color(0.015f, 0.035f, 0.075f, 1f);
    private static readonly string[] LayerObjectPaths =
    {
        "Lymphatic/Layer - Lymphatic System",
        "Circulatory/Layer - Circulatory System",
        "Digestive/Layer - Digestive System",
        "Respiratory/Layer- Respiratory System"
    };

    private sealed class RendererMaterialSnapshot
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }

    private readonly List<RendererMaterialSnapshot> rendererSnapshots = new List<RendererMaterialSnapshot>();
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private bool materialsApplied;

    private void Awake()
    {
        ApplyToRenderers();
    }

    private void OnDestroy()
    {
        for (int index = rendererSnapshots.Count - 1; index >= 0; index--)
        {
            RendererMaterialSnapshot snapshot = rendererSnapshots[index];
            if (snapshot.renderer != null)
                snapshot.renderer.sharedMaterials = snapshot.originalMaterials;
        }

        for (int index = 0; index < runtimeMaterials.Count; index++)
        {
            if (runtimeMaterials[index] != null)
                Destroy(runtimeMaterials[index]);

    
        }
    }
    /// <summary>Applies the runtime Toon Shader to instances of the Platelet drone static prefab.</summary>
    public static void ApplyToPlateletDrone(GameObject plateletDrone, string sourcePrefabName)
    {
        if (plateletDrone == null ||
            !string.Equals(sourcePrefabName, PlateletDronePrefabName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ToonMaterialSwapper swapper = plateletDrone.GetComponent<ToonMaterialSwapper>();
        if (swapper == null)
        {
            plateletDrone.AddComponent<ToonMaterialSwapper>();
        }
        else
        {
            swapper.ApplyToRenderers();
        }
    }
    /// <summary>Applies the project Toon Shader to a pathogen and tints its runtime toon materials.</summary>
    public static void ApplyToPathogen(GameObject pathogen, Color baseColor)
    {
        if (pathogen == null)
            return;

        ToonMaterialSwapper swapper = pathogen.GetComponent<ToonMaterialSwapper>();
        if (swapper == null)
            swapper = pathogen.AddComponent<ToonMaterialSwapper>();

        swapper.ApplyPathogenTint(baseColor);
    }

    private void ApplyPathogenTint(Color baseColor)
    {
        ApplyToRenderers();
        foreach (Material material in runtimeMaterials)
        {
            if (material == null)
                continue;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);
            if (material.HasProperty("_1st_ShadeColor"))
                material.SetColor("_1st_ShadeColor", GetFirstShadeColor(baseColor));
            if (material.HasProperty("_2nd_ShadeColor"))
                material.SetColor("_2nd_ShadeColor", GetSecondShadeColor(baseColor));
        }
    }



    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyToSystemLayerMeshes()
    {
        GameObject layersRoot = GameObject.Find(LayersRootName);
        if (layersRoot == null)
        {
            Debug.LogWarning("[ToonMaterialSwapper] System Layers root was not found.");
            return;
        }

        Shader toonShader = Shader.Find(ToonShaderName);
        if (toonShader == null)
        {
            Debug.LogError($"[ToonMaterialSwapper] Shader '{ToonShaderName}' was not found.");
            return;
        }

        foreach (string layerObjectPath in LayerObjectPaths)
        {
            Transform layerTransform = layersRoot.transform.Find(layerObjectPath);
            if (layerTransform == null)
            {
                Debug.LogWarning($"[ToonMaterialSwapper] Layer mesh '{layerObjectPath}' was not found.");
                continue;
            }

            ToonMaterialSwapper swapper = layerTransform.GetComponent<ToonMaterialSwapper>();
            if (swapper == null)
                swapper = layerTransform.gameObject.AddComponent<ToonMaterialSwapper>();

            swapper.ApplyToRenderers(toonShader);
        }
    }

    private void ApplyToRenderers()
    {
        Shader toonShader = Shader.Find(ToonShaderName);
        if (toonShader == null)
        {
            Debug.LogError($"[ToonMaterialSwapper] Shader '{ToonShaderName}' was not found.", this);
            return;
        }

        ApplyToRenderers(toonShader);
    }

    private void ApplyToRenderers(Shader toonShader)
    {
        if (materialsApplied || toonShader == null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] originalMaterials = renderer.sharedMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
                continue;

            Material[] toonMaterials = new Material[originalMaterials.Length];
            bool hasReplacement = false;
            for (int materialIndex = 0; materialIndex < originalMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = originalMaterials[materialIndex];
                if (sourceMaterial == null)
                    continue;

                if (IsSourceTransparent(sourceMaterial, GetSourceBaseColor(sourceMaterial)))
                {
                    toonMaterials[materialIndex] = sourceMaterial;
                    continue;
                }

                Material toonMaterial = CreateToonMaterial(toonShader, sourceMaterial);
                if (toonMaterial == null)
                    continue;

                toonMaterials[materialIndex] = toonMaterial;
                runtimeMaterials.Add(toonMaterial);
                hasReplacement = true;
            }

            if (!hasReplacement)
                continue;

            rendererSnapshots.Add(new RendererMaterialSnapshot
            {
                renderer = renderer,
                originalMaterials = originalMaterials
            });
            renderer.sharedMaterials = toonMaterials;
        }

        materialsApplied = true;
    }

    private Material CreateToonMaterial(Shader toonShader, Material sourceMaterial)
    {
        Color baseColor = GetSourceBaseColor(sourceMaterial);
        bool isTransparent = IsSourceTransparent(sourceMaterial, baseColor);
        Material toonMaterial = new Material(toonShader)
        {
            name = sourceMaterial.name + " (Toon Runtime)",
            renderQueue = isTransparent ? TransparentRenderQueue : OpaqueRenderQueue
        };

        toonMaterial.SetColor("_BaseColor", baseColor);
        toonMaterial.SetColor("_Color", baseColor);
        toonMaterial.SetColor("_1st_ShadeColor", GetFirstShadeColor(baseColor));
        toonMaterial.SetColor("_2nd_ShadeColor", GetSecondShadeColor(baseColor));
        toonMaterial.SetFloat("_BaseColor_Step", BaseColorStep);
        toonMaterial.SetFloat("_BaseShade_Feather", BaseShadeFeather);
        toonMaterial.SetFloat("_1st_ShadeColor_Step", FirstShadeStep);
        toonMaterial.SetFloat("_1st_ShadeColor_Feather", FirstShadeFeather);
        toonMaterial.SetFloat("_2nd_ShadeColor_Step", SecondShadeStep);
        toonMaterial.SetFloat("_2nd_ShadeColor_Feather", SecondShadeFeather);
        toonMaterial.SetFloat("_RimLight", 1f);
        toonMaterial.SetColor("_RimLightColor", RimLightColor);
        toonMaterial.SetFloat("_Is_LightColor_RimLight", 0f);
        toonMaterial.SetFloat("_RimLight_Power", RimLightPower);
        toonMaterial.SetFloat("_Outline_Width", OutlineWidth);
        toonMaterial.SetColor("_Outline_Color", OutlineColor);
        toonMaterial.SetFloat("_Is_BlendBaseColor", 0f);
        toonMaterial.SetFloat("_Is_LightColor_Outline", 0f);
        toonMaterial.SetFloat("_Is_OutlineTex", 0f);
        toonMaterial.SetFloat("_TransparentEnabled", isTransparent ? 1f : 0f);
        toonMaterial.SetFloat("_ZWriteMode", isTransparent ? 0f : 1f);
        toonMaterial.SetOverrideTag("RenderType", isTransparent ? "Transparent" : "Opaque");
        toonMaterial.SetOverrideTag("IgnoreProjection", isTransparent ? "True" : "False");

        CopyBaseTexture(sourceMaterial, toonMaterial);
        return toonMaterial;
    }

    private static Color GetSourceBaseColor(Material sourceMaterial)
    {
        if (sourceMaterial.HasProperty("_BaseColor"))
            return sourceMaterial.GetColor("_BaseColor");
        if (sourceMaterial.HasProperty("_Color"))
            return sourceMaterial.GetColor("_Color");
        return Color.white;
    }

    private static bool IsSourceTransparent(Material sourceMaterial, Color baseColor)
    {
        bool surfaceIsTransparent = sourceMaterial.HasProperty("_Surface") && sourceMaterial.GetFloat("_Surface") > 0.5f;
        bool alphaIsTransparent = baseColor.a < OpaqueAlphaThreshold;
        bool queueIsTransparent = sourceMaterial.renderQueue >= TransparentRenderQueue;
        return surfaceIsTransparent || alphaIsTransparent || queueIsTransparent;
    }

    private static Color GetFirstShadeColor(Color baseColor)
    {
        return new Color(
            Mathf.Clamp01(baseColor.r * 0.58f + 0.015f),
            Mathf.Clamp01(baseColor.g * 0.58f + 0.035f),
            Mathf.Clamp01(baseColor.b * 0.65f + 0.07f),
            1f);
    }

    private static Color GetSecondShadeColor(Color baseColor)
    {
        return new Color(
            Mathf.Clamp01(baseColor.r * 0.32f + 0.01f),
            Mathf.Clamp01(baseColor.g * 0.34f + 0.02f),
            Mathf.Clamp01(baseColor.b * 0.4f + 0.04f),
            1f);
    }

    private static void CopyBaseTexture(Material sourceMaterial, Material toonMaterial)
    {
        if (sourceMaterial.HasProperty("_BaseMap"))
        {
            Texture baseMap = sourceMaterial.GetTexture("_BaseMap");
            if (baseMap != null)
            {
                toonMaterial.SetTexture("_BaseMap", baseMap);
                toonMaterial.SetTexture("_MainTex", baseMap);
                toonMaterial.SetTextureScale("_MainTex", sourceMaterial.GetTextureScale("_BaseMap"));
                toonMaterial.SetTextureOffset("_MainTex", sourceMaterial.GetTextureOffset("_BaseMap"));
            }
        }
        else if (sourceMaterial.HasProperty("_MainTex"))
        {
            Texture mainTexture = sourceMaterial.GetTexture("_MainTex");
            if (mainTexture != null)
            {
                toonMaterial.SetTexture("_MainTex", mainTexture);
                toonMaterial.SetTextureScale("_MainTex", sourceMaterial.GetTextureScale("_MainTex"));
                toonMaterial.SetTextureOffset("_MainTex", sourceMaterial.GetTextureOffset("_MainTex"));
            }
        }
    }
}
