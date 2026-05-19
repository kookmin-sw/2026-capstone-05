using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BunkerSpotLight : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Light targetLight;
    [SerializeField] private bool flicker;

    private const float OffChance = 0.25f;
    private const float DimChance = 0.45f;
    private const float DimIntensityMin = 0.25f;
    private const float DimIntensityMax = 0.75f;
    private const float FlickerIntervalMin = 0.04f;
    private const float FlickerIntervalMax = 0.35f;

    private float baseIntensity;
    private bool baseEnabled;
    private float nextFlickerTime;
    private Renderer[] targetRenderers;
    private readonly List<EmissionTarget> emissionTargets = new List<EmissionTarget>();
    private MaterialPropertyBlock propertyBlock;

    private struct EmissionTarget
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Color BaseColor;
    }

    private void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>(true);
        }

        if (targetLight == null)
        {
            enabled = false;
            return;
        }

        baseIntensity = targetLight.intensity;
        baseEnabled = targetLight.enabled;
        CaptureEmission();
        ScheduleNextFlicker();
    }

    private void OnDisable()
    {
        if (targetLight == null)
        {
            return;
        }

        targetLight.enabled = baseEnabled;
        targetLight.intensity = baseIntensity;
        ApplyEmission(1f);
    }

    private void Update()
    {
        if (targetLight == null)
        {
            return;
        }

        if (!flicker)
        {
            ApplyFlicker(1f);
            return;
        }

        if (Time.time < nextFlickerTime)
        {
            return;
        }

        ApplyFlicker(PickFlickerMultiplier());
        ScheduleNextFlicker();
    }

    private void ApplyFlicker(float multiplier)
    {
        targetLight.enabled = baseEnabled && multiplier > 0.001f;
        targetLight.intensity = baseIntensity * multiplier;
        ApplyEmission(multiplier);
    }

    private void CaptureEmission()
    {
        emissionTargets.Clear();
        targetRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !material.HasProperty(EmissionColorId))
                {
                    continue;
                }

                emissionTargets.Add(new EmissionTarget
                {
                    Renderer = targetRenderer,
                    MaterialIndex = i,
                    BaseColor = material.GetColor(EmissionColorId)
                });
            }
        }
    }

    private void ApplyEmission(float multiplier)
    {
        if (emissionTargets.Count == 0)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        foreach (EmissionTarget emissionTarget in emissionTargets)
        {
            if (emissionTarget.Renderer == null)
            {
                continue;
            }

            emissionTarget.Renderer.GetPropertyBlock(propertyBlock, emissionTarget.MaterialIndex);
            propertyBlock.SetColor(EmissionColorId, emissionTarget.BaseColor * multiplier);
            emissionTarget.Renderer.SetPropertyBlock(propertyBlock, emissionTarget.MaterialIndex);
        }
    }

    private float PickFlickerMultiplier()
    {
        float roll = Random.value;
        if (roll < OffChance)
        {
            return 0f;
        }

        if (roll < OffChance + DimChance)
        {
            return Random.Range(DimIntensityMin, DimIntensityMax);
        }

        return 1f;
    }

    private void ScheduleNextFlicker()
    {
        nextFlickerTime = Time.time + Random.Range(FlickerIntervalMin, FlickerIntervalMax);
    }
}
