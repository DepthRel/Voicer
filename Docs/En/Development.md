# Developing Voicer

English · [Русский](../Ru/Development.md)

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
`Tests/Voicer.Localization.Tests` checks that both packs have the same keys and placeholders, that every key the code
and the views use exists, and that no Russian text sits in the sources.

## Adding a language

1. `Sources/Core/Voicer.Localization/Languages/<code>.json` with every key of `en.json` (it is picked up by itself).
2. The flag: a block in `Scripts/Assets/generate-flags.ps1`, then run it (`Sources/Apps/Voicer.UI/Assets/Flags/<code>.png`).
3. The installer: a line in `[Languages]` and blocks in `[Messages]`, `[CustomMessages]` of `Scripts/App/voicer.iss`
   (UTF-8 with BOM), and `Scripts/Shared/License/intro.<code>.txt`.
4. The notes of the packages: `Scripts/App/Notes/README.<code>.txt`; the menu entries in `Scripts/App/build-deb.sh`
   and `publish-common.sh`.
5. The documentation: `Docs/<Code>/` with the same files as `Docs/En`.

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

## Packages

See [Scripts/README.md](../../Scripts/README.md). A package carries only the native libraries of its own platform;
models, FFmpeg and llama.cpp are not in it, the user downloads them from the settings.
