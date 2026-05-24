using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class InteractableFocusHighlighter : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int SweepColorId = Shader.PropertyToID("_SweepColor");
    private static readonly int PeakIntensityId = Shader.PropertyToID("_PeakIntensity");
    private static readonly int StartTimeId = Shader.PropertyToID("_StartTime");
    private static readonly int DurationId = Shader.PropertyToID("_Duration");
    private static readonly int SweepOriginId = Shader.PropertyToID("_SweepOrigin");
    private static readonly int SweepDirectionId = Shader.PropertyToID("_SweepDirection");
    private static readonly int SweepLengthId = Shader.PropertyToID("_SweepLength");
    private static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidth");
    private static readonly int SweepSoftnessId = Shader.PropertyToID("_SweepSoftness");
    private const string GlintSweepShaderName = "NUNBORA/Interactable Glint Sweep";
    private const string GlintSweepMaterialResourcePath = "Materials/InteractableGlintSweep";

    [Header("Renderer Search")]
    [SerializeField] private bool includeChildren = true;

    [Header("Highlight")]
    [SerializeField, ColorUsage(false, true)] private Color emissionColor = new Color(1f, 0.58f, 0.18f);
    [SerializeField, Min(0f)] private float baseIntensity = 0.15f;
    [SerializeField, Min(0f)] private float pulseIntensity = 0.2f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;

    [Header("Proximity Glint Sweep")]
    [SerializeField] private Vector3 sweepDirection = new Vector3(1f, 0.25f, 0.15f);
    [SerializeField, Min(0.01f)] private float sweepWidth = 0.18f;
    [SerializeField, Min(0.01f)] private float sweepSoftness = 0.25f;

    private readonly List<MaterialState> materialStates = new List<MaterialState>();
    private readonly List<GlintOverlayState> glintOverlays = new List<GlintOverlayState>();
    private Renderer[] renderers;
    private Material glintMaterialTemplate;
    private bool ownsGlintMaterialTemplate;
    private bool isHighlighted;
    private HighlightMode activeMode;
    private Color activeEmissionColor;
    private float activeBaseIntensity;
    private float activePulseIntensity;
    private float activePulseSpeed;
    private float proximityGlintStartTime;
    private float proximityGlintDuration;
    private float proximityGlintPeakIntensity;

    private void Awake()
    {
        CacheRenderers();
    }

    private void Update()
    {
        if (!isHighlighted)
        {
            return;
        }

        if (activeMode == HighlightMode.ProximityGlint)
        {
            UpdateProximityGlint();
            return;
        }

        float pulse = activePulseIntensity <= 0f
            ? 0f
            : (Mathf.Sin(Time.time * activePulseSpeed) * 0.5f + 0.5f) * activePulseIntensity;
        ApplyEmission(activeBaseIntensity + pulse);
    }

    private void OnDisable()
    {
        HideFocus();
    }

    private void OnDestroy()
    {
        HideFocus();
        if (ownsGlintMaterialTemplate && glintMaterialTemplate != null)
        {
            Destroy(glintMaterialTemplate);
        }
    }

    public void ShowFocus()
    {
        ShowWithSettings(HighlightMode.Focus, emissionColor, baseIntensity, pulseIntensity, pulseSpeed);
    }

    public void ShowProximityHint(Color color, float baseIntensity, float pulseIntensity, float pulseSpeed)
    {
        PlayProximityGlint(color, baseIntensity + pulseIntensity, 0.45f, sweepDirection, sweepWidth, sweepSoftness);
    }

    public void PlayProximityGlint(
        Color color,
        float peakIntensity,
        float duration,
        Vector3 direction,
        float width,
        float softness)
    {
        if (activeMode == HighlightMode.Focus)
        {
            return;
        }

        TryShowSweepGlint(color, peakIntensity, duration, direction, width, softness);

        proximityGlintStartTime = Time.time;
        proximityGlintDuration = Mathf.Max(0.05f, duration);
        proximityGlintPeakIntensity = Mathf.Max(0f, peakIntensity);
    }

    public void HideProximityHint()
    {
        if (activeMode == HighlightMode.ProximityGlint)
        {
            HideFocus();
        }
    }

    public void Configure(Color color, float baseIntensity, float pulseIntensity, float pulseSpeed)
    {
        emissionColor = color;
        this.baseIntensity = Mathf.Max(0f, baseIntensity);
        this.pulseIntensity = Mathf.Max(0f, pulseIntensity);
        this.pulseSpeed = Mathf.Max(0f, pulseSpeed);

        if (isHighlighted)
        {
            ShowFocus();
        }
    }

    public void HideFocus()
    {
        if (!isHighlighted && materialStates.Count == 0 && glintOverlays.Count == 0)
        {
            return;
        }

        for (int i = 0; i < materialStates.Count; i++)
        {
            materialStates[i].Restore();
        }

        DestroyGlintOverlays();
        materialStates.Clear();
        isHighlighted = false;
        activeMode = HighlightMode.None;
    }

    private void UpdateProximityGlint()
    {
        float normalizedTime = (Time.time - proximityGlintStartTime) / proximityGlintDuration;
        if (normalizedTime >= 1f)
        {
            HideFocus();
            return;
        }

        float arc = Mathf.Sin(normalizedTime * Mathf.PI);
        float intensity = proximityGlintPeakIntensity * Mathf.SmoothStep(0f, 1f, arc);
        ApplyEmission(intensity);
    }

    private bool TryShowSweepGlint(
        Color color,
        float peakIntensity,
        float duration,
        Vector3 direction,
        float width,
        float softness)
    {
        if (!TryGetGlintMaterialTemplate(out Material template))
        {
            return false;
        }

        DestroyGlintOverlays();
        CacheRenderers();
        if (!TryGetRenderersBounds(out Bounds bounds))
        {
            return false;
        }

        Vector3 sweepDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.right;
        float halfLength = Mathf.Max(0.1f, Vector3.Dot(bounds.extents, Abs(sweepDirection)));
        Vector3 origin = bounds.center - sweepDirection * halfLength;
        float length = halfLength * 2f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer sourceRenderer = renderers[i];
            if (!IsValidRenderer(sourceRenderer) || sourceRenderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (sourceRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                CreateSkinnedGlintOverlay(
                    skinnedMeshRenderer,
                    template,
                    color,
                    peakIntensity,
                    duration,
                    origin,
                    sweepDirection,
                    length,
                    width,
                    softness);
                continue;
            }

            MeshFilter meshFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            CreateMeshGlintOverlay(
                sourceRenderer,
                meshFilter,
                template,
                color,
                peakIntensity,
                duration,
                origin,
                sweepDirection,
                length,
                width,
                softness);
        }

        activeMode = HighlightMode.ProximityGlint;
        isHighlighted = glintOverlays.Count > 0;
        return isHighlighted;
    }

    private void CreateMeshGlintOverlay(
        Renderer sourceRenderer,
        MeshFilter sourceMeshFilter,
        Material template,
        Color color,
        float peakIntensity,
        float duration,
        Vector3 origin,
        Vector3 direction,
        float length,
        float width,
        float softness)
    {
        GameObject overlayObject = CreateOverlayObject(sourceRenderer);
        MeshFilter overlayMeshFilter = overlayObject.AddComponent<MeshFilter>();
        overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        MeshRenderer overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
        ConfigureOverlayRenderer(sourceRenderer, overlayRenderer, template, color, peakIntensity, duration, origin, direction, length, width, softness);
        glintOverlays.Add(new GlintOverlayState(overlayObject, overlayRenderer.sharedMaterial));
    }

    private void CreateSkinnedGlintOverlay(
        SkinnedMeshRenderer sourceRenderer,
        Material template,
        Color color,
        float peakIntensity,
        float duration,
        Vector3 origin,
        Vector3 direction,
        float length,
        float width,
        float softness)
    {
        if (sourceRenderer.sharedMesh == null)
        {
            return;
        }

        GameObject overlayObject = CreateOverlayObject(sourceRenderer);
        SkinnedMeshRenderer overlayRenderer = overlayObject.AddComponent<SkinnedMeshRenderer>();
        overlayRenderer.sharedMesh = sourceRenderer.sharedMesh;
        overlayRenderer.bones = sourceRenderer.bones;
        overlayRenderer.rootBone = sourceRenderer.rootBone;
        overlayRenderer.localBounds = sourceRenderer.localBounds;
        overlayRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        ConfigureOverlayRenderer(sourceRenderer, overlayRenderer, template, color, peakIntensity, duration, origin, direction, length, width, softness);
        glintOverlays.Add(new GlintOverlayState(overlayObject, overlayRenderer.sharedMaterial));
    }

    private GameObject CreateOverlayObject(Renderer sourceRenderer)
    {
        GameObject overlayObject = new GameObject("Interactable Glint Sweep");
        overlayObject.hideFlags = HideFlags.DontSave;
        overlayObject.layer = sourceRenderer.gameObject.layer;
        Transform overlayTransform = overlayObject.transform;
        overlayTransform.SetParent(sourceRenderer.transform, false);
        overlayTransform.localPosition = Vector3.zero;
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = Vector3.one;
        return overlayObject;
    }

    private void ConfigureOverlayRenderer(
        Renderer sourceRenderer,
        Renderer overlayRenderer,
        Material template,
        Color color,
        float peakIntensity,
        float duration,
        Vector3 origin,
        Vector3 direction,
        float length,
        float width,
        float softness)
    {
        Material material = Instantiate(template);
        material.SetColor(SweepColorId, color);
        material.SetFloat(PeakIntensityId, Mathf.Max(0f, peakIntensity));
        material.SetFloat(StartTimeId, Time.time);
        material.SetFloat(DurationId, Mathf.Max(0.05f, duration));
        material.SetVector(SweepOriginId, origin);
        material.SetVector(SweepDirectionId, direction);
        float resolvedLength = Mathf.Max(0.01f, length);
        float resolvedWidth = Mathf.Clamp(width, 0.01f, resolvedLength * 0.18f);
        float resolvedSoftness = Mathf.Clamp(softness, 0.01f, resolvedLength * 0.25f);

        material.SetFloat(SweepLengthId, resolvedLength);
        material.SetFloat(SweepWidthId, resolvedWidth);
        material.SetFloat(SweepSoftnessId, resolvedSoftness);

        overlayRenderer.sharedMaterial = material;
        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
        overlayRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
        overlayRenderer.probeAnchor = sourceRenderer.probeAnchor;
        overlayRenderer.enabled = sourceRenderer.enabled;
    }

    private bool TryGetGlintMaterialTemplate(out Material template)
    {
        if (glintMaterialTemplate != null)
        {
            template = glintMaterialTemplate;
            return true;
        }

        Material resourceMaterial = Resources.Load<Material>(GlintSweepMaterialResourcePath);
        if (resourceMaterial != null)
        {
            glintMaterialTemplate = resourceMaterial;
            ownsGlintMaterialTemplate = false;
            template = glintMaterialTemplate;
            return true;
        }

        Shader shader = Shader.Find(GlintSweepShaderName);
        if (shader == null)
        {
            template = null;
            return false;
        }

        glintMaterialTemplate = new Material(shader)
        {
            name = "Interactable Glint Sweep (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        ownsGlintMaterialTemplate = true;
        template = glintMaterialTemplate;
        return true;
    }

    private bool TryGetRenderersBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (!IsValidRenderer(targetRenderer) || targetRenderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private void DestroyGlintOverlays()
    {
        for (int i = 0; i < glintOverlays.Count; i++)
        {
            glintOverlays[i].Destroy();
        }

        glintOverlays.Clear();
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void ShowWithSettings(
        HighlightMode mode,
        Color color,
        float baseIntensity,
        float pulseIntensity,
        float pulseSpeed)
    {
        bool switchingFromGlint = activeMode == HighlightMode.ProximityGlint && mode != HighlightMode.ProximityGlint;
        if (mode != HighlightMode.ProximityGlint)
        {
            DestroyGlintOverlays();
        }

        if (!isHighlighted || switchingFromGlint || (mode == HighlightMode.Focus && materialStates.Count == 0))
        {
            CacheRenderers();
            CacheMaterialStates();
            isHighlighted = true;
        }

        activeMode = mode;
        activeEmissionColor = color;
        activeBaseIntensity = Mathf.Max(0f, baseIntensity);
        activePulseIntensity = Mathf.Max(0f, pulseIntensity);
        activePulseSpeed = Mathf.Max(0f, pulseSpeed);
        ApplyEmission(activeBaseIntensity);
    }

    private void CacheRenderers()
    {
        renderers = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();
    }

    private void CacheMaterialStates()
    {
        materialStates.Clear();

        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (!IsValidRenderer(targetRenderer))
            {
                continue;
            }

            Material[] materials;
            try
            {
                materials = targetRenderer.materials;
            }
            catch (MissingReferenceException)
            {
                continue;
            }

            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null || IsOutlineMaterial(material) || !material.HasProperty(EmissionColorId))
                {
                    continue;
                }

                materialStates.Add(new MaterialState(material));
            }
        }
    }

    private void ApplyEmission(float intensity)
    {
        Color targetColor = activeEmissionColor * Mathf.Max(0f, intensity);

        for (int i = 0; i < materialStates.Count; i++)
        {
            materialStates[i].Apply(targetColor);
        }
    }

    private static bool IsOutlineMaterial(Material material)
    {
        string materialName = material.name;
        return materialName.Contains("OutlineMask") || materialName.Contains("OutlineFill");
    }

    private static bool IsValidRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        try
        {
            return renderer.gameObject != null;
        }
        catch (MissingReferenceException)
        {
            return false;
        }
    }

    private enum HighlightMode
    {
        None,
        ProximityGlint,
        Focus
    }

    private readonly struct GlintOverlayState
    {
        private readonly GameObject gameObject;
        private readonly Material material;

        public GlintOverlayState(GameObject gameObject, Material material)
        {
            this.gameObject = gameObject;
            this.material = material;
        }

        public void Destroy()
        {
            if (material != null)
            {
                Object.Destroy(material);
            }

            if (gameObject != null)
            {
                Object.Destroy(gameObject);
            }
        }
    }

    private readonly struct MaterialState
    {
        private const string EmissionKeyword = "_EMISSION";

        private readonly Material material;
        private readonly Color originalEmissionColor;
        private readonly bool hadEmissionKeyword;
        private readonly MaterialGlobalIlluminationFlags originalGlobalIlluminationFlags;

        public MaterialState(Material material)
        {
            this.material = material;
            originalEmissionColor = material.GetColor(EmissionColorId);
            hadEmissionKeyword = material.IsKeywordEnabled(EmissionKeyword);
            originalGlobalIlluminationFlags = material.globalIlluminationFlags;
        }

        public void Apply(Color color)
        {
            if (material == null)
            {
                return;
            }

            material.EnableKeyword(EmissionKeyword);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor(EmissionColorId, color);
        }

        public void Restore()
        {
            if (material == null)
            {
                return;
            }

            material.SetColor(EmissionColorId, originalEmissionColor);
            material.globalIlluminationFlags = originalGlobalIlluminationFlags;

            if (hadEmissionKeyword)
            {
                material.EnableKeyword(EmissionKeyword);
            }
            else
            {
                material.DisableKeyword(EmissionKeyword);
            }
        }
    }
}
