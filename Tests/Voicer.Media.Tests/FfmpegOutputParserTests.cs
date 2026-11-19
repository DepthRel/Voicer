using FluentAssertions;
using Voicer.Media.Ffmpeg;

namespace Voicer.Media.Tests;

/// <summary>
/// Tests of the reading of what FFmpeg reports.
/// </summary>
public sealed class FfmpegOutputParserTests
{
    /// <summary>
    /// The duration of the input is read from the log.
    /// </summary>
    [Fact]
    public void TryParseDuration_ReadsTheLog()
    {
        FfmpegOutputParser.TryParseDuration("  Duration: 01:02:03.50, start: 0.000000, bitrate: 128 kb/s", out var duration).Should().BeTrue();

        duration.Should().Be(new TimeSpan(0, 1, 2, 3, 500));
    }

    /// <summary>
    /// An unknown duration or another line gives nothing.
    /// </summary>
    /// <param name="line">The line.</param>
    [Theory]
    [InlineData("  Duration: N/A, bitrate: N/A")]
    [InlineData("Stream #0:0: Audio: mp3")]
    [InlineData(null)]
    public void TryParseDuration_SkipsOtherLines(string? line)
    {
        FfmpegOutputParser.TryParseDuration(line, out _).Should().BeFalse();
    }

    /// <summary>
    /// The position of the output comes from the progress lines in microseconds.
    /// </summary>
    [Fact]
    public void TryParsePosition_ReadsMicroseconds()
    {
        FfmpegOutputParser.TryParsePosition("out_time_us=83450000", out var position).Should().BeTrue();

        position.Should().Be(TimeSpan.FromSeconds(83.45));
        FfmpegOutputParser.TryParsePosition("out_time_us=N/A", out _).Should().BeFalse();
        FfmpegOutputParser.TryParsePosition("progress=continue", out _).Should().BeFalse();
    }

    /// <summary>
    /// A file without sound is recognized by the complaint of FFmpeg.
    /// </summary>
    [Fact]
    public void IsNoAudio_RecognizesTheComplaint()
    {
        FfmpegOutputParser.IsNoAudio("Stream map '0:a:0' matches no streams.").Should().BeTrue();
        FfmpegOutputParser.IsNoAudio("Output file #0 does not contain any stream").Should().BeTrue();
        FfmpegOutputParser.IsNoAudio("size=     512kB time=00:00:16.38").Should().BeFalse();
    }
}
