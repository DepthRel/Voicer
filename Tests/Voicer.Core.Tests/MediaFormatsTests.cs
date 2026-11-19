using FluentAssertions;
using Voicer.Core.Media;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the kinds of files whose sound can be recognized.
/// </summary>
public sealed class MediaFormatsTests
{
    /// <summary>
    /// Audio and video files are taken whatever the case of the extension.
    /// </summary>
    /// <param name="name">The file name.</param>
    [Theory]
    [InlineData("talk.mp3")]
    [InlineData("TALK.WAV")]
    [InlineData("film.mkv")]
    [InlineData("clip.MP4")]
    [InlineData("old.avi")]
    [InlineData("C:\\folder\\voice.m4a")]
    public void IsSupported_TakesMediaFiles(string name)
    {
        MediaFormats.IsSupported(name).Should().BeTrue();
    }

    /// <summary>
    /// Other files and empty names are refused.
    /// </summary>
    /// <param name="name">The file name.</param>
    [Theory]
    [InlineData("notes.txt")]
    [InlineData("picture.png")]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSupported_RefusesOtherFiles(string? name)
    {
        MediaFormats.IsSupported(name).Should().BeFalse();
    }
}
