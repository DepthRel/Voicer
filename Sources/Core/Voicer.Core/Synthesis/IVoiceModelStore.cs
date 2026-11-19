namespace Voicer.Core.Synthesis;

/// <summary>
/// The voice models on this computer: each in a folder of its own, <c>Data/Models/&lt;id&gt;</c>, with all its files.
/// </summary>
public interface IVoiceModelStore
{
    /// <summary>
    /// Gets the folder of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The full path of the folder; it may not exist yet.</returns>
    string DirectoryOf(VoiceModel model);

    /// <summary>
    /// Gets the path of a file of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="file">One of the files of the model.</param>
    /// <returns>The full path of the file; it may not exist yet.</returns>
    string FileOf(VoiceModel model, VoiceModelFile file);

    /// <summary>
    /// Tells whether every file of a model is downloaded completely.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns><see langword="true"/> when all its files are there with the expected sizes.</returns>
    bool IsInstalled(VoiceModel model);

    /// <summary>
    /// Deletes every file of a model from the disk, with its folder once it is left empty.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <exception cref="IOException">A file is in use or the disk failed.</exception>
    /// <exception cref="UnauthorizedAccessException">A file may not be deleted.</exception>
    void Delete(VoiceModel model);

    /// <summary>
    /// Lists the models of the catalog that are downloaded.
    /// </summary>
    /// <returns>The installed models, in the order of the catalog.</returns>
    IReadOnlyList<VoiceModel> Installed();
}
