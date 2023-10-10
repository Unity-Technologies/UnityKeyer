using System;

namespace Unity.Media.Keyer
{
    // An attribute used to load shaders.
    [AttributeUsage(AttributeTargets.Field)]
    class ShaderNameAttribute : Attribute
    {
        public string Name { get; }

        public ShaderNameAttribute(string name)
        {
            Name = name;
        }
    }
}
