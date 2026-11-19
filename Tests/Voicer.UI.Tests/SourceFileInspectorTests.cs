using FluentAssertions;
using Voicer.Localization;
using Voicer.UI.Services.Transcription;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the check of a chosen file: a readable file is taken with its size, a missing or empty one is refused with
/// a text for the user.
/// </summary>
public sealed class SourceFileInspectorTests : IDisposable
{
    /// <summary>
    /// A folder of its own for every test.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-source-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The inspector under test.
    /// </summary>
    private readonly SourceFileInspector _inspector = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceFileInspectorTests"/> class.
    /// </summary>
    public SourceFileInspectorTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// A readable file is taken with its full path and size.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Inspect_TakesAReadableFile()
    {
        var path = Path.Combine(_folder, "talk.wav");
        await File.WriteAllBytesAsync(path, new byte[300]);

        var file = await _inspector.InspectAsync(path, CancellationToken.None);

        file.Should().Be(new SourceFile(Path.GetFullPath(path), 300));
    }

    /// <summary>
    /// A file that is not there is refused with a text for the user.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Inspect_RefusesAMissingFile()
    {
        var act = () => _inspector.InspectAsync(Path.Combine(_folder, "gone.wav"), CancellationToken.None);

        var failure = await act.Should().ThrowAsync<FileNotFoundException>();
        TestTexts.English.Describe(failure.Which).Should().Be(TestTexts.English.Get("Transcription.SourceMissing"));
    }

    /// <summary>
    /// An empty file is refused: it holds no sound.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Inspect_RefusesAnEmptyFile()
    {
        var path = Path.Combine(_folder, "empty.wav");
        await File.WriteAllBytesAsync(path, []);

        var act = () => _inspector.InspectAsync(path, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<IOException>();
        TestTexts.English.Describe(failure.Which).Should().Be(TestTexts.English.Get("Transcription.SourceEmpty"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
