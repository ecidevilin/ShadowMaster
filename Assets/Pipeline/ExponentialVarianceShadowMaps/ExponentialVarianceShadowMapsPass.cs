using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline.Extension
{
    public class ExponentialVarianceShadowMapsPass : ScriptableRenderPass
    {
        public bool _Enabled;
        public Vector4 _EVSMExponent;
        public ShadowMapsPrecision _ShadowMapsPrecision;
        public bool _UseMipmaps;
        public ComputeShader _Compute;

        const string _UniformFilteredMainLightSM = "_FilteredMainLightSM";
        const string _UniformTmpMainLightSM = "_TmpMainLightSM";

        const string _Compute_InputTex = "_InputTex";
        const string _Compute_OutputTex = "_OutputTex";
        const string _Compute_InputTex_TexelSize = "_InputTex_TexelSize";
        const string _Compute_EVSMExponent = "_EVSMExponent";
        const string _Compute_Vertical = "_Vertical";

        const string _KernelFilteringName = "KernelFiltering";
        private int _KernelFiltering;
        const string _KernelFirstFilteringName = "KernelFirstFiltering";
        private int _KernelFirstFiltering;
        private uint _NumThreadX;
        private uint _NumThreadY;
        private uint _NumThreadZ;


        RenderTargetHandle _FilteredMainLightSMHandle;
        RenderTargetHandle _TmpMainLightSMHandle;
        RenderTextureDescriptor _MainLightFilteredSMDescriptor;
        RenderTextureFormat _SMFormat;

        public ExponentialVarianceShadowMapsPass()
        {
            _FilteredMainLightSMHandle.Init(_UniformFilteredMainLightSM);
            _TmpMainLightSMHandle.Init(_UniformTmpMainLightSM);
        }
        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle mainLightShadowmapHandle)
        {
            if (null == _Compute)
            {
                _Enabled = false;
                return;
            }
            _KernelFiltering = _Compute.FindKernel(_KernelFilteringName);
            _KernelFirstFiltering = _Compute.FindKernel(_KernelFirstFilteringName);
            _Compute.GetKernelThreadGroupSizes(_KernelFiltering, out _NumThreadX, out _NumThreadY, out _NumThreadZ);

            if (_ShadowMapsPrecision == ShadowMapsPrecision.Half)
            {
                _SMFormat = RenderTextureFormat.ARGBHalf;
            }
            else
            {
                _SMFormat = RenderTextureFormat.ARGBFloat;
            }
            baseDescriptor.depthBufferBits = 0;
            baseDescriptor.colorFormat = _SMFormat;
            bool useMipmaps = _UseMipmaps;
            baseDescriptor.autoGenerateMips = useMipmaps;
            baseDescriptor.useMipMap = useMipmaps;
            baseDescriptor.enableRandomWrite = true;
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
            CoreUtils.SetKeyword(cmd, "_EXP_VARIANCE_SHADOW_MAPS", false);
            base.FrameCleanup(cmd);
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

            int w = renderingData.shadowData.mainLightShadowmapWidth;
            int h = renderingData.shadowData.mainLightShadowmapHeight;
            _MainLightFilteredSMDescriptor.width = w;
            _MainLightFilteredSMDescriptor.height = h;

            if (renderingData.shadowData.requiresScreenSpaceShadowResolve)
            {
                _MainLightFilteredSMDescriptor.autoGenerateMips = false;
                _MainLightFilteredSMDescriptor.useMipMap = false;
            }
            CommandBuffer cmd = CommandBufferPool.Get(_KernelFilteringName);
            if (!CheckEnabled(ref renderingData))
            {
                context.ExecuteCommandBuffer(cmd);
                CoreUtils.SetKeyword(cmd, "_EXP_VARIANCE_SHADOW_MAPS", false);
                CommandBufferPool.Release(cmd);
                return;
            }
            using (new ProfilingSample(cmd, _KernelFilteringName))
            {

                cmd.SetComputeVectorParam(_Compute, _Compute_EVSMExponent, _EVSMExponent);
                cmd.SetComputeVectorParam(_Compute, _Compute_InputTex_TexelSize, new Vector4(1.0f / w, 1.0f / h, w, h));
                cmd.GetTemporaryRT(_FilteredMainLightSMHandle.id, _MainLightFilteredSMDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(_TmpMainLightSMHandle.id, _MainLightFilteredSMDescriptor, FilterMode.Bilinear);
                RenderTargetIdentifier srti = _TmpMainLightSMHandle.Identifier();
                RenderTargetIdentifier drti = _FilteredMainLightSMHandle.Identifier();

                int tgx = (int)((w + 121) / 122);

                cmd.SetComputeIntParam(_Compute, _Compute_Vertical, 0);
                cmd.SetComputeTextureParam(_Compute, _KernelFirstFiltering, _Compute_OutputTex, srti);
                cmd.DispatchCompute(_Compute, _KernelFirstFiltering, tgx, h, 1);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.SetComputeTextureParam(_Compute, _KernelFiltering, _Compute_InputTex, srti);
                cmd.SetComputeTextureParam(_Compute, _KernelFiltering, _Compute_OutputTex, drti);
                cmd.SetComputeIntParam(_Compute, _Compute_Vertical, 1);
                cmd.DispatchCompute(_Compute, _KernelFiltering, tgx, h, 1);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            CoreUtils.SetKeyword(cmd, "_EXP_VARIANCE_SHADOW_MAPS", true);
            cmd.SetGlobalVector(_Compute_EVSMExponent, _EVSMExponent);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}