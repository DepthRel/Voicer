Voicer {version} ({runtime}) : de la parole au texte

[win] Lancement : Voicer.exe dans ce dossier (vous pouvez lui créer un raccourci ou l'épingler à la barre des tâches).
[unix] Lancement : ./Voicer.sh
[linux] Menu des applications : lancez une fois ./install-desktop-entry.sh.
[macos] Ou double-cliquez sur Voicer.command.
Les modèles, les outils, les réglages et l'historique des textes reconnus (voicer.db) sont conservés dans le dossier Data à côté du programme. Un modèle Whisper (de Large v3, 2,9 Go, à Small Q5, 180 Mo) se télécharge une seule fois, dans les réglages.
Synthèse vocale : installez une fois dans les réglages llama.cpp et un modèle de voix (Qwen3-TTS, Chatterbox, Kokoro) ; collez ensuite un texte dans l'onglet « Synthèse vocale », écoutez-le et enregistrez-le en WAV.
Recherche par le sens dans l'historique sans serveur : dans les réglages, sous « Recherche par le sens », choisissez un modèle de Voicer et téléchargez-le (bge-m3) ; il tourne dans llama.cpp seulement pendant la recherche, et les réglages montrent son état et peuvent l'arrêter.
[win] Installé pour tous les utilisateurs, Voicer les conserve à la place dans %LOCALAPPDATA%\Voicer\Data de chaque utilisateur.
[win] FFmpeg, qui lit le son des fichiers audio et vidéo, s'installe depuis les réglages en un clic.
[linux] Installez FFmpeg avec le gestionnaire de paquets : sudo apt install ffmpeg (ou dnf, pacman, ...). La carte graphique est utilisée via Vulkan (libvulkan1).
[macos] Installez FFmpeg avec Homebrew : brew install ffmpeg.
[win] Windows affiche un avertissement SmartScreen tant que le build n'est pas signé.
[macos] Le build n'est pas signé par Apple. Avant le premier lancement, exécutez dans ce dossier : xattr -dr com.apple.quarantine .
[macos] Si macOS refuse encore de le lancer, signez-le sur ce Mac : find . -type f \( -perm -u+x -o -name "*.dylib" \) -exec codesign --force --sign - {} \;
Voicer est un logiciel libre sous la GNU GPL, version 3 ou ultérieure (LICENSE.txt) ; les composants d'autres auteurs et leurs licences sont listés dans THIRD-PARTY-NOTICES.txt.
Projet : https://github.com/DepthRel/Voicer
