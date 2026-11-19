namespace Voicer.UI.Services;

/// <summary>
/// An <see cref="IProgress{T}"/> that hands every value on at once, on the thread that reports it. It translates the
/// progress of one stage into the progress of the whole without posting it to the UI thread twice.
/// </summary>
/// <typeparam name="T">The type of the progress value.</typeparam>
/// <param name="handler">Receives every value.</param>
public sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    /// <summary>
    /// Receives every value.
    /// </summary>
    private readonly Action<T> _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    /// <inheritdoc />
    public void Report(T value)
    {
        _handler(value);
    }
}
