using UnityEngine;
using UnityEngine.Rendering;

public class DayNightCycle : MonoBehaviour
{
    [Header("References")]
    public Light sun;
    public Material skyboxMaterial;

    [Header("Time")]
    [Min(0.1f)]
    public float cycleDuration = 30f;
    [Range(0f, 1f)]
    public float startTime01 = 0.25f;
    public bool playOnStart = true;

    [Header("Sun")]
    public Vector3 sunRotationOffset = new Vector3(-90f, 0f, 0f);
    public Gradient sunColor;
    public AnimationCurve sunIntensityCurve = new AnimationCurve(
        new Keyframe(0f, 0.18f),
        new Keyframe(0.08f, 0.45f),
        new Keyframe(0.25f, 1f),
        new Keyframe(0.42f, 0.6f),
        new Keyframe(0.5f, 0.18f),
        new Keyframe(0.58f, 0.04f),
        new Keyframe(0.75f, 0f),
        new Keyframe(0.92f, 0.04f),
        new Keyframe(1f, 0.18f));

    [Header("Day")]
    public Color dayZenith = new Color(0.42f, 0.72f, 0.95f);
    public Color dayHorizon = new Color(0.78f, 0.9f, 1f);
    public Color dayGround = new Color(0.66f, 0.76f, 0.82f);
    public float dayIntensity = 0.95f;
    [Range(0.1f, 5f)]
    public float dayAtmosphereThickness = 0.85f;
    [Range(0f, 2f)]
    public float dayHorizonFogDensity = 0.8f;

    [Header("Night")]
    public Color nightZenith  = new Color(0.012f, 0.025f, 0.075f);
    public Color nightHorizon = new Color(0.07f, 0.095f, 0.17f);
    public Color nightGround  = new Color(0.04f, 0.06f, 0.12f);
    public float nightIntensity = 0.06f;
    [Range(0.1f, 5f)]
    public float nightAtmosphereThickness = 1.35f;
    [Range(0f, 2f)]
    public float nightHorizonFogDensity = 0.05f;

    [Header("Sky Objects")]
    [Range(0f, 1f)]
    public float starsThreshold = 0.6f;
    [Range(0f, 1f)]
    public float moonThreshold = 0.6f;
    [Range(0f, 1f)]
    public float daySunSize = 0.045f;
    [Range(0f, 1f)]
    public float nightSunSize = 0f;
    [Range(0f, 1f)]
    public float dayMoonSize = 0f;
    [Range(0f, 1f)]
    public float nightMoonSize = 0.15f;

    [Header("Ambient")]
    public bool controlAmbientLight = true;
    public Color dayAmbient = new Color(0.64f, 0.75f, 0.82f);
    public Color nightAmbient = new Color(0.055f, 0.07f, 0.13f);

    [Header("Fog")]
    public bool controlFog = true;
    public FogMode fogMode = FogMode.ExponentialSquared;
    public Color dayFog = new Color(0.78f, 0.88f, 0.96f);
    public Color nightFog = new Color(0.025f, 0.035f, 0.08f);
    [Range(0f, 0.1f)]
    public float dayFogDensity = 0.008f;
    [Range(0f, 0.1f)]
    public float nightFogDensity = 0.018f;

    private static readonly int ZenithColorId = Shader.PropertyToID("_ZenithColor");
    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
    private static readonly int AtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int HorizonFogDensityId = Shader.PropertyToID("_HorizonFogDensity");
    private static readonly int HorizonFogColorId = Shader.PropertyToID("_HorizonFogColor");
    private static readonly int EnableStarsId = Shader.PropertyToID("_EnableStars");
    private static readonly int EnableMoonId = Shader.PropertyToID("_EnableMoon");
    private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
    private static readonly int MoonSizeId = Shader.PropertyToID("_MoonSize");

    private float timer;
    private Material runtimeSkyboxMaterial;

    private void Reset()
    {
        sun = RenderSettings.sun;
        skyboxMaterial = RenderSettings.skybox;
        SetupDefaultSunColor();
        SetupDefaultSunIntensityCurve();
    }

    private void Awake()
    {
        SetupDefaultSunColor();
        SetupDefaultSunIntensityCurve();

        if (sun == null)
        {
            sun = RenderSettings.sun;
        }
        else
        {
            RenderSettings.sun = sun;
        }

        if (skyboxMaterial == null)
        {
            skyboxMaterial = RenderSettings.skybox;
        }

        if (skyboxMaterial != null)
        {
            runtimeSkyboxMaterial = new Material(skyboxMaterial);
            RenderSettings.skybox = runtimeSkyboxMaterial;
        }

        timer = Mathf.Clamp01(startTime01) * cycleDuration;
        ApplyCycle(timer / cycleDuration);
    }

    private void Update()
    {
        if (!playOnStart)
        {
            return;
        }

        timer = Mathf.Repeat(timer + Time.deltaTime, cycleDuration);
        ApplyCycle(timer / cycleDuration);
    }

    private void OnValidate()
    {
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        starsThreshold = Mathf.Clamp01(starsThreshold);
        moonThreshold = Mathf.Clamp01(moonThreshold);

        SetupDefaultSunColor();
        SetupDefaultSunIntensityCurve();

        if (sun == null)
        {
            sun = RenderSettings.sun;
        }

        if (skyboxMaterial == null)
        {
            skyboxMaterial = RenderSettings.skybox;
        }

        if (!Application.isPlaying)
        {
            ApplyCycle(startTime01);
            return;
        }

        ApplyCycle(timer / cycleDuration);
    }

    private void ApplyCycle(float time01)
    {
        float normalizedTime = Mathf.Repeat(time01, 1f);
        float daylight = Mathf.Clamp01(Mathf.Sin(normalizedTime * Mathf.PI * 2f));
        float nightBlend = 1f - daylight;
        Material targetSkybox = runtimeSkyboxMaterial != null ? runtimeSkyboxMaterial : skyboxMaterial;

        ApplySun(normalizedTime);
        ApplySkybox(targetSkybox, nightBlend);
        ApplyAmbient(normalizedTime, nightBlend);

        if (controlFog)
        {
            ApplyFog(normalizedTime, nightBlend);
        }
    }

    private void ApplyFog(float time01, float nightBlend)
    {
        Color fogColor = Color.Lerp(dayFog, nightFog, nightBlend);
        float fogDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, GetDarkness01(time01));

        RenderSettings.fog = true;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    private void ApplySun(float time01)
    {
        if (sun == null)
        {
            return;
        }

        float angle = time01 * 360f;
        sun.transform.rotation = Quaternion.Euler(sunRotationOffset + new Vector3(angle, 0f, 0f));
        sun.intensity = Mathf.Lerp(nightIntensity, dayIntensity, sunIntensityCurve.Evaluate(time01));
        sun.color = sunColor.Evaluate(time01);
    }

    private void ApplySkybox(Material targetSkybox, float nightBlend)
    {
        if (targetSkybox == null)
        {
            return;
        }

        Color zenith = Color.Lerp(dayZenith, nightZenith, nightBlend);
        Color horizon = Color.Lerp(dayHorizon, nightHorizon, nightBlend);
        Color ground = Color.Lerp(dayGround, nightGround, nightBlend);
        targetSkybox.SetColor(ZenithColorId, zenith);
        targetSkybox.SetColor(HorizonColorId, horizon);
        targetSkybox.SetColor(GroundColorId, ground);
        targetSkybox.SetFloat(AtmosphereThicknessId, Mathf.Lerp(dayAtmosphereThickness, nightAtmosphereThickness, nightBlend));
        targetSkybox.SetFloat(HorizonFogDensityId, Mathf.Lerp(dayHorizonFogDensity, nightHorizonFogDensity, nightBlend));
        targetSkybox.SetColor(HorizonFogColorId, Color.Lerp(dayFog, nightFog, nightBlend));

        float skyObjectNight = Mathf.Clamp01(nightBlend);
        targetSkybox.SetFloat(EnableStarsId, skyObjectNight >= starsThreshold ? 1f : 0f);
        targetSkybox.SetFloat(EnableMoonId, skyObjectNight >= moonThreshold ? 1f : 0f);
        targetSkybox.SetFloat(SunSizeId, Mathf.Lerp(daySunSize, nightSunSize, nightBlend));
        targetSkybox.SetFloat(MoonSizeId, Mathf.Lerp(dayMoonSize, nightMoonSize, nightBlend));
    }

    private void ApplyAmbient(float time01, float nightBlend)
    {
        if (!controlAmbientLight)
        {
            return;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        Color ambient = Color.Lerp(dayAmbient, nightAmbient, nightBlend);
        float brightness = Mathf.Lerp(0.35f, 1f, sunIntensityCurve.Evaluate(time01));
        RenderSettings.ambientLight = ambient * brightness;
    }

    private float GetDarkness01(float time01)
    {
        return 1f - sunIntensityCurve.Evaluate(time01);
    }

    private void SetupDefaultSunColor()
    {
        if (sunColor != null && sunColor.colorKeys.Length > 0)
        {
            return;
        }

        sunColor = CreateDefaultSunColor();
    }

    private void SetupDefaultSunIntensityCurve()
    {
        if (sunIntensityCurve != null && sunIntensityCurve.length > 0)
        {
            return;
        }

        sunIntensityCurve = CreateDefaultSunIntensityCurve();
    }

    private static Gradient CreateDefaultSunColor()
    {
        return new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(1f, 0.74f, 0.52f), 0f),
                new GradientColorKey(new Color(1f, 0.82f, 0.62f), 0.08f),
                new GradientColorKey(new Color(0.94f, 0.98f, 1f), 0.25f),
                new GradientColorKey(new Color(1f, 0.84f, 0.64f), 0.42f),
                new GradientColorKey(new Color(1f, 0.7f, 0.5f), 0.5f),
                new GradientColorKey(new Color(0.68f, 0.75f, 1f), 0.75f),
                new GradientColorKey(new Color(1f, 0.74f, 0.52f), 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };
    }

    private static AnimationCurve CreateDefaultSunIntensityCurve()
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.18f),
            new Keyframe(0.08f, 0.45f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.42f, 0.6f),
            new Keyframe(0.5f, 0.18f),
            new Keyframe(0.58f, 0.04f),
            new Keyframe(0.75f, 0f),
            new Keyframe(0.92f, 0.04f),
            new Keyframe(1f, 0.18f));

        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        return curve;
    }
}
