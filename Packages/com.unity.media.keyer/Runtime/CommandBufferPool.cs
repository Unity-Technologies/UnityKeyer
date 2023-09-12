using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    static class CommandBufferPool
    {
        static ObjectPool<CommandBuffer> s_BufferPool = new(() => new CommandBuffer(), null, x => x.Clear());

        public static CommandBuffer Get()
        {
            var cmd = s_BufferPool.Get();
            cmd.name = "";
            return cmd;
        }

        public static CommandBuffer Get(string name)
        {
            var cmd = s_BufferPool.Get();
            cmd.name = name;
            return cmd;
        }

        public static void Release(CommandBuffer buffer)
        {
            s_BufferPool.Release(buffer);
        }
    }
}
