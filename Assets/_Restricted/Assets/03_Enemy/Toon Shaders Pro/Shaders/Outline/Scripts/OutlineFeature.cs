using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RendererUtils;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ToonShadersPro.URP
{
    public class OutlineFeature : ScriptableRendererFeature
    {
        OutlineRenderPass pass;

        public override void Create()
        {
            pass = new OutlineRenderPass();
            name = "Outlines";
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var settings = VolumeManager.instance.stack.GetComponent<OutlineSettings>();

            if (settings != null && settings.IsActive())
            {
                if (settings.outlineType.value != OutlineType.NoOutlines)
                {
                    pass.ConfigureInput(ScriptableRenderPassInput.Depth);
                }

                if (settings.outlineType.value == OutlineType.DepthNormalOutlines)
                {
                    pass.ConfigureInput(ScriptableRenderPassInput.Normal);
                }

                renderer.EnqueuePass(pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            pass.Dispose();
            base.Dispose(disposing);
        }

        class OutlineRenderPass : ScriptableRenderPass
        {
            private Material material;
            private Material maskMaterial;
            private Material hullMaterial;

            private RTHandle tempTexHandle;
            private RTHandle maskedObjectsHandle;

            private ProfilingSampler maskProfilingSampler;
            private ProfilingSampler hullProfilingSampler;
            private ProfilingSampler outlineProfilingSampler;

            public OutlineRenderPass()
            {
                profilingSampler = new ProfilingSampler("Toon Shaders Pro - Outlines");
                maskProfilingSampler = new ProfilingSampler("TSP - Object Mask Pass");
                hullProfilingSampler = new ProfilingSampler("TSP - Hull Outline Pass");
                outlineProfilingSampler = new ProfilingSampler("TSP - Post Process Outline Pass");

#if UNITY_6000_0_OR_NEWER
                requiresIntermediateTexture = true;
#endif
            }

            private void CreateMaterial()
            {
                var shader = Shader.Find("Hidden/ToonShadersPro/URP/Outlines");

                if (shader == null)
                {
                    Debug.LogError("Cannot find shader: \"Hidden/ToonShadersPro/URP/Outlines\".");
                    return;
                }

                material = new Material(shader);

                shader = Shader.Find("Hidden/ToonShadersPro/URP/MaskObject");

                if (shader == null)
                {
                    Debug.LogError("Cannot find shader: \"Hidden/ToonShadersPro/URP/MaskObject\".");
                    return;
                }

                maskMaterial = new Material(shader);

                shader = Shader.Find("Hidden/ToonShadersPro/URP/HullOutlines");

                if (shader == null)
                {
                    Debug.LogError("Cannot find shader: \"Hidden/ToonShadersPro/URP/HullOutlines\".");
                    return;
                }

                hullMaterial = new Material(shader);
            }

            private static RenderTextureDescriptor GetCopyPassDescriptor(RenderTextureDescriptor descriptor)
            {
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = (int)DepthBits.None;

                return descriptor;
            }
            
#if !UNITY_6000_4_OR_NEWER

#if UNITY_6000_0_OR_NEWER
            [System.Obsolete]
#endif
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ResetTarget();

                var descriptor = GetCopyPassDescriptor(cameraTextureDescriptor);
                RenderingUtils.ReAllocateIfNeeded(ref tempTexHandle, descriptor);

                descriptor.colorFormat = RenderTextureFormat.R16;
                RenderingUtils.ReAllocateIfNeeded(ref maskedObjectsHandle, descriptor);

                base.Configure(cmd, cameraTextureDescriptor);
            }

#if UNITY_6000_0_OR_NEWER
            [System.Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.isPreviewCamera)
                {
                    return;
                }

                if (material == null || maskMaterial == null || hullMaterial == null)
                {
                    CreateMaterial();
                }

                CommandBuffer cmd = CommandBufferPool.Get();

                // Set Outline effect properties.
                var settings = VolumeManager.instance.stack.GetComponent<OutlineSettings>();
                renderPassEvent = settings.renderPassEvent.value.Convert();

                RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
                //RTHandle cameraDepthHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;

                // Perform the Blit operations for the Colorize effect.
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // This should never be the case because the effect deactivates with NoOutlines.
                    if (settings.outlineType.value != OutlineType.NoOutlines)
                    {
                        Blitter.BlitCameraTexture(cmd, cameraTargetHandle, tempTexHandle);
                        material.SetColor("_OutlineColor", settings.outlineColor.value);
                    }

                    if (settings.maskIgnoreDepth.value)
                    {
                        maskMaterial.EnableKeyword("_IGNORE_DEPTH");
                    }
                    else
                    {
                        maskMaterial.DisableKeyword("_IGNORE_DEPTH");
                    }

                    if (settings.useNoiseOffsets.value)
                    {
                        material.EnableKeyword("_USE_NOISE_OFFSETS");
                        material.SetFloat("_NoiseScale", settings.noiseScale.value);
                        material.SetFloat("_NoiseOffset", settings.noiseOffset.value);
                        material.SetFloat("_NoiseStrength", settings.noiseStrength.value);
                    }
                    else
                    {
                        material.DisableKeyword("_USE_NOISE_OFFSETS");
                    }

                    RenderQueueRange range = settings.renderQueue.value.Convert();
                    int passIndex = 0;

                    switch (settings.outlineType.value)
                    {
                        case OutlineType.DepthNormalOutlines:
                            {
                                material.SetFloat("_ColorSensitivity", settings.colorSensitivity.value);
                                material.SetFloat("_ColorStrength", settings.colorStrength.value);
                                material.SetFloat("_DepthSensitivity", settings.depthSensitivity.value);
                                material.SetFloat("_DepthStrength", settings.depthStrength.value);
                                material.SetFloat("_NormalsSensitivity", settings.normalSensitivity.value);
                                material.SetFloat("_NormalsStrength", settings.normalStrength.value);
                                material.SetFloat("_DepthThreshold", settings.depthThreshold.value);

                                using (new ProfilingScope(cmd, outlineProfilingSampler))
                                {
                                    Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle, material, 0);
                                }
                                break;
                            }
                        case OutlineType.HighQualityObjectMaskOutlines:
                            {
                                material.SetTexture("_MaskedObjects", maskedObjectsHandle);
                                material.SetInteger("_OutlineWidth", settings.maskedOutlineThickness.value);
                                float drawInside = (settings.outlineDrawSides.value != DrawSides.Outside ? 1.0f : 0.0f);
                                float drawOutside = (settings.outlineDrawSides.value != DrawSides.Inside ? 1.0f : 0.0f);
                                material.SetVector("_DrawSides", new Vector2(drawInside, drawOutside));
                                material.SetFloat("_OutlineFadeStart", settings.outlineFadeStart.value);
                                material.SetFloat("_OutlineFadeEnd", settings.outlineFadeEnd.value);
                                material.SetFloat("_Spread", 1.0f / (settings.maskedOutlineSmoothing.value * 32.0f * Mathf.Pow(settings.maskedOutlineThickness.value / 6.0f, 2)));

                                if (settings.useDepthNormals.value)
                                {
                                    material.EnableKeyword("_USE_DEPTH_NORMALS");
                                    material.SetFloat("_NormalsSensitivity", settings.normalSensitivity.value);
                                    material.SetFloat("_NormalsStrength", settings.normalStrength.value);
                                }
                                else
                                {
                                    material.DisableKeyword("_USE_DEPTH_NORMALS");
                                }

                                CoreUtils.SetRenderTarget(cmd, maskedObjectsHandle);
                                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);
                                passIndex = settings.maskDrawingMode.value.Convert();
                                DrawObjects(context, ref renderingData, cmd, settings, maskMaterial, passIndex, maskProfilingSampler);

                                using (new ProfilingScope(cmd, outlineProfilingSampler))
                                {
                                    Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle, material, 1);
                                }
                                break;
                            }
                        case OutlineType.PixelWidthObjectMaskOutlines:
                            {
                                material.SetTexture("_MaskedObjects", maskedObjectsHandle);

                                CoreUtils.SetRenderTarget(cmd, maskedObjectsHandle);
                                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);
                                passIndex = settings.maskDrawingMode.value.Convert();
                                DrawObjects(context, ref renderingData, cmd, settings, maskMaterial, passIndex, maskProfilingSampler);

                                using (new ProfilingScope(cmd, outlineProfilingSampler))
                                {
                                    Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle, material, 2);
                                }
                                break;
                            }
                        case OutlineType.HullOutlines:
                            {
                                hullMaterial.SetColor("_OutlineColor", settings.outlineColor.value);
                                hullMaterial.SetFloat("_OutlineThickness", settings.outlineThickness.value);

                                if (settings.outlineTransparency.value)
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                                }
                                else
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
                                }

                                if (settings.outlineLighting.value)
                                {
                                    hullMaterial.EnableKeyword("_HULL_LIGHTING_ON");
                                    hullMaterial.SetFloat("_OutlineDirection", settings.flipOutlineDirection.value ? 1.0f : -1.0f);
                                    hullMaterial.SetFloat("_OutlineMinLighting", settings.outlineMinLighting.value);
                                }
                                else
                                {
                                    hullMaterial.DisableKeyword("_HULL_LIGHTING_ON");
                                }

                                CoreUtils.SetRenderTarget(cmd, tempTexHandle);
                                DrawObjects(context, ref renderingData, cmd, settings, hullMaterial, 0, hullProfilingSampler);

                                Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle);
                                break;
                            }
                        case OutlineType.DebugOutlineMask:
                            {
                                CoreUtils.SetRenderTarget(cmd, maskedObjectsHandle);
                                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);
                                passIndex = settings.maskDrawingMode.value.Convert();
                                DrawObjects(context, ref renderingData, cmd, settings, maskMaterial, passIndex, maskProfilingSampler);

                                Blitter.BlitCameraTexture(cmd, maskedObjectsHandle, cameraTargetHandle);
                                break;
                            }
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
            
#endif

            private static void DrawObjects(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, OutlineSettings settings, Material drawMaterial, int passIndex, ProfilingSampler profilingSampler)
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    var cullingResults = renderingData.cullResults;

                    FilteringSettings filteringSettings =
                        new FilteringSettings(settings.renderQueue.value.Convert(), settings.objectMask.value);

                    DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(settings.lightModes.value.Convert(), ref renderingData, SortingCriteria.RenderQueue);
                    drawingSettings.overrideMaterial = drawMaterial;
                    drawingSettings.overrideMaterialPassIndex = passIndex;

                    RendererListParams rendererParams = new RendererListParams(cullingResults, drawingSettings, filteringSettings);
                    RendererList rendererList = context.CreateRendererList(ref rendererParams);

                    cmd.DrawRendererList(rendererList);
                }
            }

            public void Dispose()
            {
                tempTexHandle?.Release();
            }

#if UNITY_6000_0_OR_NEWER

            private class CopyPassData
            {
                public TextureHandle inputTexture;
            }

            private class DepthNormalData
            {
                public Material outlineMaterial;
                public TextureHandle tempTexture;
            }

            private class MaskData
            {
                public RendererListHandle rendererList;
            }

            private class HQOutlineData
            {
                public Material outlineMaterial;
                public TextureHandle tempTexture;
                public TextureHandle maskTexture;
            }

            private class LQOutlineData
            {
                public Material outlineMaterial;
                public TextureHandle tempTexture;
                public TextureHandle maskTexture;
            }

            private static void ExecuteCopyPass(RasterCommandBuffer cmd, RTHandle source)
            {
                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), 0.0f, false);
            }

            private static void DepthNormalOutlines(RasterCommandBuffer cmd, RTHandle source, Material material)
            {
                var settings = VolumeManager.instance.stack.GetComponent<OutlineSettings>();

                material.SetFloat("_ColorSensitivity", settings.colorSensitivity.value);
                material.SetFloat("_ColorStrength", settings.colorStrength.value);
                material.SetFloat("_DepthSensitivity", settings.depthSensitivity.value);
                material.SetFloat("_DepthStrength", settings.depthStrength.value);
                material.SetFloat("_NormalsSensitivity", settings.normalSensitivity.value);
                material.SetFloat("_NormalsStrength", settings.normalStrength.value);
                material.SetFloat("_DepthThreshold", settings.depthThreshold.value);

                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), material, 0);
            }

            private static void DrawObjects(MaskData data, RasterGraphContext context, bool clear)
            {
                if(clear)
                {
                    context.cmd.ClearRenderTarget(true, true, Color.black);
                }

                context.cmd.DrawRendererList(data.rendererList);
            }

            private static void HighQualityMaskOutlinesPass(RasterCommandBuffer cmd, RTHandle source, RTHandle maskedObjectsHandle, Material material)
            {
                var settings = VolumeManager.instance.stack.GetComponent<OutlineSettings>();

                material.SetTexture("_MaskedObjects", maskedObjectsHandle);
                material.SetInteger("_OutlineWidth", settings.maskedOutlineThickness.value);
                float drawInside = (settings.outlineDrawSides.value != DrawSides.Outside ? 1.0f : 0.0f);
                float drawOutside = (settings.outlineDrawSides.value != DrawSides.Inside ? 1.0f : 0.0f);
                material.SetVector("_DrawSides", new Vector2(drawInside, drawOutside));
                material.SetFloat("_OutlineFadeStart", settings.outlineFadeStart.value);
                material.SetFloat("_OutlineFadeEnd", settings.outlineFadeEnd.value);
                material.SetFloat("_Spread", 1.0f / (settings.maskedOutlineSmoothing.value * 32.0f * Mathf.Pow(settings.maskedOutlineThickness.value / 6.0f, 2)));

                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), material, 1);
            }

            private static void LowQualityMaskOutlinesPass(RasterCommandBuffer cmd, RTHandle source, RTHandle maskedObjectsHandle, Material material)
            {
                var settings = VolumeManager.instance.stack.GetComponent<OutlineSettings>();

                material.SetTexture("_MaskedObjects", maskedObjectsHandle);
                
                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), material, 2);
            }

            public RendererListHandle GetRendererList(RenderGraph renderGraph, UniversalRenderingData renderingData, Camera camera, OutlineSettings settings, List<ShaderTagId> shaderTagIDs, Material material)
            {
                var cullingResults = renderingData.cullResults;

                RenderQueueRange renderQueueRange = settings.renderQueue.value.Convert();

                int passIndex = (settings.maskDrawingMode.value.Convert());
                SortingCriteria sortingCriteria = 
                    (settings.renderQueue.value == RenderQueueType.Transparent) ? SortingCriteria.CommonTransparent : SortingCriteria.CommonOpaque;

                var rendererListDesc = new RendererListDesc(shaderTagIDs.ToArray(), cullingResults, camera)
                {
                    renderQueueRange = renderQueueRange,
                    layerMask = settings.objectMask.value,
                    overrideMaterial = material,
                    overrideMaterialPassIndex = passIndex,
                    sortingCriteria = sortingCriteria
                };

                return renderGraph.CreateRendererList(rendererListDesc);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

                if (cameraData.isPreviewCamera)
                {
                    return;
                }

                if (material == null || maskMaterial == null || hullMaterial == null)
                {
                    CreateMaterial();
                }

                var settings = VolumeManager.instance.stack.GetComponent<OutlineSettings>();
                renderPassEvent = settings.renderPassEvent.value.Convert();

                UniversalRenderer renderer = (UniversalRenderer)cameraData.renderer;
                Camera camera = cameraData.camera;

                var descriptor = GetCopyPassDescriptor(cameraData.cameraTargetDescriptor);
                TextureHandle tempTexHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_OutlineColorTexture", false);

                descriptor.colorFormat = RenderTextureFormat.R16;
                TextureHandle maskedObjectsHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_MaskedObjects", false);

                if(settings.outlineType.value != OutlineType.NoOutlines &&
                    settings.outlineType.value != OutlineType.HullOutlines)
                {
                    material.SetColor("_OutlineColor", settings.outlineColor.value);

                    using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("Outline_CopyColorTexture", out var passData, profilingSampler))
                    {
                        passData.inputTexture = resourceData.activeColorTexture;

                        builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                        builder.SetRenderAttachment(tempTexHandle, 0, AccessFlags.Write);
                        builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => ExecuteCopyPass(context.cmd, data.inputTexture));
                    }
                }

                if (settings.maskIgnoreDepth.value)
                {
                    maskMaterial.EnableKeyword("_IGNORE_DEPTH");
                }
                else
                {
                    maskMaterial.DisableKeyword("_IGNORE_DEPTH");
                }

                switch (settings.outlineType.value)
                {
                    case OutlineType.DepthNormalOutlines:
                        {
                            using (var builder = renderGraph.AddRasterRenderPass<DepthNormalData>("Outline_DepthNormalOutlines", out var passData, profilingSampler))
                            {
                                passData.tempTexture = tempTexHandle;
                                passData.outlineMaterial = material;

                                builder.UseTexture(tempTexHandle, AccessFlags.Read);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((DepthNormalData data, RasterGraphContext context) => DepthNormalOutlines(context.cmd, data.tempTexture, data.outlineMaterial));
                            }
                            break;
                        }

                    case OutlineType.HighQualityObjectMaskOutlines:
                        {
                            // Render the object mask.
                            using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Outline_DrawMask", out var passData, maskProfilingSampler))
                            {
                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), maskMaterial);

                                builder.SetRenderAttachment(maskedObjectsHandle, 0, AccessFlags.Write);
                                builder.SetGlobalTextureAfterPass(in maskedObjectsHandle, Shader.PropertyToID("_MaskedObjects"));
                                builder.UseRendererList(passData.rendererList);
                                builder.SetRenderFunc((MaskData data, RasterGraphContext context) => DrawObjects(data, context, true));
                            }

                            // Render the outlines in high-quality mode.
                            using (var builder = renderGraph.AddRasterRenderPass<HQOutlineData>("Outline_DrawHQOutlines", out var passData, outlineProfilingSampler))
                            {
                                passData.outlineMaterial = material;
                                passData.tempTexture = tempTexHandle;
                                passData.maskTexture = maskedObjectsHandle;

                                builder.UseTexture(maskedObjectsHandle, AccessFlags.Read);
                                builder.UseTexture(tempTexHandle, AccessFlags.Read);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((HQOutlineData data, RasterGraphContext context) => HighQualityMaskOutlinesPass(context.cmd, data.tempTexture, data.maskTexture, data.outlineMaterial));
                            }
                            break;
                        }

                    case OutlineType.PixelWidthObjectMaskOutlines:
                        {
                            // Render the object mask.
                            using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Outline_DrawMask", out var passData, maskProfilingSampler))
                            {
                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), maskMaterial);

                                builder.UseRendererList(passData.rendererList);
                                builder.SetRenderAttachment(maskedObjectsHandle, 0, AccessFlags.Write);
                                builder.SetGlobalTextureAfterPass(in maskedObjectsHandle, Shader.PropertyToID("_MaskedObjects"));
                                builder.SetRenderFunc((MaskData data, RasterGraphContext context) => DrawObjects(data, context, true));
                            }

                            // Render the outlines in low-quality mode.
                            using (var builder = renderGraph.AddRasterRenderPass<LQOutlineData>("Outline_DrawLQOutlines", out var passData, outlineProfilingSampler))
                            {
                                passData.outlineMaterial = material;
                                passData.tempTexture = tempTexHandle;
                                passData.maskTexture = maskedObjectsHandle;

                                builder.UseTexture(maskedObjectsHandle, AccessFlags.Read);
                                builder.UseTexture(tempTexHandle, AccessFlags.Read);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((LQOutlineData data, RasterGraphContext context) => LowQualityMaskOutlinesPass(context.cmd, data.tempTexture, data.maskTexture, data.outlineMaterial));
                            }
                        }
                        break;

                    case OutlineType.HullOutlines:
                        {
                            // Render the hull outlines.
                            using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Outline_DrawHullOutlines", out var passData, maskProfilingSampler))
                            {
                                hullMaterial.SetColor("_OutlineColor", settings.outlineColor.value);
                                hullMaterial.SetFloat("_OutlineThickness", settings.outlineThickness.value);

                                if (settings.outlineTransparency.value)
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                                }
                                else
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
                                }

                                if (settings.outlineLighting.value)
                                {
                                    hullMaterial.EnableKeyword("_HULL_LIGHTING_ON");
                                    hullMaterial.SetFloat("_OutlineDirection", settings.flipOutlineDirection.value ? 1.0f : -1.0f);
                                    hullMaterial.SetFloat("_OutlineMinLighting", settings.outlineMinLighting.value);
                                }
                                else
                                {
                                    hullMaterial.DisableKeyword("_HULL_LIGHTING_ON");
                                }

                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), hullMaterial);

                                builder.UseRendererList(passData.rendererList);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((MaskData data, RasterGraphContext context) => DrawObjects(data, context, false));
                            }
                        }
                        break;

                    case OutlineType.DebugOutlineMask:
                        {
                            // Render the object mask.
                            using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Outline_DrawMask", out var passData, maskProfilingSampler))
                            {
                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), maskMaterial);

                                builder.UseRendererList(passData.rendererList);
                                builder.SetRenderAttachment(maskedObjectsHandle, 0, AccessFlags.Write);
                                builder.SetRenderFunc((MaskData data, RasterGraphContext context) => DrawObjects(data, context, true));
                            }

                            // Copy the mask texture to the camera output.
                            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("Outline_DebugDraw", out var passData, profilingSampler))
                            {
                                passData.inputTexture = maskedObjectsHandle;

                                builder.UseTexture(maskedObjectsHandle, AccessFlags.Read);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => ExecuteCopyPass(context.cmd, data.inputTexture));
                            }
                            break;
                        }
                }
            }

#endif
        }
    }
}
