# Architektur von Voicer

[English](../En/Architecture.md) · **Deutsch** · [Español](../Es/Architecture.md) · [Français](../Fr/Architecture.md) · [Português](../Pt/Architecture.md) · [Русский](../Ru/Architecture.md)

Voicer verwandelt die Sprache von Audio- und Videodateien in Text und einen Text in Sprache, auf dem Computer des
Benutzers. Die Tabs sind Transkription, Sprachausgabe, Verlauf und Einstellungen.

## Projekte

| Projekt | Zuständig für |
|---|---|
| `Sources/Core/Voicer.Localization` | Sprachpakete (`Languages/<code>.json`), `ILocalizer`, `LocalizedError` |
| `Sources/Core/Voicer.Core` | die Datenordner (`AppPaths`), den Modellkatalog und seinen Speicher, Rechengeräte, Transkripte und ihren Markdown-Export, den Verlauf der Erkennungen (`ITranscriptHistory`), Medienformate, das Protokoll des Workers |
| `Sources/Infrastructure/Voicer.Downloads` | Downloads über HTTPS, die nach einer Unterbrechung fortgesetzt und nach Größe und SHA-256 geprüft werden; der Installer der Modelle |
| `Sources/Infrastructure/Voicer.Media` | FFmpeg: finden, den festgelegten Build unter Windows installieren, den Ton jeder Datei als WAV 16 kHz mono extrahieren, ihn für den Verlauf nach Opus komprimieren, einen gespeicherten Ton für den Player dekodieren |
| `Sources/Infrastructure/Voicer.Storage` | die Datenbank des Datenordners (SQLite, `Data/voicer.db`): der Verlauf der Erkennungen und ihr Ton |
| `Sources/Infrastructure/Voicer.LanguageModels` | der Client für Server von Sprachmodellen mit OpenAI-kompatibler API (LM Studio, Ollama, llama.cpp, Jan): die Liste der Modelle, gestreamte Antworten |
| `Sources/Infrastructure/Voicer.Audio` | der Player eines gespeicherten Tons: `AudioPlayer` über dem FFmpeg-Decoder und dem Tongerät (OpenAL Soft) |
| `Sources/Speech/Voicer.Speech.Whisper` | Whisper über Whisper.net: die Liste der Geräte, die Erkennung, der Host des Workers |
| `Sources/Speech/Voicer.Speech.Llama` | Text zu Sprache mit llama.cpp: sein festgelegter Build und dessen Installation, die Argumente von llama-tts und der Synthesizer, der es für jedes Textstück startet; der llama-server der Suche nach Bedeutung (`LlamaEmbeddingServer`), sein Zustand und sein Beenden |
| `Sources/Apps/Voicer.UI` | die Avalonia-Anwendung (MVVM mit CommunityToolkit.Mvvm); dasselbe Programm ist der Worker |
| `Sources/Apps/Voicer.Pages` | die Projektseite: statische Dateien, die GitHub Pages veröffentlicht |

Abhängigkeiten zeigen nach innen: Der Kern kennt weder UI noch Netzwerk noch Engine.

## Eine Erkennung

```
Datei (mp3, mp4, mkv, …)
  └─ FfmpegAudioExtractor ── ffmpeg ──► Data/Temp/<guid>.wav (16 kHz, mono, 16 Bit)
       └─ ProcessWorkerClient ── Voicer.UI --worker --parent <pid> transcribe --model … --audio … --device <n>
            └─ WorkerHost ─ WhisperSpeechEngine (Whisper.net, whisper.cpp)
                 stdout: ein JSON-Objekt pro Zeile: ModelLoaded, Progress, Segment…, Done | Error
       ◄─ Transcript (Segmente, erkannte Sprache) ── das WAV wird gelöscht
```

`TranscriptionRunner` steuert die Schritte; `TranscriptionViewModel` zeigt die Phase und ihren Fortschritt, legt den
Text im Verlauf ab und öffnet ihn im Fenster des Eintrags, das ihn aus der Datenbank zurückliest und nach Markdown
exportiert (`MarkdownTranscriptWriter`).

`WhisperSpeechEngine` dekodiert jedes 30-Sekunden-Fenster des Tons für sich (`n_max_text_ctx = 0`). Standardmäßig gibt
whisper.cpp jedem Fenster den Text der vorigen als Prompt, und `no_context` leert diesen Text nur vor dem ersten
Fenster: Ein Satz, den das Modell einmal wiederholt oder über Stille erfunden hat, kehrt dann in jedem folgenden
Fenster bis zum Ende einer langen Datei zurück und verdrängt die echte Sprache. Die Fenster werden mit Beam Search der
Breite 5 dekodiert, wie es `whisper-cli` tut, mit dem Rückfall auf eine höhere Temperatur (Schritt 0,2) für ein
Fenster, das repetitiv (Entropie unter 2,4) oder unsicher (mittlere Log-Wahrscheinlichkeit unter -1) ausfällt.
`RepetitionGuard` lässt ein Stück aus drei oder mehr Wörtern weg, das eines der letzten drei Stücke Wort für Wort
wiederholt.

Vor dem Modell findet ein Sprachdetektor, wo jemand spricht: Silero VAD 6.2.0 im ggml-Format
(`SpeechDetectorModel`, 0,9 MB, festgelegt wie jeder Download, abgelegt in `Data/Models/silero-vad`), von whisper.cpp
auf dem Prozessor ausgeführt. `SpeechMap` verbindet die Sprachabschnitte mit Pausen von 0,3 s, das Modell erkennt nur
sie, und die Zeiten jedes Stücks werden in die der Datei zurückgerechnet. Stille, Musik und Geräusche erreichen das
Modell nie, sodass es für sie nichts erfindet (Whisper schreibt „Thank you.“ über Stille), und eine lange Pause kostet
keine Zeit. Eine Datei ohne Sprache ergibt einen leeren Text. Der Detektor kommt mit dem ersten Whisper-Modell; ein
vorher heruntergeladenes Modell bekommt ihn bei der ersten Erkennung (`SpeechDetectorSource`). Kann er nicht
heruntergeladen werden, wird der ganze Ton erkannt, und die Seite sagt das.

## Warum ein Worker-Prozess

whisper.cpp läuft in nativem Code, auf den Treibern der Grafikkarte. Ein Treiber, der versagt (auf dem
Entwicklungsrechner stürzt eine integrierte AMD-GPU beim Laden eines Modells in Vulkan ab), beendet den ganzen Prozess.
Darum läuft die Engine nie im Fenster: Das Programm startet sich für jeden Befehl selbst mit `--worker`, liest dessen
JSON-Zeilen und macht aus einem Versagen eine Meldung (`WorkerSession`). Der Worker folgt der mit `--parent`
übergebenen Prozess-Id und endet, wenn die Anwendung endet. Das Abbrechen einer Erkennung beendet den Worker sofort.

## Geräte

`GgmlDeviceProbe` fragt die ggml-Bibliothek der Laufzeit, die Whisper.net geladen hat (`ggml_backend_dev_*`), nach
ihren Geräten mit deren freiem und gesamtem Speicher. whisper.cpp nummeriert die Grafikkarten (diskrete und
integrierte) in der Reihenfolge dieses Registers, sodass der gezeigte Index genau das `GpuDevice` ist, das die Engine
nimmt; der Prozessor ist `-1`. Die Laufzeiten sind die CPU-Laufzeit (mit Metal auf Apple Silicon) und Vulkan (Windows
und Linux, jede Grafikkarte). Vorausgewählt wird eine diskrete Karte, dann eine integrierte, dann der Prozessor. Kann
der Worker die Geräte nicht auflisten, wird der Prozessor allein angeboten.

## Modelle und Werkzeuge

Der Katalog (`SpeechModelCatalog`) enthält die bewährten mehrsprachigen Whisper-Modelle in den ggml-Dateien von
whisper.cpp, vom schwersten zum leichtesten: large-v3, large-v3-turbo, medium und small (tiny und base verlieren zu
viel Genauigkeit). Jedes Modell kommt in den Quantisierungen, die whisper.cpp dafür veröffentlicht
(`ModelQuantization`): F16 (volle Genauigkeit), Q8 und Q5; large-v3 hat kein Q8. Jede Datei ist nach Größe und
SHA-256 festgelegt. Die Einstellungen zeigen eine Karte pro Modell mit einer Auswahl der Quantisierung; eine Datei wird
in den Ordner ihrer Familie heruntergeladen, `Data/Models/<family>`, sodass mehrere Quantisierungen eines Modells
nebeneinander liegen, und die Erkennung listet jede heruntergeladene Datei als `<Name> <Quantisierung>`. Der
empfangene Teil wartet in `<file>.part`, sodass ein unterbrochener Download fortgesetzt wird, und die Datei wird erst
verwendet, wenn ihre Prüfsumme stimmt. FFmpeg wird unter Windows genauso installiert (der Gyan-Build 9.0.2 essentials
nach `Data/Tools/FFmpeg`); Linux und macOS verwenden das FFmpeg des Systems, gefunden über `PATH` und an den üblichen
Orten der Paketverwaltungen.

Die Einstellungen halten die Modelle aus dem Weg: Eine Zeile pro Katalog sagt, wie viele Modelle heruntergeladen sind,
und ihr Knopf öffnet ein Fenster mit einer Karte pro Modell (`ModelCatalogViewModel`, `ModelsWindow`). Ein
heruntergeladenes Modell hat eine beleuchtete Karte mit der Markierung „heruntergeladen“; ein Modell, das allein auf
dem Prozessor gut läuft (Whisper Small, Kokoro), trägt das als Vorteil. Die Sprachmodelle und llama.cpp sind unter
[Sprachausgabe](#sprachausgabe) beschrieben.

## Verlauf

Jeder erkannte Text kommt in die SQLite-Datenbank des Datenordners, `Data/voicer.db` (`SqliteTranscriptHistory` über
Microsoft.Data.Sqlite): eine Zeile von `records` pro Erkennung (Datei, Modell, Gerät, Sprache, Zeit mit ihrem
UTC-Versatz, Dauer, der Anfang des Textes) und ihre Fragmente in `segments`. Die Schema-Version steht in
`PRAGMA user_version`; eine Datei einer neueren Version wird abgelehnt. Ein Eintrag weiß, wie er entstand (`kind`,
Schema-Version 5): eine Erkennung oder eine erzeugte Sprache; der Tab Verlauf kennzeichnet jeden Eintrag so und listet
sie, die neuesten zuerst; ein Eintrag öffnet sich in seinem Fenster, aus der Datenbank gelesen, und kann nach einer
Bestätigung gelöscht werden. Eine Löschung ist physisch: `secure_delete` überschreibt die gelöschten Zeilen und
`auto_vacuum = INCREMENTAL` gibt die frei gewordenen Seiten an das Dateisystem zurück, sodass der Text nicht mehr in
der Datei steht. Der Speicher formuliert seine Fehler als `IOException` mit einem lokalisierten Text; die Seiten zeigen
sie.

Nach einer Erkennung komprimiert `RecordKeeper` die erste Tonspur der Datei mit FFmpeg nach Opus (mono, 48 kHz,
32 kbit/s, etwa 14 MB pro Stunde, `Data/Temp/Audio`) und legt sie in einer Transaktion mit dem Text ab: die Tabelle
`audio` (Schema-Version 2), mit `SqliteBlob` hinein- und herausgestreamt, nie ganz im Speicher. Ein Ton, der sich nicht
komprimieren lässt (etwa ohne `libopus` im FFmpeg des Systems), lässt den Text ohne ihn gespeichert und einen Hinweis
auf der Seite. Die Länge eines Eintrags mit Ton ist die Länge des Tons.

Das Fenster eines Eintrags hat oben den Player, darunter den Text. `RecordViewer` schreibt den Ton in eine temporäre
Datei, solange das Fenster offen ist. `AudioPlayer` startet FFmpeg (`FfmpegPcmDecoder`) ab dem gewählten Moment im
gewählten Tempo (der Filter `atempo` behält die Tonhöhe der Stimme; von halb so schnell bis doppelt so schnell) und
gibt seine rohen Samples (16 Bit mono 48 kHz) an OpenAL Soft (`OpenAlOutput`: eine Quelle mit vier Puffern zu
100 ms); seine wenigen Funktionen bindet `OpenAlNative`, die native Bibliothek kommt aus dem Paket
`Silk.NET.OpenAL.Soft.Native`. Ein Sprung oder ein neues Tempo startet einen neuen Lauf des Decoders; die Position ist
der Start des Laufs plus das, was das Gerät abgespielt hat, mal dem Tempo. Die Knöpfe springen zehn Sekunden zurück und
vor; der Schieberegler wird bei der nächsten Aktualisierung des Fensters angewandt (fünfmal pro Sekunde), sodass das
Ziehen nicht für jedes Pixel FFmpeg startet.

## Zusammenfassungen

Eine Zusammenfassung schreibt ein Sprachmodell auf einem Server, den der Benutzer betreibt: LM Studio, Ollama,
llama.cpp (`llama-server`), Jan oder ein anderer mit der OpenAI-kompatiblen API. Die Einstellungen speichern seine
Adresse, einen optionalen Schlüssel, das Modell und den Kontext, mit dem es geladen ist (`summary.*` in
`Data/settings.json`); die Modelle werden mit `GET models` aufgelistet. Die Sprach-Engine ist dann nicht mehr im
Speicher: Ihr Worker endete mit der Erkennung.

`Summarizer` (Core) passt den Text an den Kontext an: Ein Text, der passt, wird in einer Anfrage zusammengefasst; ein
längerer wird von `TextChunker` (Zeilen, dann Sätze, dann Wörter) in Teile von etwa 2,5 Zeichen pro Token geschnitten,
jeder Teil liefert Notizen, und die Notizen werden zur Zusammenfassung verbunden (erst gruppenweise, wenn sie zusammen
zu lang sind, höchstens drei Runden). Die Anweisungen kommen aus dem Sprachpaket (`Summary.Prompt.*`), sodass die
Zusammenfassung in der Sprache der Oberfläche ist. `ReasoningFilter` entfernt das `<think>…</think>` denkender
Modelle. `OpenAiCompatibleClient` streamt die Antwort (`stream: true`, Server-Sent Events; ein Server, der am Stück
antwortet, wird auch verstanden) und formuliert seine Fehler: ein Server, der nicht antwortet, eine Ablehnung mit ihrem
Code und ihrer Meldung, eine Antwort, die nicht lesbar ist.

Die Zusammenfassung wird mit ihrem Eintrag gespeichert (`summaries`, Schema-Version 3: Text, Modell, Zeit) und durch
eine neue ersetzt; sie geht mit dem Eintrag. Die Seite Transkription bietet nach einer Erkennung „Text
zusammenfassen“ an und öffnet das Fenster des Eintrags, wenn sie fertig ist; das Fenster zeigt die Zusammenfassung
unter dem Text, schreibt sie (erneut) und streamt sie, während sie eintrifft. Der Export nach Markdown stellt die
Zusammenfassung vor den Text.

## Suche und Fragen

Der Tab Verlauf durchsucht die Einträge und beantwortet Fragen dazu (`Core/Search`). Wenn ein Eintrag gespeichert
wird, werden seine Fragmente zu Abschnitten von etwa 600 Zeichen mit ihren Momenten zusammengesetzt
(`PassageBuilder`): die Einheit, die die Suche findet, das Modell liest und zu der der Player springt (Schema-Version 4:
`passages`, vorher gespeicherte Einträge werden beim Upgrade indiziert).

- **Nach Wörtern**: der FTS5-Index `passages_fts` (`unicode61`, Diakritika gefaltet, nach BM25 sortiert).
  `FullTextQuery` macht aus dem, was der Benutzer getippt hat, eine sichere Abfrage: Wörter in Anführungszeichen
  bilden eine Wendung; die anderen Wörter werden nach ihrem Anfang ohne die letzten ein oder zwei Buchstaben gesucht,
  sodass andere Formen passen (*договоров* findet *договор*); ein beliebiges Wort genügt, mehr davon stehen höher. Nur
  Buchstaben und Ziffern erreichen den Index.
- **Nach Bedeutung**: ein Embedding-Modell auf demselben Server (`POST embeddings`), in den Einstellungen neben dem
  Modell der Zusammenfassungen gewählt; ein mehrsprachiges (bge-m3, multilingual-e5) für nicht englische Sprache. Vor
  einer Suche bekommt jeder Abschnitt ohne Vektor dieses Modells einen, sechzehn pro Anfrage (die Indizierung wird
  angezeigt); die Vektoren werden normiert und pro Modell in `embeddings` gespeichert, sodass ein Wechsel des Modells
  neu indiziert, ohne die alten zu verlieren. Die nächsten werden per Kosinus im Speicher gefunden: Tausende Abschnitte
  brauchen Millisekunden, und keine native Erweiterung ist nötig. `EmbeddingText` fügt die Präfixe hinzu, die manche
  Familien erwarten (`query:`/`passage:` von e5, `search_query:`/`search_document:` von nomic).
  Das Modell kann eines von Voicer sein: bge-m3 (MIT), in den Einstellungen heruntergeladen und von `llama-server` desselben
  festgelegten Builds ausgeführt (`LlamaEmbeddingServer`): nur auf 127.0.0.1, auf einem freien Port, mit einem
  zufälligen Schlüssel; von der ersten Suche gestartet, für jede Anfrage gehalten, nach fünf Minuten ohne Anfragen, beim
  Beenden in den Einstellungen und mit der Anwendung beendet. Die Einstellungen zeigen seinen Zustand, den Speicher und
  den Prozessoranteil seines Prozesses.
  Der Abschnitt „Suche nach Bedeutung“ der Einstellungen enthält eine einzige Wahl der Quelle (`SearchSource`,
  gespeichert als `search.source`): aus, ein Modell von Voicer oder ein Embedding-Modell des Servers; eine Wahl, die
  noch nicht bereit ist (Modell nicht heruntergeladen, llama.cpp fehlt, kein Modell des Servers gewählt), sucht nach
  Wörtern und sagt, warum. Vor der ersten Wahl folgt die Quelle dem Installierten. Ist der Server die Quelle, fragt
  seine Prüfung in den Einstellungen das gewählte Modell nach einem Vektor, sodass ein versehentlich gewähltes
  Chat-Modell dort gemeldet wird (LM Studio antwortet darauf mit einem irreführenden „No models loaded“).
- **Fusion**: Beide Listen werden per Reciprocal Rank Fusion (k = 60) zusammengeführt, wie bei der hybriden Suche von
  Elasticsearch. Ohne Embedding-Modell sucht die Suche nur nach Wörtern.
- **Markierungen**: Ein gefundener Abschnitt zeigt die Wörter der Anfrage fett auf Akzentgrund, erkannt an denselben
  Anfängen, die der Index gesucht hat (`SearchMarks`), und beginnt nahe dem ersten, wenn es weit hinten steht. Aus der
  Suche geöffnet, scrollt die Aufnahme zum Abschnitt, zeigt ihn auf Akzentgrund und spielt ab seinem Anfang.
- **Fragen** (Retrieval-Augmented Generation, `RecordQuestions`): Die besten Abschnitte, die in den Kontext passen,
  gehen an das Modell der Zusammenfassungen, nummeriert mit ihrer Datei, ihrem Datum und ihrer Zeit; die Anweisungen
  des Sprachpakets sagen ihm, nur daraus zu antworten und sie als [1], [2] zu zitieren. Die Antwort wird gestreamt;
  ihre Quellen stehen darunter. Nichts gefunden heißt keine Anfrage.

Ein gefundener Abschnitt öffnet seinen Eintrag mit dem Player am Anfang des Abschnitts. Die Abschnitte, ihr Index (FTS5
`secure-delete`) und ihre Vektoren gehen mit dem Eintrag und bleiben nicht in der Datei.

Manche Server legen die ganze Antwort eines Modells in `reasoning_content`, wenn das Modell sein Nachdenken nie
abschließt; `OpenAiCompatibleClient` nimmt sie dann, statt nichts zurückzugeben.

## Sprachausgabe

Der Tab Sprachausgabe spricht einen Text mit einem freien Sprachmodell auf diesem Computer, für eine Lesung von bis zu
mehreren Dutzend Minuten. Die Modelle (`VoiceModelCatalog`) sind die GGUF-Dateien, die das llama.cpp-Projekt selbst
konvertiert und veröffentlicht (ggml-org auf Hugging Face), jede nach Größe und SHA-256 bei einer festen Revision ihres
Repositorys festgelegt: Qwen3-TTS 1.7B Base (Apache-2.0, zehn Sprachen, die Stimme einer Probe), Chatterbox Turbo (MIT,
Englisch, eine eingebaute Stimme oder eine Probe), Kokoro 82M (Apache-2.0, Englisch, 28 mitgelieferte Stimmen, schnell
auf dem Prozessor) und Chatterbox Multilingual (MIT, 23 Sprachen). Ein Modell besteht aus zwei Dateien, seinen
Gewichten und seinem Projektor (`mmproj`: der Audio-Codec, die Stimmen, der Sprecher-Encoder), heruntergeladen nach
`Data/Models/<id>`.

Sie laufen in llama.cpp (MIT). Die Anwendung installiert eine festgelegte Version davon (`LlamaCppPackage`) nach
`Data/Tools/LlamaCpp/<release>`: den Vulkan-Build unter Windows und Linux x64, den Metal-Build unter macOS arm64; das
Archiv wird daneben entpackt, jeder Eintrag geprüft, dass er im Ordner bleibt, und als Ganzes an seinen Platz gelegt.
Ein llama.cpp des Systems wird nicht genommen: Die Modelle brauchen die Version, mit der sie geprüft wurden.

```
Text
  └─ SpeechScript: eine Zeile ist ein Absatz, ein Absatz wird zwischen Sätzen in Stücke bis 250 Zeichen geschnitten
       ├─ Probe (optional): FfmpegVoiceSampleExtractor ── erste 30 s, mono, 24 kHz WAV
       └─ für jedes Stück: LlamaTtsSynthesizer ── llama-tts -m … -mm … -p <Stück> --output part-N.wav
                     [--tts-lang] [--tts-voice] [--tts-speaker-file] [--device VulkanN | none]
  └─ WaveJoiner: die Samples unverändert kopiert, 250 ms Stille zwischen Sätzen, 650 ms zwischen Absätzen
       ◄─ Data/Temp/Generation/<guid>/<erste Wörter>.wav + ein Fragment pro Stück mit seinen Momenten
```

Jedes Stück spricht ein eigener Prozess, ohne Shell gestartet: Das Modell wird geladen, spricht und verlässt mit dem
Prozess den Speicher, sodass nach der Sprachausgabe nichts davon bleibt, und ein natives Versagen eines Grafiktreibers
beendet nur diesen Prozess; seine letzten Protokollzeilen formulieren das Versagen. Abbrechen beendet den Prozess
sofort und entfernt den Ordner der Sprachausgabe. Das Gerät ist das der Erkennung: Der Index einer Grafikkarte unter
den GPUs von ggml ist das `VulkanN` von llama.cpp; der Prozessor bekommt `--device none -ngl 0`; unter macOS nimmt
Metal sein Gerät selbst.

Das Ergebnis spielt auf der Seite (der Player des Verlaufs) und wird als WAV gespeichert, wo der Benutzer will; es geht
wie eine Erkennung in den Verlauf, sein Ton nach Opus komprimiert, als Sprachausgabe gekennzeichnet. Modell, Sprache,
Stimme und Probe der letzten Sprachausgabe werden gemerkt (`generation.*` in `Data/settings.json`), sodass eine Reihe
von Aufnahmen eine Stimme behält.

## Datenordner

`AppPaths` wählt den Datenordner: `--data <folder>`; sonst `Data` neben der Markierung `voicer.portable` eines
Pakets; sonst `Data` neben dem Programm; und wenn dieser Ordner nicht beschreibbar ist (Program Files, `/opt`), das
Profil des Benutzers (`%LOCALAPPDATA%\Voicer\Data`, `~/.local/share/Voicer/Data`).

Ein Versagen des Fensters, mit dem nichts gerechnet hat, beendet die Anwendung nicht: `App` fängt es am Dispatcher ab,
schreibt es in `Data/errors.log` (`ErrorLog`: der Moment, der Typ, die Meldung und der Stack; die Datei beginnt ab
1 MB neu) und zeigt einen Hinweis mit dem Pfad des Protokolls, einen nach dem anderen.

## Oberfläche

Ein dunkles Design: die Palette von RCynQ mit mehr Licht: Der Akzent leuchtet um die Hauptknöpfe, den gewählten Tab,
Fortschrittsbalken und eine Datei, die über den Ablagebereich gezogen wird. Die Sprache wird beim ersten Start aus der
Sprache des Systems gewählt und gemerkt; ein Wechsel formuliert jeden Bildschirm sofort neu (`LocalizationSource`,
`LocalizedViewModel`).
