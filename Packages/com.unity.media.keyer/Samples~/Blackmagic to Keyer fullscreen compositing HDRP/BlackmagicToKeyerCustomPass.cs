using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
#if BMD_AVAILABLE
using Unity.Media.Blackmagic;
#endif

namespace Unity.Media.Keyer
{
    /// <summary>
    /// This class blits the Blackmagic input video into a result render texture.
    /// </summary>
    [Serializable]
    public sealed class BlackmagicToKeyerCustomPass : CustomPass
    {
#if BMD_AVAILABLE
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;

        [SerializeField]
        RenderTexture m_ToKeyerRenderTexture;
#endif
        protected override void Execute(CustomPassContext ctx)
        {
#if BMD_AVAILABLE
            if (!m_InputDevice.IsActive())
                return;

            if (m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                ctx.cmd.Blit(inputTexture, m_ToKeyerRenderTexture);
            }
#else
            Debug.LogError("The Blackmagic package com.unity.media.blackmagic is not installed");
#endif
        }
    }
}
