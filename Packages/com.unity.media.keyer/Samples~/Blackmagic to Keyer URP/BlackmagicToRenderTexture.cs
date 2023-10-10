using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Blackmagic
{
    [ExecuteAlways]
    public sealed class BlackmagicToRenderTexture : MonoBehaviour
    {
#if BMD_AVAILABLE
        [SerializeField]
        InputVideoDeviceHandle m_InputDevice;
#endif
        [SerializeField]
        RenderTexture m_ToKeyerRenderTexture;

        CommandBuffer m_CommandBuffer;

        void OnEnable()
        {
#if URP_AVAILABLE
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            m_CommandBuffer = new CommandBuffer();
#else
            Debug.LogError("This script is only available when the URP package is installed.");
#endif
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
#if BMD_AVAILABLE && URP_AVAILABLE
            if (!m_InputDevice.IsActive())
                return;

            if (m_InputDevice.TryGetRenderTexture(out var inputTexture))
            {
                m_CommandBuffer.Clear();
                m_CommandBuffer.Blit(inputTexture, m_ToKeyerRenderTexture);

                context.ExecuteCommandBuffer(m_CommandBuffer);
                context.Submit();
            }
#else
            Debug.LogError("The Blackmagic package com.unity.media.blackmagic is not installed.");
#endif
        }

        void OnDisable()
        {
#if URP_AVAILABLE
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            m_CommandBuffer.Release();
#endif
        }
    }
}
