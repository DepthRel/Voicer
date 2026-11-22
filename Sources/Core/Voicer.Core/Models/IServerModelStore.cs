namespace Voicer.Core.Models;

/// <summary>
/// Where the downloaded models of one kind run by llama-server live: <c>Data/Models/&lt;id&gt;</c>.
/// </summary>
/// <typeparam name="TModel">The kind of the models.</typeparam>
public interface IServerModelStore<TModel>
    where TModel : class, IServerModel
{
    /// <summary>
    /// Gets the folder of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The full path of the folder.</returns>
    string DirectoryOf(TModel model);

    /// <summary>
    /// Gets the file of the weights of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The full path of the file.</returns>
    string FileOf(TModel model);

    /// <summary>
    /// Tells whether a model is downloaded whole.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns><see langword="true"/> when its file has its full size.</returns>
    bool IsInstalled(TModel model);

    /// <summary>
    /// Deletes the file of a model and its folder when it is left empty.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <exception cref="IOException">A file could not be deleted.</exception>
    void Delete(TModel model);

    /// <summary>
    /// Finds the model that is used: the first installed model of the catalog.
    /// </summary>
    /// <returns>The model, or <see langword="null"/> when none is installed.</returns>
    TModel? Preferred();
}
