using Voicer.Core.Models;

namespace Voicer.Core.Embeddings;

/// <summary>
/// Where the downloaded models of embeddings live: <c>Data/Models/&lt;id&gt;</c>.
/// </summary>
public interface IEmbeddingModelStore : IServerModelStore<EmbeddingModel>
{
}
