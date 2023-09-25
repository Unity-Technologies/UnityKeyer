using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class RenderGraphTests
    {
        struct PassData
        {
            public TextureHandle Input;
            public TextureHandle Output;
        }

        class Pass : IRenderPass
        {
            readonly PassData m_Data;

            public Pass(PassData data)
            {
                m_Data = data;
            }

            public void Execute(CommandBuffer cmd, Context ctx)
            {
                // Just consume handles.
                var input = (Texture)m_Data.Input;
                var output = (Texture)m_Data.Output;
            }

            public TextureHandle Output => m_Data.Output;

            public IEnumerable<TextureHandle> Inputs
            {
                get { yield return m_Data.Input; }
            }
        }

        CommandBuffer m_CommandBuffer;

        [SetUp]
        public void Setup()
        {
            AssetDatabase.Refresh();
            m_CommandBuffer = new CommandBuffer();
            m_CommandBuffer.name = nameof(RenderGraphTests);
        }

        [TearDown]
        public void TearDown()
        {
            m_CommandBuffer.Dispose();
        }

        [Test]
        public void UsedPassesAreNotCulled()
        {
            var graph = new RenderGraph();

            using (var builder = graph.GetBuilder(128, 128))
            {
                var pass0 = new Pass(new PassData
                {
                    Input = builder.Handles.FromTexture(Texture2D.blackTexture),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "test"),
                });
                builder.AddPass(pass0);

                var pass1 = new Pass(new PassData
                {
                    Input = builder.Handles.CreateReadTransient(pass0.Output),
                    Output = builder.Handles.FromTexture(Texture2D.blackTexture)
                });
                builder.AddPass(pass1);
            }

            Assert.IsTrue(graph.RenderPasses.Count == 2);

            graph.Dispose();
        }

        [Test]
        public void UselessPassIsCulled()
        {
            var graph = new RenderGraph();

            using (var builder = graph.GetBuilder(128, 128))
            {
                var pass0 = new Pass(new PassData
                {
                    Input = builder.Handles.FromTexture(Texture2D.blackTexture),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "test"),
                });
                builder.AddPass(pass0);
            }

            Assert.IsTrue(graph.RenderPasses.Count == 0);

            graph.Dispose();
        }

        [Test]
        public void PassCullingIsRecursive()
        {
            var graph = new RenderGraph();

            using (var builder = graph.GetBuilder(128, 128))
            {
                var pass0 = new Pass(new PassData
                {
                    Input = builder.Handles.FromTexture(Texture2D.blackTexture),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "out0"),
                });
                builder.AddPass(pass0);

                var pass1 = new Pass(new PassData
                {
                    Input = builder.Handles.CreateReadTransient(pass0.Output),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "out1"),
                });
                builder.AddPass(pass1);

                var pass2 = new Pass(new PassData
                {
                    Input = builder.Handles.CreateReadTransient(pass1.Output),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "out2"),
                });
                builder.AddPass(pass2);
            }

            // All 3 passes were culled for the final output is not used.
            Assert.IsTrue(graph.RenderPasses.Count == 0);

            graph.Dispose();
        }

        [Test]
        public void PassCullingDoesTraversal()
        {
            var graph = new RenderGraph();
            Pass pass0;
            Pass pass1;
            Pass pass2;

            using (var builder = graph.GetBuilder(128, 128))
            {
                pass0 = new Pass(new PassData
                {
                    Input = builder.Handles.FromTexture(Texture2D.blackTexture),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "out0"),
                });
                builder.AddPass(pass0);

                pass1 = new Pass(new PassData
                {
                    Input = builder.Handles.CreateReadTransient(pass0.Output),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "out1"),
                });
                builder.AddPass(pass1);

                pass2 = new Pass(new PassData
                {
                    Input = builder.Handles.CreateReadTransient(pass0.Output),
                    Output = builder.Handles.FromTexture(Texture2D.blackTexture),
                });
                builder.AddPass(pass2);
            }

            Assert.IsTrue(graph.RenderPasses.Count == 2);
            Assert.IsTrue(graph.RenderPasses.Contains(pass0));
            Assert.IsFalse(graph.RenderPasses.Contains(pass1));
            Assert.IsTrue(graph.RenderPasses.Contains(pass2));

            graph.Dispose();
        }

        [Test]
        public void CannotAddSamePassTwice()
        {
            var graph = new RenderGraph();

            using (var builder = graph.GetBuilder(128, 128))
            {
                var pass0 = new Pass(new PassData
                {
                    Input = builder.Handles.FromTexture(Texture2D.blackTexture),
                    Output = builder.Handles.FromTexture(Texture2D.blackTexture),
                });
                builder.AddPass(pass0);

                Assert.Throws<InvalidOperationException>(() => builder.AddPass(pass0));
            }

            graph.Dispose();
        }

        [Test]
        public void ExecuteWhileBuildingThrows()
        {
            using var graph = new RenderGraph();
            using var builder = graph.GetBuilder(128, 128);

            var pass0 = new Pass(new PassData
            {
                Input = builder.Handles.FromTexture(Texture2D.blackTexture),
                Output = builder.Handles.FromTexture(Texture2D.blackTexture),
            });
            builder.AddPass(pass0);

            Assert.Throws<InvalidOperationException>(() =>
            {
                try
                {
                    graph.Execute(m_CommandBuffer);
                }
                catch (InvalidOperationException)
                {
                    builder.Dispose();
                    throw;
                }
            });
        }

        [Test]
        public void BuilderWithInvalidSizeThrows()
        {
            var graph = new RenderGraph();

            Assert.Throws<InvalidOperationException>(() =>
            {
                using var builder = graph.GetBuilder(0, 128);
            });

            graph.Dispose();
        }
    }
}
