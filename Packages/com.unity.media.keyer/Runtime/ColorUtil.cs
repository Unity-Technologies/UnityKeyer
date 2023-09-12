using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    static class ColorUtil
    {
        static readonly Matrix4x4 k_RgbToYuvTransform = new(
            new Vector4(00.182586f, 00.614231f, 00.062007f, 0.062745f),
            new Vector4(-0.100644f, -0.338572f, 00.439216f, 0.501961f),
            new Vector4(00.439216f, -0.398942f, -0.040274f, 0.501961f),
            new Vector4(00.000000f, 00.000000f, 00.000000f, 1.000000f));

        // Just makes eyeballing the matrix when comparing with the shader one easier.
        static readonly Matrix4x4 k_RgbToYuvTransformTranspose = k_RgbToYuvTransform.transpose;

        public static Vector3 RgbToYuv(Color rgb)
        {
            return k_RgbToYuvTransformTranspose * new Vector4(rgb.r, rgb.g, rgb.b, 1);
        }
    }
}
