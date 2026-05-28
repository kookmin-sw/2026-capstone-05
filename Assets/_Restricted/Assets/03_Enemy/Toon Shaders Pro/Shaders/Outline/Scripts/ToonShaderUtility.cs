using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.Rendering;
#endif

namespace ToonShadersPro.URP
{
    public static class ToonShaderUtility
    {
        // Get a reference to the forward renderer so we can check if an effect has been added.
        public static ScriptableRendererData GetForwardRenderer()
        {
            ScriptableRendererData[] rendererDataList =
                ((ScriptableRendererData[])typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(UniversalRenderPipeline.asset));
            int index = (int)typeof(UniversalRenderPipelineAsset)
                .GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(UniversalRenderPipeline.asset);

            return rendererDataList[index];
        }

        // Check the Renderer and make sure the specified effect is attached.
        public static bool CheckEffectEnabled<T>() where T : ScriptableRendererFeature
        {
            if (UniversalRenderPipeline.asset == null)
            {
                return false;
            }

            ScriptableRendererData forwardRenderer =
                ((ScriptableRendererData[])typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(UniversalRenderPipeline.asset))[0];

            foreach (ScriptableRendererFeature item in forwardRenderer.rendererFeatures)
            {
                if (item?.GetType() == typeof(T))
                {
                    return true;
                }
            }

            return false;
        }

        // Add a missing RendererFeature to the Renderer.
        public static void AddEffectToPipelineAsset<T>() where T : ScriptableRendererFeature
        {
#if UNITY_EDITOR
            if (UniversalRenderPipeline.asset == null)
            {
                Debug.LogError("No URP asset detected. Please make sure your project is using URP.");
                return;
            }

            var forwardRenderer = GetForwardRenderer();
            var effect = ScriptableRendererFeature.CreateInstance<T>();

            AssetDatabase.AddObjectToAsset(effect, forwardRenderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(effect, out var guid, out long localID);

            forwardRenderer.rendererFeatures.Add(effect);
            forwardRenderer.SetDirty();

            Debug.Log("Added " + typeof(T).ToString() + " effect to the active Renderer (" + forwardRenderer.name + ").", forwardRenderer);
#endif
        }
    }

    public enum PostProcessRenderPassEvent
    {
        BeforeURPPostProcessing,
        AfterURPPostProcessing
    }

    // Allow each volume settings object to track the render pass event.
    [Serializable]
    public sealed class RenderPassEventParameter : VolumeParameter<PostProcessRenderPassEvent>
    {
        public RenderPassEventParameter(PostProcessRenderPassEvent value, bool overrideState = false) : base(value, overrideState) { }
    }

    public enum OutlineType
    {
        NoOutlines,
        DepthNormalOutlines,
        HighQualityObjectMaskOutlines,
        PixelWidthObjectMaskOutlines,
        HullOutlines,
        DebugOutlineMask
    }

    // Allow each volume settings object to track the type of outline being used.
    [Serializable]
    public sealed class OutlineTypeParameter : VolumeParameter<OutlineType>
    {
        public OutlineTypeParameter(OutlineType value, bool overrideState = false) : base(value, overrideState) { }
    }

    public enum RenderQueueType
    {
        Opaque,
        Transparent,
        All
    }

    // Allow each volume settings object to track which type of object should have outlines.
    [Serializable]
    public sealed class RenderQueueParameter : VolumeParameter<RenderQueueType>
    {
        public RenderQueueParameter(RenderQueueType value, bool overrideState = false) : base(value, overrideState) { }
    }

    public enum MaskDrawingMode
    {
        [InspectorName("Per Object")] PerObject = 0,
        [InspectorName("Per Triangle")] PerTriangle = 1,
        [InspectorName("Merge All Masked Objects")] All = 2,
        [InspectorName("Vertex Colors")] VertexColors = 3,
        [InspectorName("Renderer Shader User Value (Unity 6.3+)")] RSUV = 4
    }

    // Allow the mask shader to swap between different mask-drawing approaches.
    [Serializable]
    public sealed class MaskDrawingParameter : VolumeParameter<MaskDrawingMode>
    {
        public MaskDrawingParameter(MaskDrawingMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    public enum LightModeType
    {
        [InspectorName("UniversalForwardOnly (incl. Toon shader)")] UniversalForwardOnly,
        [InspectorName("UniversalForward (Lit shaders)")] UniversalForward,
        [InspectorName("SRPDefaultUnlit (Unlit shaders)")] SRPDefaultUnlit,
        [InspectorName("UniversalGBuffer")] UniversalGBuffer,
        [InspectorName("Universal2D")] Universal2D,
        [InspectorName("ShadowCaster")] ShadowCaster,
        [InspectorName("DepthOnly")] DepthOnly,
        [InspectorName("DepthNormals")] DepthNormals,
        [InspectorName("DepthNormalsOnly")] DepthNormalsOnly,
        [InspectorName("Meta")] Meta
    }

    [Serializable]
    public sealed class LightModeTypeListParameter : VolumeParameter<List<LightModeType>>
    {
        public LightModeTypeListParameter(List<LightModeType> value, bool overrideState = false) : base(value, overrideState) { }
    }

#if UNITY_EDITOR
    [VolumeParameterDrawer(typeof(LightModeTypeListParameter))]
    sealed class LightModeTypeListParameterDrawer : VolumeParameterDrawer
    {
        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var value = parameter.value;

            if (value.propertyType != SerializedPropertyType.Generic)
            {
                return false;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(value, title, true);
            EditorGUI.indentLevel--;

            return true;
        }
    }
#endif

    public enum DrawSides
    {
        Inside,
        Outside,
        Both
    }

    [Serializable]
    public sealed class DrawSidesParameter : VolumeParameter<DrawSides>
    {
        public DrawSidesParameter(DrawSides value, bool overrideState = false) : base(value, overrideState) { }
    }

    /*
    [Serializable]
    public sealed class RendererListParameter : VolumeParameter<List<Renderer>>
    {
        public RendererListParameter(List<Renderer> value, bool overrideState = false) : base(value, overrideState) { }
    }

    [VolumeParameterDrawer(typeof(RendererListParameter))]
    sealed class RendererListParameterDrawer : VolumeParameterDrawer
    {
        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var value = parameter.value;

            if (value.propertyType != SerializedPropertyType.Generic)
            {
                return false;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(value, title, true);
            EditorGUI.indentLevel--;

            return true;
        }
    }
    */

    public static class ParameterTypeExtensions
    {
        public static RenderPassEvent Convert(this PostProcessRenderPassEvent renderPassEvent)
        {
            if (renderPassEvent == PostProcessRenderPassEvent.BeforeURPPostProcessing)
            {
                return RenderPassEvent.BeforeRenderingPostProcessing;
            }

            return RenderPassEvent.AfterRenderingPostProcessing;
        }

        public static RenderQueueRange Convert(this RenderQueueType renderQueueType)
        {
            if (renderQueueType == RenderQueueType.Opaque)
            {
                return RenderQueueRange.opaque;
            }
            else if (renderQueueType == RenderQueueType.Transparent)
            {
                return RenderQueueRange.transparent;
            }

            return RenderQueueRange.all;
        }

        public static string Convert(this LightModeType lightModeType)
        {
            switch(lightModeType)
            {
                case LightModeType.UniversalForwardOnly:
                    return "UniversalForwardOnly";
                case LightModeType.UniversalForward:
                    return "UniversalForward";
                case LightModeType.SRPDefaultUnlit:
                    return "SRPDefaultUnlit";
                case LightModeType.UniversalGBuffer:
                    return "UniversalGBuffer";
                case LightModeType.Universal2D:
                    return "Universal2D";
                case LightModeType.ShadowCaster:
                    return "ShadowCaster";
                case LightModeType.DepthOnly:
                    return "DepthOnly";
                case LightModeType.DepthNormals:
                    return "DepthNormals";
                case LightModeType.DepthNormalsOnly:
                    return "DepthNormalsOnly";
                case LightModeType.Meta:
                    return "Meta";
            }

            return "";
        }

        public static List<ShaderTagId> Convert(this List<LightModeType> lightModeTypes)
        {
            var lightModeList = new List<ShaderTagId>(lightModeTypes.Count);

            for(int i = 0; i < lightModeTypes.Count; ++i)
            {
                lightModeList.Add(new ShaderTagId(lightModeTypes[i].Convert()));
            }

            return lightModeList;
        }

        public static int Convert(this MaskDrawingMode mode)
        {
            return (int)mode;
        }
    }
}
