using Voicer.Core.Enums;

namespace Voicer.Core.Models;

/// <summary>
/// The speech models the application offers: the proven multilingual Whisper models, from the heaviest and most
/// accurate to the lightest one that still recognizes well, each in the quantizations whisper.cpp publishes. The file of
/// every model is pinned by its size and SHA-256, so a download that was damaged or replaced on the way is refused.
/// </summary>
public static class SpeechModelCatalog
{
    /// <summary>
    /// The folder of the ggml files of whisper.cpp the models are downloaded from.
    /// </summary>
    private const string WHISPER_CPP_FILES = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    /// <summary>
    /// The family of Whisper large-v3: the largest and most accurate Whisper model, for about a hundred languages.
    /// </summary>
    private const string LARGE_V3 = "whisper-large-v3";

    /// <summary>
    /// The family of Whisper large-v3-turbo: large-v3 with a short decoder, nearly as accurate and several times faster.
    /// </summary>
    private const string LARGE_V3_TURBO = "whisper-large-v3-turbo";

    /// <summary>
    /// The family of Whisper medium: a balance of accuracy and speed for video cards with little memory.
    /// </summary>
    private const string MEDIUM = "whisper-medium";

    /// <summary>
    /// The family of Whisper small: the lightest model that still recognizes well, for the processor.
    /// </summary>
    private const string SMALL = "whisper-small";

    /// <summary>
    /// Gets Whisper large-v3 in full precision: the largest and most accurate Whisper model, for about a hundred
    /// languages.
    /// </summary>
    public static SpeechModel WhisperLargeV3 { get; } = Whisper(
        LARGE_V3,
        "Whisper Large v3",
        ModelQuantization.F16,
        "ggml-large-v3.bin",
        3_095_033_483,
        "64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2");

    /// <summary>
    /// Gets every model of the catalog, in the order they are offered: the families from the heaviest to the lightest,
    /// and inside a family from the most accurate quantization to the smallest.
    /// </summary>
    public static IReadOnlyList<SpeechModel> All { get; } =
    [
        WhisperLargeV3,
        Whisper(
            LARGE_V3,
            "Whisper Large v3",
            ModelQuantization.Q5,
            "ggml-large-v3-q5_0.bin",
            1_081_140_203,
            "d75795ecff3f83b5faa89d1900604ad8c780abd5739fae406de19f23ecd98ad1"),
        Whisper(
            LARGE_V3_TURBO,
            "Whisper Large v3 Turbo",
            ModelQuantization.F16,
            "ggml-large-v3-turbo.bin",
            1_624_555_275,
            "1fc70f774d38eb169993ac391eea357ef47c88757ef72ee5943879b7e8e2bc69"),
        Whisper(
            LARGE_V3_TURBO,
            "Whisper Large v3 Turbo",
            ModelQuantization.Q8,
            "ggml-large-v3-turbo-q8_0.bin",
            874_188_075,
            "317eb69c11673c9de1e1f0d459b253999804ec71ac4c23c17ecf5fbe24e259a1"),
        Whisper(
            LARGE_V3_TURBO,
            "Whisper Large v3 Turbo",
            ModelQuantization.Q5,
            "ggml-large-v3-turbo-q5_0.bin",
            574_041_195,
            "394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2"),
        Whisper(
            MEDIUM,
            "Whisper Medium",
            ModelQuantization.F16,
            "ggml-medium.bin",
            1_533_763_059,
            "6c14d5adee5f86394037b4e4e8b59f1673b6cee10e3cf0b11bbdbee79c156208"),
        Whisper(
            MEDIUM,
            "Whisper Medium",
            ModelQuantization.Q8,
            "ggml-medium-q8_0.bin",
            823_369_779,
            "42a1ffcbe4167d224232443396968db4d02d4e8e87e213d3ee2e03095dea6502"),
        Whisper(
            MEDIUM,
            "Whisper Medium",
            ModelQuantization.Q5,
            "ggml-medium-q5_0.bin",
            539_212_467,
            "19fea4b380c3a618ec4723c3eef2eb785ffba0d0538cf43f8f235e7b3b34220f"),
        Whisper(
            SMALL,
            "Whisper Small",
            ModelQuantization.F16,
            "ggml-small.bin",
            487_601_967,
            "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b"),
        Whisper(
            SMALL,
            "Whisper Small",
            ModelQuantization.Q8,
            "ggml-small-q8_0.bin",
            264_464_607,
            "49c8fb02b65e6049d5fa6c04f81f53b867b5ec9540406812c643f177317f779f"),
        Whisper(
            SMALL,
            "Whisper Small",
            ModelQuantization.Q5,
            "ggml-small-q5_1.bin",
            190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"),
    ];

    /// <summary>
    /// Finds a model by its identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The model, or <see langword="null"/> when the catalog has no such model.</returns>
    public static SpeechModel? Find(string? id)
    {
        return All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Describes a ggml file of whisper.cpp.
    /// </summary>
    /// <param name="family">The identifier of the model family.</param>
    /// <param name="name">The name of the model family.</param>
    /// <param name="quantization">The quantization of the file.</param>
    /// <param name="fileName">The name of the file in the repository of whisper.cpp.</param>
    /// <param name="sizeBytes">The exact size of the file.</param>
    /// <param name="sha256">The SHA-256 of the file.</param>
    /// <returns>The model.</returns>
    private static SpeechModel Whisper(string family, string name, ModelQuantization quantization, string fileName, long sizeBytes, string sha256)
    {
        var id = $"{family}-{quantization.ToString().ToLowerInvariant()}";

        // Only the lightest family recognizes at a usable speed on the processor alone.
        var runsOnCpu = family == SMALL;

        return new SpeechModel(id, family, name, quantization, fileName, new Uri(WHISPER_CPP_FILES + fileName), sizeBytes, sha256, runsOnCpu);
    }
}
