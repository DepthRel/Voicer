using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Voicer.Core.History;
using Voicer.Core.Transcripts;
using Voicer.Media;
using Voicer.UI.Services.History;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the keeping of a record: the text always, the sound when it can be compressed, and no file left behind.
/// </summary>
public sealed class RecordKeeperTests : IDisposable
{
    /// <summary>
    /// The recognized text of every test.
    /// </summary>
    private static readonly Transcript _transcript = new("talk.mp4", "Whisper Small Q5", "CPU", "en", DateTimeOffset.UnixEpoch, []);

    /// <summary>
    /// The history.
    /// </summary>
    private readonly ITranscriptHistory _history = Substitute.For<ITranscriptHistory>();

    /// <summary>
    /// The compressor of the sound.
    /// </summary>
    private readonly IAudioEncoder _encoder = Substitute.For<IAudioEncoder>();

    /// <summary>
    /// A temporary folder of the sound.
    /// </summary>
    private readonly TemporaryAudioFolder _temporary = new(Path.Combine(Path.GetTempPath(), "voicer-keeper-" + Guid.NewGuid().ToString("N")));

    /// <summary>
    /// The keeper under test.
    /// </summary>
    private readonly RecordKeeper _keeper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordKeeperTests"/> class.
    /// </summary>
    public RecordKeeperTests()
    {
        _history.AddAsync(default!, default, default).ReturnsForAnyArgs(5L);
        _keeper = new RecordKeeper(_history, _encoder, _temporary);
    }

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_temporary.Directory))
        {
            Directory.Delete(_temporary.Directory, recursive: true);
        }
    }

    /// <summary>
    /// The compressed sound is kept with the text, and its temporary file is removed.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Keep_StoresTheSoundWithTheText()
    {
        string? compressed = null;
        _encoder.EncodeAsync("talk.mp4", Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMinutes(3))
            .AndDoes(call =>
            {
                compressed = call.ArgAt<string>(1);
                File.WriteAllBytes(compressed, [1, 2, 3]);
            });

        var kept = await _keeper.KeepAsync(_transcript, null, CancellationToken.None);

        kept.Should().Be(new KeptRecord(5, true, null));
        await _history.Received(1).AddAsync(_transcript, new RecordAudio(compressed!, TimeSpan.FromMinutes(3)), CancellationToken.None);
        File.Exists(compressed).Should().BeFalse();
    }

    /// <summary>
    /// A sound that cannot be compressed leaves the text kept without it, with the reason.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Keep_WithoutTheSound_WhenItCannotBeCompressed()
    {
        var failure = new InvalidDataException("no libopus");
        _encoder.EncodeAsync(default!, default!, default, default).ThrowsAsyncForAnyArgs(failure);

        var kept = await _keeper.KeepAsync(_transcript, null, CancellationToken.None);

        kept.Should().Be(new KeptRecord(5, false, failure));
        await _history.Received(1).AddAsync(_transcript, null, CancellationToken.None);
    }

    /// <summary>
    /// A compression the user stopped leaves the text kept without the sound and without a complaint.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Keep_WithoutTheSound_WhenStopped()
    {
        _encoder.EncodeAsync(default!, default!, default, default).ThrowsAsyncForAnyArgs(new OperationCanceledException());

        var kept = await _keeper.KeepAsync(_transcript, null, CancellationToken.None);

        kept.Should().Be(new KeptRecord(5, false, null));
        await _history.Received(1).AddAsync(_transcript, null, CancellationToken.None);
    }
}
