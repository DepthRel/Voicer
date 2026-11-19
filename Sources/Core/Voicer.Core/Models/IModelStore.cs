namespace Voicer.Core.Models;

/// <summary>
/// The speech models on this computer: each family in a folder of its own, <c>Data/Models/&lt;family&gt;</c>, which keeps
/// the files of its quantizations side by side.
/// </summary>
public interface IModelStore
{
    /// <summary>
    /// Gets the folder of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The full path of the folder; it may not exist yet.</returns>
    string DirectoryOf(SpeechModel model);

    /// <summary>
    /// Gets the file of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The full path of the file; it may not exist yet.</returns>
    string FileOf(SpeechModel model);

    /// <summary>
    /// Tells whether a model is downloaded completely.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns><see langword="true"/> when its file is there and has the expected size.</returns>
    bool IsInstalled(SpeechModel model);

    /// <summary>
    /// Lists the models of the catalog that are downloaded.
    /// </summary>
    /// <returns>The installed models, in the order of the catalog.</returns>
    IReadOnlyList<SpeechModel> Installed();
}
