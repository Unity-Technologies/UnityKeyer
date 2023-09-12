float DeterminantSymmetric(in float3x3 m)
{
    return 2 * m[0][1] * m[0][2] * m[1][2]
             - m[0][0] * m[1][2] * m[1][2]
             + m[0][0] * m[1][1] * m[2][2]
             - m[0][1] * m[0][1] * m[2][2]
             - m[0][2] * m[0][2] * m[1][1];
}

float3x3 InvertSymmetric(in float3x3 m, float detReciprocal)
{
    float3x3 inv;
    inv[0][0] = (-m[1][2] * m[1][2] + m[1][1] * m[2][2]) * detReciprocal;
    inv[0][1] = ( m[0][2] * m[1][2] - m[0][1] * m[2][2]) * detReciprocal;
    inv[0][2] = (-m[0][2] * m[1][1] + m[0][1] * m[1][2]) * detReciprocal;
    inv[1][0] = inv[0][1];
    inv[1][1] = (-m[0][2] * m[0][2] + m[0][0] * m[2][2]) * detReciprocal;
    inv[1][2] = ( m[0][1] * m[0][2] - m[0][0] * m[1][2]) * detReciprocal;
    inv[2][0] = inv[0][2];
    inv[2][1] = inv[1][2];
    inv[2][2] = (-m[0][1] * m[0][1] + m[0][0] * m[1][1]) * detReciprocal;

    return inv;
}

float3x3 OuterProduct(in float3 a, in float3 b)
{
    return float3x3(a.x * b.x, a.x * b.y, a.x * b.z,
                    a.y * b.x, a.y * b.y, a.y * b.z,
                    a.z * b.x, a.z * b.y, a.z * b.z);
}
