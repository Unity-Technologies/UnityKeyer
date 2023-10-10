using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer
{
    // All data is written in written flat, and .reshape() is required on the python side.
    static class NumpyExport
    {
        public static void Write(string path, NativeArray<float3x3> data)
        {
            using var writer = new StreamWriter(path);
            foreach (var item in data)
            {
                Write(writer, item);
            }
        }

        public static void Write(string path, NativeArray<Vector4> data, int size)
        {
            using var writer = new StreamWriter(path);
            for (var i = 0; i != size; ++i)
            {
                Write(writer, data[i]);
            }
        }

        public static void Write(string path, NativeArray<float3> data)
        {
            using var writer = new StreamWriter(path);
            foreach (var item in data)
            {
                Write(writer, item);
            }
        }

        public static void Write(StreamWriter writer, float3x3 value)
        {
            Write(writer, Row0(value));
            Write(writer, Row1(value));
            Write(writer, Row2(value));
        }

        public static void Write(StreamWriter writer, Vector3 value)
        {
            writer.WriteLine(value.x.ToString());
            writer.WriteLine(value.y.ToString());
            writer.WriteLine(value.z.ToString());
        }

        static void Write(StreamWriter writer, Vector4 value)
        {
            writer.WriteLine(value.x.ToString());
            writer.WriteLine(value.y.ToString());
            writer.WriteLine(value.z.ToString());
            writer.WriteLine(value.w.ToString());
        }

        static Vector3 Row0(float3x3 m)
        {
            return new(m.c0.x, m.c1.x, m.c2.x);
        }

        static Vector3 Row1(float3x3 m)
        {
            return new(m.c0.y, m.c1.y, m.c2.y);
        }

        static Vector3 Row2(float3x3 m)
        {
            return new(m.c0.z, m.c1.z, m.c2.z);
        }
    }
}
