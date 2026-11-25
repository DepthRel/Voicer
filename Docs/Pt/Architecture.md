# Arquitetura do Voicer

[English](../En/Architecture.md) · [Deutsch](../De/Architecture.md) · [Español](../Es/Architecture.md) · [Français](../Fr/Architecture.md) · **Português** · [Русский](../Ru/Architecture.md)

O Voicer transforma em texto a fala de ficheiros de áudio e vídeo, e um texto em fala, no computador do utilizador. Os
separadores são Transcrição, Geração, Histórico e Definições.

## Projetos

| Projeto | Responsável por |
|---|---|
| `Sources/Core/Voicer.Localization` | os pacotes de idioma (`Languages/<code>.json`), `ILocalizer`, `LocalizedError` |
| `Sources/Core/Voicer.Core` | as pastas de dados (`AppPaths`), o catálogo de modelos e o seu armazenamento, os dispositivos de cálculo, as transcrições e a sua exportação para Markdown, o histórico dos reconhecimentos (`ITranscriptHistory`), os formatos multimédia, o protocolo do processo de trabalho |
| `Sources/Infrastructure/Voicer.Downloads` | transferências por HTTPS que continuam após uma interrupção e são verificadas por tamanho e SHA-256; o instalador dos modelos |
| `Sources/Infrastructure/Voicer.Media` | FFmpeg: encontrá-lo, instalar a build fixada no Windows, extrair o som de qualquer ficheiro como WAV de 16 kHz mono, comprimi-lo em Opus para o histórico, descodificar um som guardado para o leitor |
| `Sources/Infrastructure/Voicer.Storage` | a base de dados da pasta de dados (SQLite, `Data/voicer.db`): o histórico dos reconhecimentos e o seu som |
| `Sources/Infrastructure/Voicer.LanguageModels` | o cliente de servidores de modelos de linguagem com a API compatível com OpenAI (LM Studio, Ollama, llama.cpp, Jan): a lista de modelos, as respostas em streaming |
| `Sources/Infrastructure/Voicer.Audio` | o leitor de um som guardado: `AudioPlayer` sobre o descodificador do FFmpeg e o dispositivo de som (OpenAL Soft) |
| `Sources/Speech/Voicer.Speech.Whisper` | Whisper através do Whisper.net: a lista de dispositivos, o reconhecimento, o anfitrião do processo de trabalho |
| `Sources/Speech/Voicer.Speech.Llama` | texto para fala com o llama.cpp: a sua build fixada e a sua instalação, os argumentos do llama-tts e o sintetizador que o executa para cada parte do texto; os llama-server dos modelos do Voicer (`LlamaModelServer`): da pesquisa por significado (`LlamaEmbeddingServer`) e dos resumos (`LlamaChatServer`), o seu estado e a sua paragem |
| `Sources/Apps/Voicer.UI` | a aplicação Avalonia (MVVM com CommunityToolkit.Mvvm); o mesmo programa é o processo de trabalho |
| `Sources/Apps/Voicer.Pages` | a página do projeto: ficheiros estáticos publicados pelo GitHub Pages |

As dependências apontam para dentro: o núcleo não conhece a interface, a rede nem o motor.

## Um reconhecimento

```
ficheiro (mp3, mp4, mkv, …)
  └─ FfmpegAudioExtractor ── ffmpeg ──► Data/Temp/<guid>.wav (16 kHz, mono, 16 bits)
       └─ ProcessWorkerClient ── Voicer.UI --worker --parent <pid> transcribe --model … --audio … --device <n>
            └─ WorkerHost ─ WhisperSpeechEngine (Whisper.net, whisper.cpp)
                 stdout: um objeto JSON por linha: ModelLoaded, Progress, Segment…, Done | Error
       ◄─ Transcript (segmentos, idioma detetado) ── o WAV é eliminado
```

`TranscriptionRunner` conduz os passos; `TranscriptionViewModel` mostra a etapa e o seu progresso, guarda o texto no
histórico e abre-o na janela do registo, que o volta a ler da base de dados e o exporta para Markdown
(`MarkdownTranscriptWriter`).

`WhisperSpeechEngine` descodifica cada janela de 30 segundos do som por si só (`n_max_text_ctx = 0`). Por omissão, o
whisper.cpp dá a cada janela o texto das anteriores como prompt, e `no_context` apaga esse texto apenas antes da
primeira janela: uma frase que o modelo repetiu uma vez, ou inventou sobre silêncio, volta então em cada janela
seguinte até ao fim de um ficheiro longo e afasta a fala real. As janelas são descodificadas por pesquisa em feixe de
largura 5, como faz o `whisper-cli`, com o recurso a uma temperatura mais alta (passo 0,2) para uma janela que sai
repetitiva (entropia abaixo de 2,4) ou insegura (log-probabilidade média abaixo de -1). `RepetitionGuard` deixa de
fora uma parte de três palavras ou mais que repete palavra a palavra uma das três últimas.

Antes do modelo, um detetor de fala encontra onde alguém fala: Silero VAD 6.2.0 no formato ggml
(`SpeechDetectorModel`, 0,9 MB, fixado como todas as transferências, guardado em `Data/Models/silero-vad`), executado
pelo whisper.cpp no processador. `SpeechMap` junta os trechos de fala com pausas de 0,3 s, o modelo reconhece só
esses trechos, e os tempos de cada parte são convertidos de volta nos do ficheiro. O silêncio, a música e o ruído
nunca chegam ao modelo, por isso ele não inventa nada para eles (o Whisper escreve «Thank you.» sobre silêncio), e uma
pausa longa não custa tempo. Um ficheiro sem fala dá um texto vazio. O detetor vem com o primeiro modelo Whisper; um
modelo transferido antes recebe-o no primeiro reconhecimento (`SpeechDetectorSource`). Se não puder ser transferido, é
reconhecido todo o som e a página indica-o.

## Porquê um processo de trabalho

O whisper.cpp corre em código nativo, sobre os controladores da placa gráfica. Um controlador que falha (na máquina de
desenvolvimento, uma GPU AMD integrada falha dentro do Vulkan ao carregar um modelo) termina todo o processo. Por isso
o motor nunca corre na janela: o programa inicia-se a si próprio com `--worker` para cada comando, lê as suas linhas
JSON e transforma uma falha numa mensagem (`WorkerSession`). O processo de trabalho segue o id de processo dado com
`--parent` e para quando a aplicação termina. Cancelar um reconhecimento termina o processo de trabalho de imediato.

## Dispositivos

`GgmlDeviceProbe` pergunta à biblioteca ggml do runtime que o Whisper.net carregou (`ggml_backend_dev_*`) pelos seus
dispositivos, com a sua memória livre e total. O whisper.cpp numera as placas gráficas (dedicadas e integradas) pela
ordem deste registo, por isso o índice mostrado é exatamente o `GpuDevice` que o motor usa; o processador é `-1`. Os
runtimes são o de CPU (com Metal no Apple silicon) e o Vulkan (Windows e Linux, qualquer placa gráfica). É
pré-selecionada uma placa dedicada, depois uma integrada, depois o processador. Se o processo de trabalho não conseguir
listar os dispositivos, é oferecido só o processador.

## Modelos e ferramentas

O catálogo (`SpeechModelCatalog`) contém os modelos Whisper multilingues comprovados nos ficheiros ggml do whisper.cpp,
do mais pesado ao mais leve: large-v3, large-v3-turbo, medium e small (tiny e base perdem demasiada precisão). Cada
modelo vem nas quantizações que o whisper.cpp publica para ele (`ModelQuantization`): F16 (precisão total), Q8 e Q5; o
large-v3 não tem Q8. Cada ficheiro é fixado por tamanho e SHA-256. As definições mostram um cartão por modelo com uma
escolha de quantização; um ficheiro é transferido para a pasta da sua família, `Data/Models/<family>`, por isso várias
quantizações de um modelo convivem lado a lado, e o reconhecimento lista cada ficheiro transferido como
`<nome> <quantização>`. A parte recebida espera em `<file>.part`, por isso uma transferência interrompida continua, e
o ficheiro só é usado depois de a sua soma de verificação coincidir. O FFmpeg é instalado da mesma forma no Windows (a
build essentials 9.0.2 da Gyan em `Data/Tools/FFmpeg`); sem ela, e no Linux e no macOS, é usado o FFmpeg do sistema,
encontrado no `PATH` e nos locais habituais dos gestores de pacotes.

As definições mantêm os modelos fora do caminho: uma linha por catálogo diz quantos modelos estão transferidos, e o
seu botão abre uma janela com um cartão por modelo (`ModelCatalogViewModel`, `ModelsWindow`). Um modelo transferido
tem um cartão iluminado marcado como recebido; um modelo que funciona bem só no processador (Whisper Small, Kokoro)
mostra isso como vantagem. Os modelos de voz e o llama.cpp estão descritos em [Geração de voz](#geração-de-voz).

## Histórico

Cada texto reconhecido vai para a base de dados SQLite da pasta de dados, `Data/voicer.db`
(`SqliteTranscriptHistory` através do Microsoft.Data.Sqlite): uma linha de `records` por reconhecimento (ficheiro,
modelo, dispositivo, idioma, hora com o seu desvio UTC, duração, o início do texto) e os seus fragmentos em
`segments`. A versão do esquema é guardada em `PRAGMA user_version`; um ficheiro de uma versão mais recente é
recusado. Os registos são numerados sem reutilizar números (`AUTOINCREMENT`, versão de esquema 7), por isso uma página
que ainda guarda o número de um registo apagado nunca abre nem altera outro. Um registo sabe como foi feito (`kind`, versão de esquema 5): um reconhecimento ou uma fala gerada; o
separador Histórico marca assim cada registo e lista-os do mais recente para o mais antigo; um registo abre-se na sua
janela, lido da base de dados, e pode ser eliminado após uma confirmação. A eliminação é física: `secure_delete`
sobrescreve as linhas eliminadas e `auto_vacuum = INCREMENTAL` devolve as páginas libertadas ao sistema de ficheiros,
por isso o texto já não está no ficheiro. O armazenamento descreve as suas falhas como `IOException` com um texto
localizado; as páginas mostram-nas.

Após um reconhecimento, `RecordKeeper` comprime a primeira faixa de som do ficheiro com o FFmpeg em Opus (mono,
48 kHz, 32 kbit/s, cerca de 14 MB por hora, `Data/Temp/Audio`) e guarda-a com o texto numa só transação: a tabela
`audio` (versão de esquema 2), lida e escrita em streaming com `SqliteBlob`, nunca inteira em memória. Um som que não
pode ser comprimido (sem `libopus` no FFmpeg do sistema, por exemplo) deixa o texto guardado sem ele e uma nota na
página. A duração de um registo com som é a duração do som.

A janela de um registo tem o leitor em cima e o texto em baixo. `RecordViewer` escreve o som num ficheiro temporário
enquanto a janela está aberta. `AudioPlayer` executa o FFmpeg (`FfmpegPcmDecoder`) a partir do momento escolhido à
velocidade escolhida (o filtro `atempo` mantém o tom da voz; de metade a dobro da velocidade) e passa as suas amostras
em bruto (16 bits mono 48 kHz) ao OpenAL Soft (`OpenAlOutput`: uma fonte com quatro buffers de 100 ms); as suas
poucas funções são ligadas por `OpenAlNative`, e a biblioteca nativa vem do pacote `Silk.NET.OpenAL.Soft.Native`. Um
salto ou uma velocidade nova inicia uma nova execução do descodificador; a posição é o início da execução mais o que
o dispositivo reproduziu vezes a velocidade. Os botões saltam dez segundos para trás e para a frente; o cursor é
aplicado na atualização seguinte da janela (cinco vezes por segundo), por isso arrastá-lo não inicia o FFmpeg para
cada píxel.

Enquanto o som toca, a frase que está a ser dita fica marcada no texto e à vista (`TranscriptLayout`; a janela segue
o leitor cinco vezes por segundo e só desloca o texto quando a frase sai da vista). O texto de um reconhecimento pode
ser corrigido a partir do menu do texto: uma palavra ou uma seleção, escrita de novo ou eliminada, com o som em pausa
entretanto. Os fragmentos reconhecidos em `segments` nunca mudam: uma correção é uma linha de `corrections` (versão 6
do esquema: a frase, a posição e o comprimento no seu texto aparado, o pedaço reconhecido, a substituição, a hora) que
se sobrepõe a eles (`TranscriptCorrections`, Core). Uma correção sobre outra junta-se a ela, de modo que o pedaço
reconhecido continua a ser o texto do Whisper; repô-lo retira a correção, e uma linha que já não encaixa no texto é
ignorada. Um local corrigido aparece sublinhado e indica o que ali foi reconhecido; o menu repõe-no. Guardar as
correções volta a escrever as passagens do registo e o início do seu texto na lista na mesma transação; os vetores das
novas passagens são calculados pela pesquisa seguinte. A exportação, um novo resumo e as perguntas usam o texto
corrigido; uma fala gerada é o que foi dito e não é corrigida.

## Resumos

Um resumo é escrito por um modelo de linguagem num servidor que é do utilizador: LM Studio, Ollama, llama.cpp
(`llama-server`), Jan ou outro com a API compatível com OpenAI. As definições guardam o seu endereço, uma chave
opcional, o modelo e o contexto com que está carregado (`summary.*` em `Data/settings.json`); os modelos são listados
por `GET models`. Nessa altura o motor de fala já não está em memória: o seu processo de trabalho terminou com o
reconhecimento.

`Summarizer` (Core) ajusta o texto ao contexto: um texto que cabe é resumido num pedido; um mais longo é cortado por
`TextChunker` (linhas, depois frases, também após os sinais finais do chinês, do japonês, das escritas da Índia e do
árabe, depois palavras, depois carateres inteiros) em partes que cabem segundo a estimativa de `TokenBudget` (uma letra
de uma escrita com palavras conta como 0,4 de token; um sinal do chinês, do japonês, do coreano, do tailandês ou de
uma escrita da Índia, como um token inteiro, por isso esse texto recebe partes mais pequenas), cada parte dá notas, e as notas são juntas no resumo (primeiro por grupos quando juntas são demasiado longas, no máximo três
rondas). As instruções vêm do pacote de idioma (`Summary.Prompt.*`), por isso o resumo está no idioma da interface.
`ReasoningFilter` retira o `<think>…</think>` dos modelos que raciocinam. `OpenAiCompatibleClient` recebe a resposta
em streaming (`stream: true`, server-sent events; um servidor que responde de uma vez também é compreendido) e descreve
as suas falhas: um servidor que não responde, uma recusa com o seu código e mensagem, uma resposta que não se consegue
ler.

O resumo é guardado com o seu registo (`summaries`, versão de esquema 3: texto, modelo, hora) e substituído por um
novo; vai com o registo. A página Transcrição oferece «Resumir o texto» após um reconhecimento e abre a janela do
registo quando termina; a janela mostra o resumo sob o texto, escreve-o (de novo) e mostra-o à medida que chega. A
exportação para Markdown põe o resumo antes do texto.

As definições guardam uma única escolha do que escreve os resumos e as respostas (`ModelSource`, guardada como
`summary.source`): desligado, um modelo leve do Voicer ou um modelo do servidor. A ligação ao servidor (o seu endereço,
a sua chave e os seus modelos) é uma secção própria, partilhada pelos resumos e pela pesquisa. Os modelos do Voicer são
o Qwen3-4B-Instruct-2507 e o Qwen3-1.7B (Apache-2.0, GGUF Q4_K_M, cerca de cem línguas); são mais fracos do que um
modelo completo de um servidor, e as definições dizem-no. O primeiro instalado corre no `llama-server` da build fixada
(`LlamaChatServer`), como o modelo da pesquisa: só em 127.0.0.1, numa porta livre, com uma chave aleatória, um slot com
um contexto de 8192 tokens; `--reasoning-budget 0` impede o Qwen3-1.7B de raciocinar em voz alta. `ChatModelSource`
abre uma sessão para um resumo ou uma pergunta e mantém o servidor até ela terminar. Cada modelo do Voicer em memória é
mostrado na barra de separadores de todas as páginas, com a memória que ocupa e um botão que o retira da memória de
imediato, mesmo enquanto trabalha: esse trabalho falha com uma mensagem de que o modelo foi retirado da memória, e o
pedido seguinte volta a iniciá-lo. Um modelo do Voicer cuja fonte deixou de estar escolhida também é retirado da
memória.

## Pesquisa e perguntas

O separador Histórico pesquisa nos registos e responde a perguntas sobre eles (`Core/Search`). Quando um registo é
guardado, os seus fragmentos são agrupados em excertos de cerca de 600 caracteres com os seus momentos
(`PassageBuilder`): a unidade que a pesquisa encontra, que o modelo lê e para a qual o leitor salta (versão de esquema
4: `passages`; os registos guardados antes são indexados na atualização).

- **Por palavras**: o índice FTS5 `passages_fts` (`unicode61`, sem diacríticos, ordenado por BM25). Uma palavra é
  feita de letras, números e dos sinais unidos às letras (`categories 'L* N* Co M*'`, versão de esquema 7), por isso
  uma palavra em hindi, bengali ou tailandês fica inteira. `FullTextQuery` transforma o que o utilizador escreveu numa
  consulta segura: as palavras entre aspas formam uma frase; as outras são procuradas pelo seu início sem a última ou
  as duas últimas letras, por isso outras formas coincidem (*contracts* encontra *contract* e *contracted*); uma
  palavra com um dígito é procurada inteira; qualquer palavra basta, e mais palavras sobem na ordenação. Só os
  carateres das palavras chegam ao índice. O chinês e o japonês escrevem-se sem espaços, por isso o índice toma uma
  sequência inteira dos seus sinais como uma palavra: esse texto só é encontrado pelo seu início, e melhor pelo
  significado.
- **Por significado**: um modelo de embeddings no mesmo servidor (`POST embeddings`), escolhido nas definições ao lado
  do modelo dos resumos; um multilingue (bge-m3, multilingual-e5) para fala que não seja em inglês. Antes de uma
  pesquisa, cada excerto sem vetor desse modelo recebe um, dezasseis por pedido (a indexação é mostrada); os vetores
  são normalizados e guardados por modelo em `embeddings`, por isso mudar de modelo indexa de novo sem perder os
  antigos. Os mais próximos são encontrados por cosseno em memória: milhares de excertos levam milissegundos e não é
  precisa nenhuma extensão nativa. `EmbeddingText` acrescenta os prefixos que algumas famílias esperam
  (`query:`/`passage:` do e5, `search_query:`/`search_document:` do nomic).
  O modelo pode ser um do Voicer: bge-m3 (MIT), transferido nas definições e executado pelo `llama-server` da mesma
  build fixada (`LlamaEmbeddingServer`): só em 127.0.0.1, numa porta livre, com uma chave aleatória; iniciado pela
  primeira pesquisa, retido por cada pedido, parado após cinco minutos sem pedidos, ao pará-lo nas definições e com a
  aplicação. As definições mostram o seu estado, a memória e a parte do processador do seu processo.
  A chave de um servidor do Voicer é dada no seu ambiente (`LLAMA_API_KEY`), nunca entre os seus argumentos, que
  qualquer utilizador do computador pode ler. No Windows cada processo do llama.cpp entra num objeto de tarefa (job
  object) que o sistema termina com a aplicação, seja como for que ela termine; cada servidor é também anotado em
  `Data/Temp/Servers` (`ServerProcessRegistry`), por isso o arranque seguinte termina no Linux e no macOS um servidor
  deixado por uma execução que falhou.
  A secção «Modelos da pesquisa no histórico» das definições guarda uma única escolha da fonte (`ModelSource`, guardada como
  `search.source`): desligada, um modelo do Voicer ou um modelo de embeddings do servidor; uma escolha que ainda não
  está pronta (modelo não transferido, falta o llama.cpp, nenhum modelo do servidor escolhido) pesquisa por palavras e
  diz porquê. Antes da primeira escolha a fonte segue o que está instalado. Quando a fonte é o servidor, a sua
  verificação nas definições pede um vetor ao modelo escolhido, por isso um modelo de chat escolhido por engano é
  assinalado aí (o LM Studio responde com um enganador «No models loaded»).
- **Fusão**: as duas listas são combinadas por Reciprocal Rank Fusion (k = 60), como a pesquisa híbrida do
  Elasticsearch. Sem modelo de embeddings, a pesquisa é só por palavras.
- **Marcas**: um excerto encontrado mostra as palavras da consulta a negrito sobre fundo de destaque, reconhecidas pelos
  mesmos inícios que o índice procurou (`SearchMarks`), e começa perto da primeira quando ela está longe. Aberta a
  partir da pesquisa, a gravação desloca-se até ao excerto, mostra-o sobre fundo de destaque e toca desde o seu início.
- **Perguntas** (geração aumentada por recuperação, `RecordQuestions`): os melhores excertos que cabem no contexto são
  dados ao modelo dos resumos, numerados com o seu ficheiro, data e hora; as instruções do pacote de idioma dizem-lhe
  que responda só a partir deles e os cite como [1], [2]. A resposta chega em streaming; as suas fontes aparecem por
  baixo. Nada encontrado significa nenhum pedido.

Um excerto encontrado abre o seu registo com o leitor no início do excerto. Os excertos, o seu índice (FTS5
`secure-delete`) e os seus vetores vão com o registo e não ficam no ficheiro.

Alguns servidores põem toda a resposta de um modelo em `reasoning_content` quando o modelo nunca fecha o seu
raciocínio; `OpenAiCompatibleClient` usa-a então em vez de não devolver nada.

## Geração de voz

O separador Geração lê um texto com um modelo de voz livre neste computador, para uma narração de até dezenas de
minutos. Os modelos (`VoiceModelCatalog`) são os ficheiros GGUF que o próprio projeto llama.cpp converte e publica
(ggml-org no Hugging Face), cada um fixado por tamanho e SHA-256 numa revisão fixa do seu repositório: Qwen3-TTS 1.7B
Base (Apache-2.0, dez idiomas, a voz de uma amostra), Chatterbox Turbo (MIT, inglês, uma voz incorporada ou uma
amostra), Kokoro 82M (Apache-2.0, inglês, 28 vozes incluídas, rápido no processador) e Chatterbox Multilingual (MIT,
23 idiomas). Um modelo são dois ficheiros, os seus pesos e o seu projetor (`mmproj`: o codec de áudio, as vozes, o
codificador do orador), transferidos para `Data/Models/<id>`.

Correm no llama.cpp (MIT). A aplicação instala uma versão fixada dele (`LlamaCppPackage`) em
`Data/Tools/LlamaCpp/<release>`: a build Vulkan em Windows e Linux x64, a build Metal em macOS arm64; o arquivo é
descompactado à parte, cada entrada é verificada para ficar dentro da pasta, e é posto no lugar inteiro. Um llama.cpp
do sistema não é usado: os modelos precisam da versão com que foram verificados.

```
texto
  └─ SpeechScript: uma linha é um parágrafo, um parágrafo é cortado entre frases em partes de até 250 caracteres
       ├─ amostra (opcional): FfmpegVoiceSampleExtractor ── primeiros 30 s, mono, WAV de 24 kHz
       └─ para cada parte: LlamaTtsSynthesizer ── llama-tts -m … -mm … -p <parte> --output part-N.wav
                     [--tts-lang] [--tts-voice] [--tts-speaker-file] [--device VulkanN | none]
  └─ WaveJoiner: as amostras copiadas tal como estão, 250 ms de silêncio entre frases, 650 ms entre parágrafos
       ◄─ Data/Temp/Generation/<guid>/<primeiras palavras>.wav + um fragmento por parte com os seus momentos
```

Cada parte é lida por um processo próprio, iniciado sem shell: o modelo é carregado, fala e sai da memória com o
processo, por isso nada dele fica após a geração, e uma falha nativa de um controlador de vídeo termina só esse
processo; as suas últimas linhas de registo descrevem a falha. Cancelar termina o processo de imediato e elimina a
pasta da geração. O dispositivo é o do reconhecimento: o índice de uma placa gráfica entre as GPU do ggml é o
`VulkanN` do llama.cpp; o processador recebe `--device none -ngl 0`; no macOS, o Metal escolhe o seu dispositivo
sozinho.

O resultado é reproduzido na página (o leitor do histórico) e guardado como WAV onde o utilizador escolher; vai para o
histórico como um reconhecimento, com o seu som comprimido em Opus, marcado como geração. O modelo, o idioma, a voz e
a amostra da última geração são lembrados (`generation.*` em `Data/settings.json`), por isso uma série de gravações
mantém uma mesma voz.

## Pasta de dados

`AppPaths` escolhe a pasta de dados: `--data <folder>`; senão, `Data` ao lado do marcador `voicer.portable` de um
pacote; senão, `Data` ao lado do programa; e quando essa pasta não permite escrita (Program Files, `/opt`), o perfil do
utilizador (`%LOCALAPPDATA%\Voicer\Data`, `~/.local/share/Voicer/Data`).

A primeira janela sobre uma pasta de dados retém o seu ficheiro de bloqueio, `Data/voicer.lock` (`DataFolderLock`); só
essa janela limpa o que execuções anteriores deixaram em `Data/Temp`, por isso uma segunda janela não apaga os
ficheiros de som com que a primeira trabalha. O ficheiro das definições é substituído de forma atómica a cada
alteração; um ficheiro que não pode ser lido nesse momento (outro programa retém-no) não é escrito por cima, por isso
uma alteração nunca apaga as outras definições.

Uma falha da janela que nada previa não termina a aplicação: `App` apanha-a no dispatcher, escreve-a em
`Data/errors.log` (`ErrorLog`: o momento, o tipo, a mensagem e a pilha; o ficheiro recomeça depois de 1 MB) e mostra
um aviso com o caminho do registo, um de cada vez.

## Interface

Um único tema escuro: a paleta do RCynQ com mais luz: o acento brilha à volta dos botões principais, do separador
escolhido, das barras de progresso e de um ficheiro arrastado sobre a área de largar. O idioma é escolhido no primeiro
início a partir do idioma do sistema e lembrado; mudá-lo volta a redigir cada ecrã de imediato (`LocalizationSource`,
`LocalizedViewModel`).
