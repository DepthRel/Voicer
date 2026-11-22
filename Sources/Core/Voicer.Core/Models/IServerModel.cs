namespace Voicer.Core.Models;

/// <summary>
/// A model the application downloads and runs itself in llama-server: one GGUF file of a pinned revision, checked by its
/// size and SHA-256.
/// </summary>
public interface IServerModel
{
    /// <summary>
    /// Gets the identifier: the name of its folder in <c>Data/Models</c> and of the model in the requests.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the name shown to the user.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name of the file of the weights.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Gets where the file is downloaded from, over HTTPS.
    /// </summary>
    Uri DownloadUri { get; }

    /// <summary>
    /// Gets the exact size of the file.
    /// </summary>
    long SizeBytes { get; }

    /// <summary>
    /// Gets the SHA-256 of the file, in lower-case hex.
    /// </summary>
    string Sha256 { get; }

    /// <summary>
    /// Gets the key of the description of the model in the language packs.
    /// </summary>
    string DescriptionKey { get; }
}
