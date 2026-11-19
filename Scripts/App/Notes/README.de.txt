Voicer {version} ({runtime}): Sprache zu Text

[win] Start: Voicer.exe in diesem Ordner (Sie können eine Verknüpfung dazu anlegen oder es an die Taskleiste anheften).
[unix] Start: ./Voicer.sh
[linux] Anwendungsmenü: führen Sie einmal ./install-desktop-entry.sh aus.
[macos] Oder Doppelklick auf Voicer.command.
Modelle, Werkzeuge, Einstellungen und der Verlauf der erkannten Texte (voicer.db) liegen im Ordner Data neben dem Programm. Ein Whisper-Modell (von Large v3, 2,9 GB, bis Small Q5, 180 MB) wird einmal heruntergeladen, in den Einstellungen.
Sprachausgabe: Installieren Sie einmal in den Einstellungen llama.cpp und ein Sprachmodell (Qwen3-TTS, Chatterbox, Kokoro); fügen Sie dann einen Text auf dem Tab „Sprachausgabe“ ein, hören Sie ihn an und speichern Sie ihn als WAV.
[win] Bei einer Installation für alle Benutzer liegen sie stattdessen in %LOCALAPPDATA%\Voicer\Data jedes Benutzers.
[win] FFmpeg, das den Ton von Audio- und Videodateien liest, wird mit einem Klick in den Einstellungen installiert.
[linux] Installieren Sie FFmpeg mit der Paketverwaltung: sudo apt install ffmpeg (oder dnf, pacman, ...). Die Grafikkarte wird über Vulkan genutzt (libvulkan1).
[macos] Installieren Sie FFmpeg mit Homebrew: brew install ffmpeg.
[win] Windows zeigt eine SmartScreen-Warnung, solange der Build nicht signiert ist.
[macos] Der Build ist nicht von Apple signiert. Führen Sie vor dem ersten Start in diesem Ordner aus: xattr -dr com.apple.quarantine .
[macos] Wenn macOS ihn trotzdem nicht startet, signieren Sie ihn auf diesem Mac: find . -type f \( -perm -u+x -o -name "*.dylib" \) -exec codesign --force --sign - {} \;
Voicer ist freie Software unter der GNU GPL, Version 3 oder später (LICENSE.txt); die Komponenten anderer Autoren und ihre Lizenzen stehen in THIRD-PARTY-NOTICES.txt.
Projekt: https://github.com/DepthRel/Voicer
