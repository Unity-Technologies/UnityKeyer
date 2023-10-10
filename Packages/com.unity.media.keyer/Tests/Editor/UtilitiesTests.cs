using NUnit.Framework;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class UtilitiesTests
    {
        RenderTexture m_RenderTexture;

        [TearDown]
        public void TearDown()
        {
            Utilities.DeallocateIfNeeded(ref m_RenderTexture);
        }

        [Test]
        public void AllocatesWhenNull()
        {
            var rt = default(RenderTexture);
            var alloc = Utilities.AllocateIfNeeded(ref rt, 2, 2);
            Assert.IsTrue(alloc);
        }

        [Test]
        public void DoesNotReallocate()
        {
            var rt = default(RenderTexture);
            var alloc = Utilities.AllocateIfNeeded(ref rt, 2, 2);
            Assert.IsTrue(alloc);
            alloc = Utilities.AllocateIfNeeded(ref rt, 2, 2);
            Assert.IsFalse(alloc);
        }

        [Test]
        public void ReallocatesWhenParamsChange()
        {
            var rt = default(RenderTexture);
            var alloc = Utilities.AllocateIfNeeded(ref rt, 2, 2);
            Assert.IsTrue(alloc);
            alloc = Utilities.AllocateIfNeeded(ref rt, 2, 4);
            Assert.IsTrue(alloc);
        }
    }
}
