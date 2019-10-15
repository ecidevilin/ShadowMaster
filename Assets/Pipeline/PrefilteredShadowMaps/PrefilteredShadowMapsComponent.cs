using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline.Extension
{
    public class PrefilteredShadowMapsComponent : MonoBehaviour, IAfterMainLightShadowCasterPass
    {
        [Range(1,45)]
        [SerializeField] private int _EVSMExponentPos = 10;
        [Range(1, 45)]
        [SerializeField] private int _EVSMExponentNeg = 20;
        [SerializeField] private ShadowMapsType _ShadowMapsType = ShadowMapsType.VSM;
        [SerializeField] private ShadowMapsPrecision _ShadowMapPrecision = ShadowMapsPrecision.Half;
        [Header("Mipmaps are disable when cascade is larger than one.")]
        [SerializeField] private bool _UseMipmaps = false;
        void OnEnable()
        {

        }

        public ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle mainLightShadowmapHandle)
        {
            PrefilterShadowMapsPass pass = new PrefilterShadowMapsPass();
            pass._Enabled = isActiveAndEnabled;
            pass._EVSMExponent = new Vector2(_EVSMExponentPos, _EVSMExponentNeg);
            pass._ShadowMapsType = _ShadowMapsType;
            pass._ShadowMapsPrecision = _ShadowMapPrecision;
            pass._UseMipmaps = _UseMipmaps;
            pass.Setup(baseDescriptor, mainLightShadowmapHandle);
            return pass;
        }
    }
}
