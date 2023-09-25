using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Media.Keyer
{
    // This enum gives us a place to decide on the type of formats we want to use.
    // We are only concerned about the channels available.
    enum BufferFormat
    {
        None,
        RGBA,
        R
    }

    // Will allow us to support HDR.
    // Based on RenderingQuality we'll use different sets of formats.
    enum RenderingQuality
    {
        Standard,
        HDRMedium,
        HDRHigh
    }

    // static class as Quality is consistent across the project.
    static class BufferFormatUtility
    {
        static GraphicsFormat s_Color = GraphicsFormat.None;
        static GraphicsFormat s_SingleChannel = GraphicsFormat.None;

        public static GraphicsFormat GetGraphicsFormat(BufferFormat format)
        {
            if (s_Color == GraphicsFormat.None || s_SingleChannel == GraphicsFormat.None)
            {
                throw new InvalidOperationException(
                    $"{nameof(BufferFormatUtility)} has not been configured using {nameof(SetQuality)}.");
            }

            switch (format)
            {
                case BufferFormat.RGBA: return s_Color;
                case BufferFormat.R: return s_SingleChannel;
            }

            return GraphicsFormat.None;
        }

        public static BufferFormat GetBufferFormat(GraphicsFormat format)
        {
            if (GetGraphicsFormat(BufferFormat.RGBA) == format)
            {
                return BufferFormat.RGBA;
            }

            if (GetGraphicsFormat(BufferFormat.R) == format)
            {
                return BufferFormat.R;
            }

            throw new InvalidOperationException(
                $"{nameof(TexturePool)}, Unexpected graphics format {format}.");
        }

        // The boolean return value represents whether or not internal formats changed as a result of the call.
        // It does not correspond to a notion of success or failure of the operation.
        // In that respect the operation is always expected to succeed.
        // The usefulness of knowing whether or not internal formats change is that we may need to
        // invalidate caches and reallocate buffers matching the new formats in use.
        public static bool SetQuality(RenderingQuality quality)
        {
            var prevColor = s_Color;
            var prevSingleChannel = s_SingleChannel;

            // See CustomBufferFormat in HDRP.
            switch (quality)
            {
                case RenderingQuality.Standard:
                    s_Color = GetSupportedFormat(GraphicsFormat.R8G8B8A8_UNorm);
                    s_SingleChannel = GetSupportedFormat(GraphicsFormat.R8_UNorm, GraphicsFormat.R8G8B8A8_UNorm);
                    break;
                case RenderingQuality.HDRMedium:
                    s_Color = GetSupportedFormat(GraphicsFormat.R8G8B8A8_UNorm);
                    s_SingleChannel = GetSupportedFormat(GraphicsFormat.R16_UNorm, GraphicsFormat.R16G16B16A16_UNorm);
                    break;
                case RenderingQuality.HDRHigh:
                    s_Color = GetSupportedFormat(GraphicsFormat.R16G16B16A16_SFloat);
                    s_SingleChannel = GetSupportedFormat(GraphicsFormat.R16_UNorm, GraphicsFormat.R16G16B16A16_UNorm);
                    break;
            }

            return prevColor != s_Color || prevSingleChannel != s_SingleChannel;
        }

        static GraphicsFormat GetSupportedFormat(GraphicsFormat format, GraphicsFormat fallbackFormat)
        {
            var compatibleFormat = SystemInfo.GetCompatibleFormat(format, FormatUsage.LoadStore);
            if (compatibleFormat == GraphicsFormat.None)
            {
                return fallbackFormat;
            }

            return compatibleFormat;
        }

        static GraphicsFormat GetSupportedFormat(GraphicsFormat format) => GetSupportedFormat(format, format);
    }
}
