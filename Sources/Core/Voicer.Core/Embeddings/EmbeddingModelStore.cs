using Voicer.Core.Models;

namespace Voicer.Core.Embeddings;

/// <summary>
/// <see cref="IEmbeddingModelStore"/> over the folder of the models.
/// </summary>
public sealed class EmbeddingModelStore : ServerModelStore<EmbeddingModel>, IEmbeddingModelStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingModelStore"/> class.
    /// </summary>
    /// <param name="root">The folder of the models (<c>Data/Models</c>).</param>
    /// <param name="catalog">The models known; <see cref="EmbeddingModelCatalog.All"/> when not given.</param>
    public EmbeddingModelStore(string root, IReadOnlyList<EmbeddingModel>? catalog = null)
        : base(root, catalog ?? EmbeddingModelCatalog.All)
    {
    }
}
