using System.Text.Json;
using System.Text.Json.Nodes;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// <see cref="ISettingsStore"/> backed by a small JSON file, <c>Data/settings.json</c>.
/// </summary>
/// <remarks>
/// A missing or damaged file reads as empty, and the next write replaces it; the file is written atomically.
/// </remarks>
public sealed class JsonSettingsStore : ISettingsStore
{
    /// <summary>
    /// The JSON options of the file.
    /// </summary>
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    /// <summary>
    /// Serializes the writes of one process.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSettingsStore"/> class.
    /// </summary>
    /// <param name="path">The path of the settings file; its folder is created on the first write.</param>
    public JsonSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = Path.GetFullPath(path);
    }

    /// <summary>
    /// Gets the full path of the settings file.
    /// </summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var root = ReadRoot();

        return root.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    /// <inheritdoc />
    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            var root = ReadRoot();
            root[key] = value;

            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            var temp = Path.Combine(directory, $".{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temp, root.ToJsonString(_json));
                File.Move(temp, FilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }
    }

    /// <summary>
    /// Reads the file as a JSON object.
    /// </summary>
    /// <returns>The object; empty when the file is missing, unreadable or not an object.</returns>
    private JsonObject ReadRoot()
    {
        try
        {
            return File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject root ? root : new JsonObject();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new JsonObject();
        }
    }
}
