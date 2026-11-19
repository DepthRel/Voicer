# Entwicklung von Voicer

[English](../En/Development.md) · **Deutsch** · [Español](../Es/Development.md) · [Français](../Fr/Development.md) · [Português](../Pt/Development.md) · [Русский](../Ru/Development.md)

## Bauen, testen, starten

```bash
dotnet build Voicer.slnx                     # 0 Warnungen erwartet: Code-Regeln sind Fehler
dotnet test Voicer.slnx                      # xUnit 2 auf VSTest
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
```

In VS Code hält das Profil „Voicer“ aus `.vscode/launch.json` die Daten der Debug-Kopie in `.vscode/dev/data` (von
git ignoriert), sodass ein heruntergeladenes Modell einen sauberen Build übersteht. Der Worker lässt sich allein
ausprobieren:

```bash
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker devices
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker transcribe --model <ggml-Modell> --audio <WAV 16 kHz> --device 0
```

## Regeln

Die Code-Regeln stehen in [AGENTS.md](../../AGENTS.md); der Build und `Tests/Voicer.Conventions.Tests` setzen sie
durch. `Tests/Voicer.Localization.Tests` prüft, dass jedes Paket dieselben Schlüssel und Platzhalter hat, dass jeder
Schlüssel, den Code und Ansichten verwenden, existiert und dass kein russischer Text in den Quellen steht.

## Eine Sprache hinzufügen

1. `Sources/Core/Voicer.Localization/Languages/<code>.json` mit jedem Schlüssel aus `en.json` (wird von selbst
   gefunden).
2. Die Flagge: ein Block in `Scripts/Assets/generate-flags.ps1`, dann das Skript ausführen
   (`Sources/Apps/Voicer.UI/Assets/Flags/<code>.png`).
3. Der Installer: eine Zeile in `[Languages]` und Blöcke in `[Messages]`, `[CustomMessages]` von
   `Scripts/App/voicer.iss` (UTF-8 mit BOM) sowie `Scripts/Shared/License/intro.<code>.txt`.
4. Die Hinweise der Pakete: `Scripts/App/Notes/README.<code>.txt`; die Menüeinträge in `Scripts/App/build-deb.sh`
   und `publish-common.sh`.
5. Die Dokumentation: `Docs/<Code>/` mit denselben Dateien wie `Docs/En` (siehe [Dokumentation](#dokumentation)).
6. Die Projektseite: ein Eintrag in `list` und `docs` von `Sources/Apps/Voicer.Pages/assets/languages.js`, ihre Flagge
   `assets/flags/<code>.svg` und eine Tabelle in `assets/site.js`.

## Dokumentation

Die Dokumente sind nach Sprachen abgelegt: `Docs/En` ist der Hauptsatz; `Docs/De`, `Docs/Es`, `Docs/Fr`, `Docs/Pt`
und `Docs/Ru` enthalten denselben Satz auf Deutsch, Spanisch, Französisch, Portugiesisch und Russisch. Die Zeile unter
dem Titel jedes Dokuments verweist auf dasselbe Dokument in den anderen Sprachen.

- die Dateien und ihre Namen sind in jeder Sprache dieselben; eine Änderung der Bedeutung geht in alle Sprachen
  zugleich;
- Links von außen (das README) führen zu `Docs/En`; die Projektseite öffnet die Dokumente in der dort gewählten
  Sprache.

## Ein Modell hinzufügen

Ein Eintrag pro Quantisierung in `SpeechModelCatalog` (Familie = Ordnername, Quantisierung, HTTPS-Adresse, genaue
Größe, SHA-256), in der Reihenfolge des Katalogs: Familien vom schwersten zum leichtesten, Quantisierungen von der
genauesten zur kleinsten. Der Text `Model.<Familie>.Description` kommt in jedes Paket; eine neue Quantisierung braucht
außerdem `Quantization.<Wert>.Name` und `.Description`. Die Lizenz des Modells kommt in `THIRD-PARTY-NOTICES.txt`.

Ein Sprachmodell ist ein Eintrag in `VoiceModelCatalog`: seine Id (der Ordnername), die Gewichte und der Projektor
(`mmproj`) eines für llama.cpp veröffentlichten GGUF, jeweils aus einer festen Revision mit genauer Größe und
SHA-256, seine Sprachen (die Standardsprache zuerst), die mitgelieferten Stimmen, ob es mit der Stimme einer Probe
spricht, ob es eine braucht und ob es gut auf dem Prozessor läuft. Der Text `VoiceModel.<id>.Description` und
`SpeechLanguage.<code>` einer neuen Sprache kommen in jedes Paket; die Version von llama.cpp in `LlamaCppPackage` muss
das Modell unterstützen. Eine neue Version von llama.cpp wird dort mit Größe und SHA-256 jedes Archivs festgelegt.

## Pakete und die Projektseite

Siehe [Scripts/README.md](../../Scripts/README.md). Ein Paket enthält nur die nativen Bibliotheken seiner eigenen
Plattform; Modelle, FFmpeg und llama.cpp sind nicht darin, der Benutzer lädt sie in den Einstellungen herunter.

GitHub Actions: `ci.yml` baut und testet unter Windows und Linux bei jedem Push nach `main` und bei jedem Pull
Request; `packages.yml` baut alle Pakete von Hand oder für ein Versions-Tag und hängt sie an einen Release-Entwurf;
`pages.yml` veröffentlicht die Projektseite (`Sources/Apps/Voicer.Pages`, siehe ihr README) bei jeder Änderung daran
in `main`.
