# Developing Voicer

**English** · [Deutsch](../De/Development.md) · [Español](../Es/Development.md) · [Français](../Fr/Development.md) · [Português](../Pt/Development.md) · [Русский](../Ru/Development.md)

## Build, test, run

```bash
dotnet build Voicer.slnx                     # 0 warnings expected: code rules are errors
dotnet test Voicer.slnx                      # xUnit 2 on VSTest
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
```

In VS Code: the "Voicer" profile of `.vscode/launch.json` keeps the data of the debug copy in `.vscode/dev/data`
(ignored by git), so a downloaded model survives a clean build. The worker can be tried on its own:

```bash
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker devices
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker transcribe --model <ggml model> --audio <16 kHz wav> --device 0
```

## Rules

The code rules are in [AGENTS.md](../../AGENTS.md); the build and `Tests/Voicer.Conventions.Tests` enforce them.
`Tests/Voicer.Localization.Tests` checks that every pack has the same keys and placeholders, that every key the code
and the views use exists, and that no Russian text sits in the sources.

## Adding a language

1. `Sources/Core/Voicer.Localization/Languages/<code>.json` with every key of `en.json` (it is picked up by itself).
2. The flag: a block in `Scripts/Assets/generate-flags.ps1`, then run it (`Sources/Apps/Voicer.UI/Assets/Flags/<code>.png`).
3. The installer: a line in `[Languages]` and blocks in `[Messages]`, `[CustomMessages]` of `Scripts/App/voicer.iss`
   (UTF-8 with BOM), and `Scripts/Shared/License/intro.<code>.txt`.
4. The notes of the packages: `Scripts/App/Notes/README.<code>.txt`; the menu entries in `Scripts/App/build-deb.sh`
   and `publish-common.sh`.
5. The documentation: `Docs/<Code>/` with the same files as `Docs/En` (see [Documentation](#documentation)).
6. The project page: an entry in `list` and `docs` of `Sources/Apps/Voicer.Pages/assets/languages.js`, its flag
   `assets/flags/<code>.svg` and a table in `assets/site.js`.

## Documentation

The documents are kept by language: `Docs/En` is the main set; `Docs/De`, `Docs/Es`, `Docs/Fr`, `Docs/Pt` and
`Docs/Ru` hold the same set in German, Spanish, French, Portuguese and Russian. The line under the title of every
document links the same document in the other languages.

- the set of files and their names are the same in every language; a change of meaning goes into all languages at
  once;
- links from outside (the README) lead to `Docs/En`; the project page opens the documents in the language chosen on
  it.

## Adding a model

A record per quantization in `SpeechModelCatalog` (family = folder name, quantization, HTTPS address, exact size,
SHA-256), in the order of the catalog: families from the heaviest to the lightest, quantizations from the most accurate
to the smallest. The text `Model.<family>.Description` goes into every pack; a new quantization also needs
`Quantization.<value>.Name` and `.Description`. The license of the model goes into `THIRD-PARTY-NOTICES.txt`.

A voice model is a record in `VoiceModelCatalog`: its id (the folder name), the weights and the projector (`mmproj`)
of a GGUF published for llama.cpp, each from a fixed revision with its exact size and SHA-256, its languages (the
default first), the voices it ships, whether it speaks in the voice of a sample, whether it needs one and whether it
runs well on the processor. The text `VoiceModel.<id>.Description` and `SpeechLanguage.<code>` of a new language go
into every pack; the release of llama.cpp in `LlamaCppPackage` must support the model. A new release of llama.cpp is
pinned there with the size and SHA-256 of every archive.

## Packages and the project page

See [Scripts/README.md](../../Scripts/README.md). A package carries only the native libraries of its own platform;
models, FFmpeg and llama.cpp are not in it, the user downloads them from the settings.

GitHub Actions: `ci.yml` builds and tests on Windows and Linux for every push to `main` and every pull request;
`packages.yml` builds every package by hand or for a version tag and attaches them to a draft release; `pages.yml`
publishes the project page (`Sources/Apps/Voicer.Pages`, see its README) on every change of it in `main`.
