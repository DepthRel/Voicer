namespace Voicer.Core.Search;

/// <summary>
/// The arithmetic of the vectors of meaning: of unit length, their dot product is the cosine of their angle.
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Makes a vector of unit length.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>A new vector in the same direction; zeros stay zeros.</returns>
    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        var result = vector.ToArray();
        var sum = 0.0;
        foreach (var value in vector)
        {
            sum += (double)value * value;
        }

        var length = Math.Sqrt(sum);
        if (length > 0)
        {
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (float)(result[i] / length);
            }
        }

        return result;
    }

    /// <summary>
    /// Tells how close two vectors of unit length are.
    /// </summary>
    /// <param name="first">The first vector.</param>
    /// <param name="second">The second vector; of another length, it is not comparable.</param>
    /// <returns>The cosine, from -1 to 1; <see cref="float.NegativeInfinity"/> for vectors of different lengths.</returns>
    public static float Similarity(ReadOnlySpan<float> first, ReadOnlySpan<float> second)
    {
        if (first.Length != second.Length)
        {
            return float.NegativeInfinity;
        }

        var sum = 0f;
        for (var i = 0; i < first.Length; i++)
        {
            sum += first[i] * second[i];
        }

        return sum;
    }
}
