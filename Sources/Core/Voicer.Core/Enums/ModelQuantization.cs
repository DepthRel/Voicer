namespace Voicer.Core.Enums;

/// <summary>
/// How finely the weights of a model file are stored: the fewer bits, the smaller the file and the memory it takes,
/// at the cost of a little accuracy. The values go from the most accurate to the smallest.
/// </summary>
public enum ModelQuantization
{
    /// <summary>
    /// Full precision, 16-bit floating point: the most accurate and the largest.
    /// </summary>
    F16,

    /// <summary>
    /// 8-bit integers: practically the accuracy of full precision at about half the size.
    /// </summary>
    Q8,

    /// <summary>
    /// 5-bit integers: about a third of the size of full precision, a little less accurate.
    /// </summary>
    Q5,
}
