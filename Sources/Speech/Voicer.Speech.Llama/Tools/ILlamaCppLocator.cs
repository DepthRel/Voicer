namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// Finds the llama-tts program of the build of llama.cpp the application installed.
/// </summary>
public interface ILlamaCppLocator
{
    /// <summary>
    /// Gets the folder of the builds of llama.cpp (<c>Data/Tools/LlamaCpp</c>).
    /// </summary>
    string RootDirectory { get; }

    /// <summary>
    /// Gets the folder of the pinned build (<c>Data/Tools/LlamaCpp/&lt;version&gt;</c>).
    /// </summary>
    string InstallDirectory { get; }

    /// <summary>
    /// Finds llama-tts in the folder of the pinned build. Another build, or a llama.cpp of the system, is not taken: the
    /// voice models need the release they were checked with.
    /// </summary>
    /// <returns>The full path of the program, or <see langword="null"/> when it is not installed.</returns>
    string? Locate();

    /// <summary>
    /// Finds llama-server in the folder of the pinned build: it runs the models of embeddings of the search by meaning.
    /// </summary>
    /// <returns>The full path of the program, or <see langword="null"/> when it is not installed.</returns>
    string? LocateServer();
}
