using Voicer.Core.Summaries;
using Voicer.UI.Enums;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// <see cref="ISummarySourceStore"/> in the settings of the user. Before the first choice: the server, when its model of
/// summaries was chosen (as before the choice existed), else a downloaded model of Voicer, else none.
/// </summary>
public sealed class SummarySourceStore : ModelSourceStore, ISummarySourceStore
{
    /// <summary>
    /// The key of the choice in the settings.
    /// </summary>
    private const string SOURCE = "summary.source";

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IChatModelStore _models;

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="SummarySourceStore"/> class.
    /// </summary>
    /// <param name="settings">The settings of the user.</param>
    /// <param name="models">Where the models of Voicer live.</param>
    /// <param name="server">The settings of the server of language models.</param>
    public SummarySourceStore(ISettingsStore settings, IChatModelStore models, ISummaryPreferencesStore server)
        : base(settings, SOURCE)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <inheritdoc />
    protected override ModelSource Guess()
    {
        if (_server.Load().ToSettings() is not null)
        {
            return ModelSource.Server;
        }

        return _models.Preferred() is not null ? ModelSource.Local : ModelSource.Off;
    }
}
