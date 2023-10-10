using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;

namespace Unity.Media.Keyer
{
    enum ResourceType : byte
    {
        Static,
        Transient
    }

    enum TransientUsage : byte
    {
        // None is used for handles who actually are not transients.
        None,
        Read,
        Write,
        Temp
    }

    readonly struct TextureHandle
    {
        readonly ResourceType m_Type;
        readonly BufferFormat m_Format;
        readonly TransientUsage m_TransientUsage;

        // We have one id per ResourceType.
        // See comments in TextureHandles data structures declaration.
        readonly int m_StaticId;
        readonly byte m_TransientId;

        // We could have chosen not to carry a reference to the handles manager.
        // But doing so forces the instantiator to have a reference to that manager,
        // which lowers the odds of handles created outside of a manager (and therefore invalid).
        readonly TextureHandles m_Handles;

        public TextureHandle(TextureHandles handles, ResourceType type, BufferFormat format, TransientUsage transientUsage, int staticId, byte transientId)
        {
            m_Handles = handles;
            m_Type = type;
            m_Format = format;
            m_TransientUsage = transientUsage;
            m_StaticId = staticId;
            m_TransientId = transientId;
        }

        public bool IsValid() => m_Handles != null && m_Handles.IsValid(this);

        public string Name => m_Handles?.GetName(this);
        public ResourceType Type => m_Type;
        public BufferFormat Format => m_Format;
        public TransientUsage TransientUsage => m_TransientUsage;
        public int StaticId => m_StaticId;
        public byte TransientId => m_TransientId;

        // For lighter user code.
        public static implicit operator Texture(TextureHandle id) => id.m_Handles.GetTexture(id);

        public GraphicsFormat GetGraphicsFormat()
        {
            // In case of a static resources, simply return the texture's format,
            // the texture is known already.
            if (m_Type == ResourceType.Static)
            {
                return m_Handles.GetTexture(this).graphicsFormat;
            }

            // In case of a transient resources, we know we are using a non-None BufferFormat,
            // and can confidently perform the conversion.
            // Alternatively, we could rely on fetching the underlying texture as well,
            // but that may trigger an unnecessary allocation of that texture.
            return BufferFormatUtility.GetGraphicsFormat(m_Format);
        }
    }

    class TextureHandles : IDisposable
    {
        // For static handles, we must rely on an hashcode (int).
        readonly Dictionary<int, Texture> m_StaticTextures = new();

        // For transient ones, we id them through a bounded index (byte),
        // that allows us to use faster data structures to manage them. (arrays instead of dictionary.)
        readonly RenderTexture[] m_TransientTextures = new RenderTexture[Byte.MaxValue];

        // refs are counted using byte as no more than Byte.MaxValue transients can be allocated.
        readonly byte[] m_TransientRefs = new byte[Byte.MaxValue];
        readonly byte[] m_TransientDeRefsOnFrame = new byte[Byte.MaxValue];

        // These names for transient textures are meant for meaningful error messages.
        // It is easy to mismanage transients and clients need help to figure where in the render graph the issue lies.
        readonly string[] m_TransientNames = new string[Byte.MaxValue];

        // Track available transient slots.
        readonly Stack<byte> m_FreeTransients = new();

        // Track in use transient slots.
        readonly HashSet<byte> m_ActiveTransients = new();
        readonly HashSet<byte> m_TransientsPendingRelease = new();

        TexturePool m_TexturePool;

        public TextureHandles(TexturePool texturePool)
        {
            m_TexturePool = texturePool;
            for (byte i = 0; i != Byte.MaxValue; ++i)
            {
                m_FreeTransients.Push(i);
            }
        }

        // Note that whether or not pending deletions have been executed will affect the result..
        public int GetAllocatedTransientTexturesCount()
        {
            var count = 0;
            foreach (var id in m_ActiveTransients)
            {
                if (m_TransientTextures[id] != null)
                {
                    ++count;
                }
            }

            return count;
        }

        public bool IsValid(TextureHandle handle)
        {
            if (handle.Type == ResourceType.Static)
            {
                return handle.TransientUsage == TransientUsage.None && m_StaticTextures.ContainsKey(handle.StaticId);
            }

            if (!m_ActiveTransients.Contains(handle.TransientId))
            {
                return false;
            }

            switch (handle.TransientUsage)
            {
                case TransientUsage.None: return false;
                case TransientUsage.Read:
                case TransientUsage.Temp:
                    return m_TransientRefs[handle.TransientId] > 0;
            }

            return true;
        }

        public string GetName(TextureHandle handle)
        {
            if (handle.Type == ResourceType.Static)
            {
                return GetTexture(handle).name;
            }

            return m_TransientNames[handle.TransientId];
        }

        byte AllocateTransient()
        {
            // This is obviously an error for no realistic render graph would max out the transient limit.
            // (Using byte allows for reasonable memory usage.)
            if (m_FreeTransients.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(TextureHandles)}, too many transient textures. " +
                    $"Make sure to call {nameof(Dispose)} before creating new handles.");
            }

            var id = m_FreeTransients.Pop();
            m_ActiveTransients.Add(id);
            return id;
        }

        // We expect every transient to have at least one ref. Otherwise there was no point in writing to it.
        public void CheckForUnusedTransients()
        {
            foreach (var id in m_ActiveTransients)
            {
                if (m_TransientRefs[id] < 1)
                {
                    throw new InvalidOperationException(
                        $"{nameof(TextureHandles)}, unused transient texture \"{m_TransientNames[id]}\".");
                }
            }
        }

        public bool IsUnusedTransient(TextureHandle handle)
        {
            if (handle.Type == ResourceType.Transient)
            {
                if (m_TransientRefs[handle.TransientId] < 1)
                {
                    return true;
                }
            }

            return false;
        }

        public void Reset()
        {
            foreach (var id in m_ActiveTransients)
            {
                m_TransientDeRefsOnFrame[id] = 0;
            }
        }

        public void Dispose()
        {
            // We do not own static textures, so don't dispose them.
            m_StaticTextures.Clear();
            m_TransientsPendingRelease.Clear();

            foreach (var id in m_ActiveTransients)
            {
                DestroyTransient(id);
                m_FreeTransients.Push(id);
            }

            m_ActiveTransients.Clear();

            // All slots should be free.
            Assert.AreEqual(m_FreeTransients.Count, Byte.MaxValue);
        }

        public int ReleaseConsumedTransients()
        {
            foreach (var index in m_TransientsPendingRelease)
            {
                m_TexturePool.Release(m_TransientTextures[index]);
                m_TransientTextures[index] = null;
                m_TransientDeRefsOnFrame[index] = 0;
            }

            var count = m_TransientsPendingRelease.Count;
            m_TransientsPendingRelease.Clear();
            return count;
        }

        public bool DestroyIfTransient(TextureHandle handle)
        {
            CheckHandleIsValid(handle);

            if (handle.Type == ResourceType.Transient)
            {
                switch (handle.TransientUsage)
                {
                    case TransientUsage.Write:
                    case TransientUsage.Temp:
                        DestroyTransient(handle.TransientId);
                        m_ActiveTransients.Remove(handle.TransientId);
                        m_FreeTransients.Push(handle.TransientId);
                        return true;
                    case TransientUsage.Read:
                    {
                        m_TransientRefs[handle.TransientId]--;
                        return true;
                    }
                }
            }

            return false;
        }

        public TextureHandle CreateWriteTransient(BufferFormat format, string name)
        {
            CheckFormatIsNotNone(format);
            var id = AllocateTransient();
            m_TransientRefs[id] = 0;
            m_TransientDeRefsOnFrame[id] = 0;
            m_TransientNames[id] = name;
            return new TextureHandle(this, ResourceType.Transient, format, TransientUsage.Write, -1, id);
        }

        public TextureHandle CreateTempTransient(BufferFormat format, string name)
        {
            CheckFormatIsNotNone(format);
            var id = AllocateTransient();

            // One ref automatically to pass sanity checks.
            // Will be released right away in any case.
            m_TransientRefs[id] = 1;
            m_TransientDeRefsOnFrame[id] = 0;
            m_TransientNames[id] = name;
            return new TextureHandle(this, ResourceType.Transient, format, TransientUsage.Temp, -1, id);
        }

        public TextureHandle CreateReadTransient(TextureHandle handle)
        {
            CheckHandleIsValid(handle);

            if (!m_ActiveTransients.Contains(handle.TransientId))
            {
                throw new InvalidOperationException(
                    $"{nameof(CreateReadTransient)}, attempt to read a transient texture \"{handle.Name}\" " +
                    $"that was not first created using {nameof(CreateReadTransient)}.");
            }

            if (handle.Type != ResourceType.Transient)
            {
                throw new InvalidOperationException(
                    $"{nameof(CreateReadTransient)} expects a transient {nameof(TextureHandle)}.");
            }

            if (handle.TransientUsage != TransientUsage.Write)
            {
                throw new InvalidOperationException(
                    $"{nameof(CreateReadTransient)} expects a transient with usage {handle.TransientUsage}.");
            }

            // Register the read operation.
            m_TransientRefs[handle.TransientId]++;
            return new TextureHandle(this, handle.Type, handle.Format, TransientUsage.Read, -1, handle.TransientId);
        }

        public TextureHandle FromTexture(Texture tex)
        {
            var hash = tex.GetHashCode();

            // Insert if needed. Repeated calls to this method with the same parameter is not an error.
            if (!m_StaticTextures.ContainsKey(hash))
            {
                m_StaticTextures.Add(hash, tex);
            }

            // We are not concerned about the format of static handles yet.
            return new TextureHandle(this, ResourceType.Static, BufferFormat.None, TransientUsage.None, hash, 0);
        }

        public Texture GetTexture(TextureHandle handle)
        {
            // For static handles we simply return the reference.
            if (handle.Type == ResourceType.Static)
            {
                if (m_StaticTextures.TryGetValue(handle.StaticId, out var staticResult))
                {
                    return staticResult;
                }

                throw new InvalidOperationException(
                    $"Attempt to read invalid static texture \"{handle.Name}\". " +
                    $"Make sure the texture was registered using {nameof(FromTexture)}.");
            }

            switch (handle.TransientUsage)
            {
                case TransientUsage.Temp: return TempTransient(handle);
                case TransientUsage.Read: return ReadTransient(handle);
                case TransientUsage.Write: return WriteTransient(handle);
                case TransientUsage.None:
                    throw new InvalidOperationException(
                        $"Transient texture \"{handle.Name}\" cannot have usage {TransientUsage.None}.");
            }

            // Just for syntactic correctness, code should never be reached.
            return null;
        }

        Texture ReadTransient(TextureHandle handle)
        {
            if (!m_ActiveTransients.Contains(handle.TransientId))
            {
                throw new InvalidOperationException(
                    $"Attempt to read invalid transient texture \"{handle.Name}\". " +
                    $"Make sure the texture was registered using {nameof(CreateWriteTransient)}.");
            }

            // For transient handles we need to keep track of read counts and disposal.
            var transientResult = m_TransientTextures[handle.TransientId];

            if (transientResult == null)
            {
                throw new InvalidOperationException(
                    $"Attempt to read invalid transient texture \"{handle.Name}\", " +
                    "Make sure the texture was written to before attempting read access.");
            }

            var deRef = ++m_TransientDeRefsOnFrame[handle.TransientId];
            var refs = m_TransientRefs[handle.TransientId];
            if (deRef >= refs)
            {
                // We use a set to avoid scheduling multiple releases of the same resource.
                m_TransientsPendingRelease.Add(handle.TransientId);
            }

            return transientResult;
        }

        Texture WriteTransient(TextureHandle handle)
        {
            if (m_TransientTextures[handle.TransientId] != null)
            {
                throw new InvalidOperationException(
                    $"Writing to already written to transient texture \"{handle.Name}\" is not allowed.");
            }

            var result = m_TexturePool.Get(handle.Format);
            m_TransientTextures[handle.TransientId] = result;
            return result;
        }

        Texture TempTransient(TextureHandle handle)
        {
            // Immediate scheduling for release.
            m_TransientsPendingRelease.Add(handle.TransientId);
            return WriteTransient(handle);
        }

        void DestroyTransient(byte id)
        {
            var texture = m_TransientTextures[id];
            if (texture != null)
            {
                m_TexturePool.Release(texture);
                m_TransientTextures[id] = null;
            }

            m_TransientTextures[id] = null;
            m_TransientRefs[id] = default;
            m_TransientNames[id] = String.Empty;
        }

        static void CheckHandleIsValid(TextureHandle handle)
        {
            if (!handle.IsValid())
            {
                throw new InvalidOperationException(
                    $"{nameof(TextureHandle)} \"{handle.Name}\" is invalid.");
            }
        }

        static void CheckFormatIsNotNone(BufferFormat format)
        {
            if (format == BufferFormat.None)
            {
                throw new InvalidOperationException(
                    $"{nameof(BufferFormat)} {nameof(BufferFormat.None)} is invalid.");
            }
        }
    }
}
