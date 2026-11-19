using System.Globalization;
using FluentAssertions;

namespace Voicer.Media.Tests;

/// <summary>
/// Tests of the arguments the decoder gives FFmpeg: the moment, the speed and the raw format of the output.
/// </summary>
public sealed class FfmpegPcmDecoderTests
{
    /// <summary>
    /// The decoder starts at the moment, changes the speed with atempo and writes mono 48 kHz 16-bit samples.
    /// </summary>
    [Fact]
    public void Arguments_SeekChangeTheSpeedAndWriteRawSamples()
    {
        var arguments = FfmpegPcmDecoder.Arguments(@"C:\data\record.ogg", TimeSpan.FromSeconds(83.25), 1.5);

        arguments.Should().ContainInOrder("-ss", "83.25", "-i", @"C:\data\record.ogg");
        arguments.Should().ContainInOrder("-af", "atempo=1.5");
        arguments.Should().ContainInOrder("-ac", "1", "-ar", "48000", "-f", "s16le", "pipe:1");
        arguments[^1].Should().Be("pipe:1");
    }

    /// <summary>
    /// At the speed of the record no filter is added.
    /// </summary>
    [Fact]
    public void Arguments_AtNormalSpeed_HaveNoFilter()
    {
        FfmpegPcmDecoder.Arguments("record.ogg", TimeSpan.Zero, 1).Should().NotContain("-af");
    }

    /// <summary>
    /// The numbers are written the same way whatever the culture of the user.
    /// </summary>
    [Fact]
    public void Arguments_IgnoreTheCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        try
        {
            var arguments = FfmpegPcmDecoder.Arguments("record.ogg", TimeSpan.FromSeconds(1.5), 1 / 1.5);

            arguments.Should().Contain("1.5").And.Contain("atempo=0.666667");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>
    /// A moment before the beginning starts at the beginning, and the speed stays between half and double.
    /// </summary>
    [Fact]
    public void Arguments_StayInTheirRanges()
    {
        FfmpegPcmDecoder.Arguments("record.ogg", TimeSpan.FromSeconds(-5), 9).Should()
            .ContainInOrder("-ss", "0").And.Contain("atempo=2");
        FfmpegPcmDecoder.Arguments("record.ogg", TimeSpan.Zero, 0.1).Should().Contain("atempo=0.5");
    }
}
