using Voicer.Core.Models;

namespace Voicer.Core.Summaries;

/// <summary>
/// <see cref="IChatModelStore"/> over the folder of the models.
/// </summary>
public sealed class ChatModelStore : ServerModelStore<ChatModel>, IChatModelStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatModelStore"/> class.
    /// </summary>
    /// <param name="root">The folder of the models (<c>Data/Models</c>).</param>
    /// <param name="catalog">The models known; <see cref="ChatModelCatalog.All"/> when not given.</param>
    public ChatModelStore(string root, IReadOnlyList<ChatModel>? catalog = null)
        : base(root, catalog ?? ChatModelCatalog.All)
    {
    }
}
