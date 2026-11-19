namespace Voicer.Core.Models;

/// <summary>
/// <see cref="IModelStore"/> over the models folder of the data folder.
/// </summary>
public sealed class ModelStore : IModelStore
{
    /// <summary>
    /// The folder that holds a folder per model.
    /// </summary>
    private readonly string _root;

    /// <summary>
    /// The models that can be installed.
    /// </summary>
    private readonly IReadOnlyList<SpeechModel> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelStore"/> class.
    /// </summary>
    /// <param name="root">The folder that holds a folder per model (<c>Data/Models</c>).</param>
    /// <param name="catalog">The models that can be installed; the whole catalog by default.</param>
    public ModelStore(string root, IReadOnlyList<SpeechModel>? catalog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        _catalog = catalog ?? SpeechModelCatalog.All;
    }

    /// <inheritdoc />
    public string DirectoryOf(SpeechModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Path.Combine(_root, model.Family);
    }

    /// <inheritdoc />
    public string FileOf(SpeechModel model)
    {
        return Path.Combine(DirectoryOf(model), model.FileName);
    }

    /// <inheritdoc />
    public bool IsInstalled(SpeechModel model)
    {
        var file = new FileInfo(FileOf(model));

        return file.Exists && file.Length == model.SizeBytes;
    }

    /// <inheritdoc />
    public string DetectorFileOf(SpeechDetectorModel detector)
    {
        ArgumentNullException.ThrowIfNull(detector);

        return Path.Combine(_root, detector.Folder, detector.FileName);
    }

    /// <inheritdoc />
    public bool IsDetectorInstalled(SpeechDetectorModel detector)
    {
        var file = new FileInfo(DetectorFileOf(detector));

        return file.Exists && file.Length == detector.SizeBytes;
    }

    /// <inheritdoc />
    public void Delete(SpeechModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        ModelFiles.Delete([FileOf(model)], DirectoryOf(model));
    }

    /// <inheritdoc />
    public IReadOnlyList<SpeechModel> Installed()
    {
        return [.. _catalog.Where(IsInstalled)];
    }
}
