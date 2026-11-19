using Voicer.Core.Enums;

namespace Voicer.Core.Models;

/// <summary>
/// A speech recognition model the application can download and run: one file of a model family in one quantization.
/// </summary>
/// <param name="Id">The identifier of this file of the model; Latin letters, digits and dashes.</param>
/// <param name="Family">
/// The identifier of the model family; it is also the name of its folder in <c>Data/Models</c>, which keeps every
/// quantization of the family, so it holds only Latin letters, digits and dashes.
/// </param>
/// <param name="Name">The name of the model family as it is shown; a product name, the same in every language.</param>
/// <param name="Quantization">How finely the weights of this file are stored.</param>
/// <param name="FileName">The name of the model file.</param>
/// <param name="DownloadUri">Where the model file is downloaded from.</param>
/// <param name="SizeBytes">The exact size of the model file, in bytes.</param>
/// <param name="Sha256">The SHA-256 of the model file, in lower-case hexadecimal.</param>
/// <param name="RunsOnCpu">Whether the model works at a usable speed on the processor alone, without a video card.</param>
public sealed record SpeechModel(
    string Id,
    string Family,
    string Name,
    ModelQuantization Quantization,
    string FileName,
    Uri DownloadUri,
    long SizeBytes,
    string Sha256,
    bool RunsOnCpu = false)
{
    /// <summary>
    /// Gets the name of the model together with its quantization, as it is shown in the list of models and written into
    /// the exported document; product and format names, the same in every language.
    /// </summary>
    public string Title => $"{Name} {Quantization}";

    /// <summary>
    /// Gets the key of the text that describes the model family to the user.
    /// </summary>
    public string DescriptionKey => $"Model.{Family}.Description";
}
