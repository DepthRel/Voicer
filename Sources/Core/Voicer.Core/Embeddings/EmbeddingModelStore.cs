using Voicer.Core.Models;

namespace Voicer.Core.Embeddings;

/// <summary>
/// <see cref="IEmbeddingModelStore"/> over the folder of the models: a model is installed when its file has its full
/// size; the downloader checked its SHA-256 before it got its name.
/// </summary>
public sealed class EmbeddingModelStore : IEmbeddingModelStore
{
    /// <summary>
    /// The folder of the models.
    /// </summary>
    private readonly string _root;

    /// <summary>
    /// The models known, the preferred first.
    /// </summary>
    private readonly IReadOnlyList<EmbeddingModel> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingModelStore"/> class.
    /// </summary>
    /// <param name="root">The folder of the models (<c>Data/Models</c>).</param>
    /// <param name="catalog">The models known; <see cref="EmbeddingModelCatalog.All"/> when not given.</param>
    public EmbeddingModelStore(string root, IReadOnlyList<EmbeddingModel>? catalog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.GetFullPath(root);
        _catalog = catalog ?? EmbeddingModelCatalog.All;
    }

    /// <inheritdoc />
    public string DirectoryOf(EmbeddingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Path.Combine(_root, model.Id);
    }

    /// <inheritdoc />
    public string FileOf(EmbeddingModel model)
    {
        return Path.Combine(DirectoryOf(model), model.FileName);
    }

    /// <inheritdoc />
    public bool IsInstalled(EmbeddingModel model)
    {
        var file = new FileInfo(FileOf(model));

        return file.Exists && file.Length == model.SizeBytes;
    }

    /// <inheritdoc />
    public void Delete(EmbeddingModel model)
    {
        ModelFiles.Delete([FileOf(model)], DirectoryOf(model));
    }

    /// <inheritdoc />
    public EmbeddingModel? Preferred()
    {
        return _catalog.FirstOrDefault(IsInstalled);
    }
}
