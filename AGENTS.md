# Voicer: development rules

Voicer is a free (GPL-3.0-or-later) cross-platform desktop application (Windows 10+, Linux, macOS) for speech: it
turns the speech of audio and video files into text with Whisper, on a video card or the processor of the computer,
and turns a text into speech with free voice models run by llama.cpp (the Generation tab). Design documentation:
[Docs/En/Architecture.md](Docs/En/Architecture.md).

You are a senior software engineer. You design reliable, maintainable, secure and well-tested .NET applications.

## Git rules

- Never commit and never push. The owner of the repository does both himself. Leave every change in the working tree.
- Never create, switch or delete branches without an explicit command from the owner. Work in the branch that is
  checked out.
- These rules override any skill, plugin or workflow that asks for a commit, a branch or a worktree.

## General rules

- The latest .NET and C# (currently .NET 10, `LangVersion=latest`); shared settings live in `Directory.Build.props`,
  package versions centrally in `Directory.Packages.props`.
- Only free dependencies compatible with GPL-3.0-or-later. Do not add packages without a need. A dependency that ends
  up in a package, or that the application downloads (a model, a tool), is listed with its license in
  `THIRD-PARTY-NOTICES.txt` in the same change.
- Domain logic does not depend on the UI, hosting or infrastructure. Interfaces, small focused services, dependencies
  through constructors.
- External commands, file paths, downloads and everything that came over the network are untrusted data: validate
  them and fail with a clear diagnostic. Everything Voicer downloads is pinned by size and SHA-256 and goes over HTTPS;
  external programs (FFmpeg, the worker) start without a shell, with an argument list.
- The speech engine runs only in the worker process (the same program started with `--worker`): a native failure of a
  video driver must end the worker, never the window.
- Text to speech runs only in `llama-tts` of the pinned llama.cpp build, a process of its own per piece of text,
  started without a shell: the model leaves memory with the process, and a native failure ends that process only.
- The models of Voicer for the search by meaning (embeddings) and for the summaries and answers (a light chat model)
  run only in `llama-server` of the same pinned build, each kind in a process of its own started without a shell,
  listening on 127.0.0.1 only, on a free port, with a random key. A server is started by a request, held by every
  request, and ended after a pause, by the user (in the settings or in the tab strip, at once, even while it works), when
  its source is no longer chosen, or with the application; a native failure ends that process only, and the next request
  starts it again. A model held in memory is always shown in the tab strip.
- Time comes from `TimeProvider`, so tests are deterministic.
- Critical logic is covered by unit tests; external dependencies (processes, network, native libraries, files) are
  replaced with stubs.
- Prefer deterministic, reproducible and testable implementations. Do not implement future stages unless explicitly
  requested. When a requirement is ambiguous, stop and list the ambiguity instead of guessing.

## Code style

- **One type per file.** Every class, struct, enum, record, interface or delegate that is not `private` lives in a file
  of its own named after it. Only `private` nested types may share a file with their owner.
- **Enumerations live in `Enums` folders.** An enum shared by several projects belongs to the shared core:
  `Voicer.Core/Enums` (namespace `Voicer.Core.Enums`). An enum that only one project needs lives in the `Enums` folder
  at the root of that project (`Voicer.UI/Enums`, `Voicer.Downloads/Enums`), with the narrowest access that works as
  is: `internal` when nothing public exposes it, otherwise `public`. A `private` nested enum stays inside its owner.
- **Comments.** Every type, method, property, field, constructor and data structure carries a short but meaningful
  `///` comment: what it is for, not how it is written. Private members and tests included; a missing comment on a
  public member is a build error (CS1591).
- **Design.** Follow SOLID, KISS, DRY and YAGNI: small classes with one responsibility, dependencies through
  interfaces, no copy-paste, no code for imagined needs.
- **Readable methods.** Separate the logical steps of a method with blank lines (checks, preparation, the work, the
  result); never write a whole method as one dense block.
- **Naming.**
  - Constants of any access, local ones too, are `UPPER_SNAKE_CASE`: `const int SAMPLE_RATE = 16000;`.
  - Private and protected fields, `static readonly` ones too, are `_camelCase`: `_reportInterval`.
  - Public fields, types, methods, properties, events and enum values are PascalCase.
  - Locals and parameters of a method or constructor are `camelCase`, without an underscore.
- **Braces.** Every construct gets a block in braces, even a single line:

  ```csharp
  if (ready)
  {
      return true;
  }
  ```

  never `if (ready) return true;` or a body on the next line without braces.
- **Indentation.** Four spaces, never tabs (two spaces in XML, JSON and YAML files, see `.editorconfig`).
- **Logical operators** stay at the end of a line; a continuation line never starts with `&&` or `||`:

  ```csharp
  var ready = HasSource && SelectedModel is not null &&
      SelectedDevice is not null;
  ```
- File-scoped namespaces; the namespace repeats the folder path inside the project.
- **`using` directives.** No unused ones. They go in three blocks, one right after another without blank lines:
  `System` and `System.*`, then the libraries (`Avalonia`, `CommunityToolkit`, `Whisper`, `NSubstitute`, …), then the
  solution (`Voicer.*`); each block is in alphabetical order. `using static` and aliases come after the blocks:

  ```csharp
  using System.Globalization;
  using Whisper.net;
  using Voicer.Core.Devices;
  using Voicer.Localization;
  ```

The rules are checked, not only written down. The build fails on a missing XML comment of a public member (CS1591),
on braces, block namespaces, naming and unused usings (analyzers of `.editorconfig`, run by `EnforceCodeStyleInBuild`).
`Tests/Voicer.Conventions.Tests` parses the sources with Roslyn and checks the rest: one type per file, private nested
types, XML comments of every member, enums in `Enums`, logical operators, the order of usings, time only through
`TimeProvider` and projects in their solution folders.

## Structure

```
Sources/
  Apps/            the desktop application Voicer.UI (Avalonia); started with --worker it is the worker process; the
                   project page Voicer.Pages (static, GitHub Pages)
  Core/            the core (Voicer.Core: data folders, model catalog, devices, transcripts, worker protocol) and the
                   localization (Voicer.Localization)
  Infrastructure/  downloads checked by SHA-256 (Voicer.Downloads), FFmpeg: finding, installing, extracting the sound
                   (Voicer.Media), the SQLite database of the history
                   (Voicer.Storage), the player of the kept sound (Voicer.Audio), the client of servers of
                   language models for summaries (Voicer.LanguageModels)
  Speech/          speech engines: Whisper through Whisper.net, the device list and the worker host
                   (Voicer.Speech.Whisper); text to speech and the embeddings of the search through the pinned
                   llama.cpp (Voicer.Speech.Llama)
Tests/             test projects (<Project>.Tests), Shared (the localizers of the tests)
Docs/              documentation by language: En (the main set), De, Es, Fr, Pt, Ru (the same set translated)
Scripts/           packaging: App (the application and its installers), Assets (pictures), Shared (helpers of the
                   others); build-release.ps1 builds a whole release; see Scripts/README.md
```

A new project is added to `Voicer.slnx` in the solution folder that repeats its path on disk.

## Data

Everything the application keeps lives in one data folder, `Data`, inside the folder of the application: downloaded
models in `Data/Models/<model id>`, tools in `Data/Tools` (FFmpeg on Windows, llama.cpp), temporary sound in
`Data/Temp`, the settings in `Data/settings.json`, the history of the recognitions and generations in the SQLite database `Data/voicer.db`. A package marks its root with `voicer.portable`; a folder the user cannot write to
(a system-wide installation) sends the data to the profile; `--data <folder>` names it explicitly
(`Voicer.Core/Paths/AppPaths.cs`).

## Localization

- Everything a user reads (labels, error and exception messages, the exported document) lives in
  `Sources/Core/Voicer.Localization/Languages/<code>.json` and is taken by key through an injected `ILocalizer`:
  `Localizer.Get("Key")` / `Localizer.Format("Key", …)` in code, `{loc:Loc Key}` in XAML, `LocalizedError` for
  exceptions. There are no human-language strings in code or markup and no "if the language is …" branches: a
  language is the chosen pack, not a condition. Developer-only log and exception details stay in English. The worker
  sends codes (`WorkerErrorCode`), not texts: the application words them.
- A key is added to every language file at once; English is the default language. The languages are English,
  German, Spanish, French, Portuguese (European) and Russian. The first start takes the language of the operating
  system when there is a pack for it, and the choice is remembered in `Data/settings.json`.
- Every language has a flag (`Scripts/Assets/generate-flags.ps1`), a table on the project page
  (`Sources/Apps/Voicer.Pages/assets/site.js`), messages and a license page in the setup wizard
  (`Scripts/App/voicer.iss`, `Scripts/Shared/License/intro.<code>.txt`) and package notes
  (`Scripts/App/Notes/README.<code>.txt`).
- The documentation is kept in every language at once: the set of files in `Docs/En`, `Docs/De`, `Docs/Es`,
  `Docs/Fr`, `Docs/Pt` and `Docs/Ru` is the same, and a change of meaning goes into all of them. Links from outside
  lead to `Docs/En`. Details:
  [Docs/En/Development.md](Docs/En/Development.md).
