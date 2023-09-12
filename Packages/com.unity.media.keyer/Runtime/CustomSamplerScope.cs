using System;
using UnityEngine.Profiling;

namespace Unity.Media.Keyer
{
    struct CustomSamplerScope : IDisposable
    {
        readonly CustomSampler m_Sampler;

        public CustomSamplerScope(CustomSampler sampler)
        {
            m_Sampler = sampler;
            m_Sampler.Begin();
        }

        public void Dispose()
        {
            m_Sampler.End();
        }
    }
}
