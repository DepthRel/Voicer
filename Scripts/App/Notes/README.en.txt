Voicer {version} ({runtime}): speech to text

[win] Start: Voicer.exe in this folder (you can make a shortcut to it or pin it to the taskbar).
[unix] Start: ./Voicer.sh
[linux] Application menu: run ./install-desktop-entry.sh once.
[macos] Or double-click Voicer.command.
Models, tools, settings and the history of recognized texts (voicer.db) are kept in the Data folder next to the program. A Whisper model (from Large v3, 2.9 GB, to Small Q5, 180 MB) is downloaded once, in Settings.
Voice generation: install llama.cpp and a voice model (Qwen3-TTS, Chatterbox, Kokoro) in Settings once; then paste a script on the Generation tab, listen to it and save it as WAV.
[win] Installed for all users, Voicer keeps them in %LOCALAPPDATA%\Voicer\Data of each user instead.
[win] FFmpeg, which reads the sound of audio and video files, is installed from Settings with one click.
[linux] Install FFmpeg with your package manager: sudo apt install ffmpeg (or dnf, pacman, ...). The video card is used through Vulkan (libvulkan1).
[macos] Install FFmpeg with Homebrew: brew install ffmpeg.
[win] Windows shows a SmartScreen warning while the build is not signed.
[macos] The build is not signed by Apple. Before the first start run, in this folder: xattr -dr com.apple.quarantine .
[macos] If macOS still refuses to start it, sign it on this Mac: find . -type f \( -perm -u+x -o -name "*.dylib" \) -exec codesign --force --sign - {} \;
Voicer is free software under the GNU GPL, version 3 or later (LICENSE.txt); the components of other authors and their licenses are listed in THIRD-PARTY-NOTICES.txt.
Project: https://github.com/DepthRel/Voicer
