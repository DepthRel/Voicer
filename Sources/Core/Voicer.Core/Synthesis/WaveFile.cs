using System.Buffers.Binary;
using System.Text;
using Voicer.Localization;

namespace Voicer.Core.Synthesis;

/// <summary>
/// Reads and writes the parts of a WAV file the joining of generated speech needs: the format of the samples and where
/// they are. Integer PCM and floating-point samples are understood, also in the extensible form.
/// </summary>
public static class WaveFile
{
    /// <summary>
    /// The format tag of the extensible form, whose real format is in its sub-format.
    /// </summary>
    private const int EXTENSIBLE = 0xFFFE;

    /// <summary>
    /// The size of the format chunk this class writes.
    /// </summary>
    private const int FORMAT_CHUNK_SIZE = 16;

    /// <summary>
    /// The size of the header this class writes, up to the first sample.
    /// </summary>
    public const int HEADER_SIZE = 44;

    /// <summary>
    /// Reads where the sound of a WAV file is and what it is.
    /// </summary>
    /// <param name="stream">The file, at its beginning; it must be seekable.</param>
    /// <param name="name">The name of the file, for the message of a failure.</param>
    /// <returns>The format and the place of the samples.</returns>
    /// <exception cref="InvalidDataException">The file is not a WAV file of a format that can be joined.</exception>
    public static WaveLayout ReadLayout(Stream stream, string name)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> header = stackalloc byte[12];
        if (!TryRead(stream, header) ||
            !header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
        {
            throw Invalid(name);
        }

        WaveFormat? format = null;
        Span<byte> chunk = stackalloc byte[8];
        while (TryRead(stream, chunk))
        {
            var id = Encoding.ASCII.GetString(chunk[..4]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);

            if (id == "fmt ")
            {
                format = ReadFormat(stream, size, name);
            }
            else if (id == "data")
            {
                if (format is null)
                {
                    throw Invalid(name);
                }

                var offset = stream.Position;
                var rest = stream.Length - offset;

                // A writer that streams leaves the size empty or too large: the samples then run to the end of the file.
                var length = size == 0 || size > rest ? rest : size;

                return new WaveLayout(format, offset, length - (length % format.BlockAlign));
            }
            else
            {
                stream.Seek(size + (size & 1), SeekOrigin.Current);
            }
        }

        throw Invalid(name);
    }

    /// <summary>
    /// Writes the header of a WAV file of a known length.
    /// </summary>
    /// <param name="stream">The file, at its beginning.</param>
    /// <param name="format">The format of the samples.</param>
    /// <param name="dataLength">The bytes of samples that follow the header.</param>
    public static void WriteHeader(Stream stream, WaveFormat format, long dataLength)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(format);
        if (dataLength is < 0 or > uint.MaxValue - HEADER_SIZE)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength), dataLength, "A WAV file holds at most 4 GB of samples.");
        }

        Span<byte> header = stackalloc byte[HEADER_SIZE];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(HEADER_SIZE - 8 + dataLength));
        "WAVE"u8.CopyTo(header[8..]);
        "fmt "u8.CopyTo(header[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], FORMAT_CHUNK_SIZE);
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], (ushort)format.FormatTag);
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], (ushort)format.Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)format.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)format.BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], (ushort)format.BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], (ushort)format.BitsPerSample);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)dataLength);

        stream.Write(header);
    }

    /// <summary>
    /// Reads the format chunk and checks that its samples can be joined.
    /// </summary>
    /// <param name="stream">The file, after the head of the chunk.</param>
    /// <param name="size">The size of the chunk.</param>
    /// <param name="name">The name of the file, for the message of a failure.</param>
    /// <returns>The format.</returns>
    private static WaveFormat ReadFormat(Stream stream, uint size, string name)
    {
        if (size is < FORMAT_CHUNK_SIZE or > 1024)
        {
            throw Invalid(name);
        }

        var body = new byte[size + (size & 1)];
        if (!TryRead(stream, body))
        {
            throw Invalid(name);
        }

        int tag = BinaryPrimitives.ReadUInt16LittleEndian(body);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(2));
        var rate = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(4));
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(14));

        // The extensible form keeps the real format in the first two bytes of its sub-format.
        if (tag == EXTENSIBLE && size >= 26)
        {
            tag = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(24));
        }

        // Unsigned 8-bit samples are left out: their silence is not zero.
        var known = (tag == WaveFormat.PCM && bits is 16 or 24 or 32) ||
            (tag == WaveFormat.IEEE_FLOAT && bits is 32 or 64);
        if (!known || channels is < 1 or > 8 || rate is < 1000 or > 384_000)
        {
            throw LocalizedError.Create(m => new InvalidDataException(m), "Generation.WaveUnsupported", name);
        }

        return new WaveFormat(tag, channels, (int)rate, bits);
    }

    /// <summary>
    /// Fills a buffer from the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="buffer">The buffer.</param>
    /// <returns><see langword="true"/> when the whole buffer was read.</returns>
    private static bool TryRead(Stream stream, Span<byte> buffer)
    {
        return stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false) == buffer.Length;
    }

    /// <summary>
    /// Creates the failure of a file that is not a WAV file.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidDataException Invalid(string name)
    {
        return LocalizedError.Create(m => new InvalidDataException(m), "Generation.WaveInvalid", name);
    }
}
