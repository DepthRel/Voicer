---
name: voicer-code-style
description: Use when writing, editing or reviewing any C# (or XAML) code in the Voicer repository, including tests, before claiming the code is done.
---

# Voicer code style

## Overview

The rules themselves are in `AGENTS.md` (General rules, Code style); read them there. This skill adds how to check
and where the rules are easy to break. Code that breaks them is not finished, even if it builds and tests pass.

## Checking

```bash
dotnet build Voicer.slnx                                 # CS1591, nullable, IDE0005/0011/0161/1006 are errors
dotnet test Tests/Voicer.Conventions.Tests               # everything else AGENTS.md says, file:line in the failure
dotnet test Tests/Voicer.Localization.Tests              # keys, placeholders, no Cyrillic in Sources
```

A failing convention test lists every offending place as `path:line`; fix them, do not loosen the test.
`.editorconfig` deliberately leaves out `dotnet_sort_system_directives_first`: order the using blocks by hand.

## Common mistakes

- A helper record or enum at the bottom of an existing file: give it its own file; an enum goes to `Enums/`.
- `Voicer.*` usings before `Avalonia.*`/`CommunityToolkit.*`/`Whisper.*`: libraries come before the solution.
- `&&` / `||` at the start of a continuation line.
- `DateTime.Now`, `DateTimeOffset.UtcNow` or `Stopwatch` in product code: take a `TimeProvider`.
- A constant in PascalCase: every `const`, local ones too, is `UPPER_SNAKE_CASE`.
- `[ObservableProperty]` fields, `[RelayCommand]` methods and `[GeneratedRegex]` partials need a `///` comment too.
- A user-facing string in code or XAML (product names like FFmpeg and Whisper excepted): put it in the packs.
- Russian text in a comment of `Sources/`: `Sources_HoldNoCyrillic` fails.
- An XML comment in a `.csproj` that contains `--`: MSBuild cannot load the project.
