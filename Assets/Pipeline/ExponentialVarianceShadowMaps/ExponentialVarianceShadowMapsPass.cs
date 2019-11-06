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
        public Vector2 _EVSMExponent;
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
        const string _Compute_FirstFiltering = "_FirstFiltering";

        const string _KernelFilteringName = "KernelFiltering";
        private int _KernelFiltering;
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