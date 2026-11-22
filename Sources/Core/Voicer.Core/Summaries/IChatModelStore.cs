using Voicer.Core.Models;

namespace Voicer.Core.Summaries;

/// <summary>
/// Where the downloaded models of Voicer that write summaries and answers live: <c>Data/Models/&lt;id&gt;</c>.
/// </summary>
public interface IChatModelStore : IServerModelStore<ChatModel>
{
}
