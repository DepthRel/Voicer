using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// The summaries and the answers to questions in the settings: what writes them (nothing, a light model of Voicer, or a
/// model of the server of language models), and why that choice does not work yet.
/// </summary>
public sealed class SummaryModelSettingsViewModel : ModelSourceSettingsViewModel
{
    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IChatModelStore _models;

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server;

    /// <summary>
    /// Finds llama-server, which runs a model of Voicer.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp;

    /// <summary>
    /// Initializes a new instance of the <see cref="SummaryModelSettingsViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="store">Remembers the choice.</param>
    /// <param name="models">Where the models of Voicer live.</param>
    /// <param name="server">The settings of the server of language models.</param>
    /// <param name="llamaCpp">Finds llama-server, which runs a model of Voicer.</param>
    /// <param name="catalog">The catalog of the models of Voicer.</param>
    /// <param name="localServer">The llama-server of a model of Voicer: its state and its stop.</param>
    /// <param name="serverSettings">The connection to the server of language models, whose models are offered.</param>
    public SummaryModelSettingsViewModel(
        ILocalizer localizer,
        ISummarySourceStore store,
        IChatModelStore models,
        ISummaryPreferencesStore server,
        ILlamaCppLocator llamaCpp,
        ModelCatalogViewModel catalog,
        LlamaServerViewModel localServer,
        LanguageServerSettingsViewModel serverSettings)
        : base(localizer, store, catalog, localServer, serverSettings)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _llamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
    }

    /// <inheritdoc />
    public override string? Warning => Source switch
    {
        ModelSource.Local when _models.Preferred() is null => Localizer.Get("Summary.Source.LocalMissing"),
        ModelSource.Local when _llamaCpp.LocateServer() is null => Localizer.Get("Summary.Source.LlamaMissing"),
        ModelSource.Server when _server.Load().ToSettings() is null => Localizer.Get("Summary.Source.ServerMissing"),
        _ => null,
    };
}
