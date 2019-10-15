using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline.Extension
{
    public enum ShadowMapsType
    {
        VSM,
        EVSM,
    }

    public enum ShadowMapsPrecision
    {
        Half,
        Single,
    }

    public class PrefilterShadowMapsPass : ScriptableRenderPass
    {
        public bool _Enabled;
        public Vector2 _EVSMExponent;
        public ShadowMapsType _ShadowMapsType;
        public ShadowMapsPrecision _ShadowMapsPrecision;
        public bool _UseMipmaps;

        const string _FilterEVSM = "Filter EVSM";
        const string _ShaderPath = "Hidden/FilterShadowMaps";

        const string _KeywordFirstFilter = "_FIRST_FILTERING";
        const string _KeywordShadowMapsPrecision = "_SHADOW_MAPS_FLOAT";

        const string _UniformEVSMExponent = "_EVSMExponent";
        const string _UniformHorizontalVertical = "_HorizontalVertical";
        const string _UniformMainTex = "_MainTex";

        const string _UniformFilteredMainLightSM = "_FilteredMainLightSM";
        const string _UniformTmpMainLightSM = "_TmpMainLightSM";

        readonly Dictionary<ShadowMapsType, string> _TypeKeywords = new Dictionary<ShadowMapsType, string>()
        {
            { ShadowMapsType.VSM, "_VARIANCE_SHADOW_MAPS"},
            { ShadowMapsType.EVSM, "_EXP_VARIANCE_SHADOW_MAPS"},
        };

        RenderTargetHandle _FilteredMainLightSMHandle;
        RenderTargetHandle _TmpMainLightSMHandle;
        RenderTextureDescriptor _MainLightFilteredSMDescriptor;
        RenderTextureFormat _SMFormat;

        Material _Material;


        public PrefilterShadowMapsPass()
        {
            _Material = CoreUtils.CreateEngineMaterial(_ShaderPath);
            _FilteredMainLightSMHandle.Init(_UniformFilteredMainLightSM);
            _TmpMainLightSMHandle.Init(_UniformTmpMainLightSM);
        }

        private bool CheckEnabled(ref RenderingData renderingData)
        {
            if (!_Enabled)
            {
                return false;
            }
            if (!renderingData.shadowData.supportsSoftShadows)
            {
                return false;
            }
            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
            {
                return false;
            }
            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            if (light.shadows != LightShadows.Soft)
            {
                return false;
            }
            return true;
        }

        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (null == renderer)
            {
                throw new ArgumentException("renderer");
            }
            
            _MainLightFilteredSMDescriptor.width = renderingData.shadowData.mainLightShadowmapWidth;
            _MainLightFilteredSMDescriptor.height = renderingData.shadowData.mainLightShadowmapHeight;

            if (renderingData.shadowData.requiresScreenSpaceShadowResolve)
            {
                _MainLightFilteredSMDescriptor.autoGenerateMips = false;
                _MainLightFilteredSMDescriptor.useMipMap = false;
            }

            CommandBuffer cmd = CommandBufferPool.Get(_FilterEVSM);
            if (!CheckEnabled(ref renderingData))
            {
                foreach (var kvp in _TypeKeywords)
                {
                    CoreUtils.SetKeyword(cmd, kvp.Value, false);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                return;
            }
            using (new ProfilingSample(cmd, _FilterEVSM))
            {
                foreach (var kvp in _TypeKeywords)
                {
                    CoreUtils.SetKeyword(cmd, kvp.Value, kvp.Key == _ShadowMapsType);
                }

                CoreUtils.SetKeyword(cmd, _KeywordShadowMapsPrecision, _ShadowMapsPrecision == ShadowMapsPrecision.Single);

                cmd.SetGlobalVector(_UniformEVSMExponent, _EVSMExponent);
                cmd.GetTemporaryRT(_FilteredMainLightSMHandle.id, _MainLightFilteredSMDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(_TmpMainLightSMHandle.id, _MainLightFilteredSMDescriptor, FilterMode.Bilinear);
                RenderTargetIdentifier srti = _TmpMainLightSMHandle.Identifier();
                RenderTargetIdentifier drti = _FilteredMainLightSMHandle.Identifier();

                CoreUtils.SetKeyword(cmd, _KeywordFirstFilter, true);

                cmd.SetGlobalVector(_UniformHorizontalVertical, Vector2.right);
                SetRenderTarget(cmd, srti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                cmd.Blit(srti, srti, _Material);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                CoreUtils.SetKeyword(cmd, _KeywordFirstFilter, false);

                cmd.SetGlobalTexture(_UniformMainTex, srti);
                cmd.SetGlobalVector(_UniformHorizontalVertical, Vector2.up);
                SetRenderTarget(cmd, drti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                cmd.Blit(srti, drti, _Material);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                //for (int i = 1; i <= 1; i++)
                //{
                //    int p = 1 << i;
                //    cmd.SetGlobalVector("_HorizontalVertical", Vector2.right * p);
                //    SetRenderTarget(cmd, srti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                //    cmd.SetGlobalTexture("_MainTex", drti);
                //    cmd.Blit(drti, srti, _Material);

                //    context.ExecuteCommandBuffer(cmd);
                //    cmd.Clear();

                //    cmd.SetGlobalVector("_HorizontalVertical", Vector2.up * p);
                //    SetRenderTarget(cmd, drti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                //    cmd.SetGlobalTexture("_MainTex", srti);
                //    cmd.Blit(srti, drti, _Material);

                //    context.ExecuteCommandBuffer(cmd);
                //    cmd.Clear();
                //}

            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle mainLightShadowmapHandle)
        {
            if (_ShadowMapsType == ShadowMapsType.EVSM)
            {
                if (_ShadowMapsPrecision == ShadowMapsPrecision.Half)
                {
                    _SMFormat = RenderTextureFormat.ARGBHalf;
                }
                else
                {
                    _SMFormat = RenderTextureFormat.ARGBFloat;
                }
            }
            else
            {
                if (_ShadowMapsPrecision == ShadowMapsPrecision.Half)
                {
                    _SMFormat = RenderTextureFormat.RGHalf;
                }
                else
                {
                    _SMFormat = RenderTextureFormat.RGFloat;
                }
            }
            baseDescriptor.depthBufferBits = 0;
            baseDescriptor.colorFormat = _SMFormat;
            baseDescriptor.autoGenerateMips = _UseMipmaps;
            baseDescriptor.useMipMap = _UseMipmaps;
            _MainLightFilteredSMDescriptor = baseDescriptor;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_FilteredMainLightSMHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_FilteredMainLightSMHandle.id);
                _FilteredMainLightSMHandle = RenderTargetHandle.CameraTarget;
            }
            if (_TmpMainLightSMHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_TmpMainLightSMHandle.id);
                _TmpMainLightSMHandle = RenderTargetHandle.CameraTarget;
            }
            base.FrameCleanup(cmd);
        }
    }
}