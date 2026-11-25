# Arquitectura de Voicer

[English](../En/Architecture.md) · [Deutsch](../De/Architecture.md) · **Español** · [Français](../Fr/Architecture.md) · [Português](../Pt/Architecture.md) · [Русский](../Ru/Architecture.md)

Voicer convierte en texto el habla de archivos de audio y vídeo, y un texto en habla, en el equipo del usuario. Las
pestañas son Transcripción, Generación, Historial y Ajustes.

## Proyectos

| Proyecto | Se encarga de |
|---|---|
| `Sources/Core/Voicer.Localization` | los paquetes de idioma (`Languages/<code>.json`), `ILocalizer`, `LocalizedError` |
| `Sources/Core/Voicer.Core` | las carpetas de datos (`AppPaths`), el catálogo de modelos y su almacén, los dispositivos de cálculo, las transcripciones y su exportación a Markdown, el historial de reconocimientos (`ITranscriptHistory`), los formatos multimedia, el protocolo del proceso de trabajo |
| `Sources/Infrastructure/Voicer.Downloads` | descargas por HTTPS que se reanudan tras un corte y se comprueban por tamaño y SHA-256; el instalador de modelos |
| `Sources/Infrastructure/Voicer.Media` | FFmpeg: encontrarlo, instalar la compilación fijada en Windows, extraer el sonido de cualquier archivo como WAV de 16 kHz mono, comprimirlo a Opus para el historial, decodificar un sonido guardado para el reproductor |
| `Sources/Infrastructure/Voicer.Storage` | la base de datos de la carpeta de datos (SQLite, `Data/voicer.db`): el historial de reconocimientos y su sonido |
| `Sources/Infrastructure/Voicer.LanguageModels` | el cliente de servidores de modelos de lenguaje con la API compatible con OpenAI (LM Studio, Ollama, llama.cpp, Jan): la lista de modelos, las respuestas en streaming |
| `Sources/Infrastructure/Voicer.Audio` | el reproductor de un sonido guardado: `AudioPlayer` sobre el decodificador de FFmpeg y el dispositivo de sonido (OpenAL Soft) |
| `Sources/Speech/Voicer.Speech.Whisper` | Whisper a través de Whisper.net: la lista de dispositivos, el reconocimiento, el host del proceso de trabajo |
| `Sources/Speech/Voicer.Speech.Llama` | texto a voz con llama.cpp: su compilación fijada y su instalación, los argumentos de llama-tts y el sintetizador que lo ejecuta para cada fragmento de texto; los llama-server de los modelos de Voicer (`LlamaModelServer`): de la búsqueda por significado (`LlamaEmbeddingServer`) y de los resúmenes (`LlamaChatServer`), su estado y su detención |
| `Sources/Apps/Voicer.UI` | la aplicación Avalonia (MVVM con CommunityToolkit.Mvvm); el mismo programa es el proceso de trabajo |
| `Sources/Apps/Voicer.Pages` | la página del proyecto: archivos estáticos que publica GitHub Pages |

Las dependencias apuntan hacia dentro: el núcleo no conoce ni la interfaz, ni la red, ni el motor.

## Un reconocimiento

```
archivo (mp3, mp4, mkv, …)
  └─ FfmpegAudioExtractor ── ffmpeg ──► Data/Temp/<guid>.wav (16 kHz, mono, 16 bits)
       └─ ProcessWorkerClient ── Voicer.UI --worker --parent <pid> transcribe --model … --audio … --device <n>
            └─ WorkerHost ─ WhisperSpeechEngine (Whisper.net, whisper.cpp)
                 stdout: un objeto JSON por línea: ModelLoaded, Progress, Segment…, Done | Error
       ◄─ Transcript (segmentos, idioma detectado) ── el WAV se elimina
```

`TranscriptionRunner` dirige los pasos; `TranscriptionViewModel` muestra la etapa y su progreso, guarda el texto en el
historial y lo abre en la ventana del registro, que lo vuelve a leer de la base de datos y lo exporta a Markdown
(`MarkdownTranscriptWriter`).

`WhisperSpeechEngine` decodifica cada ventana de 30 segundos del sonido por separado (`n_max_text_ctx = 0`). Por
defecto, whisper.cpp da a cada ventana el texto de las anteriores como prompt, y `no_context` borra ese texto solo
antes de la primera ventana: una frase que el modelo repitió una vez, o que inventó sobre el silencio, vuelve entonces
en cada ventana siguiente hasta el final de un archivo largo y desplaza el habla real. Las ventanas se decodifican con
búsqueda en haz de ancho 5, como hace `whisper-cli`, con el recurso a una temperatura más alta (paso 0,2) para una
ventana que sale repetitiva (entropía por debajo de 2,4) o insegura (log-probabilidad media por debajo de -1).
`RepetitionGuard` omite un fragmento de tres palabras o más que repite palabra por palabra uno de los tres últimos.

Antes del modelo, un detector de voz encuentra dónde habla alguien: Silero VAD 6.2.0 en formato ggml
(`SpeechDetectorModel`, 0,9 MB, fijado como toda descarga, guardado en `Data/Models/silero-vad`), ejecutado por
whisper.cpp en el procesador. `SpeechMap` une los tramos de habla con pausas de 0,3 s, el modelo reconoce solo esos
tramos, y los tiempos de cada fragmento se convierten de nuevo en los del archivo. El silencio, la música y el ruido
nunca llegan al modelo, así que no inventa nada para ellos (Whisper escribe «Thank you.» sobre el silencio), y una
pausa larga no cuesta tiempo. Un archivo sin habla da un texto vacío. El detector llega con el primer modelo Whisper;
un modelo descargado antes lo recibe en el primer reconocimiento (`SpeechDetectorSource`). Si no se puede descargar, se
reconoce todo el sonido y la página lo indica.

## Por qué un proceso de trabajo

whisper.cpp se ejecuta en código nativo, sobre los controladores de la tarjeta gráfica. Un controlador que falla (en
la máquina de desarrollo una GPU AMD integrada se cae dentro de Vulkan al cargar un modelo) termina todo el proceso.
Por eso el motor nunca se ejecuta en la ventana: el programa se inicia a sí mismo con `--worker` para cada orden, lee
sus líneas JSON y convierte un fallo en un mensaje (`WorkerSession`). El proceso de trabajo sigue el id de proceso
dado con `--parent` y se detiene cuando termina la aplicación. Cancelar un reconocimiento termina el proceso de trabajo
de inmediato.

## Dispositivos

`GgmlDeviceProbe` pregunta a la biblioteca ggml del runtime que cargó Whisper.net (`ggml_backend_dev_*`) por sus
dispositivos, con su memoria libre y total. whisper.cpp numera las tarjetas gráficas (dedicadas e integradas) en el
orden de este registro, así que el índice mostrado es exactamente el `GpuDevice` que toma el motor; el procesador es
`-1`. Los runtimes son el de CPU (con Metal en Apple silicon) y Vulkan (Windows y Linux, cualquier tarjeta gráfica). Se
preselecciona una tarjeta dedicada, luego una integrada, luego el procesador. Si el proceso de trabajo no puede
enumerar los dispositivos, se ofrece solo el procesador.

## Modelos y herramientas

El catálogo (`SpeechModelCatalog`) contiene los modelos Whisper multilingües probados en los archivos ggml de
whisper.cpp, del más pesado al más ligero: large-v3, large-v3-turbo, medium y small (tiny y base pierden demasiada
precisión). Cada modelo viene en las cuantizaciones que whisper.cpp publica para él (`ModelQuantization`): F16
(precisión completa), Q8 y Q5; large-v3 no tiene Q8. Cada archivo está fijado por tamaño y SHA-256. Los ajustes
muestran una tarjeta por modelo con una elección de cuantización; un archivo se descarga en la carpeta de su familia,
`Data/Models/<family>`, así que varias cuantizaciones de un modelo conviven, y el reconocimiento enumera cada archivo
descargado como `<nombre> <cuantización>`. La parte recibida espera en `<file>.part`, así que una descarga interrumpida
continúa, y el archivo solo se usa cuando su suma de comprobación coincide. FFmpeg se instala igual en Windows (la
compilación essentials 9.0.2 de Gyan en `Data/Tools/FFmpeg`); sin ella, y en Linux y macOS, se usa el FFmpeg del
sistema, encontrado en `PATH` y en los lugares habituales de los gestores de paquetes.

Los ajustes mantienen los modelos a un lado: una línea por catálogo dice cuántos modelos hay descargados, y su botón
abre una ventana con una tarjeta por modelo (`ModelCatalogViewModel`, `ModelsWindow`). Un modelo descargado tiene una
tarjeta iluminada marcada como recibida; un modelo que funciona bien solo con el procesador (Whisper Small, Kokoro) lo
muestra como ventaja. Los modelos de voz y llama.cpp se describen en [Generación de voz](#generación-de-voz).

## Historial

Cada texto reconocido va a la base de datos SQLite de la carpeta de datos, `Data/voicer.db` (`SqliteTranscriptHistory`
a través de Microsoft.Data.Sqlite): una fila de `records` por reconocimiento (archivo, modelo, dispositivo, idioma,
hora con su desfase UTC, duración, el comienzo del texto) y sus fragmentos en `segments`. La versión del esquema se
guarda en `PRAGMA user_version`; un archivo de una versión más reciente se rechaza. Los registros se numeran sin
reutilizar números (`AUTOINCREMENT`, versión de esquema 7), así que una página que aún guarda el número de un registro
borrado nunca abre ni cambia otro. Un registro sabe cómo se hizo
(`kind`, versión de esquema 5): un reconocimiento o una voz generada; la pestaña Historial marca así cada registro y
los enumera del más reciente al más antiguo; un registro se abre en su ventana, leído de la base de datos, y se puede
eliminar tras una confirmación. El borrado es físico: `secure_delete` sobrescribe las filas eliminadas y
`auto_vacuum = INCREMENTAL` devuelve las páginas liberadas al sistema de archivos, así que el texto ya no está en el
archivo. El almacenamiento expresa sus fallos como `IOException` con un texto localizado; las páginas los muestran.

Tras un reconocimiento, `RecordKeeper` comprime la primera pista de sonido del archivo con FFmpeg a Opus (mono, 48 kHz,
32 kbit/s, unos 14 MB por hora, `Data/Temp/Audio`) y la guarda con el texto en una sola transacción: la tabla `audio`
(versión de esquema 2), que entra y sale en streaming con `SqliteBlob`, nunca entera en memoria. Un sonido que no se
puede comprimir (sin `libopus` en el FFmpeg del sistema, por ejemplo) deja el texto guardado sin él y una nota en la
página. La duración de un registro con sonido es la duración del sonido.

La ventana de un registro tiene el reproductor arriba y el texto debajo. `RecordViewer` escribe el sonido en un archivo
temporal mientras la ventana está abierta. `AudioPlayer` ejecuta FFmpeg (`FfmpegPcmDecoder`) desde el momento elegido
a la velocidad elegida (el filtro `atempo` conserva el tono de la voz; desde la mitad hasta el doble de rápido) y pasa
sus muestras en bruto (16 bits mono 48 kHz) a OpenAL Soft (`OpenAlOutput`: una fuente con cuatro búferes de 100 ms);
sus pocas funciones las enlaza `OpenAlNative`, y la biblioteca nativa viene del paquete `Silk.NET.OpenAL.Soft.Native`.
Un salto o una velocidad nueva inician una nueva ejecución del decodificador; la posición es el inicio de la ejecución
más lo que el dispositivo ha reproducido por la velocidad. Los botones saltan diez segundos atrás y adelante; el
deslizador se aplica en el siguiente refresco de la ventana (cinco veces por segundo), así que arrastrarlo no inicia
FFmpeg por cada píxel.

Mientras suena el audio, la frase que se está diciendo queda marcada en el texto y a la vista (`TranscriptLayout`; la
ventana sigue al reproductor cinco veces por segundo y solo desplaza el texto cuando la frase sale de la vista). El
texto de un reconocimiento se puede corregir desde el menú del texto: una palabra o una selección, escrita de nuevo o
eliminada, con el audio en pausa mientras tanto. Los fragmentos reconocidos en `segments` nunca cambian: una corrección
es una fila de `corrections` (versión 6 del esquema: la frase, la posición y la longitud en su texto recortado, el
trozo reconocido, el reemplazo, la hora) que se superpone a ellos (`TranscriptCorrections`, Core). Una corrección sobre
otra se une a ella, de modo que el trozo reconocido sigue siendo el texto de Whisper; volver a ponerlo quita la
corrección, y una fila que ya no encaja con el texto se omite. Un lugar corregido aparece subrayado e indica qué se
reconoció allí; el menú lo restaura. Guardar las correcciones vuelve a escribir los pasajes del registro y el comienzo
de su texto en la lista en la misma transacción; los vectores de los pasajes nuevos los calcula la siguiente búsqueda.
La exportación, un resumen nuevo y las preguntas usan el texto corregido; un habla generada es lo que se dijo y no se
corrige.

## Resúmenes

Un resumen lo escribe un modelo de lenguaje en un servidor que mantiene el usuario: LM Studio, Ollama, llama.cpp
(`llama-server`), Jan u otro con la API compatible con OpenAI. Los ajustes guardan su dirección, una clave opcional,
el modelo y el contexto con el que está cargado (`summary.*` en `Data/settings.json`); los modelos se enumeran con
`GET models`. Para entonces el motor de voz ya no está en memoria: su proceso de trabajo terminó con el reconocimiento.

`Summarizer` (Core) ajusta el texto al contexto: un texto que cabe se resume en una petición; uno más largo lo corta
`TextChunker` (líneas, luego frases, también tras los signos de final del chino, del japonés, de las escrituras de la
India y del árabe, luego palabras, luego caracteres enteros) en partes que caben según la estimación de `TokenBudget`
(una letra de una escritura con palabras cuenta como 0,4 de token; un signo del chino, el japonés, el coreano, el
tailandés o una escritura de la India, como un token entero, así que ese texto recibe partes más pequeñas), cada parte
da notas, y
las notas se unen en el resumen (primero por grupos cuando juntas son demasiado largas, como mucho tres rondas). Las
instrucciones vienen del paquete de idioma (`Summary.Prompt.*`), así que el resumen está en el idioma de la interfaz.
`ReasoningFilter` quita el `<think>…</think>` de los modelos que razonan. `OpenAiCompatibleClient` recibe la respuesta
en streaming (`stream: true`, server-sent events; un servidor que responde de una vez también se entiende) y expresa
sus fallos: un servidor que no responde, un rechazo con su código y su mensaje, una respuesta que no se puede leer.

El resumen se guarda con su registro (`summaries`, versión de esquema 3: texto, modelo, hora) y se sustituye por uno
nuevo; se va con el registro. La página Transcripción ofrece «Resumir el texto» tras un reconocimiento y abre la
ventana del registro cuando termina; la ventana muestra el resumen bajo el texto, lo escribe (de nuevo) y lo muestra a
medida que llega. La exportación a Markdown pone el resumen antes del texto.

Los ajustes guardan una sola elección de lo que escribe los resúmenes y las respuestas (`ModelSource`, guardada como
`summary.source`): desactivado, un modelo ligero de Voicer o un modelo del servidor. La conexión con el servidor (su
dirección, su clave y sus modelos) es una sección propia que comparten los resúmenes y la búsqueda. Los modelos de
Voicer son Qwen3-4B-Instruct-2507 y Qwen3-1.7B (Apache-2.0, GGUF Q4_K_M, unos cien idiomas); son más débiles que un
modelo completo de un servidor, y los ajustes lo dicen. El primero instalado se ejecuta en `llama-server` de la
compilación fijada (`LlamaChatServer`), como el modelo de la búsqueda: solo en 127.0.0.1, en un puerto libre, con una
clave aleatoria, una ranura con un contexto de 8192 tokens; `--reasoning-budget 0` evita que Qwen3-1.7B razone en voz
alta. `ChatModelSource` abre una sesión para un resumen o una pregunta y mantiene el servidor hasta que termina. Cada
modelo de Voicer en memoria se muestra en la barra de pestañas de todas las páginas, con la memoria que ocupa y un
botón que lo quita de la memoria al instante, incluso mientras trabaja: ese trabajo falla con un mensaje de que el
modelo salió de la memoria, y la siguiente petición lo inicia de nuevo. Un modelo de Voicer cuya fuente ya no está
elegida también sale de la memoria.

## Búsqueda y preguntas

La pestaña Historial busca en los registros y responde preguntas sobre ellos (`Core/Search`). Cuando se guarda un
registro, sus fragmentos se agrupan en pasajes de unos 600 caracteres con sus momentos (`PassageBuilder`): la unidad
que encuentra la búsqueda, que lee el modelo y a la que salta el reproductor (versión de esquema 4: `passages`; los
registros guardados antes se indexan en la actualización).

- **Por palabras**: el índice FTS5 `passages_fts` (`unicode61`, sin diacríticos, ordenado por BM25). Una palabra se
  compone de letras, números y los signos unidos a las letras (`categories 'L* N* Co M*'`, versión de esquema 7), así
  que una palabra en hindi, bengalí o tailandés queda entera. `FullTextQuery` convierte lo que escribió el usuario en
  una consulta segura: las palabras entre comillas forman una frase; las demás se buscan por su comienzo sin la última
  o las dos últimas letras, así que coinciden otras formas (*contracts* encuentra *contract* y *contracted*); una
  palabra con un dígito se busca entera; basta cualquier palabra, y más de ellas puntúan más alto. Solo los caracteres
  de las palabras llegan al índice. El chino y el japonés se escriben sin espacios, así que el índice toma toda una
  serie de sus signos como una palabra: ese texto solo se encuentra por su comienzo, y mejor por significado.
- **Por significado**: un modelo de embeddings en el mismo servidor (`POST embeddings`), elegido en los ajustes junto
  al modelo de resúmenes; uno multilingüe (bge-m3, multilingual-e5) para habla que no sea en inglés. Antes de una
  búsqueda, cada pasaje sin vector de ese modelo recibe uno, dieciséis por petición (la indexación se muestra); los
  vectores se normalizan y se guardan por modelo en `embeddings`, así que cambiar de modelo indexa de nuevo sin perder
  los antiguos. Los más cercanos se encuentran por coseno en memoria: miles de pasajes llevan milisegundos y no hace
  falta ninguna extensión nativa. `EmbeddingText` añade los prefijos que esperan algunas familias
  (`query:`/`passage:` de e5, `search_query:`/`search_document:` de nomic).
  El modelo puede ser uno de Voicer: bge-m3 (MIT), descargado en los ajustes y ejecutado por `llama-server` de la misma
  compilación fijada (`LlamaEmbeddingServer`): solo en 127.0.0.1, en un puerto libre, con una clave aleatoria; lo
  inicia la primera búsqueda, cada petición lo retiene, y se detiene tras cinco minutos sin peticiones, al detenerlo en
  los ajustes y con la aplicación. Los ajustes muestran su estado, la memoria y la parte del procesador de su proceso.
  La clave de un servidor de Voicer se da en su entorno (`LLAMA_API_KEY`), nunca entre sus argumentos, que cualquier
  usuario del ordenador puede leer. En Windows cada proceso de llama.cpp entra en un objeto de trabajo (job object) que
  el sistema termina con la aplicación, acabe como acabe; además cada servidor se anota en `Data/Temp/Servers`
  (`ServerProcessRegistry`), así que el siguiente inicio termina en Linux y macOS un servidor que dejó una ejecución
  fallida.
  La sección «Modelos de la búsqueda en el historial» de los ajustes guarda una sola elección de la fuente (`ModelSource`,
  guardada como `search.source`): desactivada, un modelo de Voicer o un modelo de embeddings del servidor; una
  elección que aún no está lista (modelo sin descargar, falta llama.cpp, ningún modelo del servidor elegido) busca por
  palabras y dice por qué. Antes de la primera elección la fuente sigue lo instalado. Cuando la fuente es el servidor,
  su comprobación en los ajustes pide un vector al modelo elegido, así que un modelo de chat elegido por error se
  señala allí (LM Studio responde con un engañoso «No models loaded»).
- **Fusión**: ambas listas se combinan con Reciprocal Rank Fusion (k = 60), como la búsqueda híbrida de Elasticsearch.
  Sin modelo de embeddings la búsqueda va solo por palabras.
- **Marcas**: un pasaje encontrado muestra las palabras de la consulta en negrita sobre fondo de acento, reconocidas por
  los mismos comienzos que buscó el índice (`SearchMarks`), y empieza cerca de la primera cuando está lejos. Abierta
  desde la búsqueda, la grabación se desplaza al pasaje, lo muestra sobre fondo de acento y suena desde su comienzo.
- **Preguntas** (generación aumentada por recuperación, `RecordQuestions`): los mejores pasajes que caben en el
  contexto se dan al modelo de resúmenes, numerados con su archivo, fecha y hora; las instrucciones del paquete de
  idioma le dicen que responda solo a partir de ellos y que los cite como [1], [2]. La respuesta llega en streaming;
  sus fuentes se muestran debajo. Si no se encuentra nada, no hay petición.

Un pasaje encontrado abre su registro con el reproductor al inicio del pasaje. Los pasajes, su índice (FTS5
`secure-delete`) y sus vectores se van con el registro y no quedan en el archivo.

Algunos servidores ponen toda la respuesta de un modelo en `reasoning_content` cuando el modelo nunca cierra su
razonamiento; `OpenAiCompatibleClient` la toma entonces en lugar de no devolver nada.

## Generación de voz

La pestaña Generación lee un texto con un modelo de voz libre en este equipo, para una narración de hasta decenas de
minutos. Los modelos (`VoiceModelCatalog`) son los archivos GGUF que el propio proyecto llama.cpp convierte y publica
(ggml-org en Hugging Face), cada uno fijado por tamaño y SHA-256 en una revisión fija de su repositorio: Qwen3-TTS 1.7B
Base (Apache-2.0, diez idiomas, la voz de una muestra), Chatterbox Turbo (MIT, inglés, una voz integrada o una
muestra), Kokoro 82M (Apache-2.0, inglés, 28 voces incluidas, rápido en el procesador) y Chatterbox Multilingual (MIT,
23 idiomas). Un modelo son dos archivos, sus pesos y su proyector (`mmproj`: el códec de audio, las voces, el
codificador del hablante), descargados en `Data/Models/<id>`.

Se ejecutan en llama.cpp (MIT). La aplicación instala una versión fijada (`LlamaCppPackage`) en
`Data/Tools/LlamaCpp/<release>`: la compilación Vulkan en Windows y Linux x64, la compilación Metal en macOS arm64; el
archivo comprimido se descomprime aparte, se comprueba que cada entrada queda dentro de la carpeta y se coloca entero.
No se usa un llama.cpp del sistema: los modelos necesitan la versión con la que se comprobaron.

```
texto
  └─ SpeechScript: una línea es un párrafo, un párrafo se corta entre frases en piezas de hasta 250 caracteres
       ├─ muestra (opcional): FfmpegVoiceSampleExtractor ── primeros 30 s, mono, WAV de 24 kHz
       └─ para cada pieza: LlamaTtsSynthesizer ── llama-tts -m … -mm … -p <pieza> --output part-N.wav
                     [--tts-lang] [--tts-voice] [--tts-speaker-file] [--device VulkanN | none]
  └─ WaveJoiner: las muestras copiadas tal cual, 250 ms de silencio entre frases, 650 ms entre párrafos
       ◄─ Data/Temp/Generation/<guid>/<primeras palabras>.wav + un fragmento por pieza con sus momentos
```

Cada pieza la lee un proceso propio, iniciado sin shell: el modelo se carga, habla y sale de la memoria con el
proceso, así que no queda nada de él tras la generación, y un fallo nativo de un controlador de vídeo termina solo ese
proceso; sus últimas líneas de registro explican el fallo. Cancelar termina el proceso de inmediato y elimina la
carpeta de la generación. El dispositivo es el del reconocimiento: el índice de una tarjeta gráfica entre las GPU de
ggml es el `VulkanN` de llama.cpp; el procesador recibe `--device none -ngl 0`; en macOS, Metal toma su dispositivo
por sí mismo.

El resultado se reproduce en la página (el reproductor del historial) y se guarda como WAV donde el usuario elija; va
al historial como un reconocimiento, con su sonido comprimido a Opus, marcado como generación. El modelo, el idioma,
la voz y la muestra de la última generación se recuerdan (`generation.*` en `Data/settings.json`), así que una serie de
grabaciones mantiene una misma voz.

## Carpeta de datos

`AppPaths` elige la carpeta de datos: `--data <folder>`; si no, `Data` junto a la marca `voicer.portable` de un
paquete; si no, `Data` junto al programa; y cuando esa carpeta no admite escritura (Program Files, `/opt`), el perfil
del usuario (`%LOCALAPPDATA%\Voicer\Data`, `~/.local/share/Voicer/Data`).

La primera ventana sobre una carpeta de datos retiene su archivo de bloqueo, `Data/voicer.lock` (`DataFolderLock`);
solo esa ventana limpia lo que dejaron ejecuciones anteriores en `Data/Temp`, así que una segunda ventana no borra los
archivos de sonido con los que trabaja la primera. El archivo de ajustes se sustituye de forma atómica en cada cambio;
un archivo que no se puede leer en ese momento (otro programa lo retiene) no se sobrescribe, así que un cambio nunca
borra los demás ajustes.

Un fallo de la ventana que nadie esperaba no termina la aplicación: `App` lo captura en el dispatcher, lo escribe en
`Data/errors.log` (`ErrorLog`: el momento, el tipo, el mensaje y la pila; el archivo vuelve a empezar al pasar de
1 MB) y muestra un aviso con la ruta del registro, de uno en uno.

## Interfaz

Un único tema oscuro: la paleta de RCynQ con más luz: el acento brilla alrededor de los botones principales, la
pestaña elegida, las barras de progreso y un archivo arrastrado sobre la zona de soltar. El idioma se elige en el
primer inicio a partir del idioma del sistema y se recuerda; cambiarlo vuelve a redactar cada pantalla al instante
(`LocalizationSource`, `LocalizedViewModel`).
