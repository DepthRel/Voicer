# Voicer architecture

English · [Русский](../Ru/Architecture.md)

Voicer turns the speech of audio and video files into text, and a text into speech, on the computer of the user. The
tabs are Transcription, Generation, History and Settings.

## Projects

| Project | Owns |
|---|---|
| `Sources/Core/Voicer.Localization` | language packs (`Languages/<code>.json`), `ILocalizer`, `LocalizedError` |
| `Sources/Core/Voicer.Core` | the data folders (`AppPaths`), the catalog of models and their store, compute devices, transcripts and their Markdown export, the history of the recognitions (`ITranscriptHistory`), media formats, the protocol of the worker |
| `Sources/Infrastructure/Voicer.Downloads` | downloads over HTTPS that resume after a break and are checked by size and SHA-256; the installer of models |
| `Sources/Infrastructure/Voicer.Media` | FFmpeg: finding it, installing the pinned build on Windows, extracting the sound of any file as 16 kHz mono WAV, compressing it to Opus for the history, decoding a kept sound for the player |
| `Sources/Infrastructure/Voicer.Storage` | the database of the data folder (SQLite, `Data/voicer.db`): the history of the recognitions and their sound |
| `Sources/Infrastructure/Voicer.LanguageModels` | the client of servers of language models with the OpenAI-compatible API (LM Studio, Ollama, llama.cpp, Jan): the list of models, streamed answers |
| `Sources/Infrastructure/Voicer.Audio` | the player of a kept sound: `AudioPlayer` over the FFmpeg decoder and the sound device (OpenAL Soft) |
| `Sources/Speech/Voicer.Speech.Whisper` | Whisper through Whisper.net: the list of devices, the recognition, the worker host |
| `Sources/Speech/Voicer.Speech.Llama` | text to speech with llama.cpp: its pinned build and installation, the arguments of llama-tts and the synthesizer that runs it per piece of text |
| `Sources/Apps/Voicer.UI` | the Avalonia application (MVVM with CommunityToolkit.Mvvm); the same program is the worker |

Dependencies point inwards: Core knows no UI, network or engine.

## A recognition

```
file (mp3, mp4, mkv, …)
  └─ FfmpegAudioExtractor ── ffmpeg ──► Data/Temp/<guid>.wav (16 kHz, mono, 16-bit)
       └─ ProcessWorkerClient ── Voicer.UI --worker --parent <pid> transcribe --model … --audio … --device <n>
            └─ WorkerHost ─ WhisperSpeechEngine (Whisper.net, whisper.cpp)
                 stdout: one JSON object per line: ModelLoaded, Progress, Segment…, Done | Error
       ◄─ Transcript (segments, detected language) ── the WAV is removed
```

`TranscriptionRunner` drives the steps; `TranscriptionViewModel` shows the stage and its progress, keeps the text in
the history and opens it in the window of the record, which reads it back from the database and exports it to Markdown
(`MarkdownTranscriptWriter`).

`WhisperSpeechEngine` decodes every 30-second window of the sound on its own (`n_max_text_ctx = 0`). By default
whisper.cpp gives each window the text of the previous ones as a prompt, and `no_context` clears that text only before
the first window: a sentence the model once repeated, or made up over silence, then comes back in every following
window to the end of a long file and pushes out the real speech. The windows are decoded by beam search of width 5,
as `whisper-cli` does, with the fallback to a higher temperature (step 0.2) for a window that comes out repetitive
(entropy below 2.4) or unsure (average log probability below -1). `RepetitionGuard` leaves out a piece of three words
or more that repeats one of the last three pieces word for word.

Before the model, a detector of speech finds where someone speaks: Silero VAD 6.2.0 in the ggml format
(`SpeechDetectorModel`, 0.9 MB, pinned like every download, kept in `Data/Models/silero-vad`), run by whisper.cpp on
the processor. `SpeechMap` joins the stretches of speech with pauses of 0.3 s, the model recognizes only them, and the
times of every piece are turned back into those of the file. Silence, music and noise never reach the model, so it
makes up nothing for them (Whisper writes "Thank you." over silence), and a long pause costs no time. A file without
speech gives an empty text. The detector comes with the first Whisper model; a model downloaded before that gets it on
the first recognition (`SpeechDetectorSource`). When it cannot be downloaded, the whole sound is recognized and the
page says so.

## Why a worker process

whisper.cpp runs in native code, on the drivers of the video card. A driver that fails (on the development machine an
integrated AMD GPU crashes inside Vulkan while loading a model) ends the whole process. So the engine never runs in
the window: the program starts itself with `--worker` for every command, reads its JSON lines and turns a failure into
a message (`WorkerSession`). The worker follows the process id given with `--parent` and stops when the application
ends. Cancelling a recognition ends the worker at once.

## Devices

`GgmlDeviceProbe` asks the ggml library of the runtime Whisper.net loaded (`ggml_backend_dev_*`) for its devices, with
their free and total memory. whisper.cpp numbers the video cards (discrete and integrated) in the order of this
registry, so the index shown is exactly the `GpuDevice` the engine takes; the processor is `-1`. The runtimes are the
CPU one (with Metal on Apple silicon) and Vulkan (Windows and Linux, any video card). A discrete card is preselected,
then an integrated one, then the processor. When the worker cannot list the devices, the processor is offered alone.

## Models and tools

The catalog (`SpeechModelCatalog`) holds the proven multilingual Whisper models in the ggml files of whisper.cpp, from
the heaviest to the lightest: large-v3, large-v3-turbo, medium and small (tiny and base lose too much accuracy). Every
model comes in the quantizations whisper.cpp publishes for it (`ModelQuantization`): F16 (full precision), Q8 and Q5;
large-v3 has no Q8. Every file is pinned by size and SHA-256. The settings show one card per model with a choice of the
quantization; a file is downloaded into the folder of its family, `Data/Models/<family>`, so several quantizations of
one model live side by side, and the recognition lists every downloaded file as `<name> <quantization>`. The received
part waits in `<file>.part`, so an interrupted download continues, and the file is used only after its checksum
matches. FFmpeg is installed the same way on Windows (the Gyan 9.0.2 essentials build into `Data/Tools/FFmpeg`); Linux
and macOS use the FFmpeg of the system, found on `PATH` and in the usual places of the package managers.

The settings keep the models out of the way: a line per catalog tells how many models are downloaded, and its button
opens a window with a card per model (`ModelCatalogViewModel`, `ModelsWindow`). A downloaded model has a lit card
marked as received; a model that runs well on the processor alone (Whisper Small, Kokoro) carries that as an
advantage. The voice models and llama.cpp are described in [Voice generation](#voice-generation).

## History

Every recognized text goes into the SQLite database of the data folder, `Data/voicer.db` (`SqliteTranscriptHistory`
through Microsoft.Data.Sqlite): a row of `records` per recognition (file, model, device, language, time with its UTC
offset, duration, the beginning of the text) and its fragments in `segments`. The schema version is kept in
`PRAGMA user_version`; a file of a newer version is refused. A record knows how it was made (`kind`, schema version
5): a recognition or a generated speech; the History tab marks every record so and lists them the newest first; a
record opens in its window, read from the database, and can be deleted after a confirmation. A deletion is physical:
`secure_delete` overwrites the deleted rows and `auto_vacuum = INCREMENTAL` gives the freed pages back to the file
system, so the text is no longer in the file. The storage words its failures as `IOException` with a localized
text; the pages show them.

After a recognition `RecordKeeper` compresses the first sound track of the file with FFmpeg into Opus (mono, 48 kHz,
32 kbit/s, about 14 MB an hour, `Data/Temp/Audio`) and keeps it with the text in one transaction: the `audio` table
(schema version 2), streamed in and out with `SqliteBlob`, never whole in memory. A sound that cannot be compressed (no
`libopus` in the FFmpeg of the system, say) leaves the text kept without it and a note on the page. The length of a
record with a sound is the length of the sound.

The window of a record has the player at the top, the text below. `RecordViewer` writes the sound into a temporary
file for as long as the window is open. `AudioPlayer` runs FFmpeg (`FfmpegPcmDecoder`) from the chosen moment at the
chosen speed (the `atempo` filter keeps the pitch of the voice; from twice as slow to twice as fast) and feeds its raw
samples (16-bit mono 48 kHz) to OpenAL Soft (`OpenAlOutput`: a source with four buffers of 100 ms); its few
functions are bound by `OpenAlNative`, the native library comes from the package `Silk.NET.OpenAL.Soft.Native`. A
jump or a new speed starts a new run of the decoder; the position is the start of the run plus what the device has
played times the speed. The buttons jump ten seconds back and forward; the slider is applied on the next refresh of
the window (five times a second), so dragging it does not start FFmpeg for every pixel.

## Summaries

A summary is written by a language model on a server the user runs: LM Studio, Ollama, llama.cpp (`llama-server`),
Jan or any other with the OpenAI-compatible API. The settings keep its address, an optional key, the model and the
context it is loaded with (`summary.*` in `Data/settings.json`); the models are listed by `GET models`. The speech
engine is not in memory by then: its worker ended with the recognition.

`Summarizer` (Core) fits the text to the context: a text that fits is summarized in one request; a longer one is cut by
`TextChunker` (lines, then sentences, then words) into parts of about 2.5 characters a token, every part gives notes,
and the notes are joined into the summary (joined by groups first when they are too long together, at most three
rounds). The instructions come from the language pack (`Summary.Prompt.*`), so the summary is in the language of the
interface. `ReasoningFilter` removes the `<think>…</think>` of thinking models. `OpenAiCompatibleClient` streams the
answer (`stream: true`, server-sent events; a server that answers whole is understood too) and words its failures: a
server that does not answer, a refusal with its code and message, an answer that cannot be read.

The summary is kept with its record (`summaries`, schema version 3: text, model, time) and replaced by a new one; it
goes with the record. The Transcription page offers "Summarize the text" after a recognition and opens the window of
the record when it is done; the window shows the summary under the text, writes it (again) and streams it as it
arrives. The export to Markdown puts the summary before the text.

## Search and questions

The History tab searches the records and answers questions about them (`Core/Search`). When a record is kept, its
fragments are put together into passages of about 600 characters with their moments (`PassageBuilder`): the unit the
search finds, the model reads and the player jumps to (schema version 4: `passages`, records kept before are
indexed by the upgrade).

- **By words**: the FTS5 index `passages_fts` (`unicode61`, diacritics folded, ranked by BM25). `FullTextQuery` turns
  what the user typed into a safe query: words in quotes form a phrase; the other words are looked up by their
  beginning without the last one or two letters, so other forms match (*договоров* finds *договор*); any word is
  enough, more of them rank higher. Only letters and digits reach the index.
- **By meaning**: a model of embeddings on the same server (`POST embeddings`), chosen in the settings next to the
  model of summaries; a multilingual one (bge-m3, multilingual-e5) for Russian speech. Before a search, every passage
  without a vector of that model gets one, sixteen per request (the indexing is shown); the vectors are normalized and
  kept per model in `embeddings`, so changing the model indexes again without losing the old ones. The closest are
  found by cosine in memory: thousands of passages take milliseconds and no native extension is needed.
  `EmbeddingText` adds the prefixes some families expect (`query:`/`passage:` of e5, `search_query:`/
  `search_document:` of nomic).
- **Fusion**: both lists are merged by Reciprocal Rank Fusion (k = 60), like the hybrid search of Elasticsearch.
  Without a model of embeddings the search goes by words only.
- **Questions** (retrieval-augmented generation, `RecordQuestions`): the best passages that fit into the context are
  given to the model of summaries, numbered with their file, date and time; the instructions of the language pack
  tell it to answer from them only and to cite them as [1], [2]. The answer streams in; its sources are shown below
  it. Nothing found means no request.

A found passage opens its record with the player at the start of the passage. The passages, their index (FTS5
`secure-delete`) and their vectors go with the record and are not left in the file.

Some servers put the whole answer of a model into `reasoning_content` when the model never closes its reasoning;
`OpenAiCompatibleClient` takes it then rather than return nothing.

## Voice generation

The Generation tab speaks a text with a free voice model on this computer, for a narration up to tens of minutes long.
The models (`VoiceModelCatalog`) are the GGUF files the llama.cpp project converts and publishes itself (ggml-org on
Hugging Face), each pinned by size and SHA-256 at a fixed revision of its repository: Qwen3-TTS 1.7B Base (Apache-2.0,
ten languages, the voice of a sample), Chatterbox Turbo (MIT, English, a built-in voice or a sample), Kokoro 82M
(Apache-2.0, English, 28 shipped voices, fast on the processor) and Chatterbox Multilingual (MIT, 23 languages). A model
is two files, its weights and its projector (`mmproj`: the audio codec, the voices, the speaker encoder), downloaded
into `Data/Models/<id>`.

They run in llama.cpp (MIT). The application installs one pinned release of it (`LlamaCppPackage`) into
`Data/Tools/LlamaCpp/<release>`: the Vulkan build on Windows and Linux x64, the Metal build on macOS arm64; the archive
is unpacked aside, every entry checked to stay inside the folder, and put in place whole. A llama.cpp of the system is
not taken: the models need the release they were checked with.

```
text
  └─ SpeechScript: a line is a paragraph, a paragraph is cut between sentences into pieces of up to 250 characters
       ├─ sample (optional): FfmpegVoiceSampleExtractor ── first 30 s, mono, 24 kHz WAV
       └─ for every piece: LlamaTtsSynthesizer ── llama-tts -m … -mm … -p <piece> --output part-N.wav
                     [--tts-lang] [--tts-voice] [--tts-speaker-file] [--device VulkanN | none]
  └─ WaveJoiner: the samples copied as they are, 250 ms of silence between sentences, 650 ms between paragraphs
       ◄─ Data/Temp/Generation/<guid>/<first words>.wav + a fragment per piece with its moments
```

Every piece is spoken by a process of its own, started without a shell: the model is loaded, speaks and leaves memory
with the process, so nothing of it stays after the generation, and a native failure of a video driver ends only that
process; its last log lines word the failure. Cancelling ends the process at once and removes the folder of the
generation. The device is the one of the recognition: the index of a video card among the GPUs of ggml is the
`VulkanN` of llama.cpp; the processor gets `--device none -ngl 0`; on macOS Metal takes its device by itself.

The result plays on the page (the player of the history) and is saved as WAV where the user chooses; it goes into the
history like a recognition, its sound compressed to Opus, marked as a generation. The model, language, voice and
sample of the last generation are remembered (`generation.*` in `Data/settings.json`), so a series of recordings keeps
one voice.

## Data folder

`AppPaths` chooses the data folder: `--data <folder>`; otherwise `Data` next to the `voicer.portable` marker of a
package; otherwise `Data` next to the program; and when that folder is not writable (Program Files, `/opt`), the
profile of the user (`%LOCALAPPDATA%\Voicer\Data`, `~/.local/share/Voicer/Data`).

A failure of the window that nothing expected does not end the application: `App` catches it on the dispatcher, writes
it to `Data/errors.log` (`ErrorLog`: the moment, the type, the message and the stack; the file starts again past
1 MB) and shows a notice with the path of the log, one at a time.

## Interface

One dark theme: the palette of RCynQ with more light: the accent glows around the primary buttons, the selected tab,
progress bars and a file dragged over the drop area. The language is chosen on the first start from the language of
the system and remembered; switching it re-words every screen at once (`LocalizationSource`, `LocalizedViewModel`).
