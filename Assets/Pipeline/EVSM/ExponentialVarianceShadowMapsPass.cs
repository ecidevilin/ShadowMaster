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
    public class ExponentialVarianceShadowMapsPass : ScriptableRenderPass
    {
        bool _Enabled;
        float _EVSMExponent;
        ShadowMapsType _ShadowMapsType;

        const string _FilterEVSM = "Filter EVSM";
        const string _ShaderPath = "Hidden/FilterEVSM";
        const string _FilterKeyword = "_FIRST_FILTERING";

        readonly Dictionary<ShadowMapsType, string> _TypeKeywords = new Dictionary<ShadowMapsType, string>()
        {
            { ShadowMapsType.VSM, "_VARIANCE_SHADOW_MAPS"},
            { ShadowMapsType.EVSM, "_EXP_VARIANCE_SHADOW_MAPS"},
        };

        RenderTargetHandle _FilteredMailLightEVSMHandle;
        RenderTargetHandle _TmpFilteredMailLightEVSMHandle;
        RenderTextureDescriptor _MailLightEVSMDescriptor;
        RenderTextureFormat _SMFormat;

        Material _Material;


        public ExponentialVarianceShadowMapsPass()
        {
            _Material = CoreUtils.CreateEngineMaterial(_ShaderPath);
            _FilteredMailLightEVSMHandle.Init("_FilteredMailLightEVSM");
            _TmpFilteredMailLightEVSMHandle.Init("_TmpFilteredMailLightEVSM");
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
            if (!_Enabled)
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

                cmd.SetGlobalFloat("_EVSMExponent", _EVSMExponent);
                CoreUtils.SetKeyword(cmd, _FilterKeyword, true);
                cmd.GetTemporaryRT(_FilteredMailLightEVSMHandle.id, _MailLightEVSMDescriptor);
                cmd.GetTemporaryRT(_TmpFilteredMailLightEVSMHandle.id, _MailLightEVSMDescriptor);
                RenderTargetIdentifier srti = _TmpFilteredMailLightEVSMHandle.Identifier();
                RenderTargetIdentifier drti = _FilteredMailLightEVSMHandle.Identifier();

                cmd.SetGlobalVector("_HorizontalVertical", Vector2.right);
                SetRenderTarget(cmd, srti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                cmd.SetGlobalTexture("_MainTex", srti);
                cmd.Blit(srti, srti, _Material);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                CoreUtils.SetKeyword(cmd, _FilterKeyword, false);

                cmd.SetGlobalVector("_HorizontalVertical", Vector2.up);
                SetRenderTarget(cmd, drti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                cmd.Blit(srti, drti, _Material);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                //for (int i = 2; i <= 2; i++)
                //{
                //    cmd.SetGlobalVector("_HorizontalVertical", Vector2.right * i);
                //    SetRenderTarget(cmd, srti, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                //    cmd.SetGlobalTexture("_MainTex", drti);
                //    cmd.Blit(drti, srti, _Material);

                //    context.ExecuteCommandBuffer(cmd);
                //    cmd.Clear();

                //    cmd.SetGlobalVector("_HorizontalVertical", Vector2.up * i);
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

        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle, bool enabled, float exponent, ShadowMapsType smType)
        {
            _ShadowMapsType = smType;
            if (_ShadowMapsType == ShadowMapsType.EVSM)
            {
                _SMFormat = RenderTextureFormat.ARGBFloat;
            }
            else
            {
                _SMFormat = RenderTextureFormat.RGFloat;
            }
            _Enabled = enabled;
            baseDescriptor.depthBufferBits = 0;
            baseDescriptor.colorFormat = _SMFormat;
            baseDescriptor.autoGenerateMips = true;
            baseDescriptor.useMipMap = true;
            _MailLightEVSMDescriptor = baseDescriptor;
            _EVSMExponent = exponent;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_FilteredMailLightEVSMHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_FilteredMailLightEVSMHandle.id);
                _FilteredMailLightEVSMHandle = RenderTargetHandle.CameraTarget;
            }
            if (_TmpFilteredMailLightEVSMHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_TmpFilteredMailLightEVSMHandle.id);
                _TmpFilteredMailLightEVSMHandle = RenderTargetHandle.CameraTarget;
            }
            base.FrameCleanup(cmd);
        }
    }
}