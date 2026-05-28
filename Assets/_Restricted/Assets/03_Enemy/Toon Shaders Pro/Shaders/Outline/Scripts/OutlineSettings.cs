using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ToonShadersPro.URP
{
    [System.Serializable, VolumeComponentMenu("Toon Shaders Pro/Outlines")]
    public sealed class OutlineSettings : VolumeComponent, IPostProcessComponent
    {
        public OutlineSettings()
        {
            displayName = "Outlines";
        }

        [Tooltip("Choose where to insert this pass in URP's render loop.\n" +
            "\nURP's internal post processing includes effects like bloom and color-correction, which may impact the appearance of the outlines.\n" +
            "\nFor example, with the Before setting, high-intensity HDR colors will be impacted by Bloom.")]
        public RenderPassEventParameter renderPassEvent = new RenderPassEventParameter(PostProcessRenderPassEvent.AfterURPPostProcessing);

        [Tooltip("Which outline-drawing algorithm to use.\n" + 
            "\n<b>No Outlines</b>" +
            "\n  Draws no outlines.\n" +
            "\n<b>Depth Normal Outlines</b>" +
            "\n  Detects small gradients in the color and depth-normal textures.\n" +
            "\n<b>High Quality Mask Outlines</b>" +
            "\n  Renders objects in specific layers to a mask and draws outlines along mask boundaries with error correction and thickness options.\n" +
            "\n<b>Pixel Width Mask Outlines</b>" +
            "\n  Also masks objects as before, but with more error cases and only pixel-width outlines.\n" +
            "\n<b>Hull Outlines</b>" +
            "\n  Renders all objects in specific layers with an inverted hull shader.\n" +
            "\n<b>Debug Outline Mask</b>" +
            "\n  Renders the mask texture used for outline detection.")]
        public OutlineTypeParameter outlineType = new OutlineTypeParameter(OutlineType.NoOutlines);

        [Tooltip("Color of the outlines.")]
        public ColorParameter outlineColor = new ColorParameter(Color.white, true, true, true);

        [Tooltip("Threshold for color-based edge detection.")]
        public ClampedFloatParameter colorSensitivity = new ClampedFloatParameter(0.1f, 0.0f, 1.0f);

        [Tooltip("Strength of color-based edges.")]
        public ClampedFloatParameter colorStrength = new ClampedFloatParameter(0.5f, 0.0f, 1.0f);

        [Tooltip("Threshold for depth-based edge detection.")]
        public ClampedFloatParameter depthSensitivity = new ClampedFloatParameter(0.01f, 0.0f, 1.0f);

        [Tooltip("Strength of depth-based edges.")]
        public ClampedFloatParameter depthStrength = new ClampedFloatParameter(0.75f, 0.0f, 1.0f);

        [Tooltip("Threshold for normal-based edge detection.")]
        public ClampedFloatParameter normalSensitivity = new ClampedFloatParameter(0.1f, 0.0f, 1.0f);

        [Tooltip("Strength of normal-based edges.")]
        public ClampedFloatParameter normalStrength = new ClampedFloatParameter(0.75f, 0.0f, 1.0f);

        [Tooltip("Pixels past this depth threshold will not be edge-detected.")]
        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.99f, 0.0f, 1.0f);

        [Tooltip("Apply to the following regular layers.")]
        public LayerMaskParameter objectMask = new LayerMaskParameter(0);

        [Tooltip("How should the masking pass detect unique maskable areas?\n" + 
            "\n<b>Per Object</b>" +
            "\n  Use the world-space position of the mesh origin as a seed value.\n" +
            "\n<b>Per Triangle</b>" +
            "\n  Same as per-object, but the triangle ID is added to the seed value.\n" +
            "\n<b>Merge All Masked Objects</b>" +
            "\n  Produces a binary mask; all masked objects use the same seed value.\n" +
            "\n<b>Vertex Colors</b>" +
            "\n  Use vertex colors baked into each mesh as a seed value.\n" +
            "\n<b>Renderer Shader User Value</b>" +
            "\n  In Unity 6.3+ only, use RSUV as a seed value. Search MeshRenderer.SetShaderUserValue(uint value) for more info.")]
        public MaskDrawingParameter maskDrawingMode = new MaskDrawingParameter(MaskDrawingMode.PerObject);

        [Tooltip("Which LightMode tags should be included in the mask?\n" +
            "\n  <b>UniversalForwardOnly</b> includes the base Toon shader." + 
            "\n  <b>UniversalForward</b> includes most lit shaders, including Shader Graphs." +
            "\n  <b>SRPDefaultUnlit</b> includes most unlit shaders, including Shader Graphs." +
            "\n  Most other settings will capture almost all shaders.\n" +
            "\n  <b>Warning</b>: Duplicated entries will increase resource usage with no benefit.")]
        public LightModeTypeListParameter lightModes = new LightModeTypeListParameter(new List<LightModeType>() { LightModeType.UniversalForwardOnly });

        [Tooltip("Should outlines be applied to opaque or transparent objects?" +
            "\nCurrently, the outlines only function with opaque objects.")]
        public RenderQueueParameter renderQueue = new RenderQueueParameter(RenderQueueType.Opaque);

        [Tooltip("If ticked, objects are drawn to the mask texture without considering depth (outlines will be visible through walls)." +
            "\nOpaque objects are drawn front-to back, and transparents are drawn back-to-front.\n" +
            "\nUsing this mode to draw several objects will likely result in strange artefacts.")]
        public BoolParameter maskIgnoreDepth = new BoolParameter(false);

        [Tooltip("Should masked outlines also use normal-based edge detection?")]
        public BoolParameter useDepthNormals = new BoolParameter(false);

        [Tooltip("Thickness of masked outlines.")]
        public ClampedIntParameter maskedOutlineThickness = new ClampedIntParameter(1, 1, 5);

        [Tooltip("How much additional smoothing to apply to outlines.")]
        public ClampedFloatParameter maskedOutlineSmoothing = new ClampedFloatParameter(1.0f, 0.1f, 5.0f);

        [Tooltip("Should outlines be drawn only inside, only outside, or on both sides of mask boundaries?")]
        public DrawSidesParameter outlineDrawSides = new DrawSidesParameter(DrawSides.Both);

        [Tooltip("Start to fade outlines out at this distance.")]
        public FloatParameter outlineFadeStart = new FloatParameter(25.0f);

        [Tooltip("End fading outlines out at this distance.")]
        public FloatParameter outlineFadeEnd = new FloatParameter(50.0f);

        [Tooltip("Thickness of hull outlines.")]
        public ClampedFloatParameter outlineThickness = new ClampedFloatParameter(0.02f, 0.0f, 0.2f);

        [Tooltip("Should hull outlines use transparency?")]
        public BoolParameter outlineTransparency = new BoolParameter(false);

        [Tooltip("Should hull outlines use diffuse lighting from the main light?")]
        public BoolParameter outlineLighting = new BoolParameter(false);

        [Tooltip("Should hull outline normal direction be flipped?")]
        public BoolParameter flipOutlineDirection = new BoolParameter(true);

        [Tooltip("Minimum lighting amount applied to hull outlines.")]
        public ClampedFloatParameter outlineMinLighting = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        [Tooltip("Should the outline effect use a Perlin noise offset? (Warning: this might be a bit expensive).")]
        public BoolParameter useNoiseOffsets = new BoolParameter(false);

        [Tooltip("Scale to use for the Perlin noise generator. Higher values result in more noise variation over UV space.")] 
        public ClampedFloatParameter noiseScale = new ClampedFloatParameter(1.0f, 1.0f, 500.0f);
        
        [Tooltip("Offset value to use for the Perlin noise generator. You can manually change this to tweak the appearance of the noise pattern.")]
        public FloatParameter noiseOffset = new FloatParameter(0.0f);

        [Tooltip("How strongly the noise values offset the outline UV calculations.")] 
        public ClampedFloatParameter noiseStrength = new ClampedFloatParameter(0.05f, 0.0f, 0.1f);
        
        /*
        [Tooltip("A custom list of renderers to include in outline rendering.")]
        public RendererListParameter overrideIncludeRenderers = new RendererListParameter(new List<Renderer>());

        [Tooltip("A custom list of renderers to exclude from outline rendering.")]
        public RendererListParameter overrideExcludeRenderers = new RendererListParameter(new List<Renderer>());
        */

        public bool IsActive()
        {
            return outlineType.value != OutlineType.NoOutlines && active;
        }

        public bool IsTileCompatible()
        {
            return false;
        }
    }
}
