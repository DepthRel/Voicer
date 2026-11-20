Voicer {version} ({runtime}): de voz a texto

[win] Inicio: Voicer.exe en esta carpeta (puedes crearle un acceso directo o anclarlo a la barra de tareas).
[unix] Inicio: ./Voicer.sh
[linux] Menú de aplicaciones: ejecuta una vez ./install-desktop-entry.sh.
[macos] O haz doble clic en Voicer.command.
Los modelos, las herramientas, los ajustes y el historial de textos reconocidos (voicer.db) se guardan en la carpeta Data junto al programa. Un modelo Whisper (desde Large v3, 2,9 GB, hasta Small Q5, 180 MB) se descarga una sola vez, en los ajustes.
Generación de voz: instala una vez en los ajustes llama.cpp y un modelo de voz (Qwen3-TTS, Chatterbox, Kokoro); luego pega un texto en la pestaña «Generación», escúchalo y guárdalo como WAV.
Búsqueda por significado en el historial sin servidor: en los ajustes, en «Búsqueda por significado», elige un modelo de Voicer y descárgalo (bge-m3); se ejecuta en llama.cpp solo mientras buscas, y los ajustes muestran su estado y pueden detenerlo.
[win] Si se instala para todos los usuarios, Voicer los guarda en cambio en %LOCALAPPDATA%\Voicer\Data de cada usuario.
[win] FFmpeg, que lee el sonido de los archivos de audio y vídeo, se instala desde los ajustes con un clic.
[linux] Instala FFmpeg con el gestor de paquetes: sudo apt install ffmpeg (o dnf, pacman, ...). La tarjeta gráfica se usa mediante Vulkan (libvulkan1).
[macos] Instala FFmpeg con Homebrew: brew install ffmpeg.
[win] Windows muestra un aviso de SmartScreen mientras la compilación no esté firmada.
[macos] La compilación no está firmada por Apple. Antes del primer inicio ejecuta, en esta carpeta: xattr -dr com.apple.quarantine .
[macos] Si macOS sigue sin iniciarla, fírmala en este Mac: find . -type f \( -perm -u+x -o -name "*.dylib" \) -exec codesign --force --sign - {} \;
Voicer es software libre bajo la GNU GPL, versión 3 o posterior (LICENSE.txt); los componentes de otros autores y sus licencias se enumeran en THIRD-PARTY-NOTICES.txt.
Proyecto: https://github.com/DepthRel/Voicer
