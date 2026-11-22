namespace Voicer.Core.Models;

/// <summary>
/// <see cref="IServerModelStore{TModel}"/> over the folder of the models: a model is installed when its file has its full
/// size; the downloader checked its SHA-256 before it got its name.
/// </summary>
/// <typeparam name="TModel">The kind of the models.</typeparam>
public class ServerModelStore<TModel> : IServerModelStore<TModel>
    where TModel : class, IServerModel
{
    /// <summary>
    /// The folder of the models.
    /// </summary>
    private readonly string _root;

    /// <summary>
    /// The models known, the preferred first.
    /// </summary>
    private readonly IReadOnlyList<TModel> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerModelStore{TModel}"/> class.
    /// </summary>
    /// <param name="root">The folder of the models (<c>Data/Models</c>).</param>
    /// <param name="catalog">The models known, the preferred first.</param>
    public ServerModelStore(string root, IReadOnlyList<TModel> catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.GetFullPath(root);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <inheritdoc />
    public string DirectoryOf(TModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Path.Combine(_root, model.Id);
    }

    /// <inheritdoc />
    public string FileOf(TModel model)
    {
        return Path.Combine(DirectoryOf(model), model.FileName);
    }

    /// <inheritdoc />
    public bool IsInstalled(TModel model)
    {
        var file = new FileInfo(FileOf(model));

        return file.Exists && file.Length == model.SizeBytes;
    }

    /// <inheritdoc />
    public void Delete(TModel model)
    {
        ModelFiles.Delete([FileOf(model)], DirectoryOf(model));
    }

    /// <inheritdoc />
    public TModel? Preferred()
    {
        return _catalog.FirstOrDefault(IsInstalled);
    }
}
