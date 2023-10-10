using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    interface IRenderPass
    {
        // Context allows us to pass resources such as shaders, etc... to passes,
        // in a low-footprint manner, as needs arise we simply edit the context.
        // Passes usually have different needs so that should not alter their shared interface.
        void Execute(CommandBuffer cmd, Context ctx);

        // It is fair to assume every pass will have one Output.
        // Multiple outputs is not expected in the context of keying so far.
        TextureHandle Output { get; }

        // A pass can have multiple inputs.
        IEnumerable<TextureHandle> Inputs { get; }
    }
}
