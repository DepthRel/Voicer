namespace Voicer.Core.Synthesis;

/// <summary>
/// The voice models the application offers: free models in the GGUF files the llama.cpp project converts and publishes
/// itself (ggml-org on Hugging Face). Every file is pinned by its size and SHA-256 and downloaded from a fixed revision
/// of its repository.
/// </summary>
public static class VoiceModelCatalog
{
    /// <summary>
    /// The address of the files of Hugging Face.
    /// </summary>
    private const string HUGGING_FACE = "https://huggingface.co/";

    /// <summary>
    /// The repository of Qwen3-TTS 1.7B Base (Apache-2.0) and its revision.
    /// </summary>
    private const string QWEN3_TTS = "ggml-org/Qwen3-TTS-12Hz-1.7B-Base-GGUF/resolve/ca27d74bc954b73dadab5b71ca265d87fc861a7c/";

    /// <summary>
    /// The repository of Chatterbox Turbo (MIT) and its revision.
    /// </summary>
    private const string CHATTERBOX_TURBO = "ggml-org/chatterbox-turbo-GGUF/resolve/66f0393558d0ac1e0337e23a4d15f26e8eca0e12/";

    /// <summary>
    /// The repository of Chatterbox Multilingual V3 (MIT) and its revision.
    /// </summary>
    private const string CHATTERBOX = "ggml-org/chatterbox-GGUF/resolve/a5c19d4a1377a265a3488d13d9ef53314cc06e81/";

    /// <summary>
    /// The repository of Kokoro 82M (Apache-2.0) and its revision.
    /// </summary>
    private const string KOKORO = "ggml-org/Kokoro-82M-GGUF/resolve/ed4a98eab16351c7d803ff011e88267a5b036666/";

    /// <summary>
    /// The English voices shipped with Kokoro: the first letter is the accent (<c>a</c> American, <c>b</c> British),
    /// the second one the voice (<c>f</c> female, <c>m</c> male). Its voices of other languages need phonemes, not text.
    /// </summary>
    private static readonly string[] _kokoroVoices =
    [
        "af_heart", "af_alloy", "af_aoede", "af_bella", "af_jessica", "af_kore", "af_nicole", "af_nova", "af_river",
        "af_sarah", "af_sky", "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael", "am_onyx", "am_puck",
        "am_santa", "bf_alice", "bf_emma", "bf_isabella", "bf_lily", "bm_daniel", "bm_fable", "bm_george", "bm_lewis",
    ];

    /// <summary>
    /// Gets Qwen3-TTS 1.7B: ten languages, speaks in the voice of a sample recording.
    /// </summary>
    public static VoiceModel Qwen3Tts { get; } = new(
        "qwen3-tts-1.7b",
        "Qwen3-TTS 1.7B",
        File(QWEN3_TTS, "Qwen3-TTS-12Hz-1.7B-Base-Q8_0.gguf", 1_847_874_400, "ac7931aeb2e7aad1a6ed6602d353a5679c9d096b18ce8204ac730a8408d572e1"),
        File(QWEN3_TTS, "mmproj-Qwen3-TTS-12Hz-1.7B-Base-Q8_0.gguf", 446_422_912, "6fd65188839bcd6ecc91b277ad471e22a0edfada4699a0fe82f1165c18cfcce2"),
        ["en", "zh", "ja", "ko", "de", "fr", "ru", "pt", "es", "it"],
        [],
        CanCloneVoice: true,
        NeedsSample: true,
        RunsOnCpu: false);

    /// <summary>
    /// Gets Chatterbox Turbo: English, a built-in voice or the voice of a sample recording.
    /// </summary>
    public static VoiceModel ChatterboxTurbo { get; } = new(
        "chatterbox-turbo",
        "Chatterbox Turbo",
        File(CHATTERBOX_TURBO, "chatterbox-turbo-Q8_0.gguf", 481_791_712, "c3b93d3c65327ef5affbdf6c7975475782bb29bc70357c7e863e2d564cadf70f"),
        File(CHATTERBOX_TURBO, "mmproj-chatterbox-turbo-F16.gguf", 555_371_392, "de86b2ead959ba7ffc487ac2ba278e99aed0ef3b5bd04f083352b36c9b86738f"),
        ["en"],
        [],
        CanCloneVoice: true,
        NeedsSample: false,
        RunsOnCpu: false);

    /// <summary>
    /// Gets Kokoro 82M: English, a choice of voices, light enough for the processor.
    /// </summary>
    public static VoiceModel Kokoro { get; } = new(
        "kokoro-82m",
        "Kokoro 82M",
        File(KOKORO, "Kokoro-82M-F32.gguf", 61_774_240, "be19298184f1a2577eb20e4689f1e2b315cbf474c39e113f192991fef3c85c6e"),
        File(KOKORO, "mmproj-Kokoro-82M-F16.gguf", 208_456_512, "071d20f53b7c5d64b08a24bff5dfb77c2ea0df843a7c6c2356998a735ec6929d"),
        ["en"],
        _kokoroVoices,
        CanCloneVoice: false,
        NeedsSample: false,
        RunsOnCpu: true);

    /// <summary>
    /// Gets Chatterbox Multilingual: 23 languages, a built-in voice or the voice of a sample recording.
    /// </summary>
    public static VoiceModel ChatterboxMultilingual { get; } = new(
        "chatterbox-multilingual",
        "Chatterbox Multilingual",
        File(CHATTERBOX, "chatterbox-Q8_0.gguf", 558_520_640, "8fb23a4008577f4fab96083b04da83789934624b0675aafeb7ed72c2bfa5f8d5"),
        File(CHATTERBOX, "mmproj-chatterbox-F16.gguf", 595_185_120, "e2bc7506753d8f4ad6f66afe8fb27fdea4256c9a88a37386303e97e0c4a9f492"),
        [
            "en", "ar", "da", "de", "el", "es", "fi", "fr", "he", "hi", "it", "ja", "ko", "ms", "nl", "no", "pl", "pt", "ru",
            "sv", "sw", "tr", "zh",
        ],
        [],
        CanCloneVoice: true,
        NeedsSample: false,
        RunsOnCpu: false);

    /// <summary>
    /// Gets every model of the catalog, in the order they are offered.
    /// </summary>
    public static IReadOnlyList<VoiceModel> All { get; } = [Qwen3Tts, ChatterboxTurbo, Kokoro, ChatterboxMultilingual];

    /// <summary>
    /// Describes a file of a repository of Hugging Face.
    /// </summary>
    /// <param name="repository">The repository with its revision, ending with a slash.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="sizeBytes">The exact size of the file.</param>
    /// <param name="sha256">The SHA-256 of the file.</param>
    /// <returns>The pinned file.</returns>
    private static VoiceModelFile File(string repository, string fileName, long sizeBytes, string sha256)
    {
        return new VoiceModelFile(fileName, new Uri(HUGGING_FACE + repository + fileName), sizeBytes, sha256);
    }
}
