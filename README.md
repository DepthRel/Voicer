# Voicer

Voicer turns the speech of audio and video files into text, on your own computer. Drop an MP3, WAV, MP4, MKV, AVI or
any other media file, pick the model and the device (a video card with its free memory, or the processor with its
RAM), press **Recognize**, then view the text and export it to Markdown. Or paste a script, pick a voice model and a
voice (or a sample of your own voice), press **Generate**, then listen to the narration and save it as WAV.

- Speech recognition: [Whisper](https://github.com/openai/whisper) large-v3, large-v3-turbo, medium or small, in full
  precision or quantized (Q8, Q5) for video cards with less memory, through
  [whisper.cpp](https://github.com/ggml-org/whisper.cpp) and [Whisper.net](https://github.com/sandrohanea/whisper.net),
  on any video card (Vulkan) or the processor; Metal on Apple silicon.
- Voice generation: [Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS) 1.7B (ten languages, the voice of a sample),
  Chatterbox Turbo and Multilingual, Kokoro (28 English voices, fast on the processor), run by
  [llama.cpp](https://github.com/ggml-org/llama.cpp); a long script is spoken sentence group by sentence group and
  joined, the model leaves memory as soon as it is done.
- History: every recognized text and every generated speech is kept, marked as such, with its sound (Opus) in a
  SQLite database in the `Data` folder; a record opens in its window with a player (ten seconds back and forward,
  volume, speed from 0.5x to 2x without a change of pitch), and a deleted one is erased from the database for good.
- Summaries: a language model on your own server (LM Studio, Ollama, llama.cpp, Jan or any other with the
  OpenAI-compatible API) writes the summary of a record, part by part for long texts; it is kept with the record and
  exported with it.
- Search: by words (word forms, "phrases") and, with a multilingual model of embeddings on the same server, by
  meaning; ask a question and the model answers from the records with references to them; a found passage plays from
  its moment.
- Models, FFmpeg and llama.cpp are downloaded once, from the settings, into the `Data` folder of the application, and
  checked by SHA-256.
- Windows 10+, Linux, macOS. English and Russian interface.

## Build

```bash
dotnet build Voicer.slnx
dotnet test Voicer.slnx
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
```

Packages and installers: [Scripts/README.md](Scripts/README.md). Design: [Docs/En/Architecture.md](Docs/En/Architecture.md).
Rules for contributors: [AGENTS.md](AGENTS.md).

## License

GPL-3.0-or-later ([LICENSE.txt](LICENSE.txt)); the components of other authors are listed in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
