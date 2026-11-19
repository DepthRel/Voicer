using System.Buffers.Binary;
using FluentAssertions;
using Voicer.Core.Synthesis;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the reading of WAV files and of the joining of the pieces of a generated speech.
/// </summary>
public sealed class WaveJoinerTests : IDisposable
{
    /// <summary>
    /// 16-bit mono at 1000 samples a second: a millisecond is two bytes.
    /// </summary>
    private static readonly WaveFormat _pcm = new(WaveFormat.PCM, 1, 1000, 16);

    /// <summary>
    /// A temporary folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-wave-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Initializes a new instance of the <see cref="WaveJoinerTests"/> class.
    /// </summary>
    public WaveJoinerTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// The samples of the pieces are copied as they are, with silence between them and none after the last one; every
    /// piece is placed where it lies.
    /// </summary>
    [Fact]
    public void Join_CopiesThePieces_WithPausesBetweenThem()
    {
        var first = Write("a.wav", _pcm, [1, 2, 3, 4]);
        var second = Write("b.wav", _pcm, [5, 6]);
        var output = Path.Combine(_folder, "joined.wav");

        var spans = WaveJoiner.Join([first, second], [TimeSpan.FromMilliseconds(3), TimeSpan.FromSeconds(9)], output);

        spans.Should().Equal(
            new WaveSpan(TimeSpan.Zero, TimeSpan.FromMilliseconds(4)),
            new WaveSpan(TimeSpan.FromMilliseconds(7), TimeSpan.FromMilliseconds(9)));
        Samples(output).Should().Equal(1, 2, 3, 4, 0, 0, 0, 5, 6);
    }

    /// <summary>
    /// The layout of a file with other chunks before its samples, and with the size of a streamed writer, is found.
    /// </summary>
    [Fact]
    public void ReadLayout_SkipsOtherChunks_AndTakesTheRestOfAStreamedFile()
    {
        using var stream = new MemoryStream();
        stream.Write("RIFF\0\0\0\0WAVE"u8);
        WriteChunk(stream, "LIST", [1, 2, 3]);
        WriteChunk(stream, "fmt ", Format(WaveFormat.IEEE_FLOAT, 1, 24000, 32));
        stream.Write("data"u8);
        stream.Write(BitConverter.GetBytes(uint.MaxValue));
        stream.Write(new byte[16]);
        stream.Position = 0;

        var layout = WaveFile.ReadLayout(stream, "streamed.wav");

        layout.Format.Should().Be(new WaveFormat(WaveFormat.IEEE_FLOAT, 1, 24000, 32));
        layout.DataLength.Should().Be(16);
    }

    /// <summary>
    /// Pieces of different formats cannot be joined: the failure names the piece.
    /// </summary>
    [Fact]
    public void Join_RefusesPiecesOfDifferentFormats()
    {
        var first = Write("a.wav", _pcm, [1]);
        var second = Write("b.wav", _pcm with { SampleRate = 2000 }, [1]);

        var act = () => WaveJoiner.Join([first, second], [TimeSpan.Zero, TimeSpan.Zero], Path.Combine(_folder, "joined.wav"));

        act.Should().Throw<InvalidDataException>().Which.Message.Should().Contain("b.wav");
    }

    /// <summary>
    /// A file that is not a WAV file is refused.
    /// </summary>
    [Fact]
    public void ReadLayout_RefusesWhatIsNotAWavFile()
    {
        using var stream = new MemoryStream("not a wave file at all"u8.ToArray());

        var act = () => WaveFile.ReadLayout(stream, "text.wav");

        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// Writes a WAV file of 16-bit samples.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="format">The format of the samples.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The full path of the file.</returns>
    private string Write(string name, WaveFormat format, short[] samples)
    {
        var path = Path.Combine(_folder, name);
        using var file = File.Create(path);
        WaveFile.WriteHeader(file, format, samples.Length * 2);
        foreach (var sample in samples)
        {
            file.Write(BitConverter.GetBytes(sample));
        }

        return path;
    }

    /// <summary>
    /// Reads the 16-bit samples of a WAV file written by the joiner.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The samples.</returns>
    private static short[] Samples(string path)
    {
        var bytes = File.ReadAllBytes(path).AsSpan(WaveFile.HEADER_SIZE);
        var samples = new short[bytes.Length / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes[(i * 2)..]);
        }

        return samples;
    }

    /// <summary>
    /// Writes a chunk of a RIFF file.
    /// </summary>
    /// <param name="stream">The file.</param>
    /// <param name="id">The identifier of the chunk.</param>
    /// <param name="body">The body of the chunk.</param>
    private static void WriteChunk(Stream stream, string id, byte[] body)
    {
        stream.Write(System.Text.Encoding.ASCII.GetBytes(id));
        stream.Write(BitConverter.GetBytes((uint)body.Length));
        stream.Write(body);
        if (body.Length % 2 == 1)
        {
            stream.WriteByte(0);
        }
    }

    /// <summary>
    /// Builds the body of a format chunk.
    /// </summary>
    /// <param name="tag">The format tag.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="rate">The sample rate.</param>
    /// <param name="bits">The bits of a sample.</param>
    /// <returns>The 16 bytes of the chunk.</returns>
    private static byte[] Format(int tag, int channels, int rate, int bits)
    {
        var body = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(body, (ushort)tag);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), (uint)rate);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(8), (uint)(rate * channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(12), (ushort)(channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(14), (ushort)bits);

        return body;
    }
}
