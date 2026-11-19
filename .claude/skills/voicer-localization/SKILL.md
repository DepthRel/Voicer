---
name: voicer-localization
description: Use when adding or changing any text a Voicer user reads, adding a language, or editing documentation in Docs/, package notes or the installer.
---

# Voicer localization and documentation

## Overview

The rules are in `AGENTS.md` (Localization). The languages are English (default) and Russian; the first start picks
the pack by the two-letter code of the OS UI culture and stores the choice in `Data/settings.json`.

## Where texts live

| Surface | Files | Guard |
|---|---|---|
| App, worker errors, exported Markdown | `Sources/Core/Voicer.Localization/Languages/<code>.json` | `Voicer.Localization.Tests`: same keys, same `{n}` placeholders, keys used in code and views exist, keys built from `WorkerErrorCode` and the model catalog exist, no Cyrillic in `Sources` |
| Flags | `Sources/Apps/Voicer.UI/Assets/Flags/<code>.png` via `Scripts/Assets/generate-flags.ps1` | — |
| Installer | `[Languages]`, `[Messages]`, `[CustomMessages]` in `Scripts/App/voicer.iss` (UTF-8 with BOM); `Scripts/Shared/License/intro.<code>.txt` | same message count per language |
| Package READMEs | `Scripts/App/Notes/README.<code>.txt`, tags `[win] [linux] [macos] [unix]` | — |
| `.deb` menu entry and AppStream data | `Comment[<code>]`, `summary xml:lang` in `Scripts/App/build-deb.sh` and `publish-common.sh` | — |
| Docs | `Docs/En` and `Docs/Ru`, same files, the language line under every title, index `Docs/README.md` | manual |

## Common mistakes

- A key added to `en.json` only: the parity test fails.
- A key built at run time (`"Worker.Error." + code`, `key + "Total"`) without a test listing every value.
- A user-facing string formatted in code (`$"{n} fragments"`): put it in the packs, take it with `ILocalizer.Format`.
- Units of size written in code: use `DisplayText.Bytes` (the units are in the packs: `Units.*`).
- Tests compare with `TestTexts.English` / `TestTexts.Russian` texts taken by key, not with literals.
- A changed meaning written into `Docs/En` only.
