namespace Voicer.Core.Embeddings;

/// <summary>
/// Where the downloaded models of embeddings live: <c>Data/Models/&lt;id&gt;</c>.
/// </summary>
public interface IEmbeddingModelStore
{
    /// <summary>
    /// Gets the folder of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The full path of the folder.</returns>
    string DirectoryOf(EmbeddingModel model);

    /// <summary>
    /// Gets the file of the weights of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The full path of the file.</returns>
    string FileOf(EmbeddingModel model);

    /// <summary>
    /// Tells whether a model is downloaded whole.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns><see langword="true"/> when its file has its full size.</returns>
    bool IsInstalled(EmbeddingModel model);

    /// <summary>
    /// Deletes the file of a model and its folder when it is left empty.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <exception cref="IOException">A file could not be deleted.</exception>
    void Delete(EmbeddingModel model);

    /// <summary>
    /// Finds the model the search by meaning uses: the first installed model of the catalog.
    /// </summary>
    /// <returns>The model, or <see langword="null"/> when none is installed.</returns>
    EmbeddingModel? Preferred();
}
