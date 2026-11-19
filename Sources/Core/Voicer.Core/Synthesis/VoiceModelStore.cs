using Voicer.Core.Models;

namespace Voicer.Core.Synthesis;

/// <summary>
/// <see cref="IVoiceModelStore"/> over the models folder of the data folder.
/// </summary>
public sealed class VoiceModelStore : IVoiceModelStore
{
    /// <summary>
    /// The folder that holds a folder per model.
    /// </summary>
    private readonly string _root;

    /// <summary>
    /// The models that can be installed.
    /// </summary>
    private readonly IReadOnlyList<VoiceModel> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceModelStore"/> class.
    /// </summary>
    /// <param name="root">The folder that holds a folder per model (<c>Data/Models</c>).</param>
    /// <param name="catalog">The models that can be installed; the whole catalog by default.</param>
    public VoiceModelStore(string root, IReadOnlyList<VoiceModel>? catalog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        _catalog = catalog ?? VoiceModelCatalog.All;
    }

    /// <inheritdoc />
    public string DirectoryOf(VoiceModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Path.Combine(_root, model.Id);
    }

    /// <inheritdoc />
    public string FileOf(VoiceModel model, VoiceModelFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return Path.Combine(DirectoryOf(model), file.FileName);
    }

    /// <inheritdoc />
    public bool IsInstalled(VoiceModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Files.All(f =>
        {
            var info = new FileInfo(FileOf(model, f));

            return info.Exists && info.Length == f.SizeBytes;
        });
    }

    /// <inheritdoc />
    public void Delete(VoiceModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        ModelFiles.Delete(model.Files.Select(file => FileOf(model, file)), DirectoryOf(model));
    }

    /// <inheritdoc />
    public IReadOnlyList<VoiceModel> Installed()
    {
        return [.. _catalog.Where(IsInstalled)];
    }
}
