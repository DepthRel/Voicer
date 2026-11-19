---
name: voicer-build-test-release
description: Use when building, testing, running, packaging or releasing Voicer, or when a Voicer test fails unexpectedly.
---

# Voicer build, test and release

## Quick reference

```bash
dotnet build Voicer.slnx                                      # 0 warnings expected
dotnet test Voicer.slnx                                       # xUnit 2 on VSTest
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker devices   # the worker alone
```

| Need | Command / place |
|---|---|
| Debug the app | VS Code profile "Voicer" in `.vscode/launch.json` (data in `.vscode/dev/data`) |
| Windows folder + zip | `Scripts/App/publish-win.ps1` → `artifacts/publish/win-x64` (`Voicer.exe`, `ui/`), `Voicer-<version>-win-x64.zip` |
| Windows installer | `Scripts/App/build-installer.ps1 [-SkipPublish]` (Inno Setup 6+) |
| Linux `.tar.gz` + `.deb` from Windows | `Scripts/App/publish-linux.ps1` (WSL, Ubuntu) |
| macOS `.tar.gz` from Windows | `Scripts/App/publish-macos.ps1 [-Runtime osx-x64]` (WSL; unsigned) |
| Whole release | `Scripts/build-release.ps1 [-Skip …] [-ContinueOnError] [-Sign]` → packages + `SHA256SUMS-<version>.txt` |
| Icon, flags, installer art | `Scripts/Assets/generate-icon.ps1`, `generate-flags.ps1`, `generate-installer-art.ps1` |

## Test facts

- Tests name their language: `TestTexts.English` / `TestTexts.Russian` from `Tests/Shared/TestTexts.cs` (linked into
  every test project except `Voicer.Conventions.Tests`).
- No test needs a GPU, a model, FFmpeg or the network: the worker host, the runner and the view models are tested with
  NSubstitute fakes, the downloader with a fake `HttpMessageHandler`.
- A manual end-to-end check: `--worker transcribe --model <ggml-tiny.bin> --audio <16 kHz wav> --device -1`.

## Release

1. Bump `<Version>` in `Directory.Build.props`.
2. The owner commits, tags `X.Y.Z` and pushes himself (Git rules of `AGENTS.md`). The **Packages** workflow builds every
   package into a **draft** release; locally the same comes from `Scripts/build-release.ps1`.

CI (`.github/workflows/ci.yml`) builds and tests every push to `main` and every PR on Windows and Linux.

## Environment gotchas

- A running Voicer locks `Sources/Apps/Voicer.UI/bin`: close it before building.
- Publishing over `artifacts/publish/<runtime>` while it runs fails: the script says so.
- Whisper.net copies the natives of every platform into `runtimes/`; the publish scripts keep only their own.
