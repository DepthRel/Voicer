Voicer {version} ({runtime}): de fala para texto

[win] Início: Voicer.exe nesta pasta (pode criar-lhe um atalho ou afixá-lo na barra de tarefas).
[unix] Início: ./Voicer.sh
[linux] Menu de aplicações: execute uma vez ./install-desktop-entry.sh.
[macos] Ou faça duplo clique em Voicer.command.
Os modelos, as ferramentas, as definições e o histórico de textos reconhecidos (voicer.db) ficam na pasta Data ao lado do programa. Um modelo Whisper (do Large v3, 2,9 GB, ao Small Q5, 180 MB) é transferido uma só vez, nas definições.
Geração de voz: instale uma vez nas definições o llama.cpp e um modelo de voz (Qwen3-TTS, Chatterbox, Kokoro); depois cole um texto no separador «Geração», ouça-o e guarde-o em WAV.
[win] Instalado para todos os utilizadores, o Voicer guarda-os em vez disso em %LOCALAPPDATA%\Voicer\Data de cada utilizador.
[win] O FFmpeg, que lê o som dos ficheiros de áudio e vídeo, é instalado a partir das definições com um clique.
[linux] Instale o FFmpeg com o gestor de pacotes: sudo apt install ffmpeg (ou dnf, pacman, ...). A placa gráfica é usada através do Vulkan (libvulkan1).
[macos] Instale o FFmpeg com o Homebrew: brew install ffmpeg.
[win] O Windows mostra um aviso do SmartScreen enquanto a build não estiver assinada.
[macos] A build não está assinada pela Apple. Antes do primeiro início execute, nesta pasta: xattr -dr com.apple.quarantine .
[macos] Se o macOS continuar a não a iniciar, assine-a neste Mac: find . -type f \( -perm -u+x -o -name "*.dylib" \) -exec codesign --force --sign - {} \;
O Voicer é software livre sob a GNU GPL, versão 3 ou posterior (LICENSE.txt); os componentes de outros autores e as suas licenças estão enumerados em THIRD-PARTY-NOTICES.txt.
Projeto: https://github.com/DepthRel/Voicer
