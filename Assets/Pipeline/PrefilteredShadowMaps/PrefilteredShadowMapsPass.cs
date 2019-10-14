using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
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

        RenderTargetHandle _FilteredMailLightSMHandle;
        RenderTargetHandle _TmpMailLightSMHandle;
        RenderTextureDescriptor _MailLightEVSMDescriptor;
        RenderTextureFormat _SMFormat;

        Material _Material;


        public PrefilterShadowMapsPass()
        {
            _Material = CoreUtils.CreateEngineMaterial(_ShaderPath);
            _FilteredMailLightSMHandle.Init(_UniformFilteredMainLightSM);
            _TmpMailLightSMHandle.Init(_UniformTmpMainLightSM);
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
            
            _MailLightEVSMDescriptor.width = renderingData.shadowData.mainLightShadowmapWidth;
            _MailLightEVSMDescriptor.height = renderingData.shadowData.mainLightShadowmapHeight;
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
                CoreUtils.SetKeyword(cmd, _KeywordFirstFilter, true);
                cmd.GetTemporaryRT(_FilteredMailLightSMHandle.id, _MailLightEVSMDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(_TmpMailLightSMHandle.id, _MailLightEVSMDescriptor, FilterMode.Bilinear);
                RenderTargetIdentifier srti = _TmpMailLightSMHandle.Identifier();
                RenderTargetIdentifier drti = _FilteredMailLightSMHandle.Identifier();

                cmd.SetGlobalVector(_UniformHorizontalVertical, Vector2.right);
                SetRenderTarget(cmd, srti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                cmd.SetGlobalTexture(_UniformMainTex, srti);
                cmd.Blit(srti, srti, _Material);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                CoreUtils.SetKeyword(cmd, _KeywordFirstFilter, false);

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

        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
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
            baseDescriptor.autoGenerateMips = true;
            baseDescriptor.useMipMap = true;
            _MailLightEVSMDescriptor = baseDescriptor;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_FilteredMailLightSMHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_FilteredMailLightSMHandle.id);
                _FilteredMailLightSMHandle = RenderTargetHandle.CameraTarget;
            }
            if (_TmpMailLightSMHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_TmpMailLightSMHandle.id);
                _TmpMailLightSMHandle = RenderTargetHandle.CameraTarget;
            }
            base.FrameCleanup(cmd);
        }
    }
}