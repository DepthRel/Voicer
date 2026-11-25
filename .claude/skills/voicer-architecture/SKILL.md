---
name: voicer-architecture
description: Use when working anywhere in the Voicer repository and you need to know which module owns a behaviour, how a recognition flows through FFmpeg and the worker process, how devices, models and the data folder work, or which design constraints a change must respect.
---

# Voicer architecture

## Overview

Voicer is a GPL-3.0-or-later .NET 10 / Avalonia 12 desktop application (Windows 10+, Linux, macOS) that turns the
speech of audio and video files into text with Whisper (large-v3 down to small, full precision or quantized;
whisper.cpp through Whisper.net), on a video card (Vulkan; Metal on Apple silicon) or the processor. Tabs:
Transcription, Generation (text to speech: Qwen3-TTS, Chatterbox, Kokoro run by the pinned llama.cpp), History,
Settings. Full design: `Docs/En/Architecture.md`.

## Module map (`Sources/`)

| Folder | Owns |
|---|---|
| `Core/Voicer.Localization` | language packs (`Languages/<code>.json`), `ILocalizer`, `LocalizedError` |
| `Core/Voicer.Core` | `AppPaths` (data folder), `SpeechModelCatalog`/`ModelStore`, `ComputeDevice`, `Transcript` + `MarkdownTranscriptWriter`, `MediaFormats`, `WorkerProtocol`/`WorkerEvent`, `Enums/` |
| `Infrastructure/Voicer.Downloads` | `FileDownloader` (HTTPS, resume via `.part`, size + SHA-256), `ModelInstaller` |
| `Infrastructure/Voicer.Media` | `FfmpegLocator`, `FfmpegInstaller` (Windows, pinned Gyan build), `FfmpegAudioExtractor` (16 kHz mono WAV), `FfmpegOutputParser` |
| `Infrastructure/Voicer.Storage` | the SQLite database `Data/voicer.db`: `SqliteTranscriptHistory` (history of recognitions with their Opus sound, physical deletion) |
| `Infrastructure/Voicer.Audio` | `AudioPlayer` (FFmpeg decoder + `OpenAlOutput`, own P/Invoke in `OpenAlNative`), speed via `atempo` |
| `Infrastructure/Voicer.LanguageModels` | `OpenAiCompatibleClient` (models, streamed chat completions; LM Studio, Ollama, llama.cpp, Jan); `Summarizer`/`TextChunker`/`ReasoningFilter` live in `Core/Summaries` ; `Core/Search`: passages, FTS query, RRF fusion, `RecordSearch`, `RecordQuestions` (RAG); `SqliteSearchIndex` in Storage |
| `Speech/Voicer.Speech.Whisper` | `GgmlDeviceProbe` (ggml device registry by function pointers), `WhisperSpeechEngine`, `WorkerHost`, `WorkerCommandLine` |
| `Speech/Voicer.Speech.Llama` | `LlamaCppPackage`/`LlamaCppInstaller` (pinned llama.cpp build in `Data/Tools/LlamaCpp/<release>`, safe unpacking), `LlamaTtsCommandLine`, `LlamaTtsSynthesizer` (one `llama-tts` process per piece of text); `Core/Synthesis`: `VoiceModelCatalog`, `SpeechScript`, `WaveJoiner` |
| `Apps/Voicer.UI` | Avalonia MVVM app; `Program.Main` with `--worker` runs `WorkerHost` instead of the window |

Dependencies point inwards; Core knows no UI, network or engine.

## Ideas a change must keep

- **The engine runs only in the worker process** (`Voicer.UI --worker --parent <pid> <command>`), one process per
  command, JSON lines on stdout. A native crash (a video driver) must end the worker, not the window; the UI words
  the exit (`WorkerSession`). Cancel = kill the worker.
- **Device indexes come from ggml itself**: the GPU index is the position among GPU/IGPU devices of the ggml registry,
  exactly what `WhisperFactoryOptions.GpuDevice` takes; the processor is `-1`.
- **Everything downloaded is pinned** (HTTPS, exact size, SHA-256) and used only after the check; a part resumes.
- **One data folder** `Data` (`Models/<id>`, Whisper in `Models/<family>`; `Tools/FFmpeg`, `Temp`, `settings.json`,
  `voicer.db`, `voicer.lock`) next to `voicer.portable`, or next to the program, or in the profile when not writable;
  `--data` overrides.
- **A llama-server ends with the application**: its key goes in `LLAMA_API_KEY`, never in the arguments; on Windows it
  joins the job object of `ChildProcessJob`; `ServerProcessRegistry` ends what a failed run left on the next start.
- **Text to speech runs in `llama-tts`**, a process per piece of text, so the model leaves memory with it; the device
  index of ggml is its `VulkanN`.
- **Codes, not texts, cross the process boundary** (`WorkerErrorCode`): the UI localizes.

The general rules (time, texts, dependencies, git) are in `AGENTS.md`.

## Common mistakes

- Calling Whisper.net or ggml from the UI process "just to list devices quickly".
- Passing a path to FFmpeg or the worker through a shell or a single argument string: always `ArgumentList`.
- Counting video cards from Vulkan or DXGI instead of the ggml registry: the indexes would not match the engine.
- A new model without size and SHA-256, or without `Model.<id>.Description` in every pack.
