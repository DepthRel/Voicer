using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// The connection to a server of language models in the settings, local or in the cloud: its address and key, the
/// models it lists, and the models of it the summaries (with its context) and the search by meaning take. Every change
/// is remembered at once; the models are listed by asking the server.
/// </summary>
public sealed partial class LanguageServerSettingsViewModel : LocalizedViewModel
{
    /// <summary>
    /// The text whose vector the check asks for; the server sees it, a person never does.
    /// </summary>
    private const string EMBEDDING_PROBE = "probe";

    /// <summary>
    /// Remembers the choice.
    /// </summary>
    private readonly ISummaryPreferencesStore _store;

    /// <summary>
    /// Asks the server for its models.
    /// </summary>
    private readonly ILanguageModelClient _client;

    /// <summary>
    /// The choice of the source of the search by meaning: the model of embeddings is tried only when it is the server.
    /// </summary>
    private readonly ISearchSourceStore _search;

    /// <summary>
    /// The chosen program, or <see langword="null"/>; choosing one fills its address.
    /// </summary>
    [ObservableProperty]
    private LanguageModelPreset? _selectedPreset;

    /// <summary>
    /// The address of the API.
    /// </summary>
    [ObservableProperty]
    private string _address;

    /// <summary>
    /// The key of the server, or an empty text.
    /// </summary>
    [ObservableProperty]
    private string _apiKey;

    /// <summary>
    /// The chosen model, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    private string? _selectedModel;

    /// <summary>
    /// The chosen model of embeddings of the search by meaning, or <see langword="null"/> to search by words only.
    /// </summary>
    [ObservableProperty]
    private string? _selectedEmbeddingModel;

    /// <summary>
    /// The context of the model, in tokens.
    /// </summary>
    [ObservableProperty]
    private decimal _contextTokens;

    /// <summary>
    /// What the last check found, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    /// <summary>
    /// Whether the last check failed.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>
    /// Whether the server is being asked.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    private bool _isChecking;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageServerSettingsViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="store">Remembers the choice.</param>
    /// <param name="client">Asks the server for its models.</param>
    /// <param name="search">The choice of the source of the search by meaning.</param>
    public LanguageServerSettingsViewModel(ILocalizer localizer, ISummaryPreferencesStore store, ILanguageModelClient client, ISearchSourceStore search)
        : base(localizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _search = search ?? throw new ArgumentNullException(nameof(search));

        var preferences = store.Load();
        _address = preferences.Address;
        _apiKey = preferences.ApiKey;
        _contextTokens = preferences.ContextTokens;
        _selectedPreset = Presets.FirstOrDefault(p => string.Equals(p.Address, preferences.Address, StringComparison.OrdinalIgnoreCase));
        if (preferences.Model.Length > 0)
        {
            Models.Add(preferences.Model);
            _selectedModel = preferences.Model;
        }

        if (preferences.EmbeddingModel.Length > 0)
        {
            if (!Models.Contains(preferences.EmbeddingModel))
            {
                Models.Add(preferences.EmbeddingModel);
            }

            _selectedEmbeddingModel = preferences.EmbeddingModel;
        }
    }

    /// <summary>
    /// Gets the programs whose address can be filled in.
    /// </summary>
    public IReadOnlyList<LanguageModelPreset> Presets => LanguageModelPreset.All;

    /// <summary>
    /// Gets the models of the server, after a check.
    /// </summary>
    public ObservableCollection<string> Models { get; } = [];

    /// <summary>
    /// Gets the smallest context that can be chosen.
    /// </summary>
    public decimal MinContextTokens => SummarySettings.MIN_CONTEXT_TOKENS;

    /// <summary>
    /// Gets the largest context that can be chosen.
    /// </summary>
    public decimal MaxContextTokens => SummarySettings.MAX_CONTEXT_TOKENS;

    /// <summary>
    /// Gets a value indicating whether a result of a check is shown.
    /// </summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    /// <summary>
    /// Fills the address of the chosen program.
    /// </summary>
    /// <param name="value">The program.</param>
    partial void OnSelectedPresetChanged(LanguageModelPreset? value)
    {
        if (value is not null)
        {
            Address = value.Address;
        }
    }

    /// <summary>
    /// Remembers a new address; the models of another server are not known yet.
    /// </summary>
    /// <param name="value">The address.</param>
    partial void OnAddressChanged(string value)
    {
        if (!Presets.Any(p => string.Equals(p.Address, value, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedPreset = null;
        }

        Remember();
    }

    /// <summary>
    /// Remembers a new key.
    /// </summary>
    /// <param name="value">The key.</param>
    partial void OnApiKeyChanged(string value)
    {
        Remember();
    }

    /// <summary>
    /// Remembers the chosen model.
    /// </summary>
    /// <param name="value">The model.</param>
    partial void OnSelectedModelChanged(string? value)
    {
        Remember();
    }

    /// <summary>
    /// Remembers the chosen model of embeddings.
    /// </summary>
    /// <param name="value">The model.</param>
    partial void OnSelectedEmbeddingModelChanged(string? value)
    {
        Remember();
    }

    /// <summary>
    /// Remembers a new context.
    /// </summary>
    /// <param name="value">The context, in tokens.</param>
    partial void OnContextTokensChanged(decimal value)
    {
        Remember();
    }

    /// <summary>
    /// Connects to the server and lists its models, keeping the chosen one when the server still has it, then makes
    /// sure the chosen model of embeddings makes embeddings when the search by meaning uses the server.
    /// </summary>
    /// <returns>A task that completes when the server answered or failed.</returns>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        if (!LanguageModelEndpoint.TryCreate(Address, ApiKey, out var endpoint))
        {
            ShowStatus(Localizer.Get("Summary.InvalidAddress"), true);

            return;
        }

        IsChecking = true;
        ShowStatus(Localizer.Get("Summary.Checking"), false);
        try
        {
            var models = await _client.ListModelsAsync(endpoint, CancellationToken.None);
            var chosen = SelectedModel;
            var chosenEmbedding = SelectedEmbeddingModel;

            Models.Clear();
            foreach (var model in models)
            {
                Models.Add(model);
            }

            SelectedModel = models.Contains(chosen) ? chosen : models.FirstOrDefault(m => !IsEmbeddingModel(m));
            SelectedEmbeddingModel = models.Contains(chosenEmbedding) ? chosenEmbedding : BestEmbeddingModel(models);

            if (_search.Load() == ModelSource.Server && SelectedEmbeddingModel is { } embedding &&
                await TryEmbedAsync(endpoint, embedding) is { } refusal)
            {
                ShowStatus(Localizer.Format("Summary.EmbeddingRefused", models.Count, embedding, refusal), true);

                return;
            }

            ShowStatus(Localizer.Format("Summary.Connected", models.Count), false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            ShowStatus(Localizer.Describe(ex), true);
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>
    /// Asks the chosen model of embeddings for the vector of a short text, so a model that cannot make embeddings (a
    /// chat model chosen by mistake) is reported in the settings and not by the first search of the history.
    /// </summary>
    /// <param name="endpoint">The server.</param>
    /// <param name="model">The model of embeddings.</param>
    /// <returns>The words of the refusal, or <see langword="null"/> when the model made the vector.</returns>
    private async Task<string?> TryEmbedAsync(LanguageModelEndpoint endpoint, string model)
    {
        try
        {
            await _client.EmbedAsync(endpoint, model, [EMBEDDING_PROBE], CancellationToken.None);

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            return Localizer.Describe(ex);
        }
    }

    /// <summary>
    /// Tells whether the server can be asked now.
    /// </summary>
    /// <returns><see langword="true"/> when no check runs.</returns>
    private bool CanCheck()
    {
        return !IsChecking;
    }

    /// <summary>
    /// Tells whether a model of the server makes embeddings, by its name.
    /// </summary>
    /// <param name="model">The identifier of the model.</param>
    /// <returns><see langword="true"/> for a model of embeddings.</returns>
    private static bool IsEmbeddingModel(string model)
    {
        var name = model.ToLowerInvariant();

        return name.Contains("embed", StringComparison.Ordinal) || name.Contains("bge", StringComparison.Ordinal) ||
            name.Contains("e5", StringComparison.Ordinal);
    }

    /// <summary>
    /// Picks the model of embeddings to offer: a multilingual one first.
    /// </summary>
    /// <param name="models">The models of the server.</param>
    /// <returns>The model, or <see langword="null"/> when the server has none.</returns>
    private static string? BestEmbeddingModel(IReadOnlyList<string> models)
    {
        var embeddings = models.Where(IsEmbeddingModel).ToList();

        return embeddings.FirstOrDefault(m => m.Contains("m3", StringComparison.OrdinalIgnoreCase) || m.Contains("multilingual", StringComparison.OrdinalIgnoreCase)) ??
            embeddings.FirstOrDefault();
    }

    /// <summary>
    /// Shows the result of a check.
    /// </summary>
    /// <param name="text">The result.</param>
    /// <param name="isError">Whether it is a failure.</param>
    private void ShowStatus(string text, bool isError)
    {
        IsStatusError = isError;
        Status = text;
    }

    /// <summary>
    /// Writes the choice into the settings.
    /// </summary>
    private void Remember()
    {
        _store.Save(new SummaryPreferences(Address.Trim(), ApiKey.Trim(), SelectedModel ?? string.Empty, (int)ContextTokens, SelectedEmbeddingModel ?? string.Empty));
    }
}
