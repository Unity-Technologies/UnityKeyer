using System;

namespace Unity.Media.Keyer
{
    // An attribute used to load compute shaders.
    [AttributeUsage(AttributeTargets.Field)]
    class ShaderPathAttribute : Attribute
    {
        public string Path { get; }

        public ShaderPathAttribute(string path)
        {
            Path = path;
        }
    }
}
