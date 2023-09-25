using System;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer
{
    static class AlgebraUtil
    {
        public static readonly float3 AxisX = Vector3.right;
        public static readonly float3 AxisY = Vector3.up;
        public static readonly float3 AxisZ = Vector3.forward;

        /// <summary>
        /// Infers a covariance matrix from eigenvectors/eigenvalues,
        /// inferred from rotation and scale applied to basis vectors.
        /// </summary>
        /// <param name="rotation">The rotation applied to basis vectors.</param>
        /// <param name="scale">The scale applied to basis vectors.</param>
        /// <returns>The covariance matrix.</returns>
        public static float3x3 GetCovariance(float3 rotation, float3 scale)
        {
            var q = quaternion.Euler(rotation);

            var eigenVectors = float3x3.zero;
            eigenVectors.c0 = math.mul(q, AxisX);
            eigenVectors.c1 = math.mul(q, AxisY);
            eigenVectors.c2 = math.mul(q, AxisZ);

            var eigenValues = float3x3.zero;
            eigenValues[0][0] = scale.x;
            eigenValues[1][1] = scale.y;
            eigenValues[2][2] = scale.z;

            return math.mul(eigenVectors, math.mul(eigenValues, math.transpose(eigenVectors)));
        }

        /// <summary>
        /// Returns the lower Cholesky Factor, L, of input matrix A.
        /// Satisfies the equation: L*L^T = A.
        /// </summary>
        /// <param name="input">Input matrix must be square, symmetric,
        /// and positive definite. This method does not check for these properties,
        /// and may produce unexpected results of those properties are not met.</param>
        /// <returns>The lower Cholesky Factor</returns>
        public static float3x3 CholeskyDecomposition(float3x3 input)
        {
            var result = float3x3.zero;
            for (var r = 0; r < 3; r++)
                for (var c = 0; c <= r; c++)
                {
                    if (c == r)
                    {
                        var sum = 0f;
                        for (var j = 0; j < c; j++)
                        {
                            sum += result[j][c] * result[j][c];
                        }

                        result[c][c] = math.sqrt(input[c][c] - sum);
                    }
                    else
                    {
                        var sum = 0f;
                        for (var j = 0; j < c; j++)
                        {
                            sum += result[j][r] * result[j][c];
                        }

                        result[c][r] = 1 / result[c][c] * (input[c][r] - sum);
                    }
                }

            return result;
        }

        // Naive matrix comparison, relies on per-entry absolute differences.
        public static bool ApproximatelyEqual(float3x3 covariance, float3x3 reconstructed, float epsilon = math.EPSILON)
        {
            var diff = covariance - reconstructed;
            var one3 = new float3(1, 1, 1);

            // Sum the absolute values of all matrix entries.
            var sum = math.abs(math.dot(one3, diff.c0)) +
                math.abs(math.dot(one3, diff.c1)) +
                math.abs(math.dot(one3, diff.c2));
            return sum < epsilon;
        }

        public static float CorrelationMatrixDistance(float3x3 r1, float3x3 r2)
        {
            var m = math.mul(r1, r2);
            var trace = m[0][0] + m[1][1] + m[2][2];
            return 1 - trace / (FrobeniusNorm(r1) * FrobeniusNorm(r2));
        }

        public static float FrobeniusNorm(float3x3 m)
        {
            var sqSum = 0f;
            for (var i = 0; i != 3; ++i)
                for (var j = 0; j != 3; ++j)
                {
                    var e = m[i][j];
                    sqSum += e * e;
                }

            return math.sqrt(sqSum);
        }
    }
}
