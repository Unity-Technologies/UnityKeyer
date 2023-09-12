using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;

#endif
namespace Unity.Media.Keyer
{
    static class Utilities
    {
        public static readonly Vector4 IdentityScaleBias = new(1, 1, 0, 0);

        /// <summary>
        /// Allows destroying Unity.Objects.
        /// </summary>
        /// <param name="obj">The object to be destroyed.</param>
        public static void Destroy(Object obj)
        {
            if (obj == null)
                return;
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
                Object.Destroy(obj);
            else
#endif
            Object.DestroyImmediate(obj, true);
        }

        public static bool AllocateIfNeededForCompute(ref RenderTexture rt, int width, int height, GraphicsFormat format, int mipCount = 1, bool autoGenerateMips = false)
        {
            if (AllocateIfNeeded(ref rt, width, height, format, mipCount, autoGenerateMips))
            {
                // When using compute shaders, we need random access write,
                // and must create the texture explicitly since it won't be bound as a graphics target before use.
                rt.enableRandomWrite = true;
                rt.Create();
                return true;
            }

            return false;
        }

        public static bool AllocateIfNeeded(ref RenderTexture rt, int width, int height, int mipCount = 1, bool autoGenerateMips = false)
        {
            return AllocateIfNeeded(ref rt, width, height, GraphicsFormat.R8G8B8A8_UNorm, mipCount, autoGenerateMips);
        }

        public static bool AllocateIfNeeded(ref RenderTexture rt, int width, int height, GraphicsFormat format, int mipCount = 1, bool autoGenerateMips = false)
        {
            // TODO Fragile or wasteful for mipmap related params.
            if (rt == null ||
                rt.width != width ||
                rt.height != height ||
                rt.graphicsFormat != format ||
                rt.mipmapCount != mipCount)
            {
                if (rt != null)
                {
                    rt.Release();
                }

                rt = new RenderTexture(width, height, 0, format, mipCount);
                rt.useMipMap = mipCount > 0;
                rt.autoGenerateMips = autoGenerateMips;
                return true;
            }

            return false;
        }

        public static void DeallocateIfNeeded(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }

            rt = null;
        }

        public static void DeallocateIfNeeded(RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }

        public static Material CreateMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"Could not find shader \"{shaderName}\", " +
                    "make sure it has been added to the list of Always Included shaders");
            }

            return new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public static bool AllocateBufferIfNeeded<T>(
            ref ComputeBuffer buffer, int count,
            ComputeBufferType type = ComputeBufferType.Default, bool allowLarger = false) where T : struct
        {
            var stride = Marshal.SizeOf<T>();

            // We cannot fetch ComputeBufferType, but we also do not expect it to change for a given buffer.
            var needsAlloc = buffer == null || (allowLarger ? buffer.count < count : buffer.count != count);

            if (needsAlloc)
            {
                DeallocateIfNeeded(ref buffer);
                buffer = new ComputeBuffer(count, stride, type);
                return true;
            }

            return false;
        }

        public static bool AllocateBufferIfNeeded<T>(ref ComputeBuffer buffer, NativeArray<T> data) where T : struct
        {
            var allocated = AllocateBufferIfNeeded<T>(ref buffer, data.Length);
            buffer.SetData(data);
            return allocated;
        }

        public static void DeallocateIfNeeded(ref ComputeBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Dispose();
            buffer = null;
        }

        public static bool AllocateNativeArrayIfNeeded<T>(
            ref NativeArray<T> array, int size, Allocator allocator = Allocator.Persistent) where T : struct
        {
            if (!array.IsCreated || array.Length != size)
            {
                DeallocateNativeArrayIfNeeded(ref array);

                array = new NativeArray<T>(size, allocator);
                return true;
            }

            return false;
        }

        public static bool DeallocateNativeArrayIfNeeded<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
                return true;
            }

            return false;
        }

        public static void Swap<T>(ref T a, ref T b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }

        public static void GetLowDiscrepancySequence(NativeArray<Vector2> result)
        {
            for (var i = 0; i < result.Length; ++i)
            {
                var x = GetHaltonSequence((i & 1023) + 1, 2);
                var y = GetHaltonSequence((i & 1023) + 1, 3);
                result[i] = new Vector2(x, y);
            }
        }

        // Borrowed from HDRP.
        /// <summary>
        /// Gets a deterministic sample in the Halton sequence.
        /// </summary>
        /// <param name="index">The index in the sequence.</param>
        /// <param name="radix">The radix of the sequence.</param>
        /// <returns>A sample from the Halton sequence.</returns>
        static float GetHaltonSequence(int index, int radix)
        {
            var result = 0f;
            var fraction = 1f / radix;

            while (index > 0)
            {
                result += (index % radix) * fraction;

                index /= radix;
                fraction /= radix;
            }

            return result;
        }

        // A small helper handling warp sizes.
        public static void DispatchCompute(CommandBuffer cmd, ComputeShader shader, int kernel, int width, int height)
        {
            const int groupSize = 16;
            var warpX = Mathf.CeilToInt((float)width / groupSize);
            var warpY = Mathf.CeilToInt((float)height / groupSize);
            cmd.DispatchCompute(shader, kernel, warpX, warpY, 1);
        }

        public static void SetTexelSize(CommandBuffer cmd, ComputeShader shader, Texture tex)
        {
            cmd.SetComputeVectorParam(shader, ShaderIDs._TexelSize,
                new Vector4(tex.width, tex.height, 1 / (float)tex.width, 1 / (float)tex.height));
        }

        public static string GetAbsoluteOutputDirectory(string directory, string fileName)
        {
            var info = Directory.GetParent(Application.dataPath);
            Assert.IsNotNull(info);
            return Path.Combine(info.FullName, Path.Combine(directory, fileName));
        }

        public static void CreateOrClearDirectory(string path)
        {
            var info = new DirectoryInfo(path);
            if (info.Exists)
            {
                info.Delete(true);
            }

            info.Create();
        }

        public static NativeArray<T> GetTempNativeArray<T>(T[] arr) where T : struct
        {
            var nativeArr = new NativeArray<T>(arr.Length, Allocator.Temp);
            nativeArr.CopyFrom(arr);
            return nativeArr;
        }

        public static void Reverse<T>(NativeArray<T> arr) where T : struct
        {
            for (var i = 0; i != arr.Length / 2; i++)
            {
                var tmp = arr[i];
                arr[i] = arr[arr.Length - i - 1];
                arr[arr.Length - i - 1] = tmp;
            }
        }

        // Returns the smallest Rect encompassing both a and b.
        public static Rect Encompass(Rect a, Rect b)
        {
            var min = math.min(a.min, b.min);
            var max = math.max(a.max, b.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Rect Encompass(Rect rect, Vector2 point)
        {
            var min = math.min(rect.min, point);
            var max = math.max(rect.max, point);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Rect Encompass(Vector2 a, Vector2 b)
        {
            var min = math.min(a, b);
            var max = math.max(a, b);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static void SaveAsPng(RenderTexture renderTexture, string path)
        {
            var format = GraphicsFormatUtility.GetTextureFormat(renderTexture.graphicsFormat);
            SaveAsPng(renderTexture, path, format);
        }

        static void SaveAsPng(RenderTexture renderTexture, string path, TextureFormat format)
        {
            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            RenderTexture.active = previousActive;

            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }

        public static bool ReadElapsedMs(CustomSampler sampler, ref double ms)
        {
            var recorder = sampler.GetRecorder();
            if (recorder.isValid)
            {
                var prevMs = ms;
                var newMs = recorder.elapsedNanoseconds / 1e6;
                if (newMs == 0)
                {
                    return false;
                }

                ms = newMs;
                return prevMs != ms;
            }

            return false;
        }

        public static bool ReadGpuElapsedMs(CustomSampler sampler, ref double ms)
        {
            var recorder = sampler.GetRecorder();
            if (recorder.isValid)
            {
                var prevMs = ms;
                var newMs = recorder.gpuElapsedNanoseconds / 1e6;
                if (newMs == 0)
                {
                    return false;
                }

                ms = newMs;
                return prevMs != ms;
            }

            return false;
        }
    }
}
