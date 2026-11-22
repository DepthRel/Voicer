using Voicer.Core.Summaries;
using Voicer.Localization;

namespace Voicer.UI.Services.Summaries;

/// <summary>
/// A piece of work of the model of the summaries and the answers: the settings the requests use, and the llama-server
/// of a model of Voicer held until the work ends.
/// </summary>
public sealed class ChatSession : IDisposable
{
    /// <summary>
    /// The name of the model of Voicer, or <see langword="null"/> for a model of the server of language models.
    /// </summary>
    private readonly string? _localModel;

    /// <summary>
    /// The hold on the llama-server, or <see langword="null"/>.
    /// </summary>
    private readonly IDisposable? _lease;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatSession"/> class.
    /// </summary>
    /// <param name="settings">The settings the requests use.</param>
    /// <param name="localModel">The name of the model of Voicer, or <see langword="null"/> for a model of the server.</param>
    /// <param name="lease">The hold on the llama-server of the model of Voicer, or <see langword="null"/>.</param>
    public ChatSession(SummarySettings settings, string? localModel = null, IDisposable? lease = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _localModel = localModel;
        _lease = lease;
    }

    /// <summary>
    /// Gets the settings the requests use: the address, the model and its context.
    /// </summary>
    public SummarySettings Settings { get; }

    /// <summary>
    /// Runs the work of the session; a model of Voicer that stops on the way (the user unloaded it, or its process ended)
    /// is reported as stopped rather than as a server that does not answer.
    /// </summary>
    /// <typeparam name="T">The result of the work.</typeparam>
    /// <param name="work">The work, given the settings.</param>
    /// <returns>The result.</returns>
    public async Task<T> RunAsync<T>(Func<SummarySettings, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        try
        {
            return await work(Settings);
        }
        catch (HttpRequestException ex) when (_localModel is { } model && ex.StatusCode is null)
        {
            throw LocalizedError.Create(m => new HttpRequestException(m, ex), "Summary.LocalStopped", model);
        }
        catch (IOException ex) when (_localModel is { } model)
        {
            // The connection ended in the middle of a streamed answer: the process was ended under it.
            throw LocalizedError.Create(m => new IOException(m, ex), "Summary.LocalStopped", model);
        }
    }

    /// <summary>
    /// Gives the llama-server back: it stops by itself after a pause.
    /// </summary>
    public void Dispose()
    {
        _lease?.Dispose();
    }
}
