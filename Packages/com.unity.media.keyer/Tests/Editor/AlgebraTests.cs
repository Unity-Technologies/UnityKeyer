using System;
using NUnit.Framework;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class AlgebraTests
    {
        [Test]
        public void TestCholeskyDecomposition()
        {
            var random = Random.CreateFromIndex(0);

            for (var i = 0; i != 32; ++i)
            {
                // Evaluate covariance matrix from eigenvectors / eigenvalues.
                var rotation = GetRandomVector(random, 0, math.PI);
                var scale = GetRandomVector(random, .05f, .2f);
                var covariance = AlgebraUtil.GetCovariance(rotation, scale);

                // Evaluate Cholesky decomposition.
                var cholseky = AlgebraUtil.CholeskyDecomposition(covariance);
                var reconstructed = math.mul(cholseky, math.transpose(cholseky));
                Assert.IsTrue(AlgebraUtil.ApproximatelyEqual(covariance, reconstructed));
            }
        }

        static float3 GetRandomVector(Random random, float min, float max)
        {
            return new float3
            {
                x = random.NextFloat(min, max),
                y = random.NextFloat(min, max),
                z = random.NextFloat(min, max)
            };
        }
    }
}
